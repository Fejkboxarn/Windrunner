using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AirflowPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Windrunner.Production.Progression;
using Object = UnityEngine.Object;

namespace Windrunner.Frontend.Editor
{
    public static class Zone01Setup
    {
        const string Folder = "Assets/WindrunnerProduction/World";
        const string CatalogPath = Folder + "/Zone01Catalog.asset";
        const string TerrainPath = Folder + "/Terrain/CentralBasin.asset";
        const string Hunt = "basin-hunt", Return = "ridge-retrieval", Area = "North Basin";
        [MenuItem("Tools/Windrunner/Zone 01/Open world")]
        public static void Open()
        {
            if (!EditorApplication.isPlaying && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(PlayerSession.ZoneScene);
        }
        [MenuItem("Tools/Windrunner/Zone 01/Validate open zone")]
        public static void Validate()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=PlayerSession.ZoneScene)throw new InvalidOperationException("Open Zone 01 first.");
            var session=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayerSession>(true)).Single();
            var errors=AirflowPrototype.EditorTools.ProgressionSceneValidation.Validate(scene);
            errors.AddRange(ProgressionValidation.Validate(session.catalog.definitions,true));
            var terrain=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>()).Single();
            if(!terrain.terrainData || terrain.GetComponent<TerrainCollider>().terrainData!=terrain.terrainData)
                errors.Add("Terrain render and collision data must match.");
            var region=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<QuestRegion>()).Single();
            foreach(var enemy in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EnemyProgressionAdapter>()))
                if(!enemy.health || !enemy.node || !enemy.actor || !enemy.sling || enemy.region!=region || !region.Contains(enemy.transform.position))
                    errors.Add(enemy.name+" is unbound or spawns outside the kill region.");
            if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            Debug.Log("Zone terrain, spawn encounter and progression bindings validated.");
        }
        static void Focus(string path)
        {
            var root = GameObject.Find("WorldRoot/" + path);
            if (!root) return;
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
        }
        [MenuItem("Tools/Windrunner/Zone 01/Select arrival")] static void Spawn() => Focus("PlayerSpawn");
        [MenuItem("Tools/Windrunner/Zone 01/Select contact")] static void Contact() => Focus("Characters/Waykeeper");
        [MenuItem("Tools/Windrunner/Zone 01/Select encounter")] static void Region() => Focus("QuestRegions/North Basin");
        [MenuItem("Tools/Windrunner/Zone 01/Select retrieval")] static void Item() => Focus("Collectibles/Ridge cache");

        static GameObject Child(string name, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent,false); return go;
        }
        static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(parent,false); go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
        static Material MaterialAsset(string name, Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            AssetDatabase.CreateAsset(mat, Folder + "/" + name + ".mat"); return mat;
        }
        static Terrain Basin(Transform parent)
        {
            var data = new TerrainData { heightmapResolution = 1025, size = new Vector3(2048,480,2048), alphamapResolution = 512 };
            var heights = new float[1025,1025];
            for (int z=0;z<1025;z++)
                for (int x=0;x<1025;x++)
                {
                    float dx=x*2-1024, dz=z*2-1024;
                    float rim=Mathf.SmoothStep(0,1,Mathf.InverseLerp(150,880,new Vector2(dx,dz).magnitude));
                    float ridge=1-Mathf.Abs(2*Mathf.PerlinNoise(x*.004f+9,z*.004f+11)-1);
                    float pass=Mathf.Lerp(.35f,1,Mathf.Clamp01(Mathf.Abs(dx-.45f*dz)/280));
                    heights[z,x]=(18+rim*pass*(24+315*ridge*ridge)+rim*16*Mathf.PerlinNoise(x*.018f,z*.018f))/480;
                }
            data.SetHeights(0,0,heights);
            var texture=new Texture2D(4,4,TextureFormat.RGB24,false);
            texture.SetPixels(Enumerable.Repeat(new Color(.49f,.52f,.47f),16).ToArray());texture.Apply();
            AssetDatabase.CreateAsset(texture,Folder+"/Terrain/BlockoutGround.asset");
            var layer=new TerrainLayer{diffuseTexture=texture,tileSize=new Vector2(12,12)};
            AssetDatabase.CreateAsset(layer,Folder+"/Terrain/BasinGround.terrainlayer");
            data.terrainLayers=new[]{layer};
            AssetDatabase.CreateAsset(data,TerrainPath);
            var go=Terrain.CreateTerrainGameObject(data);go.name="Central basin · 2 km";
            go.transform.SetParent(parent,false);go.transform.position=new Vector3(-1024,0,-1024);
            return go.GetComponent<Terrain>();
        }
        static ProgressionCatalog Chapters()
        {
            var catalog=ScriptableObject.CreateInstance<ProgressionCatalog>();
            var d=catalog.definitions;
            var contact=new RelationshipDefinition{id="waykeeper",displayName="Waykeeper",initialRankName="Visitor",ranks=new List<string>{"Ally","Trusted"}};
            d.relationships.Add(contact);
            foreach(bool hunt in new[]{true,false})
            {
                string id=hunt?Hunt:Return, area=hunt?Area:"East overlook";
                var objective=new ObjectiveDefinition{id=hunt?"defeat":"deliver",kind=hunt?FactKind.EnemyDefeated:FactKind.ItemCollected,
                    description=hunt?"Defeat basin creatures with Air Sling":"Carry the ridge stone back to the Waykeeper",
                    definitionId=hunt?"basin-creature":"ridge-stone",regionId=hunt?Area:"",count=hunt?3:1,maximumSeconds=hunt?0:55};
                string chapter=id+"-chapter", film=id+"-milestone", reward=id+"-recognition";
                var timeline=ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline,Folder+"/Cinematics/"+film+".playable");
                var track=timeline.CreateTrack<ActivationTrack>(null,"Milestone signal");
                track.CreateDefaultClip().duration=4;
                d.cutscenes.Add(new CutsceneDefinition{id=film,displayName=hunt?"First beacon":"Second beacon",content=timeline});
                d.rewards.Add(new RewardDefinition{id=reward,contentId=id+"-sigil"});
                contact.chapters.Add(new ChapterDefinition{id=chapter,rank=hunt?1:2,questId=id,cutsceneId=film,rewards=new List<string>{reward}});
                d.quests.Add(new QuestDefinition{id=id,characterId=contact.id,chapterId=chapter,
                    displayName=hunt?"Clear the North Basin":"Return the ridge stone",
                    description=hunt?"Beyond the twin pillars, strike the creatures with Air Sling. Return to the clearing.":"Find the stone beneath the solitary east spire. Bring it to the clearing before it fades.",
                    guidance=new GuidanceDefinition{areaId=area,clue=hunt?"Look north for twin pillars.":"Look east for a single spire.",showArea=true},
                    groups=new List<ObjectiveGroup>{new ObjectiveGroup{id="task",objectives=new List<ObjectiveDefinition>{objective}}}});
                EditorUtility.SetDirty(timeline);
            }
            AssetDatabase.CreateAsset(catalog,CatalogPath);return catalog;
        }
        // Batch-only initial bake. Refuse any prior output, including an interrupted bake.
        public static void Create()
        {
            if (Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty) ||
                Directory.GetFiles(Folder,"*",SearchOption.AllDirectories).Any(f=>!f.EndsWith(".meta")))
                throw new InvalidOperationException("Zone output exists or a scene has unsaved edits.");
            if (!AssetDatabase.CopyAsset(PlayerSession.WorldScene,PlayerSession.ZoneScene)) throw new IOException("Scene copy failed.");
            var scene=EditorSceneManager.OpenScene(PlayerSession.ZoneScene);
            var originals=scene.GetRootGameObjects();
            var session=Object.FindAnyObjectByType<PlayerSession>();
            var actor=session.character.GetComponent<ProgressionActor>();
            var camera=session.orbit.GetComponent<Camera>();
            var creatures=originals.Where(g=>g.activeSelf && g.GetComponent<AirSlingNodeHealth>() && g.GetComponent<AirSlingNode>()).Take(3).ToArray();
            if(creatures.Length!=3)throw new InvalidOperationException("Missing production encounter templates.");
            var world=new GameObject("WorldRoot").transform;
            var groups="Terrain Environment Landmarks PlayerSpawn Characters Enemies QuestRegions Collectibles Traversal Interactables CutsceneStaging WorldServices DevelopmentOnly".Split(' ')
                .ToDictionary(n=>n,n=>Child(n,world).transform);
            foreach(var go in originals)
            {
                if(go==session.gameObject || go.GetComponent<Canvas>() || go.GetComponent<UnityEngine.EventSystems.EventSystem>() || go==camera.gameObject)
                    go.transform.SetParent(groups["WorldServices"],true);
                else if(go==session.character.gameObject)go.transform.SetParent(groups["Characters"],true);
                else if(go.name=="Directional Light")go.transform.SetParent(groups["Environment"],true);
                else if(creatures.Contains(go))go.transform.SetParent(groups["Enemies"],true);
                else Object.DestroyImmediate(go);
            }
            RenderSettings.fog=false;
            Lightmapping.lightingDataAsset=null;
            var terrain=Basin(groups["Terrain"]);
            Vector3 Ground(float x,float z,float up=0) => new Vector3(x,terrain.SampleHeight(new Vector3(x,0,z))+up,z);
            session.arrival=groups["PlayerSpawn"];session.arrival.position=Ground(0,0,.15f);
            session.character.transform.SetPositionAndRotation(session.arrival.position,Quaternion.identity);
            var catalog=Chapters();session.legacyCatalog=session.catalog;session.catalog=catalog;actor.host.catalog=catalog;
            var stone=MaterialAsset("LandmarkStone",new Color(.37f,.43f,.48f));
            var amber=MaterialAsset("BeaconAmber",new Color(.93f,.57f,.18f));
            var blue=MaterialAsset("RidgeBlue",new Color(.19f,.65f,.78f));
            foreach(int x in new[]{-14,14})Shape(PrimitiveType.Cube,"North gate pillar",groups["Landmarks"],Ground(x,68,7),new Vector3(4,14,4),stone);
            Shape(PrimitiveType.Cube,"East spire",groups["Landmarks"],Ground(124,4,10),new Vector3(5,20,5),stone);
            var region=Child(Area,groups["QuestRegions"]).AddComponent<QuestRegion>();region.transform.position=Ground(0,91,6);
            region.areaId=Area;region.volume=region.GetComponent<BoxCollider>();region.volume.isTrigger=true;region.volume.size=new Vector3(68,44,58);
            var sling=actor.GetComponent<AirSlingController>();
            for(int i=0;i<creatures.Length;i++)
            {
                var go=creatures[i];go.name="Basin creature "+(i+1);go.transform.position=Ground((i-1)*15,86+8*(i%2),1);
                var adapter=go.AddComponent<EnemyProgressionAdapter>();adapter.actor=actor;adapter.sling=sling;adapter.node=go.GetComponent<AirSlingNode>();
                adapter.health=go.GetComponent<AirSlingNodeHealth>();adapter.stableId="basin-spawn-"+i;adapter.enemyDefinitionId="basin-creature";adapter.region=region;
                var health=new SerializedObject(adapter.health);health.FindProperty("maxHealth").intValue=1;health.ApplyModifiedPropertiesWithoutUndo();
            }
            var interactions=actor.gameObject.AddComponent<PlayerInteractionController>();interactions.actor=actor;interactions.view=camera;interactions.reach=22;
            var guard=actor.gameObject.AddComponent<AirflowProgressionAdapter>();guard.actor=actor;guard.motor=actor.GetComponent<PlayerMotor>();
            guard.caster=actor.GetComponent<AirCaster>();guard.dash=actor.GetComponent<AirDash>();guard.sling=sling;
            var npc=Child("Waykeeper",groups["Characters"]).AddComponent<RelationshipContact>();npc.transform.position=Ground(0,7,1.1f);
            Shape(PrimitiveType.Capsule,"Contact · temporary model",npc.transform,npc.transform.position,new Vector3(1.2f,1.1f,1.2f),amber);
            npc.owner=actor;npc.catalog=catalog;npc.relationshipId="waykeeper";npc.stableId="clearing-contact";npc.range=7;
            var pickup=Child("Ridge cache",groups["Collectibles"]).AddComponent<TimedRetrieval>();
            pickup.transform.position=Ground(115,4,1);pickup.itemModel=Shape(PrimitiveType.Sphere,"Ridge stone",pickup.transform,pickup.transform.position,Vector3.one*1.6f,blue);
            pickup.owner=actor;pickup.catalog=catalog;pickup.questId=Return;pickup.objectiveId="deliver";pickup.destination=npc;pickup.range=6;pickup.prompt="Collect ridge stone";pickup.stableId="east-ridge-cache";npc.retrieval=pickup;
            var film=Child("Milestone director",groups["CutsceneStaging"]).AddComponent<ProgressionCinematicPlayer>();
            film.actor=actor;film.safety=guard;film.catalog=catalog;film.gameplayCamera=camera;film.director=film.gameObject.AddComponent<PlayableDirector>();film.director.playOnAwake=false;
            film.cinematicCamera=Child("Clearing shot",film.transform).AddComponent<Camera>();film.cinematicCamera.transform.position=Ground(-6,1,3);
            film.cinematicCamera.transform.LookAt(npc.transform.position+Vector3.up);film.cinematicCamera.enabled=false;npc.cinematics=film;
            foreach(var cut in catalog.definitions.cutscenes)
            {
                var signal=Shape(PrimitiveType.Sphere,cut.displayName,film.transform,npc.transform.position+Vector3.up*2,Vector3.one*.7f,cut==catalog.definitions.cutscenes[0]?amber:blue);
                signal.SetActive(false);
                film.director.SetGenericBinding(((TimelineAsset)cut.content).GetRootTrack(0),signal);
            }
            var journal=actor.gameObject.AddComponent<ProgressionJournal>();journal.actor=actor;journal.catalog=catalog;journal.interaction=interactions;journal.cinematics=film;
            journal.areas=new[]{Mark(region.gameObject,Area,30),Mark(pickup.gameObject,"East overlook",22)};
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Zone save failed.");
            var legacy=EditorSceneManager.OpenScene(PlayerSession.WorldScene);
            Object.FindAnyObjectByType<PlayerSession>().zoneCatalog=catalog;
            if(!EditorSceneManager.SaveScene(legacy))throw new IOException("Legacy session binding save failed.");
            var title=EditorSceneManager.OpenScene(PlayerSession.TitleScene);
            var titleSession=Object.FindAnyObjectByType<PlayerSession>();titleSession.legacyCatalog=titleSession.catalog;titleSession.catalog=catalog;
            if(!EditorSceneManager.SaveScene(title))throw new IOException("Title save failed.");
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(PlayerSession.ZoneScene,true)}).ToArray();
            AssetDatabase.SaveAssets();
        }
        static ProgressionAreaMarker Mark(GameObject go,string area,float radius)
        {
            var mark=go.AddComponent<ProgressionAreaMarker>();mark.areaId=mark.displayName=area;mark.radius=radius;return mark;
        }
    }
}
