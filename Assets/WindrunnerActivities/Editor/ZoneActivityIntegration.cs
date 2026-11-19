using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AirflowPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Windrunner.Frontend;
using Windrunner.Production.Progression;
using Windrunner.Production.Progression.Activities;
using Object=UnityEngine.Object;
namespace Windrunner.Activities.Editor
{
    public static class ZoneActivityIntegration
    {
        public const string Folder="Assets/WindrunnerProduction/World/Activities";
        public const string RelayQuest="zone-wind-relay", PursuitQuest="zone-pursuit";
        const string Catalog="Assets/WindrunnerProduction/World/Zone01Catalog.asset";
        static Terrain terrain;
        static ProgressionActor actor;
        static Vector3 Ground(float x,float z,float offset=0)=>new Vector3(x,terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y+offset,z);
        static Transform Group(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static bool Clear(Vector3 p,float radius=2)
        {
            float nx=(p.x-terrain.transform.position.x)/terrain.terrainData.size.x,nz=(p.z-terrain.transform.position.z)/terrain.terrainData.size.z;
            if(nx<0||nx>1||nz<0||nz>1||terrain.terrainData.GetSteepness(nx,nz)>18)return false;
            foreach(var c in Physics.OverlapCapsule(p+Vector3.up*.6f,p+Vector3.up*2.2f,radius,~0,QueryTriggerInteraction.Ignore))
                if(!(c is TerrainCollider)&&!c.transform.IsChildOf(actor.transform))return false;
            return true;
        }
        static Vector3 Choose(Vector2 desired,Vector2[] offsets)
        {
            foreach(var shift in new[]{Vector2.zero,new Vector2(0,-30),new Vector2(0,30),new Vector2(-35,0),new Vector2(35,0),new Vector2(-35,-30),new Vector2(35,-30)})
            {
                Vector2 origin=desired+shift;bool good=Clear(Ground(origin.x-5,origin.y-6));
                for(int i=0;i<offsets.Length&&good;i++)
                {
                    Vector2 a=origin+offsets[i],b=origin+offsets[Mathf.Min(i+1,offsets.Length-1)];
                    for(int j=0;j<=20&&good;j++){var q=Vector2.Lerp(a,b,j/20f);good=Clear(Ground(q.x,q.y));}
                }
                if(good)return Ground(origin.x,origin.y);
            }
            throw new InvalidOperationException("No clear terrain course near "+desired+". No world reshape is allowed.");
        }
        static Material MakeMaterial(string name,Color color)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;}
        static GameObject Shape(string name,PrimitiveType type,Transform parent,Vector3 position,Material mat)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.position=position;go.GetComponent<Renderer>().sharedMaterial=mat;return go;}
        static ZoneQuestActivity Station(Transform parent,string quest,string objective,Vector3 position,Material mat,RelationshipContact npc)
        {
            var go=Shape("RETRY - current attempt only",PrimitiveType.Cube,parent,position,mat);
            var b=go.AddComponent<ZoneQuestActivity>();b.actor=actor;b.questId=quest;b.objectiveId=objective;b.stableId=quest+"-"+objective+"-retry";b.range=5;b.prompt="Retry current encounter (keeps quest progress)";b.turnInAnchor=npc.transform;
            var text=new GameObject("Development label").AddComponent<TextMesh>();text.transform.SetParent(go.transform,false);text.transform.localPosition=Vector3.up*1.5f;text.characterSize=.1f;text.fontSize=48;text.anchor=TextAnchor.MiddleCenter;text.text=quest+" / "+objective+"\nInteract to retry";
            return b;
        }
        static ObjectiveGroup Objective(string group,string id,string challenge,params string[] prerequisites)
            =>new ObjectiveGroup{id=group,requiresGroups=prerequisites.ToList(),objectives=new List<ObjectiveDefinition>{new ObjectiveDefinition{id=id,description=id=="relay"?"Complete the Wind Relay":id=="ground"?"Capture the ground target":"Capture the airborne target with Air Cast",kind=FactKind.TraversalCompleted,definitionId=challenge}}};
        public static void Install()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Reviewed batch installation only; use Studio afterward.");
            if(AssetDatabase.IsValidFolder(Folder))throw new InvalidOperationException("Encounter assets already exist; never regenerate authored content.");
            var scene=EditorSceneManager.OpenScene(PlayerSession.ZoneScene);var root=scene.GetRootGameObjects().Single().transform;
            var npc=root.GetComponentInChildren<RelationshipContact>(true);actor=npc.owner;terrain=root.GetComponentInChildren<Terrain>(true);
            var catalog=AssetDatabase.LoadAssetAtPath<ProgressionCatalog>(Catalog);var d=catalog.definitions;var relationship=d.relationships.Single(r=>r.id==npc.relationshipId);
            if(relationship.chapters.Count!=2||relationship.ranks.Count!=2||d.quests.Count!=2)throw new InvalidOperationException("Unexpected existing quest structure.");
            var originalTransforms=root.GetComponentsInChildren<Transform>(true).Select(t=>new{t,parent=t.parent,p=t.position,r=t.rotation,s=t.localScale}).ToArray();
            var journal=root.GetComponentInChildren<ProgressionJournal>(true);var director=npc.cinematics.director;
            var originals=root.GetComponentsInChildren<Component>(true).Where(c=>c&&!(c is Transform)&&c!=journal&&c!=director).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
            string terrainPath=AssetDatabase.GetAssetPath(terrain.terrainData);var terrainBytes=File.ReadAllBytes(terrainPath);
            Physics.SyncTransforms();
            Vector2[] relayOffsets={new Vector2(0,0),new Vector2(20,0),new Vector2(40,0)};
            Vector2[] groundOffsets={new Vector2(0,0),new Vector2(0,24),new Vector2(30,24),new Vector2(30,0)};
            Vector2[] airOffsets={new Vector2(0,0),new Vector2(20,14),new Vector2(35,0),new Vector2(15,-12)};
            var relayOrigin=Choose(new Vector2(-85,25),relayOffsets);var groundOrigin=Choose(new Vector2(-85,-45),groundOffsets);var airOrigin=Choose(new Vector2(65,-45),airOffsets);
            AssetDatabase.CreateFolder("Assets/WindrunnerProduction/World","Activities");
            var groundMat=MakeMaterial("Ground pursuit placeholder",new Color(.25f,.8f,.35f));var airMat=MakeMaterial("Air pursuit placeholder",new Color(1,.48f,.12f));
            for(int i=0;i<2;i++)
            {
                string id=i==0?RelayQuest:PursuitQuest,previous=i==0?"ridge-retrieval":RelayQuest;string film=id+"-placeholder-milestone";
                var timeline=ScriptableObject.CreateInstance<TimelineAsset>();AssetDatabase.CreateAsset(timeline,Folder+"/"+film+".playable");var track=timeline.CreateTrack<ActivationTrack>(null,"DEVELOPMENT placeholder signal");track.CreateDefaultClip().duration=3;
                d.cutscenes.Add(new CutsceneDefinition{id=film,displayName="Placeholder chapter "+(i+3)+" milestone",content=timeline});
                relationship.ranks.Add("Placeholder Rank "+(i+3));relationship.chapters.Add(new ChapterDefinition{id=id+"-chapter",questId=id,cutsceneId=film,rank=i+3});
                d.quests.Add(new QuestDefinition{id=id,displayName=i==0?"Wind Relay":"Ground and Air Pursuit",description=i==0?"Development placeholder: complete the marked western relay, then return here.":"Development placeholder: capture the western ground target, then the eastern flying target. Return here afterward.",kind=QuestKind.Story,characterId=relationship.id,chapterId=id+"-chapter",requiresQuests=new List<string>{previous},groups=i==0?new List<ObjectiveGroup>{Objective("relay","relay",RelayQuest+"-course")}:new List<ObjectiveGroup>{Objective("ground","ground",PursuitQuest+"-ground"),Objective("air","air",PursuitQuest+"-air","ground")},guidance=new GuidanceDefinition{areaId=i==0?"Wind Relay course":"Pursuit courses",clue=i==0?"Follow the marked course west of the contact. Return to the contact afterward.":"Ground course southwest; airborne course southeast. Return to the contact after both.",showArea=true}});
                var signal=Shape("DEVELOPMENT placeholder chapter "+(i+3)+" signal",PrimitiveType.Sphere,npc.cinematics.transform,npc.transform.position+Vector3.up*2,i==0?groundMat:airMat);signal.transform.localScale=Vector3.one*.7f;Object.DestroyImmediate(signal.GetComponent<Collider>());signal.SetActive(false);director.SetGenericBinding(track,signal);EditorUtility.SetDirty(timeline);
            }
            foreach(string id in new[]{RelayQuest+"-course",PursuitQuest+"-ground",PursuitQuest+"-air"})d.challenges.Add(new ChallengeDefinition{id=id,displayName=id,resultKind=FactKind.TraversalCompleted,replayable=false});
            var relayRoot=Group(root.Find("Traversal"),"Quest03_WindRelay");relayRoot.position=relayOrigin;
            var rp=ScriptableObject.CreateInstance<WindRelayProfile>();rp.definition.challengeId=RelayQuest+"-course";rp.definition.overallSeconds=120;
            var host=relayRoot.gameObject.AddComponent<WindRelayHost>();host.actor=actor;host.profile=rp;host.caster=actor.GetComponent<AirCaster>();host.sling=actor.GetComponent<AirSlingController>();host.airPower=actor.GetComponent<AirPower>();host.motor=actor.GetComponent<PlayerMotor>();host.startOnEnable=false;host.requiredActiveQuest=RelayQuest;
            var nodes=new List<RelayInteraction>();
            for(int i=0;i<3;i++)
            {
                string id="relay-"+(i+1);rp.definition.nodes.Add(new RelayNodeDefinition{id=id,methods=ActivationMethod.Cast,window=35,warning=8});
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WindrunnerProduction/World/Prefabs/ZoneAirNode.prefab"),relayRoot);go.name="Relay "+(i+1)+" - Air Cast";go.transform.position=Ground(relayOrigin.x+relayOffsets[i].x,relayOrigin.z+relayOffsets[i].y,3.5f);
                var n=go.AddComponent<RelayInteraction>();n.relay=host;n.relayId=id;n.stableId=RelayQuest+"-"+id;n.castNode=go.GetComponent<AirNode>();n.visual=go.GetComponentInChildren<Renderer>();nodes.Add(n);
            }
            host.nodes=nodes.ToArray();AssetDatabase.CreateAsset(rp,Folder+"/WindRelay.asset");var relayBinding=Station(relayRoot,RelayQuest,"relay",Ground(relayOrigin.x-5,relayOrigin.z-6,1),groundMat,npc);relayBinding.relay=host;relayBinding.presentation=nodes.Select(n=>n.gameObject).ToArray();
            var pursuitRoot=Group(root.Find("Traversal"),"Quest04_Pursuit");
            var newAreas=new List<ProgressionAreaMarker>();var marker=relayRoot.gameObject.AddComponent<ProgressionAreaMarker>();marker.areaId="Wind Relay course";marker.displayName="Wind Relay";marker.radius=45;newAreas.Add(marker);
            for(int mode=0;mode<2;mode++)
            {
                bool air=mode==1;string objective=air?"air":"ground";Vector3 origin=air?airOrigin:groundOrigin;var offsets=air?airOffsets:groundOffsets;
                var pr=Group(pursuitRoot,air?"AirPursuit":"GroundPursuit");pr.position=origin;
                var profile=ScriptableObject.CreateInstance<PursuitProfile>();var def=profile.definition;def.challengeId=PursuitQuest+"-"+objective;def.mode=air?PursuitMode.Air:PursuitMode.Ground;def.speed=air?3.5f:4;def.maxSpeed=9;def.acceleration=8;def.deceleration=12;def.startDelay=2;def.maxSeconds=150;def.escapeDistance=180;def.capture=air?ActivationMethod.Cast:ActivationMethod.Proximity|ActivationMethod.Interact;def.captureRadius=1.8f;def.cooldown=3;
                for(int i=0;i<offsets.Length;i++){var q=offsets[i];var pos=Ground(origin.x+q.x,origin.z+q.y,air?(i==1?9:6):.9f)-origin;def.points.Add(new PursuitWaypoint{position=pos,curved=air,eventId=objective+"-checkpoint-"+i});}
                var target=Shape(air?"Orange airborne target - Air Cast":"Green ground target - capture",air?PrimitiveType.Sphere:PrimitiveType.Capsule,pr,pr.TransformPoint(def.points[0].position),air?airMat:groundMat);
                var h=pr.gameObject.AddComponent<PursuitHost>();h.actor=actor;h.profile=profile;h.target=target.transform;h.caster=actor.GetComponent<AirCaster>();h.sling=actor.GetComponent<AirSlingController>();h.startOnEnable=false;h.requiredActiveQuest=PursuitQuest;h.requiredObjectiveId=objective;h.stableId=def.challengeId+"-target";h.groundOffset=.9f;
                if(air){target.GetComponent<Collider>().isTrigger=true;target.transform.localScale=Vector3.one*1.4f;h.castNode=target.AddComponent<AirNode>();}
                var binding=Station(pr,PursuitQuest,objective,Ground(origin.x-5,origin.z-6,1),air?airMat:groundMat,npc);binding.pursuit=h;binding.presentation=new[]{target};
                marker=pr.gameObject.AddComponent<ProgressionAreaMarker>();marker.areaId="Pursuit courses";marker.displayName=air?"Air Pursuit":"Ground Pursuit";marker.radius=45;newAreas.Add(marker);
                AssetDatabase.CreateAsset(profile,Folder+"/"+(air?"Air":"Ground")+"Pursuit.asset");
            }
            journal.areas=journal.areas.Concat(newAreas).ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();Physics.SyncTransforms();Validate();
            foreach(var o in originalTransforms)if(o.t.parent!=o.parent||o.t.position!=o.p||o.t.rotation!=o.r||o.t.localScale!=o.s)throw new InvalidOperationException("Original transform changed: "+o.t.name);
            foreach(var pair in originals)if(EditorJsonUtility.ToJson(pair.Key)!=pair.Value)throw new InvalidOperationException("Original component changed: "+pair.Key.name+" "+pair.Key.GetType().Name);
            if(!File.ReadAllBytes(terrainPath).SequenceEqual(terrainBytes))throw new InvalidOperationException("TerrainData changed.");
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
            Directory.CreateDirectory("Logs/ZoneActivities");File.WriteAllText("Logs/ZoneActivities/placement.txt","Relay "+relayOrigin+"; Ground "+groundOrigin+"; Air "+airOrigin+"\nOriginal transforms preserved: "+originalTransforms.Length+"; original components except journal/director: "+originals.Count+"\nTerrainData byte-identical: "+terrainPath);
            Debug.Log("ZONE_ACTIVITIES_INSTALLED");
        }
        public static void Validate()
        {
            Windrunner.Frontend.Editor.Zone01Setup.Validate();
            var root=SceneManager.GetActiveScene().GetRootGameObjects().Single();var bindings=root.GetComponentsInChildren<ZoneQuestActivity>(true);
            if(bindings.Length!=3)throw new InvalidOperationException("Expected exactly three encounter stages.");
            foreach(var b in bindings){if(b.relay&&b.relay.profile.definition.Validate().Count>0)throw new InvalidOperationException("Relay profile invalid.");if(b.pursuit&&b.pursuit.profile.definition.Validate().Count>0)throw new InvalidOperationException("Pursuit profile invalid.");}
            foreach(var b in bindings)
                if(b.pursuit && b.pursuit.castNode)
                    foreach(var c in b.pursuit.target.GetComponentsInChildren<Collider>(true))
                        if(c.enabled && !c.isTrigger)throw new InvalidOperationException("Cast pursuit target must not obstruct its own targeting ray.");
            Debug.Log("ZONE_ACTIVITIES_VALIDATED");
        }
        public static void ValidateSaved(){EditorSceneManager.OpenScene(PlayerSession.ZoneScene);Validate();}
    }
}
