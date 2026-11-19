using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Windrunner.Suntemple.Editor;

namespace Windrunner.Suntemple.Tests
{
    public class SuntempleCompatibilityTests
    {
        const string Root="Assets/WindrunnerCompatibility/Suntemple/";
        [Test] public void DecalsRetainAuthoredMeshesAndGenerateFiniteRuntimeFallbacks()
        {
            var go=new GameObject("Disposable decal projection");var authored=new Mesh();
            try
            {
                var filter=go.AddComponent<MeshFilter>();var renderer=go.AddComponent<MeshRenderer>();var decal=go.AddComponent<DecalProjector>();
                decal.meshFilter=filter;decal.meshRenderer=renderer;decal.raycastLayer=0;
                authored.vertices=new[]{Vector3.zero,Vector3.right,Vector3.forward};authored.triangles=new[]{0,1,2};filter.sharedMesh=authored;
                typeof(DecalProjector).GetMethod("Start",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(decal,null);Assert.That(filter.sharedMesh,Is.SameAs(authored));
                filter.sharedMesh=null;typeof(DecalProjector).GetMethod("Start",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(decal,null);Assert.That(filter.sharedMesh,Is.Not.Null);
                foreach(var v in filter.sharedMesh.vertices){Assert.That(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z),Is.False);Assert.That(v.y,Is.EqualTo(-.5f).Within(.0001f));}
            }
            finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(authored);}
        }
        [Test] public void OceanCameraLookupToleratesAbsentSceneView()
        {
            var go=new GameObject("Disposable ocean lookup");
            try{var ocean=go.AddComponent<OceanTool>();Assert.DoesNotThrow(()=>ocean.GetActiveCamera());}
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test] public void VariantGuidsAreDistinctAndResolve()
        {
            var map=JsonUtility.FromJson<SuntempleRepair.Mapping>(File.ReadAllText("Documentation/SuntempleVariantMapping.json"));
            Assert.That(map.entries.Select(e=>e.variantGuid).Distinct().Count(),Is.EqualTo(map.entries.Length));
            foreach(var e in map.entries){Assert.That(e.variantGuid,Is.Not.EqualTo(e.originalGuid),e.variant);Assert.That(AssetDatabase.GUIDToAssetPath(e.variantGuid),Is.EqualTo(e.variant));}
        }
        [Test] public void TerrainGeometryAndPaintMatchSource()
        {
            var map=JsonUtility.FromJson<SuntempleRepair.Mapping>(File.ReadAllText("Documentation/SuntempleVariantMapping.json"));
            foreach(var e in map.entries.Where(e=>e.variant.EndsWith("Terrain_Data.asset")))
            {
                var a=AssetDatabase.LoadAssetAtPath<TerrainData>(e.original);var b=AssetDatabase.LoadAssetAtPath<TerrainData>(e.variant);
                Assert.That(b.size,Is.EqualTo(a.size));Assert.That(b.heightmapResolution,Is.EqualTo(a.heightmapResolution));
                CollectionAssert.AreEqual(a.GetHeights(0,0,a.heightmapResolution,a.heightmapResolution),b.GetHeights(0,0,b.heightmapResolution,b.heightmapResolution));
                CollectionAssert.AreEqual(a.GetAlphamaps(0,0,a.alphamapWidth,a.alphamapHeight),b.GetAlphamaps(0,0,b.alphamapWidth,b.alphamapHeight));
                CollectionAssert.AreEqual(a.treeInstances,b.treeInstances);Assert.That(b.detailPrototypes.Length,Is.EqualTo(a.detailPrototypes.Length));
                for(int i=0;i<a.detailPrototypes.Length;i++)CollectionAssert.AreEqual(a.GetDetailLayer(0,0,a.detailWidth,a.detailHeight,i),b.GetDetailLayer(0,0,b.detailWidth,b.detailHeight,i));
                Assert.That(b.terrainLayers.All(l=>l&&AssetDatabase.GetAssetPath(l).StartsWith(Root)),Is.True);
            }
        }
        [Test] public void DemoHasRenderableReferencesAndUrpCamera()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene=EditorSceneManager.OpenScene(SuntempleValidation.Repaired);
                foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject),Is.Zero,t.name);
                foreach(var r in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)))
                    foreach(var m in r.sharedMaterials){Assert.That(m,Is.Not.Null,r.name);Assert.That(m.shader,Is.Not.Null,m.name);Assert.That(m.shader.isSupported,Is.True,m.name);Assert.That(m.shader.name,Does.Not.Contain("InternalError"),m.name);Assert.That(m.shader.name,Does.Not.StartWith("Legacy"),m.name);Assert.That(m.shader.name,Is.Not.EqualTo("Standard"),m.name);Assert.That(m.shader.name,Is.Not.EqualTo("Particles/Standard Unlit"),m.name);}
                foreach(var m in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true)))Assert.That(m.sharedMesh,Is.Not.Null,m.name);
                Assert.That(Camera.main.GetUniversalAdditionalCameraData().requiresDepthTexture,Is.True);
                Assert.That(Camera.main.GetComponent<Flythrough>(),Is.Not.Null);
                Assert.That(Camera.main.GetComponent<SmoothMouseLook>(),Is.Not.Null);
            }
            finally{if(setup.Length>0&&setup.Any(s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
        }
        [Test] public void AllRepairedMaterialsHaveCompilingShaders()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{Root.TrimEnd('/')}))
            {
                var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));Assert.That(m.shader,Is.Not.Null,m.name);
                Assert.That(m.shader.name,Does.Not.Contain("InternalError"),m.name);
                Assert.That(ShaderUtil.GetShaderMessages(m.shader).Any(e=>e.severity.ToString()=="Error"),Is.False,m.name);
            }
        }
    }
}
