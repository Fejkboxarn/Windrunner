using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Windrunner.TerrainAuthoring.Editor;
namespace Windrunner.TerrainAuthoring.Tests
{
    public partial class TerrainStudioTests
    {
        TerrainStamp Brush(string name)=>AssetDatabase.LoadAssetAtPath<TerrainStamp>(TerrainStampLibrary.Folder+"/"+name+".asset");
        [Test] public void StampLibraryHasNineteenHighPrecisionFieldsAndEightPresets()
        {
            Assert.AreEqual(19,AssetDatabase.FindAssets("t:TerrainStamp",new[]{TerrainStampLibrary.Folder}).Length);
            Assert.AreEqual(8,AssetDatabase.FindAssets("t:TerrainStampPreset",new[]{TerrainStampLibrary.Folder}).Length);
            foreach(var name in TerrainStampLibrary.Sculpt.Concat(TerrainStampLibrary.Paint)){var b=Brush(name);Assert.NotNull(b);Assert.AreEqual(TextureFormat.R16,b.field.format);Assert.Greater(b.Samples().Distinct().Count(),256);Assert.IsNotEmpty(b.provenance);}
        }
        [Test] public void StampPreviewIsPureAndSculptPreservesPaintAndUndoRedo()
        {
            var t=Tile();var d=t.state.working;var before=d.GetHeights(0,0,65,65);var paint=Weights(t);var layers=d.terrainLayers;
            var p=TerrainStampOperations.Prepare(t,Brush("Rounded Mountain"),new StampSettings(),new Vector2(64,64));
            CollectionAssert.AreEqual(before,d.GetHeights(0,0,65,65));Assert.Greater(p.ChangedCells,0);
            TerrainStampOperations.Apply(p);var after=d.GetHeights(0,0,65,65);Assert.Greater(after[32,32],before[32,32]);EqualMap(paint,Weights(t));CollectionAssert.AreEqual(layers,d.terrainLayers);
            Assert.Throws<InvalidOperationException>(()=>TerrainStampOperations.Apply(p));Undo.PerformUndo();Assert.That(d.GetHeights(0,0,65,65)[32,32],Is.EqualTo(before[32,32]).Within(.0001));Undo.PerformRedo();Assert.That(d.GetHeights(0,0,65,65)[32,32],Is.EqualTo(after[32,32]).Within(.0001));
            AssetDatabase.SaveAssetIfDirty(d);string path=AssetDatabase.GetAssetPath(d);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);Assert.That(AssetDatabase.LoadAssetAtPath<TerrainData>(path).GetHeights(0,0,65,65)[32,32],Is.EqualTo(after[32,32]).Within(.0001));
        }
        [Test] public void StampRejectsAutomaticPaintingAndStaleTerrainPosition()
        {
            var t=Tile();t.state.automatic=true;Assert.Throws<InvalidOperationException>(()=>TerrainStampOperations.Prepare(t,Brush("Rolling Hills"),new StampSettings(),Vector2.one*64));t.state.automatic=false;
            var p=TerrainStampOperations.Prepare(t,Brush("Rolling Hills"),new StampSettings(),Vector2.one*64);t.terrain.transform.position+=Vector3.right;Assert.Throws<InvalidOperationException>(()=>TerrainStampOperations.Apply(p));
        }
        [Test] public void PaintStampPreservesHeightsPaletteAndProtectedWeights()
        {
            var t=Tile();Bake(t);var d=t.state.working;var heights=d.GetHeights(0,0,65,65);var layers=d.terrainLayers;
            PaintHistory.Resolve(t,new RectInt(28,28,8,8),true,1);var protectedBefore=d.GetAlphamaps(28,28,8,8);
            var p=TerrainStampOperations.Prepare(t,Brush("Dirt Variation"),new StampSettings{layer=profile.soil,size=110,strength=.8f},Vector2.one*64);Assert.Greater(p.ChangedCells,0);TerrainStampOperations.Apply(p);
            CollectionAssert.AreEqual(heights,d.GetHeights(0,0,65,65));CollectionAssert.AreEqual(layers,d.terrainLayers);CollectionAssert.AreEqual(protectedBefore,d.GetAlphamaps(28,28,8,8));Assert.IsFalse(TerrainPainter.Empty(t.state.nativeDirty));
            Assert.Throws<InvalidOperationException>(()=>TerrainStampOperations.Prepare(t,Brush("Dirt Variation"),new StampSettings{layer=profile.soil},Vector2.one*64));
        }

