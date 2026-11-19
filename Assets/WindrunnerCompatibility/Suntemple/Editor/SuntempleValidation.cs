using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Windrunner.Suntemple.Editor
{
    public static class SuntempleValidation
    {
        public const string Original="Assets/Polyart/PolyartStudio/DreamscapeSunTemple/Scenes/DemoMap.unity";
        public const string Repaired="Assets/WindrunnerCompatibility/Suntemple/DreamscapeSunTemple/Scenes/DemoMap.unity";
        [Serializable] public class MaterialRow {public string path,name,shader,pipeline;public bool supported,usedByRenderer;public int queue;public string[] textures,errors;}
        [Serializable] public class ObjectRow {public string name,type;public Vector3 position,extent;}
        [Serializable] public class Report {public string scene,pipeline;public int objects,renderers,missingScripts,missingMaterials,missingMeshes;public MaterialRow[] materials;public ObjectRow[] landmarks,cameras;public string[] scripts,terrains,roots;}
        public static void Before()=>Audit(Original,"before");
        public static void After()=>Audit(Repaired,"after");
        [MenuItem("Tools/Windrunner/Suntemple/Open Repaired DemoMap")]
        public static void OpenRepaired(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(Repaired);}
        public static void Views()
        {
            After();
            var renderers=UnityEngine.Object.FindObjectsByType<Renderer>();
            var source=Camera.main;
            var go=new GameObject("Temporary inspection camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;
            var data=camera.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.requiresColorTexture=true;data.renderPostProcessing=true;
            var rows=new List<ObjectRow>();
            try
            {
                foreach(var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>())ps.Simulate(2,true,true);
                foreach(var category in new[]{"FountainFlowing","Plants","Lake","Fire","Ground"})
                {
                    var r=renderers.Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.bounds.size.magnitude>.1f&&r.bounds.size.magnitude<60&&r.sharedMaterials.Any(m=>m&&(m.name.IndexOf(category,StringComparison.OrdinalIgnoreCase)>=0||m.shader.name.IndexOf(category,StringComparison.OrdinalIgnoreCase)>=0))).OrderBy(r=>Vector3.Distance(source.transform.position,r.bounds.center)).FirstOrDefault();
                    if(!r)continue;
                    var center=r.bounds.center;float distance=Mathf.Clamp(r.bounds.extents.magnitude*2.2f,4,35);
                    var direction=(source.transform.position-center).normalized;direction.y=Mathf.Max(direction.y,.18f);direction.Normalize();
                    camera.transform.position=center+direction*distance;camera.transform.LookAt(center);camera.nearClipPlane=.1f;
                    if(category=="Lake"){camera.transform.position=center+new Vector3(0,9,2);camera.transform.LookAt(center);}
                    Capture(camera,"Logs/SuntempleRepair/after-"+category+".png");rows.Add(new ObjectRow{name=category+":"+PathOf(r.transform),position=camera.transform.position,extent=camera.transform.eulerAngles});
                }
                File.WriteAllText("Logs/SuntempleRepair/viewpoints.json",JsonUtility.ToJson(new Report{cameras=rows.ToArray()},true));
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        static void Audit(string path,string label)
        {
            Directory.CreateDirectory("Logs/SuntempleRepair");
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            var roots=scene.GetRootGameObjects();var all=roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var renderers=roots.SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).ToArray();
            var cameras=roots.SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).ToArray();
            var dependencies=AssetDatabase.GetDependencies(path,true);
            var mats=dependencies.Where(p=>p.EndsWith(".mat",StringComparison.OrdinalIgnoreCase)).Select(AssetDatabase.LoadAssetAtPath<Material>).Concat(renderers.SelectMany(r=>r.sharedMaterials)).Where(m=>m).Distinct().ToArray();
            var report=new Report{scene=path,pipeline=GraphicsSettings.currentRenderPipeline?AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline):"Built-in",objects=all.Length,renderers=renderers.Length};
            report.missingScripts=all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));report.missingMaterials=renderers.Sum(r=>r.sharedMaterials.Count(m=>!m));report.missingMeshes=all.Select(t=>t.GetComponent<MeshFilter>()).Count(m=>m&&!m.sharedMesh);
            report.materials=mats.Select(m=>new MaterialRow{path=AssetDatabase.GetAssetPath(m),name=m.name,shader=m.shader?m.shader.name:"MISSING",supported=m.shader&&m.shader.isSupported,usedByRenderer=renderers.Any(r=>r.sharedMaterials.Contains(m)),pipeline=m.GetTag("RenderPipeline",false,"none"),queue=m.renderQueue,textures=m.GetTexturePropertyNames().Where(p=>m.GetTexture(p)).Select(p=>p+"="+AssetDatabase.GetAssetPath(m.GetTexture(p))).ToArray(),errors=m.shader?ShaderUtil.GetShaderMessages(m.shader).Select(e=>e.severity+": "+e.message).ToArray():new[]{"Missing shader"}}).ToArray();
            report.landmarks=renderers.Where(r=>r.enabled&&r.gameObject.activeInHierarchy).Where(r=>System.Text.RegularExpressions.Regex.IsMatch(r.name,"temple|water|fountain|tree|ground|terrain|door|window|fire",System.Text.RegularExpressions.RegexOptions.IgnoreCase)).Take(450).Select(r=>new ObjectRow{name=PathOf(r.transform),type=r.GetType().Name,position=r.bounds.center,extent=r.bounds.extents}).ToArray();
            report.roots=roots.Select(g=>g.name).ToArray();report.terrains=all.Select(t=>t.GetComponent<Terrain>()).Where(t=>t).Select(t=>t.name+"="+(t.materialTemplate?t.materialTemplate.shader.name:"default")+"="+AssetDatabase.GetAssetPath(t.terrainData)).ToArray();
            report.cameras=cameras.Select(c=>new ObjectRow{name=PathOf(c.transform),position=c.transform.position,extent=c.transform.eulerAngles}).ToArray();report.scripts=roots.SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(m=>m).GroupBy(m=>m.GetType().FullName).Select(g=>g.Key+"="+g.Count()).ToArray();
            File.WriteAllText("Logs/SuntempleRepair/"+label+"-audit.json",JsonUtility.ToJson(report,true));
            var source=cameras.FirstOrDefault(c=>c.CompareTag("MainCamera"))??cameras.FirstOrDefault();
            if(path==Repaired&&source)
            {
                var fly=source.GetComponent<Flythrough>();
                if(!fly||!fly.sun||!fly.mycamera)throw new InvalidOperationException("Repaired demo flythrough has missing required scene bindings");
                foreach(var lod in all.Select(t=>t.GetComponent<LODGroup>()).Where(l=>l))
                    if(lod.GetLODs().Any(l=>l.renderers.Any(r=>!r)))throw new InvalidOperationException("Missing LOD renderer: "+PathOf(lod.transform));
            }
            if(source){var go=new GameObject("Temporary diagnostic camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(source);camera.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);camera.enabled=false;var extra=camera.GetUniversalAdditionalCameraData();extra.requiresDepthTexture=true;extra.requiresColorTexture=true;extra.renderPostProcessing=true;Capture(camera,"Logs/SuntempleRepair/"+label+"-entry.png");UnityEngine.Object.DestroyImmediate(go);}
            Debug.Log($"SUNTEMPLE {label}: objects={report.objects}, renderers={report.renderers}, materials={mats.Length}, missingScripts={report.missingScripts}, missingMaterials={report.missingMaterials}, missingMeshes={report.missingMeshes}, pipeline={report.pipeline}");
        }
        public static void Capture(Camera camera,string path)
        {
            bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            var rt=new RenderTexture(1600,900,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);ShaderUtil.allowAsyncCompilation=async;}
        }
    }
}
