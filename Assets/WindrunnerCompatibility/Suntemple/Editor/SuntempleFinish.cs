using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Windrunner.Suntemple.Editor
{
    public static class SuntempleFinish
    {
        const string Root="Assets/WindrunnerCompatibility/Suntemple/";
        public static void Run()
        {
            var map=JsonUtility.FromJson<SuntempleRepair.Mapping>(File.ReadAllText("Documentation/SuntempleVariantMapping.json"));
            var materials=map.entries.Where(e=>e.variant.EndsWith(".mat")).ToDictionary(e=>e.original,e=>AssetDatabase.LoadAssetAtPath<Material>(e.variant));
            var fountain=AssetDatabase.LoadAssetAtPath<Material>(Root+"DreamscapeSunTemple/Materials/Water/MI_Suntemple_WaterFountainFlowing.mat");fountain.shader=Shader.Find("Windrunner/Suntemple/Fountain Flow URP");if(!fountain.shader)throw new InvalidOperationException("Missing fountain fallback");fountain.renderQueue=3000;EditorUtility.SetDirty(fountain);AssetDatabase.SaveAssetIfDirty(fountain);
            foreach(var material in materials.Values.Where(m=>m&&m.shader&&(m.shader.name.StartsWith("Legacy Shaders/Particles/")||m.shader.name=="Particles/Standard Unlit"))){SuntempleRepair.Convert(material);AssetDatabase.SaveAssetIfDirty(material);}
            string defaultPath=Root+"SharedResources/Materials/Effects/DefaultParticleURP.mat";var particle=AssetDatabase.LoadAssetAtPath<Material>(defaultPath);
            var scene=EditorSceneManager.OpenScene(SuntempleValidation.Repaired,OpenSceneMode.Single);
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).ToArray();
            if(!particle){var original=all.SelectMany(r=>r.sharedMaterials).First(m=>m&&m.name=="Default-ParticleSystem");particle=new Material(original){name="DefaultParticleURP"};SuntempleRepair.Convert(particle);AssetDatabase.CreateAsset(particle,defaultPath);}
            int slots=0;
            void Remap(GameObject go)
            {
                foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    var mats=renderer.sharedMaterials;bool dirty=false;
                    for(int i=0;i<mats.Length;i++)
                    {
                        if(!mats[i])continue;
                        if(materials.TryGetValue(AssetDatabase.GetAssetPath(mats[i]),out var target)){mats[i]=target;dirty=true;slots++;}
                        else if(mats[i].name=="Default-ParticleSystem"){mats[i]=particle;dirty=true;slots++;}
                    }
                    if(dirty){renderer.sharedMaterials=mats;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
                }
            }
            foreach(var go in scene.GetRootGameObjects())Remap(go);
            EditorSceneManager.SaveScene(scene,SuntempleValidation.Repaired);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            int prefabs=0;
            foreach(var entry in map.entries.Where(e=>e.variant.EndsWith(".prefab")&&e.variant.Contains("DreamscapeSunTemple/")))
            {
                var contents=PrefabUtility.LoadPrefabContents(entry.variant);
                try{int before=slots;Remap(contents);if(slots!=before){PrefabUtility.SaveAsPrefabAsset(contents,entry.variant);prefabs++;}}
                finally{PrefabUtility.UnloadPrefabContents(contents);}
            }
            File.WriteAllText("Logs/SuntempleRepair/final-pass.txt",$"Remapped inherited material slots: {slots}; prefab variants updated: {prefabs}. Original source assets unchanged.\n");
            Debug.Log("SUNTEMPLE final material/prefab pass complete.");
        }
    }
}