        [Test] public void StampRejectsWorkingDataReferencedByAnotherPrefab()
        {
            var t=Tile();PrefabUtility.SaveAsPrefabAsset(t.terrain.gameObject,folder+"/Shared terrain.prefab");
            var error=Assert.Throws<InvalidOperationException>(()=>TerrainStampOperations.Prepare(t,Brush("Rolling Hills"),new StampSettings(),Vector2.one*64));StringAssert.Contains("referenced",error.Message);
        }
        [Test] public void PaintStampUndoRedoAndPresetDraftAreIndependent()
        {
            var t=Tile();Bake(t);var d=t.state.working;var before=Weights(t);
            var original=AssetDatabase.LoadAssetAtPath<TerrainStampPreset>(TerrainStampLibrary.Folder+"/Preset - Natural Grass Variation.asset");float strength=original.settings.strength;var draft=original.settings.Copy();draft.strength=.9f;Assert.AreEqual(strength,original.settings.strength);Assert.AreSame(original.settings.layer,draft.layer);
            var p=TerrainStampOperations.Prepare(t,Brush("Organic Variation"),new StampSettings{layer=profile.soil,size=100,strength=.8f},Vector2.one*64);TerrainStampOperations.Apply(p);var after=Weights(t);Assert.IsFalse(before.SequenceEqual(after));Undo.PerformUndo();EqualMap(before,Weights(t));Undo.PerformRedo();EqualMap(after,Weights(t));
            AssetDatabase.SaveAssetIfDirty(d);string path=AssetDatabase.GetAssetPath(d);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);EqualMap(after,TerrainPainter.Capture(AssetDatabase.LoadAssetAtPath<TerrainData>(path),TerrainPainter.Full(d)).weights);
        }
        [Test] public void SavedDisposableDemonstrationReopensWithIndependentData()
        {
            string root=TerrainStampDemonstration.Folder;var opened=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(root+"/Disposable brush demonstration.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var terrain=opened.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>()).Single();var tile=terrain.GetComponentInChildren<StudioTile>();Assert.NotNull(tile);Assert.IsNull(TerrainPainter.Validate(tile,Array.Empty<RoadPath>()));
                var source=AssetDatabase.LoadAssetAtPath<TerrainData>(root+"/Disposable source.asset");Assert.AreNotSame(source,terrain.terrainData);Assert.That(source.GetInterpolatedHeight(.5f,.5f),Is.EqualTo(39.6f).Within(.02f));
                Assert.Greater(terrain.terrainData.GetInterpolatedHeight(350f/512,350f/512),120);Assert.Less(terrain.terrainData.GetInterpolatedHeight(340f/512,135f/512),25);Assert.AreEqual(3,terrain.terrainData.terrainLayers.Length);
                foreach(var gameObject in opened.GetRootGameObjects())foreach(var transform in gameObject.GetComponentsInChildren<Transform>(true))Assert.AreEqual(0,GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
            }
            finally{UnityEditor.SceneManagement.EditorSceneManager.CloseScene(opened,true);}
        }
        [Test] public void OwnershipValidationMeasuresNativeQueryWithoutWeakeningChecks()
        {
            var tile=Tile();var dataPath=AssetDatabase.GetAssetPath(tile.state.working);
            for(int iteration=0;iteration<3;iteration++)
            {
                var timer=System.Diagnostics.Stopwatch.StartNew();TerrainStampOperations.Validate(tile,false);double localMs=timer.Elapsed.TotalMilliseconds;
                timer.Restart();var roots=AssetDatabase.FindAssets("t:Scene t:Prefab").Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)&&p!=tile.gameObject.scene.path).ToArray();double discoveryMs=timer.Elapsed.TotalMilliseconds;
                timer.Restart();var dependencies=AssetDatabase.GetDependencies(roots,true);double queryMs=timer.Elapsed.TotalMilliseconds;
                Assert.IsFalse(dependencies.Contains(dataPath),"Disposable unsaved working terrain must have no other saved scene/prefab owner.");
                UnityEngine.Debug.Log($"Ownership native sample {iteration}: roots={roots.Length}, dependencies={dependencies.Length}, localMs={localMs:F3}, discoveryMs={discoveryMs:F3}, queryMs={queryMs:F3}");
            }
        }
        [Test] public void OwnershipRejectsNewIndirectReferenceBetweenPreviewAndApply()
        {
            var tile=Tile();var preview=TerrainStampOperations.Prepare(tile,Brush("Rolling Hills"),new StampSettings(),Vector2.one*64);
            // An otherwise unrelated prefab references the paint-state asset, which owns working data.
            var holder=new GameObject("Indirect dependency");var binding=holder.AddComponent<StudioTile>();binding.state=tile.state;
            PrefabUtility.SaveAsPrefabAsset(holder,folder+"/Indirect.prefab");UnityEngine.Object.DestroyImmediate(holder);
            Assert.Throws<InvalidOperationException>(()=>TerrainStampOperations.Apply(preview));
            AssetDatabase.DeleteAsset(folder+"/Indirect.prefab");
            TerrainStampOperations.Apply(preview);
        }
        [Test] public void EnvironmentPlacementOnStampedTerrainPreservesSculptAndPaint()
        {
            var source=new GameObject("Disposable prop");
            var prefab=PrefabUtility.SaveAsPrefabAsset(source,folder+"/Prop.prefab");UnityEngine.Object.DestroyImmediate(source);
            var t=Tile();Bake(t);var d=t.state.working;string terrainPath=AssetDatabase.GetAssetPath(d);
            TerrainStampOperations.Apply(TerrainStampOperations.Prepare(t,Brush("Rolling Hills"),new StampSettings(),Vector2.one*64));
            var heights=d.GetHeights(0,0,65,65);var paint=Weights(t);
            var point=new Vector3(64,t.terrain.SampleHeight(new Vector3(64,0,64)),64);
            var placed=EnvironmentPlacement.Place(prefab,scene,null,point,Vector3.up,new EnvironmentPlacementSettings());
            Assert.AreEqual(point,placed.transform.position);Assert.AreEqual(terrainPath,AssetDatabase.GetAssetPath(t.terrain.terrainData));CollectionAssert.AreEqual(heights,t.terrain.terrainData.GetHeights(0,0,65,65));EqualMap(paint,Weights(t));
            Undo.FlushUndoRecordObjects();Undo.PerformUndo();Assert.IsTrue(!placed);CollectionAssert.AreEqual(heights,d.GetHeights(0,0,65,65));EqualMap(paint,Weights(t));
        }
        [Test] public void StampSizeStrengthAndLowerRemainBounded()
        {
            var t=Tile();var d=t.state.working;float original=d.GetHeights(32,32,1,1)[0,0];var p=TerrainStampOperations.Prepare(t,Brush("Broad Valley"),new StampSettings{size=40,strength=.2f,height=10,operation=HeightOperation.Lower},Vector2.one*64);TerrainStampOperations.Apply(p);float lower=d.GetHeights(32,32,1,1)[0,0];Assert.Less(lower,original);Assert.GreaterOrEqual(lower,original-2/d.size.y-.0001f);
            Assert.Throws<InvalidOperationException>(()=>TerrainStampOperations.Prepare(t,Brush("Grass Patches"),new StampSettings(),Vector2.one*64));
        }
    }
}
