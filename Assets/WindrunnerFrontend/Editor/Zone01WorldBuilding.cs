using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using AirflowPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Windrunner.Frontend.Editor
{
    // One explicit authoring operation; no automatic scene changes or runtime component.
    public static class Zone01WorldBuilding
    {
        public const string Prefab="Assets/WindrunnerProduction/World/Prefabs/ZoneAirNode.prefab";
        static Transform Group(Transform parent,string name)
        {
            var found=parent.Find(name);if(found)return found;
            var g=new GameObject(name).transform;g.SetParent(parent,false);return g;
        }
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        public static void Prepare()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Run the reviewed batch authoring operation with Unity closed.");
            var scene=EditorSceneManager.OpenScene(PlayerSession.ZoneScene);
            var root=scene.GetRootGameObjects().Single().transform;
            if(root.name!="WorldRoot" || root.Find("Traversal/AirNodes"))throw new InvalidOperationException("Unexpected root or already prepared zone.");
            var originals=root.GetComponentsInChildren<Transform>(true).Select(t=>new{t,path=PathOf(t),parent=t.parent,pos=t.position,rot=t.rotation,scale=t.lossyScale}).ToArray();
            var originalComponents=root.GetComponentsInChildren<Component>(true).Where(c=>c && !(c is Transform)).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
            var terrain=root.GetComponentInChildren<Terrain>();
            var dataPath=AssetDatabase.GetAssetPath(terrain.terrainData);var dataBytes=File.ReadAllBytes(dataPath);
            var legacy=EditorSceneManager.OpenScene(PlayerSession.WorldScene,OpenSceneMode.Additive);
            GameObject template=legacy.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<AirNode>(true)).Single(n=>n.name=="Air Node 01").gameObject;
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab))throw new InvalidOperationException("Node prefab already exists.");
            foreach(var c in template.GetComponentsInChildren<Component>(true))
            {
                if(!c)throw new InvalidOperationException("Template has a missing component.");
                var it=new SerializedObject(c).GetIterator();
                while(it.Next(true)) if(!(c==template.transform && it.propertyPath=="m_Father") && it.propertyType==SerializedPropertyType.ObjectReference && it.objectReferenceValue is Component other &&
                    other.gameObject.scene.IsValid() && !other.transform.IsChildOf(template.transform))
                    throw new InvalidOperationException("External scene dependency: "+it.propertyPath);
            }
            if(!AssetDatabase.IsValidFolder("Assets/WindrunnerProduction/World/Prefabs"))AssetDatabase.CreateFolder("Assets/WindrunnerProduction/World","Prefabs");
            var asset=PrefabUtility.SaveAsPrefabAsset(template,Prefab);if(!asset)throw new IOException("Node prefab save failed");
            EditorSceneManager.CloseScene(legacy,true);SceneManager.SetActiveScene(scene);
            foreach(var n in new[]{"Rocks","Cliffs","Trees","Bushes","Grass","GroundDetails","Water","Decorations","Audio_VFX"}) Group(root.Find("Environment"),n);
            foreach(var n in new[]{"Buildings","Bridges","Ruins"})Group(Group(root,"Structures"),n);
            foreach(var n in new[]{"Roads","TerrainAuthoring"})Group(root.Find("Terrain"),n);
            foreach(var n in new[]{"SlingTargets","WindZones","TraversalHelpers"})Group(root.Find("Traversal"),n);
            var air=Group(root.Find("Traversal"),"AirNodes");
            var routes=new[]{"StartingArea","HillsideRoute","OpenTraversal"};
            var points=new[]{new Vector2(-12,15),new Vector2(12,28),new Vector2(-160,-25),new Vector2(-180,-25),new Vector2(-200,-25),new Vector2(-18,42),new Vector2(0,53),new Vector2(18,42)};
            var report=new List<string>();
            for(int i=0;i<points.Length;i++)
            {
                string route=routes[i<2?0:i<5?1:2];var p=points[i];
                var node=(GameObject)PrefabUtility.InstantiatePrefab(asset,Group(air,route));
                node.name=$"Air Node {i+1:00} - {route}";
                node.transform.position=new Vector3(p.x,terrain.SampleHeight(new Vector3(p.x,0,p.y))+terrain.transform.position.y+3.5f,p.y);
                report.Add(node.name+" "+node.transform.position.ToString("F3"));
            }
            Physics.SyncTransforms();Validate();
            foreach(var o in originals)
                if(PathOf(o.t)!=o.path || o.t.parent!=o.parent || o.t.position!=o.pos || o.t.rotation!=o.rot || o.t.lossyScale!=o.scale)
                    throw new InvalidOperationException("Original transform changed: "+o.path);
            foreach(var pair in originalComponents)
                if(EditorJsonUtility.ToJson(pair.Key)!=pair.Value)throw new InvalidOperationException("Original component changed: "+pair.Key.name+" "+pair.Key.GetType().Name);
            if(!File.ReadAllBytes(dataPath).SequenceEqual(dataBytes))throw new InvalidOperationException("TerrainData changed");
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed");
            File.WriteAllLines("Logs/ProductionMigration/zone-worldbuilding-placement.txt",new[]{"Original transforms preserved: "+originals.Length,"Original component configurations preserved: "+originalComponents.Count,"TerrainData bytes unchanged: "+dataPath}.Concat(report));
        }
        public static void Validate()
        {
            Zone01Setup.Validate();
            var root=SceneManager.GetActiveScene().GetRootGameObjects().Single().transform;
            var terrain=root.GetComponentInChildren<Terrain>();var air=root.Find("Traversal/AirNodes");
            if(!air)throw new InvalidOperationException("Missing node groups");
            var nodes=air.GetComponentsInChildren<AirNode>();if(nodes.Length!=8)throw new InvalidOperationException("Expected eight nodes");
            foreach(var node in nodes)
            {
                var p=node.TargetPosition;
                if(p.y-terrain.SampleHeight(p)-terrain.transform.position.y<2.5f)throw new InvalidOperationException("Node ground clearance: "+node.name);
                if(node.GetComponent<AirSlingNode>())throw new InvalidOperationException("Air Cast node must not become a Sling target");
                if(!node.GetComponentsInChildren<Renderer>(true).Any(r=>r.enabled))throw new InvalidOperationException("Missing node visuals");
                var start=p-new Vector3(0,0,10);start.y=terrain.SampleHeight(start)+terrain.transform.position.y+1.7f;
                if(Physics.Linecast(start,p,out var hit,1,QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(node.transform))
                    throw new InvalidOperationException("Obstructed approach: "+node.name+" / "+hit.collider.name);
            }
            foreach(Transform route in air)
            {
                var n=route.GetComponentsInChildren<AirNode>();
                for(int i=1;i<n.Length;i++)if(Vector3.Distance(n[i-1].TargetPosition,n[i].TargetPosition)>35)
                    throw new InvalidOperationException("Route spacing exceeds forgiving targeting range");
            }
            var hill=air.Find("HillsideRoute").GetComponentsInChildren<AirNode>();
            for(int i=1;i<hill.Length;i++)if(hill[i].transform.position.y<=hill[i-1].transform.position.y)
                throw new InvalidOperationException("Hillside route must ascend actual ground");
        }
    }
}
