using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using Windrunner.TerrainAuthoring.Editor;
using Object = UnityEngine.Object;

namespace Windrunner.TerrainAuthoring.Tests
{
    public class TerrainPrototypeTransferTests
    {
        Scene scene, previous;
        string folder;
        [SetUp] public void Before()
        {
            previous = SceneManager.GetActiveScene();
            if (!Application.isBatchMode && previous.isDirty) Assert.Ignore("Save the open scene first.");
            folder = "Assets/PrototypeTransferTest-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,string.IsNullOrEmpty(previous.path)?NewSceneMode.Single:NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
        }
        [TearDown] public void After()
        {
            if(previous.IsValid()) SceneManager.SetActiveScene(previous);
            if(scene.IsValid())
            {
                if(SceneManager.sceneCount == 1) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                else EditorSceneManager.CloseScene(scene,true);
            }
            if(!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
        }
        Terrain Data(string name)
        {
            var d = new TerrainData { heightmapResolution = 33, alphamapResolution = 16, size = new Vector3(32, 20, 32) };
            d.SetDetailResolution(16, 8);
            AssetDatabase.CreateAsset(d, folder + "/" + name + ".asset");
            return Terrain.CreateTerrainGameObject(d).GetComponent<Terrain>();
        }
        TerrainLayer Layer(string name)
        {
            var l = new TerrainLayer(); AssetDatabase.CreateAsset(l, folder + "/" + name + ".terrainlayer"); return l;
        }
        GameObject Tree(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, folder + "/" + name + ".prefab");
            Object.DestroyImmediate(go); return prefab;
        }
        DetailPrototype Detail(float width = 1)
        {
            return new DetailPrototype { prototypeTexture = Texture2D.whiteTexture, renderMode = DetailRenderMode.GrassBillboard,
                minWidth = width, maxWidth = width + 1, minHeight = 1, maxHeight = 2, noiseSeed = 39,
                positionJitter = .4f, holeEdgePadding = .2f, density = .6f, useDensityScaling = true };
        }
        [Test] public void AppendPreservesPaintIndicesClonesDetailsAndSupportsUndoRedo()
        {
            var source = Data("source"); var target = Data("target"); var s = source.terrainData; var d = target.terrainData;
            var first = Layer("first"); var second = Layer("second"); var tree = Tree("tree");
            s.terrainLayers = new[] { first, second, second }; d.terrainLayers = new[] { first };
            var old = new TreePrototype { prefab = tree, bendFactor = .2f, navMeshLod = 0 };
            var added = new TreePrototype { prefab = tree, bendFactor = .7f, navMeshLod = 0 };
            s.treePrototypes = new[] { old, added, added }; d.treePrototypes = new[] { old };
            d.treeInstances = new[] { new TreeInstance { prototypeIndex = 0, position = new Vector3(.3f,.1f,.4f), widthScale = 1, heightScale = 1, color = Color.white, lightmapColor = Color.white } };
            d.detailPrototypes = new[] { Detail() }; s.detailPrototypes = new[] { Detail(), Detail(2), Detail(2) };
            var density = new int[16,16]; density[4,7] = 5; d.SetDetailLayer(0,0,0,density);
            var heights = new float[33,33]; heights[7,8] = .3f; d.SetHeights(0,0,heights);
            var holes = d.GetHoles(0,0,32,32); holes[5,6] = false; d.SetHoles(0,0,holes);
            var before = Object.Instantiate(d);
            try
            {
                var plan = TerrainPrototypeTransfer.CreatePreview(source,target,PrototypeCategories.All);
                Assert.AreEqual(3,plan.Added); Assert.AreEqual(1,d.treePrototypes.Length,"Preview mutated target");
                TerrainPrototypeTransfer.Apply(plan);
                Assert.AreEqual(2,d.treePrototypes.Length); Assert.AreEqual(.2f,d.treePrototypes[0].bendFactor);
                Assert.AreEqual(2,d.detailPrototypes.Length); Assert.IsTrue(d.detailPrototypes[1].Equals(Detail(2)));
                CollectionAssert.AreEqual(new[]{first,second},d.terrainLayers);
                TerrainPrototypeTransfer.AssertPaintPreserved(before,d);
                Undo.PerformUndo(); Assert.AreEqual(1,d.treePrototypes.Length); Assert.AreEqual(1,d.detailPrototypes.Length);
                CollectionAssert.AreEqual(new[]{first},d.terrainLayers); TerrainPrototypeTransfer.AssertPaintPreserved(before,d);
                Undo.PerformRedo(); Assert.AreEqual(2,d.treePrototypes.Length); TerrainPrototypeTransfer.AssertPaintPreserved(before,d);
                Assert.AreEqual(0,TerrainPrototypeTransfer.CreatePreview(source,target,PrototypeCategories.All).Added);
                Assert.Throws<InvalidOperationException>(()=>TerrainPrototypeTransfer.Apply(plan));
            }
            finally { Object.DestroyImmediate(before); }
        }
        [Test] public void SharedDataStalePreviewInvalidReferencesAndCategoryIsolationAreSafe()
        {
            var source = Data("source"); var target = Data("target"); var d = target.terrainData;
            var first = Layer("first"); source.terrainData.terrainLayers = new[]{first}; d.terrainLayers = new[]{first};
            source.terrainData.treePrototypes = new[]{ new TreePrototype { prefab = Tree("tree") } };
            var plan = TerrainPrototypeTransfer.CreatePreview(source,target,PrototypeCategories.Trees);
            EditorUtility.SetDirty(source.terrainData);
            Assert.Throws<InvalidOperationException>(()=>TerrainPrototypeTransfer.Apply(plan)); Assert.AreEqual(0,d.treePrototypes.Length);
            Assert.AreEqual(0,TerrainPrototypeTransfer.CreatePreview(source,target,PrototypeCategories.Layers).Added);
            var shared = Terrain.CreateTerrainGameObject(d).GetComponent<Terrain>();
            Assert.Throws<ArgumentException>(()=>TerrainPrototypeTransfer.CreatePreview(shared,target,PrototypeCategories.All));
            Assert.Throws<InvalidOperationException>(()=>TerrainPrototypeTransfer.CreatePreview(source,target,PrototypeCategories.All));
            Object.DestroyImmediate(shared.gameObject);
            source.terrainData.terrainLayers = new TerrainLayer[]{null};
            Assert.Throws<InvalidOperationException>(()=>TerrainPrototypeTransfer.CreatePreview(source,target,PrototypeCategories.Layers));
            Assert.Throws<ArgumentException>(()=>TerrainPrototypeTransfer.CreatePreview(source,target,0));
        }
        [Test] public void MultiControlTextureAppendKeepsEveryWeightAcrossUndoRedoAndReload()
        {
            var source = Data("source"); var target = Data("target"); var d = target.terrainData;
            var existing = Enumerable.Range(0,4).Select(i=>Layer("layer"+i)).ToArray();
            d.terrainLayers = existing;
            var paint = new float[16,16,4];
            for(int y=0;y<16;y++) for(int x=0;x<16;x++)
            { paint[y,x,0]=.2f;paint[y,x,1]=.4f;paint[y,x,2]=.3f;paint[y,x,3]=.1f; }
            d.SetAlphamaps(0,0,paint);
            source.terrainData.terrainLayers = new[]{Layer("fifth"),existing[2],Layer("sixth")};
            var before=Object.Instantiate(d);
            try
            {
                TerrainPrototypeTransfer.Apply(TerrainPrototypeTransfer.CreatePreview(source,target,PrototypeCategories.Layers));
                Assert.AreEqual(6,d.terrainLayers.Length); Assert.AreEqual(2,d.alphamapTextureCount);
                TerrainPrototypeTransfer.AssertPaintPreserved(before,d);
                Undo.PerformUndo(); Assert.AreEqual(4,d.terrainLayers.Length); TerrainPrototypeTransfer.AssertPaintPreserved(before,d);
                Undo.PerformRedo(); Assert.AreEqual(6,d.terrainLayers.Length); TerrainPrototypeTransfer.AssertPaintPreserved(before,d);
                string path=AssetDatabase.GetAssetPath(d); AssetDatabase.SaveAssetIfDirty(d);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                var reloaded=AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                Assert.AreEqual(6,reloaded.terrainLayers.Length); TerrainPrototypeTransfer.AssertPaintPreserved(before,reloaded);
            }
            finally { Object.DestroyImmediate(before); }
        }
        [Test] public void EmptyLayerInitializationAndChangedTargetAreRefusedWithoutWrites()
        {
            var source=Data("source"); var target=Data("target"); var layer=Layer("first");
            source.terrainData.terrainLayers=new[]{layer};
            Assert.Throws<InvalidOperationException>(()=>TerrainPrototypeTransfer.CreatePreview(source,target,PrototypeCategories.Layers));
            Assert.AreEqual(0,target.terrainData.terrainLayers.Length);
            target.terrainData.terrainLayers=new[]{layer};
            source.terrainData.treePrototypes=new[]{new TreePrototype{prefab=Tree("tree")}};
            var preview=TerrainPrototypeTransfer.CreatePreview(source,target,PrototypeCategories.Trees);
            var heights=new float[33,33]; heights[1,1]=.2f; target.terrainData.SetHeights(0,0,heights);
            Assert.Throws<InvalidOperationException>(()=>TerrainPrototypeTransfer.Apply(preview));
            Assert.AreEqual(0,target.terrainData.treePrototypes.Length);
            target.GetComponent<TerrainCollider>().terrainData=source.terrainData;
            Assert.Throws<InvalidOperationException>(()=>TerrainPrototypeTransfer.CreatePreview(source,target,PrototypeCategories.Trees));
        }
        [Test] public void Zone01EditableCopyRetainsPaintHistoryRoadRulesAndOriginalAsset()
        {
            const string path = "Assets/WindrunnerProduction/World/Terrain/CentralBasin.asset";
            var bytes = File.ReadAllBytes(path);
            var original = AssetDatabase.LoadAssetAtPath<TerrainData>(path); Assert.IsNotNull(original);
            var profile = StudioSetup.Preset<SurfaceProfile>("Basin surfaces");
            var carriage = StudioSetup.Preset<RoadProfile>("Carriage road");
            var terrain = Terrain.CreateTerrainGameObject(original).GetComponent<Terrain>();
            var tile = StudioSetup.Attach(terrain,profile,folder);
            var d = tile.state.working;
            var source = Data("incoming"); source.terrainData.terrainLayers = new[]{Layer("additional")};
            source.terrainData.treePrototypes = new[]{new TreePrototype { prefab=Tree("tree") }};
            source.terrainData.detailPrototypes = new[]{Detail()};
            var before = Object.Instantiate(d);
            var state = EditorJsonUtility.ToJson(tile.state); var rules = EditorJsonUtility.ToJson(profile);
            var roadRules = EditorJsonUtility.ToJson(carriage); var rebuilds = StudioUpdates.CompletedRebuilds;
            try
            {
                var plan = TerrainPrototypeTransfer.CreatePreview(source,tile.terrain,PrototypeCategories.All);
                Assert.AreEqual(3,plan.Added); TerrainPrototypeTransfer.Apply(plan);
                TerrainPrototypeTransfer.AssertPaintPreserved(before,d);
                Assert.AreEqual(state,EditorJsonUtility.ToJson(tile.state),"Studio state changed");
                Assert.AreEqual(rules,EditorJsonUtility.ToJson(profile)); Assert.AreEqual(roadRules,EditorJsonUtility.ToJson(carriage));
                Assert.AreEqual(rebuilds,StudioUpdates.CompletedRebuilds);
                AssetDatabase.SaveAssetIfDirty(d);
                Assert.AreEqual(1,d.treePrototypes.Length);
                Undo.PerformUndo(); TerrainPrototypeTransfer.AssertPaintPreserved(before,d);
                Assert.AreEqual(before.terrainLayers.Length,d.terrainLayers.Length);
                tile.state.automatic = true;
                Assert.Throws<InvalidOperationException>(()=>TerrainPrototypeTransfer.CreatePreview(source,tile.terrain,PrototypeCategories.All));
                CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path),"Original Zone 01 asset was changed");
            }
            finally { Object.DestroyImmediate(before); }
        }
    }
}
