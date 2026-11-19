using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Windrunner.Suntemple.Editor
{
    public static class SuntempleDependencyAudit
    {
        [Serializable] public class Row {public string asset,property,detail;public bool sceneDependency;}
        [Serializable] public class Report {public Row[] findings;public string[] dependencies;}
        public static void Run()
        {
            var dependencies=new HashSet<string>(AssetDatabase.GetDependencies(SuntempleValidation.Repaired,true));
            var rows=new List<Row>();var root="Assets/WindrunnerCompatibility/Suntemple";
            foreach(var g in AssetDatabase.FindAssets("t:Material",new[]{root}))
            {
                var path=AssetDatabase.GUIDToAssetPath(g);var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                // Check only declared shader inputs; stale serialized inputs from prior shaders are separate.
                foreach(var name in m.GetTexturePropertyNames())
                {
                    var t=m.GetTexture(name);if(!t)continue;
                    var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t)) as TextureImporter;
                    if(importer!=null&&(name=="_BumpMap"||name=="_WaterNormals"))rows.Add(new Row{asset=path,property=name,detail="Assigned "+importer.textureType+": "+importer.assetPath,sceneDependency=dependencies.Contains(path)});
                }
                var serialized=new SerializedObject(m);var textures=serialized.FindProperty("m_SavedProperties.m_TexEnvs");
                if(textures==null)continue;
                for(int i=0;i<textures.arraySize;i++)
                {
                    var pair=textures.GetArrayElementAtIndex(i);string name=pair.FindPropertyRelative("first").stringValue;var value=pair.FindPropertyRelative("second.m_Texture");
                    if(value.objectReferenceValue==null&&value.objectReferenceEntityIdValue!=default)rows.Add(new Row{asset=path,property=name,detail=m.HasProperty(name)?"MISSING declared texture":"Stale unavailable texture for undeclared property",sceneDependency=dependencies.Contains(path)});
                }
            }
            foreach(var g in AssetDatabase.FindAssets("t:Prefab",new[]{root}))
            {
                string path=AssetDatabase.GUIDToAssetPath(g);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                int scripts=prefab.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                int meshes=prefab.GetComponentsInChildren<MeshFilter>(true).Count(m=>!m.sharedMesh);
                int materials=prefab.GetComponentsInChildren<Renderer>(true).Sum(r=>r.sharedMaterials.Count(m=>!m));
                if(scripts+meshes+materials>0)rows.Add(new Row{asset=path,property="Prefab",detail=$"Missing scripts={scripts}, meshes={meshes}, materials={materials}",sceneDependency=dependencies.Contains(path)});
            }
            foreach(var g in AssetDatabase.FindAssets("t:TerrainLayer",new[]{root}))
            {string path=AssetDatabase.GUIDToAssetPath(g);var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);if(!layer.diffuseTexture)rows.Add(new Row{asset=path,property="Diffuse",detail="Missing diffuse texture",sceneDependency=dependencies.Contains(path)});}
            File.WriteAllText("Logs/SuntempleRepair/dependency-findings.json",JsonUtility.ToJson(new Report{findings=rows.ToArray(),dependencies=dependencies.OrderBy(p=>p).ToArray()},true));
            SuntempleValidation.Views();
        }
    }
}
