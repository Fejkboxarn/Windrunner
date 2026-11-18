using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Windrunner.TerrainAuthoring.Editor;
using Object=UnityEngine.Object;

namespace Windrunner.TerrainAuthoring.Tests
{
    public sealed class EnvironmentAuthoringTests
    {
        string folder;EnvironmentCatalog catalog;
        [SetUp] public void Before(){folder="Assets/EnvironmentStudioTest-"+Guid.NewGuid().ToString("N");AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));}
        [TearDown] public void After(){catalog?.Dispose();catalog=null;AssetDatabase.DeleteAsset(folder);}
        GameObject Prefab(string name="Rock")
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);try{return PrefabUtility.SaveAsPrefabAsset(go,folder+"/"+name+".prefab");}finally{Object.DestroyImmediate(go);}
        }
        EnvironmentLibrarySettings Settings()=>new EnvironmentLibrarySettings{sources=new List<EnvironmentSource>{new EnvironmentSource{name="Test approved source",folder=folder,exclude=""}}};
        [UnityTest] public IEnumerator ImportsMovesRemovalAndReconnectionRetainManualGuidMetadata()
        {
            var settings=Settings();catalog=new EnvironmentCatalog(settings);catalog.Rescan();catalog.Process();Assert.IsEmpty(catalog.entries);
            var prefab=Prefab();string path=AssetDatabase.GetAssetPath(prefab),guid=AssetDatabase.AssetPathToGUID(path);
            yield return null;catalog.Process();Assert.IsTrue(catalog.entries.ContainsKey(guid),"Import callback should enqueue without a restart or explicit rescan");
            var m=settings.For(guid);m.category="My rocks";m.favorite=true;m.notes="Retain me";m.biomes="Alpine";
            settings.collections.Add(new EnvironmentCollection{guids=new List<string>{guid}});
            string moved=folder+"/Renamed.prefab";Assert.IsEmpty(AssetDatabase.MoveAsset(path,moved));yield return null;catalog.Process();
            Assert.AreEqual(moved,catalog.entries[guid].path);Assert.AreEqual(1,catalog.entries.Count);Assert.AreEqual("My rocks",settings.For(guid).category);
            byte[] bytes=File.ReadAllBytes(moved),meta=File.ReadAllBytes(moved+".meta");AssetDatabase.DeleteAsset(moved);yield return null;catalog.Process();Assert.IsTrue(catalog.entries[guid].missing);
            File.WriteAllBytes(moved,bytes);File.WriteAllBytes(moved+".meta",meta);AssetDatabase.ImportAsset(moved,ImportAssetOptions.ForceSynchronousImport);yield return null;catalog.Process();Assert.IsFalse(catalog.entries[guid].missing);
            var restored=JsonUtility.FromJson<EnvironmentLibrarySettings>(JsonUtility.ToJson(settings));Assert.IsTrue(restored.For(guid).favorite);Assert.AreEqual("Retain me",restored.For(guid).notes);Assert.AreEqual(guid,restored.collections[0].guids[0]);
            Assert.AreEqual(1,catalog.Filter("Alpine","My rocks","","",true,true,null,false).Count);
        }
        [Test] public void MissingPacksSourcesAndSavedCollectionsAreSafe()
        {
            var settings=Settings();settings.sources[0].folder="Assets/NotInstalled-"+Guid.NewGuid();settings.metadata.Add(new EnvironmentMetadata{guid="11111111111111111111111111111111",favorite=true});
            catalog=new EnvironmentCatalog(settings);catalog.Rescan();catalog.Process();Assert.AreEqual(1,catalog.entries.Count);Assert.IsTrue(catalog.entries.Values.Single().missing);
            Assert.IsEmpty(catalog.Filter("","","","",false,true,null,false));Assert.AreEqual(1,catalog.Filter("","","","",true,false,null,false).Count);
            Assert.AreEqual("Unclassified",EnvironmentCatalog.Suggest("Assets/Unknown/TreeSuper.prefab",typeof(GameObject)));
            Assert.AreEqual("Trees",EnvironmentCatalog.Suggest("Assets/Approved/Trees/Oak.prefab",typeof(GameObject)));
        }
        [Test] public void InstalledSourcesAreIndexedAndRegistryFiltersExcludeUnapprovedAssets()
        {
            var s=new EnvironmentLibrarySettings();s.Defaults();catalog=new EnvironmentCatalog(s);catalog.Rescan();while(catalog.Pending>0)catalog.Process();
            foreach(var source in s.sources.Where(x=>AssetDatabase.IsValidFolder(x.folder)))Assert.IsTrue(catalog.entries.Values.Any(e=>e.source==source.name),source.name);
            Assert.IsFalse(catalog.entries.Values.Any(e=>e.path.StartsWith("Assets/Feel/")));
            var disabled=s.sources[0];disabled.enabled=false;catalog.Rescan();while(catalog.Pending>0)catalog.Process();Assert.IsFalse(catalog.entries.Values.Any(e=>e.source==disabled.name&&!e.missing));
        }
        [Test] public void PlacementKeepsPrefabLinkAndSupportsUndoRedoWithoutTouchingSource()
        {
            var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,string.IsNullOrEmpty(previous.path)?NewSceneMode.Single:NewSceneMode.Additive);
            var prefab=Prefab();string original=File.ReadAllText(AssetDatabase.GetAssetPath(prefab));
            try
            {
                var parent=new GameObject("Manual parent");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(parent,scene);
                var placed=EnvironmentPlacement.Place(prefab,scene,parent.transform,new Vector3(4,2,8),Vector3.up,new EnvironmentPlacementSettings{yaw=30,scale=2});
                Assert.AreEqual(AssetDatabase.GetAssetPath(prefab),PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(placed));Assert.AreEqual(new Vector3(4,2,8),placed.transform.position);Assert.AreEqual(Vector3.one*2,placed.transform.localScale);
                Undo.FlushUndoRecordObjects();Undo.PerformUndo();Assert.IsTrue(!placed);Undo.PerformRedo();Assert.AreEqual(1,parent.transform.childCount);Assert.AreEqual(original,File.ReadAllText(AssetDatabase.GetAssetPath(prefab)));
                Assert.IsFalse(EnvironmentInspection.Scene(scene).Any(m=>m.StartsWith("ERROR")));
            }
            finally{if(previous.IsValid())UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);if(UnityEngine.SceneManagement.SceneManager.sceneCount==1)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);else EditorSceneManager.CloseScene(scene,true);}
        }
        [Test] public void PreparedLayerUsesExistingTransferAndPreservesPaintAndSharedDataGuard()
        {
            var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,string.IsNullOrEmpty(previous.path)?NewSceneMode.Single:NewSceneMode.Additive);
            var a=new TerrainLayer();var b=new TerrainLayer();AssetDatabase.CreateAsset(a,folder+"/A.terrainlayer");AssetDatabase.CreateAsset(b,folder+"/B.terrainlayer");
            var data=new TerrainData{heightmapResolution=33,alphamapResolution=16,size=new Vector3(32,20,32)};data.terrainLayers=new[]{a};AssetDatabase.CreateAsset(data,folder+"/Data.asset");
            var go=Terrain.CreateTerrainGameObject(data);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);var terrain=go.GetComponent<Terrain>();
            try
            {
                float[,,] before=data.GetAlphamaps(0,0,16,16);
                using(var prep=new EnvironmentPrototypePreparation(b,terrain,PrototypeCategories.Layers))
                {Assert.AreEqual(1,prep.preview.Added);Assert.AreEqual(1,data.terrainLayers.Length);prep.Apply();}
                Assert.AreSame(a,data.terrainLayers[0]);Assert.AreSame(b,data.terrainLayers[1]);var after=data.GetAlphamaps(0,0,16,16);
                for(int y=0;y<16;y++)for(int x=0;x<16;x++){Assert.AreEqual(before[y,x,0],after[y,x,0]);Assert.AreEqual(0,after[y,x,1]);}
                Undo.FlushUndoRecordObjects();Undo.PerformUndo();Assert.AreEqual(1,data.terrainLayers.Length);Undo.PerformRedo();Assert.AreEqual(2,data.terrainLayers.Length);
                var shared=Terrain.CreateTerrainGameObject(data);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(shared,scene);
                Assert.IsTrue(EnvironmentInspection.Terrain(terrain).Any(m=>m.Contains("Shared TerrainData")));
                Assert.Throws<InvalidOperationException>(()=>new EnvironmentPrototypePreparation(b,terrain,PrototypeCategories.Layers));
            }
            finally{if(previous.IsValid())UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);if(UnityEngine.SceneManagement.SceneManager.sceneCount==1)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);else EditorSceneManager.CloseScene(scene,true);}
        }
        [Test] public void MetadataAndPresetDiskRoundTripRebuildsCatalogWithoutPathIdentity()
        {
            var prefab=Prefab();string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));var settings=Settings();
            var m=settings.For(guid);m.favorite=true;m.publisher="Verified custom source";m.category="Landmarks";m.biomes="Woodland";m.approval=1;m.notes="Hand classified";
            settings.collections.Add(new EnvironmentCollection{name="My palette",guids=new List<string>{guid,"22222222222222222222222222222222"},placement=new EnvironmentPlacementSettings{yaw=45,scale=2}});
            string file=folder+"/saved-settings.json";File.WriteAllText(file,JsonUtility.ToJson(settings));
            var loaded=JsonUtility.FromJson<EnvironmentLibrarySettings>(File.ReadAllText(file));catalog=new EnvironmentCatalog(loaded);catalog.Rescan();while(catalog.Pending>0)catalog.Process();
            Assert.IsTrue(loaded.For(guid).favorite);Assert.AreEqual("Landmarks",loaded.For(guid).category);Assert.AreEqual(45,loaded.collections[0].placement.yaw);
            Assert.AreEqual(2,loaded.collections[0].guids.Count);Assert.IsTrue(catalog.entries["22222222222222222222222222222222"].missing);
            Assert.AreEqual(1,catalog.Filter("Woodland","Landmarks","custom","",true,true,loaded.collections[0],false).Count);
        }
        [Test] public void PreparedTreeAndDetailsDeduplicateRejectStalePreviewAndReleaseTemporaryObjects()
        {
            var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,string.IsNullOrEmpty(previous.path)?NewSceneMode.Single:NewSceneMode.Additive);
            var prefab=Prefab();var data=new TerrainData{heightmapResolution=33,alphamapResolution=16,size=new Vector3(32,20,32)};data.SetDetailResolution(16,8);AssetDatabase.CreateAsset(data,folder+"/Data.asset");
            var go=Terrain.CreateTerrainGameObject(data);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);var terrain=go.GetComponent<Terrain>();
            try
            {
                using(var prep=new EnvironmentPrototypePreparation(prefab,terrain,PrototypeCategories.Trees)){prep.Apply();}
                using(var prep=new EnvironmentPrototypePreparation(prefab,terrain,PrototypeCategories.Details)){prep.Apply();}
                Assert.AreEqual(1,data.treePrototypes.Length);Assert.AreEqual(1,data.detailPrototypes.Length);Assert.AreEqual(0,data.treeInstanceCount);
                for(int i=0;i<15;i++)using(var prep=new EnvironmentPrototypePreparation(prefab,terrain,PrototypeCategories.Trees)){Assert.AreEqual(0,prep.preview.Added);}
                using(var prep=new EnvironmentPrototypePreparation(prefab,terrain,PrototypeCategories.Trees))
                {EditorUtility.SetDirty(data);Assert.Throws<InvalidOperationException>(()=>prep.Apply());}
                Assert.IsFalse(Resources.FindObjectsOfTypeAll<Terrain>().Any(t=>t.name=="Environment prototype preview"));
            }
            finally{if(previous.IsValid())UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);if(UnityEngine.SceneManagement.SceneManager.sceneCount==1)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);else EditorSceneManager.CloseScene(scene,true);}
        }
        [UnityTest] public IEnumerator AssetPreviewsRenderAndRepeatedEditorDisposalDoesNotCreateSceneObjects()
        {
            var prefab=Prefab();int before=UnityEngine.SceneManagement.SceneManager.GetActiveScene().rootCount;
            var renderer=UnityEditor.Editor.CreateEditor(prefab);
            Texture2D image=null;
            try{image=renderer.RenderStaticPreview(AssetDatabase.GetAssetPath(prefab),null,160,160);Assert.IsNotNull(image,"Native prefab static preview must render in the test runner");Assert.AreEqual(160,image.width);}
            finally{if(image)Object.DestroyImmediate(image);Object.DestroyImmediate(renderer);}
            yield return null;
            for(int i=0;i<20;i++){var editor=UnityEditor.Editor.CreateEditor(prefab);Assert.IsNotNull(editor);Object.DestroyImmediate(editor);}
            Assert.AreEqual(before,UnityEngine.SceneManagement.SceneManager.GetActiveScene().rootCount,"Previews cannot create production scene objects");
        }
        [Test] public void CatalogFilteringBenchmarkAndIdleWorkAreBounded()
        {
            var s=Settings();catalog=new EnvironmentCatalog(s);
            for(int i=0;i<5000;i++)catalog.entries.Add(i.ToString(),new EnvironmentEntry{guid=i.ToString(),name="Stone "+i,category="Rocks",source="Verified",biomes="Alpine",path="Assets/Synthetic"});
            var watch=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<20;i++)Assert.AreEqual(5000,catalog.Filter("Stone","Rocks","Verified","Alpine",false,true,null,false).Count);watch.Stop();
            int processed=catalog.Processed;for(int i=0;i<100;i++)catalog.Process();Assert.AreEqual(processed,catalog.Processed);
            Debug.Log($"Environment catalog: 5,000 synthetic entries / 20 filter+sort queries {watch.Elapsed.TotalMilliseconds:F3} ms total; idle processed 0. This is not an asset-import or rendering benchmark.");
        }
    }
}
