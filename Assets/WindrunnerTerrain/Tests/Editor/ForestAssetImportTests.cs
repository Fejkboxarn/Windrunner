using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Windrunner.TerrainAuthoring.Editor;
using Object = UnityEngine.Object;

namespace Windrunner.TerrainAuthoring.Tests
{
    public sealed class ForestAssetImportTests
    {
        const string Root = "Assets/TriForge Assets/Fantasy Forest Environment";
        static string[] Paths(string type) => AssetDatabase.FindAssets("t:"+type,new[]{Root})
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        static readonly List<string> Missing = new List<string>();
        static void References(Object value)
        {
            var so=new SerializedObject(value);var it=so.GetIterator();
            while(it.Next(true))
                if(it.propertyType==SerializedPropertyType.ObjectReference && it.objectReferenceEntityIdValue!=default)
                    {
                    // Two original seasonal prefab collider meshes are absent from vendor delivery.
                    // Audit these explicitly below rather than pretending they resolve.
                    if(value is MeshCollider && it.propertyPath=="m_Mesh" && !it.objectReferenceValue) continue;
                    if(!it.objectReferenceValue) Missing.Add(AssetDatabase.GetAssetPath(value)+" / "+value.name+" / "+it.propertyPath);
                }
        }
        [Test] public void ForestRenderingReferencesMatchDocumentedVendorExceptions()
        {
            Missing.Clear();
            Assert.IsNotNull(GraphicsSettings.currentRenderPipeline,"URP must be active");
            var prefabs=Paths("Prefab"); Assert.Greater(prefabs.Length,100);
            int renderers=0,meshes=0;var missingColliders=new HashSet<string>();
            foreach(var path in prefabs)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Assert.IsNotNull(prefab,path);
                foreach(var t in prefab.GetComponentsInChildren<Transform>(true))
                    Assert.Zero(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject),path);
                foreach(var c in prefab.GetComponentsInChildren<Component>(true)) if(c) References(c);
                foreach(var r in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    renderers++; Assert.IsNotEmpty(r.sharedMaterials,path);
                    for(int i=0;i<r.sharedMaterials.Length;i++)
                    {
                        if(i==1 && r is ParticleSystemRenderer && !r.GetComponent<ParticleSystem>().trails.enabled) continue;
                        Assert.IsNotNull(r.sharedMaterials[i],path+" missing material");
                    }
                }
                foreach(var c in prefab.GetComponentsInChildren<MeshCollider>(true))
                    if(!c.sharedMesh) missingColliders.Add(Path.GetFileName(path));
                foreach(var m in prefab.GetComponentsInChildren<MeshFilter>(true))
                { Assert.IsNotNull(m.sharedMesh,path);Assert.Greater(m.sharedMesh.vertexCount,0,path);meshes++; }
            }
            Debug.Log("Forest unresolved component references: " + string.Join("\n",Missing));
            CollectionAssert.AreEquivalent(new[]{"P_FFE_Rock1_Winter.prefab","P_FFE_Rock2_Winter.prefab","P_FFE_Rock3.Prefab","P_FFE_Rock3_Winter.prefab","P_FFE_Rock4.Prefab","P_FFE_Rock4_Winter.prefab","P_FFE_Tree_1_Autumn.Prefab","P_FFE_Tree_1_Winter.prefab"},missingColliders);
            foreach(var path in Paths("Material"))
            {
                var m=AssetDatabase.LoadAssetAtPath<Material>(path);References(m);
                Assert.IsNotNull(m.shader,path);Assert.IsTrue(m.shader.isSupported,path+" unsupported shader "+m.shader.name);
                Assert.AreNotEqual("Hidden/InternalErrorShader",m.shader.name,path);
                Assert.IsEmpty(ShaderUtil.GetShaderMessages(m.shader).Where(e=>e.severity.ToString()=="Error").ToArray(),path);
                foreach(var property in m.GetTexturePropertyNames())
                {var texture=m.GetTexture(property);if(texture) Assert.Greater(texture.width,0,path+" / "+property);}
            }
            var layers=Paths("TerrainLayer");Assert.AreEqual(23,layers.Length);
            foreach(var path in layers)
            {var l=AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);References(l);Assert.IsNotNull(l.diffuseTexture,path);}
            CollectionAssert.AreEqual(new[]{Root+"/Prefabs/P_FFE_Bush_2.Prefab / bush_2 / m_Avatar"},Missing,"Unexpected unresolved reference beyond the original vendor avatar");
            Debug.Log($"Forest audit: {prefabs.Length} prefabs, {renderers} renderers, {meshes} mesh filters, {Paths("Material").Length} materials, {layers.Length} TerrainLayers.");
        }
        [Test] public void ForestTreeGrassAndLayersAppendToDisposableZoneWorkingData()
        {
            var previous=SceneManager.GetActiveScene();
            if(!Application.isBatchMode&&previous.isDirty) Assert.Ignore("Save the scene before testing.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,string.IsNullOrEmpty(previous.path)?NewSceneMode.Single:NewSceneMode.Additive);
            string folder="Assets/ForestImportTest-"+Guid.NewGuid().ToString("N");AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));
            TerrainData before=null;
            try
            {
                var original=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/WindrunnerProduction/World/Terrain/CentralBasin.asset");
                var target=Terrain.CreateTerrainGameObject(original).GetComponent<Terrain>();
                var tile=StudioSetup.Attach(target,StudioSetup.Preset<SurfaceProfile>("Basin surfaces"),folder);
                before=Object.Instantiate(target.terrainData);
                var donor=new TerrainData{heightmapResolution=33};AssetDatabase.CreateAsset(donor,folder+"/donor.asset");
                var prefabs=Paths("Prefab");
                var tree=AssetDatabase.LoadAssetAtPath<GameObject>(prefabs.First(p=>p.Contains("/Prefabs/Trees/")&&!p.Contains("Billboard")));
                var grass=AssetDatabase.LoadAssetAtPath<GameObject>(prefabs.First(p=>p.Contains("/Prefabs/Ground Foliage/")&&p.ToLowerInvariant().Contains("grass")));
                donor.treePrototypes=new[]{new TreePrototype{prefab=tree}};
                donor.detailPrototypes=new[]{new DetailPrototype{prototype=grass,usePrototypeMesh=true,useInstancing=true,
                    renderMode=DetailRenderMode.VertexLit,minWidth=1,maxWidth=1,minHeight=1,maxHeight=1}};
                donor.terrainLayers=Paths("TerrainLayer").Select(AssetDatabase.LoadAssetAtPath<TerrainLayer>).ToArray();
                var source=Terrain.CreateTerrainGameObject(donor).GetComponent<Terrain>();
                var state=EditorJsonUtility.ToJson(tile.state);
                var plan=TerrainPrototypeTransfer.CreatePreview(source,target,PrototypeCategories.All);
                Assert.AreEqual(25,plan.Added);TerrainPrototypeTransfer.Apply(plan);
                TerrainPrototypeTransfer.AssertPaintPreserved(before,target.terrainData);
                Assert.AreEqual(state,EditorJsonUtility.ToJson(tile.state));
                Assert.AreEqual(0,TerrainPrototypeTransfer.CreatePreview(source,target,PrototypeCategories.All).Added);
                Undo.PerformUndo();TerrainPrototypeTransfer.AssertPaintPreserved(before,target.terrainData);
            }
            finally
            {
                if(before)Object.DestroyImmediate(before);
                if(previous.IsValid())SceneManager.SetActiveScene(previous);
                if(SceneManager.sceneCount==1)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                else EditorSceneManager.CloseScene(scene,true);
                AssetDatabase.DeleteAsset(folder);
            }
        }
        [Test] public void RepresentativeForestPrefabsRenderWithoutErrorPink()
        {
            Assert.AreNotEqual(GraphicsDeviceType.Null,SystemInfo.graphicsDeviceType,"Run with -Rendered");
            var preview=new PreviewRenderUtility();
            var prefabs=Paths("Prefab");
            var samples=new[]{prefabs.First(p=>p.Contains("/Trees/")&&!p.Contains("Billboard")),
                prefabs.First(p=>p.Contains("Bush_1")),prefabs.First(p=>p.Contains("Rock1.")),
                prefabs.First(p=>p.Contains("/Ground Foliage/")&&p.ToLowerInvariant().Contains("grass"))};
            try
            {
                preview.camera.backgroundColor=new Color(.12f,.15f,.19f);preview.camera.clearFlags=CameraClearFlags.SolidColor;
                preview.lights[0].intensity=1.5f;preview.lights[0].transform.rotation=Quaternion.Euler(45,25,0);
                foreach(var path in samples)
                {
                    var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));preview.AddSingleGO(go);
                    var bounds=go.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
                    float radius=Mathf.Max(bounds.extents.magnitude,.5f);
                    preview.camera.transform.position=bounds.center+new Vector3(1,.4f,-1).normalized*radius*3;
                    preview.camera.transform.LookAt(bounds.center);preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=radius*10;
                    preview.BeginStaticPreview(new Rect(0,0,512,512));preview.Render(true);var image=preview.EndStaticPreview();
                    try
                    {
                        var pixels=image.GetPixels32();int pink=pixels.Count(c=>c.r>220&&c.b>220&&c.g<40);
                        Assert.Less(pink,20,path+" rendered error pink");
                        int changed=pixels.Count(c=>Math.Abs(c.r-pixels[0].r)+Math.Abs(c.g-pixels[0].g)+Math.Abs(c.b-pixels[0].b)>30);
                        Assert.Greater(changed,100,path+" must actually render");
                        File.WriteAllBytes("Logs/ProductionMigration/forest-"+Path.GetFileNameWithoutExtension(path)+".png",image.EncodeToPNG());
                    }
                    finally {Object.DestroyImmediate(image);Object.DestroyImmediate(go);}
                }
            }
            finally {preview.Cleanup();}
        }
    }
}
