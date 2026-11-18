using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Windrunner.TerrainAuthoring.Editor
{
    public sealed class EnvironmentInspection
    {
        public readonly List<string> messages = new List<string>();
        public Bounds bounds;
        public bool tree, detail;
        public static EnvironmentInspection Asset(Object asset)
        {
            var result = new EnvironmentInspection();
            if (!asset) { result.messages.Add("ERROR · Asset is missing or outside enabled sources."); return result; }
            if (asset is GameObject go)
            {
                var renderers=go.GetComponentsInChildren<Renderer>(true);
                if(renderers.Length==0)result.messages.Add("WARNING · No Renderer; not a vegetation candidate.");
                else { result.bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))result.bounds.Encapsulate(r.bounds); }
                result.tree=PrefabUtility.IsPartOfPrefabAsset(go) && renderers.Length>0;
                result.detail=result.tree && go.GetComponentInChildren<MeshFilter>() && go.GetComponentInChildren<MeshRenderer>();
                result.messages.Add($"INFO · Bounds {result.bounds.size}; {renderers.Length} renderers; {go.GetComponentsInChildren<Collider>(true).Length} colliders.");
                var lods=go.GetComponentsInChildren<LODGroup>(true);
                result.messages.Add($"INFO · {lods.Length} LOD groups. Compatibility is structural, not a rendering guarantee.");
                if(lods.Length==0)result.messages.Add("WARNING · No LODGroup; assess repeated-placement cost.");
                foreach(var lod in lods)foreach(var l in lod.GetLODs())if(l.renderers.Any(r=>!r))result.messages.Add("ERROR · Missing LOD renderer.");
                foreach(var t in go.GetComponentsInChildren<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)result.messages.Add("ERROR · Missing script on "+t.name);
                foreach(var c in go.GetComponentsInChildren<MeshCollider>(true))if(!c.sharedMesh)result.messages.Add("WARNING · Empty MeshCollider: "+c.name);
                foreach(var m in renderers.SelectMany(r=>r.sharedMaterials).Distinct())CheckMaterial(m,result.messages);
                foreach(var f in go.GetComponentsInChildren<MeshFilter>(true))if(!f.sharedMesh)result.messages.Add("ERROR · Missing mesh on "+f.name);
            }
            if(asset is Material material)CheckMaterial(material,result.messages);
            if(asset is TerrainLayer layer)
            {
                if(!layer.diffuseTexture)result.messages.Add("ERROR · Missing diffuse texture.");
                result.messages.Add($"INFO · Tile {layer.tileSize}; normal {(layer.normalMapTexture?layer.normalMapTexture.name:"none (optional)")}.");
            }
            // Serialized null references with a retained identifier distinguish broken links from optional empty slots.
            var objects=asset is GameObject prefab ? prefab.GetComponentsInChildren<Component>(true).Where(c=>c).Cast<Object>() : new[]{asset};
            foreach(var obj in objects)
            {
                var so=new SerializedObject(obj);var it=so.GetIterator();
                while(it.Next(true))if(it.propertyType==SerializedPropertyType.ObjectReference && !it.objectReferenceValue && it.objectReferenceEntityIdValue!=EntityId.None)
                    result.messages.Add("ERROR · Unresolved reference: "+obj.name+" / "+it.propertyPath);
            }
            return result;
        }
        static void CheckMaterial(Material material,List<string> messages)
        {
            if(!material){messages.Add("ERROR · Missing material slot.");return;}
            if(!material.shader){messages.Add("ERROR · Missing shader: "+material.name);return;}
            if(!material.shader.isSupported || material.shader.name.Contains("InternalErrorShader"))messages.Add("ERROR · Unsupported shader: "+material.name+" / "+material.shader.name);
            else messages.Add("INFO · "+material.name+" / "+material.shader.name);
            foreach(string property in material.GetTexturePropertyNames())
            {
                // Unassigned textures can be intentional; warn rather than fabricate a missing dependency.
                if(!material.GetTexture(property) && (property=="_BaseMap" || property=="_MainTex"))messages.Add("INFO · No base texture on "+material.name+" (may use solid color).");
            }
        }
        public static List<string> Scene(Scene scene)
        {
            var notes=new List<string>();if(!scene.IsValid()||!scene.isLoaded)return new List<string>{"ERROR · Load a scene first."};
            var roots=scene.GetRootGameObjects();var transforms=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var terrains=roots.SelectMany(r=>r.GetComponentsInChildren<Terrain>(true)).ToArray();
            var materials=new HashSet<Material>();int rendererCount=0;
            foreach(var t in transforms)
            {
                if(PrefabUtility.IsPrefabAssetMissing(t.gameObject))notes.Add("ERROR · Missing prefab: "+t.name);
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)notes.Add("ERROR · Missing script: "+t.name);
                if(t.lossyScale.x<=0 || t.lossyScale.y<=0 || t.lossyScale.z<=0 || t.lossyScale.magnitude>100)notes.Add("WARNING · Review scale: "+t.name+" "+t.lossyScale);
                var r=t.GetComponent<Renderer>();if(!r)continue;rendererCount++;foreach(var m in r.sharedMaterials)materials.Add(m);
                if(!t.GetComponentInParent<LODGroup>() && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))notes.Add("INFO · No LOD group: "+t.name);
                var terrain=terrains.FirstOrDefault(a=>a.terrainData && r.bounds.center.x>=a.transform.position.x && r.bounds.center.z>=a.transform.position.z && r.bounds.center.x<=a.transform.position.x+a.terrainData.size.x && r.bounds.center.z<=a.transform.position.z+a.terrainData.size.z);
                if(terrain && t.name.IndexOf("rock",StringComparison.OrdinalIgnoreCase)>=0)
                {
                    float ground=terrain.SampleHeight(r.bounds.center)+terrain.transform.position.y;
                    if(r.bounds.min.y>ground+1)notes.Add("WARNING · Possible floating rock (bounds heuristic): "+t.name);
                    if(r.bounds.max.y<ground)notes.Add("WARNING · Possibly buried rock: "+t.name);
                }
            }
            foreach(var m in materials)CheckMaterial(m,notes);
            foreach(var terrain in terrains)notes.AddRange(Terrain(terrain));
            notes.Insert(0,$"INFO · {scene.name}: {transforms.Length} transforms, {rendererCount} renderers. Heuristics are review hints, not measured bottlenecks.");
            if(rendererCount>5000)notes.Add("WARNING · More than 5,000 renderers; profile representative views before optimizing.");
            return notes;
        }
        public static List<string> Terrain(Terrain terrain)
        {
            var notes=new List<string>();if(!terrain || !terrain.terrainData)return new List<string>{"ERROR · Select a Terrain with TerrainData."};
            var d=terrain.terrainData;notes.Add("INFO · Data: "+AssetDatabase.GetAssetPath(d));
            if(!AssetDatabase.Contains(d)||!AssetDatabase.IsOpenForEdit(d))notes.Add("ERROR · Data is not an editable saved asset.");
            var users=Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include).Count(t=>t.terrainData==d);
            if(users>1)notes.Add("ERROR · Shared TerrainData across "+users+" loaded Terrains. Unloaded scenes are not covered.");
            if(d.terrainLayers.Any(l=>!l))notes.Add("ERROR · Missing TerrainLayer.");
            notes.Add($"INFO · {d.treePrototypes.Length} tree prototypes / {d.treeInstanceCount} trees; {d.detailPrototypes.Length} detail prototypes; {d.terrainLayers.Length} layers. No indices changed.");
            if(d.treeInstanceCount>100000)notes.Add("WARNING · Over 100,000 trees; profile density and LODs.");
            var tile=terrain.GetComponent<StudioTile>();
            if(tile && tile.state)
            {
                notes.Add($"INFO · Protected captures {tile.state.protectedPaint.Count}; native dirty {tile.state.nativeDirty}; procedural dirty {tile.state.dirty}; automatic {tile.state.automatic}.");
                try{notes.Add(SetupValidation.Inspect(tile));}catch(Exception e){notes.Add("ERROR · "+e.Message);}
            }
            else notes.Add("INFO · No Terrain Studio binding. Ownership must be confirmed before authoring; this workspace does not create one automatically.");
            return notes;
        }
    }
    // Disposable source only. Every target write goes through the established transfer validator/transaction.
    public sealed class EnvironmentPrototypePreparation : IDisposable
    {
        Scene scene; TerrainData data; Terrain source;
        public TerrainPrototypeTransfer.Preview preview;
        public EnvironmentPrototypePreparation(Object asset,Terrain target,PrototypeCategories category)
        {
            try
            {
                scene=EditorSceneManager.NewPreviewScene();data=new TerrainData{heightmapResolution=33};
                var go=new GameObject("Environment prototype preview",typeof(Terrain));SceneManager.MoveGameObjectToScene(go,scene);source=go.GetComponent<Terrain>();source.terrainData=data;
                if(category==PrototypeCategories.Layers && asset is TerrainLayer layer)data.terrainLayers=new[]{layer};
                else if(category==PrototypeCategories.Trees && asset is GameObject tree)data.treePrototypes=new[]{new TreePrototype{prefab=tree}};
                else if(category==PrototypeCategories.Details)
                {
                    var detail=new DetailPrototype{minWidth=1,maxWidth=1,minHeight=1,maxHeight=1,healthyColor=Color.white,dryColor=Color.white};
                    if(asset is GameObject mesh){detail.prototype=mesh;detail.usePrototypeMesh=true;detail.useInstancing=true;detail.renderMode=DetailRenderMode.VertexLit;}
                    else if(asset is Texture2D texture){detail.prototypeTexture=texture;detail.renderMode=DetailRenderMode.GrassBillboard;}
                    else throw new ArgumentException("Choose a mesh prefab or detail texture.");
                    data.detailPrototypes=new[]{detail};
                }
                else throw new ArgumentException("Choose an asset compatible with the selected category.");
                preview=TerrainPrototypeTransfer.CreatePreview(source,target,category);
            }
            catch{Dispose();throw;}
        }
        public void Apply()=>TerrainPrototypeTransfer.Apply(preview);
        public void Dispose(){preview=null;if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);if(data)Object.DestroyImmediate(data);data=null;}
    }
    public static class EnvironmentPlacement
    {
        public static GameObject Place(GameObject prefab,Scene scene,Transform parent,Vector3 point,Vector3 normal,EnvironmentPlacementSettings settings,float yawSample=0,float scaleSample=0)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene) || PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new InvalidOperationException("Place only in a loaded normal scene in Edit Mode.");
            if(!prefab || !PrefabUtility.IsPartOfPrefabAsset(prefab))throw new ArgumentException("Choose a prefab or model asset.");
            if(parent && parent.gameObject.scene!=scene)throw new ArgumentException("Parent must belong to the destination scene.");
            if(!(settings.scale>0) || settings.minScale<=0 || settings.maxScale<settings.minScale)throw new ArgumentException("Use positive, ordered scales.");
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Place environment prefab");
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
            Undo.RegisterCreatedObjectUndo(instance,"Place environment prefab");
            if(parent)Undo.SetTransformParent(instance.transform,parent,"Parent environment prefab");
            Undo.RecordObject(instance.transform,"Position environment prefab");
            instance.transform.position=point+normal*settings.offset;
            instance.transform.rotation=(settings.alignNormal?Quaternion.FromToRotation(Vector3.up,normal):Quaternion.identity)*Quaternion.Euler(0,settings.yaw+(settings.randomYaw?yawSample*360:0),0);
            instance.transform.localScale=prefab.transform.localScale*settings.scale*(settings.varyScale?Mathf.Lerp(settings.minScale,settings.maxScale,scaleSample):1);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);return instance;
        }
    }
}
