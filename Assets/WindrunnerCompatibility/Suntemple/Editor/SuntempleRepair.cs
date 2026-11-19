using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
namespace Windrunner.Suntemple.Editor
{
    public static class SuntempleRepair
    {
        [Serializable] public class Entry {public string original,variant,originalGuid,variantGuid,source;}
        [Serializable] public class Mapping {public Entry[] entries;}
        public static void Apply()
        {
            var map=JsonUtility.FromJson<Mapping>(File.ReadAllText("Documentation/SuntempleVariantMapping.json"));
            var materials=map.entries.Where(e=>e.variant.EndsWith(".mat")).ToDictionary(e=>e.original,e=>AssetDatabase.LoadAssetAtPath<Material>(e.variant));
            int converted=0;
            foreach(var m in materials.Values)
            {
                if(!m)throw new InvalidOperationException("Missing compatibility material");
                if(m.shader&&(m.shader.name=="Standard"||m.shader.name.StartsWith("Legacy Shaders/Particles/")||m.shader.name=="Particles/Standard Unlit")){Convert(m);converted++;}
                AssetDatabase.SaveAssetIfDirty(m);
            }
            var layers=map.entries.Where(e=>e.variant.EndsWith(".terrainlayer")).ToDictionary(e=>e.original,e=>AssetDatabase.LoadAssetAtPath<TerrainLayer>(e.variant));
            foreach(var entry in map.entries.Where(e=>e.variant.EndsWith("Terrain_Data.asset")))
            {
                var terrain=AssetDatabase.LoadAssetAtPath<TerrainData>(entry.variant);
                terrain.terrainLayers=terrain.terrainLayers.Select(l=>l&&layers.TryGetValue(AssetDatabase.GetAssetPath(l),out var replacement)?replacement:l).ToArray();
                var trees=terrain.treePrototypes;foreach(var tree in trees){if(!tree.prefab)continue;var match=map.entries.FirstOrDefault(e=>e.original==AssetDatabase.GetAssetPath(tree.prefab));if(match!=null)tree.prefab=AssetDatabase.LoadAssetAtPath<GameObject>(match.variant);}terrain.treePrototypes=trees;EditorUtility.SetDirty(terrain);AssetDatabase.SaveAssetIfDirty(terrain);
            }
            var scene=EditorSceneManager.OpenScene(SuntempleValidation.Repaired,OpenSceneMode.Single);
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var meshes=all.Select(t=>t.GetComponent<MeshFilter>()).Where(m=>m).ToDictionary(m=>m,m=>m.sharedMesh);
            var streams=all.Select(t=>t.GetComponent<MeshRenderer>()).Where(m=>m).ToDictionary(m=>m,m=>m.additionalVertexStreams);
            int removed=0;foreach(var t in all)removed+=GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach(var pair in meshes)if(pair.Key.sharedMesh!=pair.Value)throw new InvalidOperationException("Missing-component removal changed a bound mesh.");
            foreach(var pair in streams)if(pair.Key.additionalVertexStreams!=pair.Value)throw new InvalidOperationException("Missing-component removal changed vertex streams.");
            foreach(var renderer in all.Select(t=>t.GetComponent<Renderer>()).Where(r=>r))Replace(renderer,materials);
            foreach(var camera in all.Select(t=>t.GetComponent<Camera>()).Where(c=>c))
            {var data=camera.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.requiresColorTexture=true;data.renderPostProcessing=true;}
            // PPv2 and OnRenderImage are Built-in-only; replace the grading volume with the local vendor URP profile.
            foreach(var component in all.SelectMany(t=>t.GetComponents<MonoBehaviour>()).Where(c=>c).ToArray())
                if(component.GetType().Namespace=="UnityEngine.Rendering.PostProcessing"||component.GetType().FullName=="Polyart.OceanUnderwaterPostProcessing")Object.DestroyImmediate(component);
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/WindrunnerCompatibility/Suntemple/DreamscapeSunTemple/Scenes/DemoMap/PostProcess Profile.asset");
            if(profile&&!all.Any(t=>t.GetComponent<Volume>()&&t.GetComponent<Volume>().sharedProfile==profile)){var volume=new GameObject("Suntemple URP grading (vendor profile)").AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;}
            EditorSceneManager.SaveScene(scene,SuntempleValidation.Repaired);
            Debug.Log($"SUNTEMPLE REPAIR: converted {converted} standard/particle variants; removed {removed} orphaned authoring components; retained {meshes.Count} mesh bindings and {streams.Count} vertex-stream bindings.");
            File.WriteAllText("Logs/SuntempleRepair/repair-summary.txt",$"Converted fallback materials: {converted}\nRemoved orphaned authoring components: {removed}\nUnchanged bound meshes: {meshes.Count}\nUnchanged additional vertex streams: {streams.Count}\nURP vendor grading profile: {profile}\n");
        }
        static void Replace(Renderer renderer,Dictionary<string,Material> map)
        {
            var mats=renderer.sharedMaterials;bool changed=false;
            for(int i=0;i<mats.Length;i++)if(mats[i]&&map.TryGetValue(AssetDatabase.GetAssetPath(mats[i]),out var replacement)){mats[i]=replacement;changed=true;}
            if(changed){renderer.sharedMaterials=mats;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
        }
        public static void Convert(Material m)
        {
            if(m.parent)m.parent=null;
            string old=m.shader.name;bool particle=old!="Standard";bool additive=old.Contains("Additive")||(particle&&m.HasProperty("_DstBlend")&&m.GetFloat("_DstBlend")==1);
            var texture=m.HasProperty("_MainTex")?m.GetTexture("_MainTex"):null;var scale=m.HasProperty("_MainTex")?m.GetTextureScale("_MainTex"):Vector2.one;var offset=m.HasProperty("_MainTex")?m.GetTextureOffset("_MainTex"):Vector2.zero;
            var color=m.HasProperty("_Color")?m.GetColor("_Color"):m.HasProperty("_TintColor")?m.GetColor("_TintColor")*2:Color.white;
            float mode=m.HasProperty("_Mode")?m.GetFloat("_Mode"):particle?2:0;float smooth=m.HasProperty("_Glossiness")?m.GetFloat("_Glossiness"):.5f;
            bool glossMap=m.HasProperty("_MetallicGlossMap")&&m.GetTexture("_MetallicGlossMap");if(glossMap&&m.HasProperty("_GlossMapScale"))smooth=m.GetFloat("_GlossMapScale");
            bool normal=m.HasProperty("_BumpMap")&&m.GetTexture("_BumpMap");bool emission=m.IsKeywordEnabled("_EMISSION");
            m.shader=Shader.Find(particle?"Universal Render Pipeline/Particles/Unlit":"Universal Render Pipeline/Lit");if(!m.shader)throw new InvalidOperationException("URP shader not found");
            m.shaderKeywords=Array.Empty<string>();m.SetTexture("_BaseMap",texture);m.SetTextureScale("_BaseMap",scale);m.SetTextureOffset("_BaseMap",offset);m.SetColor("_BaseColor",color);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);
            bool transparent=particle||mode>=2;bool clip=mode==1;m.SetFloat("_Surface",transparent?1:0);m.SetFloat("_AlphaClip",clip?1:0);m.SetFloat("_ZWrite",transparent?0:1);
            m.SetFloat("_SrcBlend",(float)(transparent?BlendMode.SrcAlpha:BlendMode.One));m.SetFloat("_DstBlend",(float)(transparent?(additive?BlendMode.One:BlendMode.OneMinusSrcAlpha):BlendMode.Zero));
            m.SetFloat("_Blend",additive?2:0);m.SetOverrideTag("RenderType",transparent?"Transparent":clip?"TransparentCutout":"Opaque");m.renderQueue=transparent?3000:clip?2450:2000;
            if(transparent)m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");if(clip)m.EnableKeyword("_ALPHATEST_ON");if(normal)m.EnableKeyword("_NORMALMAP");if(glossMap)m.EnableKeyword("_METALLICSPECGLOSSMAP");if(emission)m.EnableKeyword("_EMISSION");m.SetShaderPassEnabled("ShadowCaster",!transparent);EditorUtility.SetDirty(m);
        }
    }
}
