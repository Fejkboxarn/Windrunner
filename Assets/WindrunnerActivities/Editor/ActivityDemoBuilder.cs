using System;
using System.Collections.Generic;
using System.Linq;
using AirflowPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Windrunner.Frontend;
using Windrunner.Production.Progression;
using Windrunner.Production.Progression.Activities;
using Object=UnityEngine.Object;
namespace Windrunner.Activities.Editor
{
    public static class ActivityDemoBuilder
    {
        public const string Folder="Assets/WindrunnerActivities/Demonstration";
        public const string ScenePath=Folder+"/RelayPursuitDemo.unity";
        [MenuItem("Tools/Windrunner/Quest Authoring/Create isolated Relay & Pursuit demonstration")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode.");
            if(AssetDatabase.IsValidFolder(Folder))throw new InvalidOperationException("Demo already exists; open it instead of overwriting authored work.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            if(!Application.isBatchMode&&setup.Any(s=>string.IsNullOrEmpty(s.path)))throw new InvalidOperationException("Save your untitled scene first.");
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Save open scenes first.");
            AssetDatabase.CreateFolder("Assets/WindrunnerActivities","Demonstration");
            if(!AssetDatabase.CopyAsset(PlayerSession.WorldScene,ScenePath))throw new InvalidOperationException("Production player scene template missing.");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            try
            {
                var session=Object.FindAnyObjectByType<PlayerSession>();var actor=session.character.GetComponent<ProgressionActor>();
                var catalog=Object.Instantiate(session.catalog);catalog.name="RelayPursuitDemoCatalog";
                AssetDatabase.CreateAsset(catalog,Folder+"/Catalog.asset");session.catalog=catalog;
                foreach(var h in Object.FindObjectsByType<ProgressionHost>(FindObjectsSortMode.None))h.catalog=catalog;
                foreach(var journal in Object.FindObjectsByType<ProgressionJournal>(FindObjectsSortMode.None))journal.catalog=catalog;
                var origin=new Vector3(250,1000,0);session.arrival.position=origin+new Vector3(0,2,-8);session.character.transform.position=session.arrival.position;
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Disposable activity lanes";floor.transform.position=origin+new Vector3(25,-1,50);floor.transform.localScale=new Vector3(110,2,140);
                var guide=new GameObject("Development instructions").AddComponent<ActivityDemoGuide>();guide.actor=actor;
                var caster=actor.GetComponent<AirCaster>();var sling=actor.GetComponent<AirSlingController>();
                var relayProfile=ScriptableObject.CreateInstance<WindRelayProfile>();relayProfile.definition.challengeId="demo-relay";relayProfile.definition.overallSeconds=90;
                var relay=new GameObject("A — Wind Relay").AddComponent<WindRelayHost>();relay.actor=actor;relay.caster=caster;relay.airPower=actor.GetComponent<AirPower>();relay.motor=actor.GetComponent<PlayerMotor>();relay.sling=sling;relay.profile=relayProfile;relay.startOnEnable=false;
                var nodes=new List<RelayInteraction>();
                for(int i=0;i<3;i++)
                {
                    string id="relay-"+i;relayProfile.definition.nodes.Add(new RelayNodeDefinition{id=id,window=25,warning=5,methods=ActivationMethod.Cast});
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WindrunnerProduction/World/Prefabs/ZoneAirNode.prefab"));go.name=id;go.transform.position=origin+new Vector3(0,3,8+i*40);go.transform.localScale=Vector3.one*1.4f;
                    var air=go.GetComponent<AirNode>();var node=go.AddComponent<RelayInteraction>();node.relay=relay;node.relayId=id;node.stableId=id;node.castNode=air;node.visual=go.GetComponentInChildren<Renderer>();nodes.Add(node);
                }
                relay.nodes=nodes.ToArray();guide.relay=relay;AssetDatabase.CreateAsset(relayProfile,Folder+"/WindRelay.asset");
                var hosts=new List<PursuitHost>();var ids=new[]{"demo-relay","demo-ground","demo-air","demo-hybrid"};
                for(int k=0;k<3;k++)
                {
                    var profile=ScriptableObject.CreateInstance<PursuitProfile>();var d=profile.definition;d.challengeId=ids[k+1];d.mode=(PursuitMode)k;d.speed=k==0?5:7;d.maxSeconds=120;d.startDelay=2;d.capture=k==0?ActivationMethod.Proximity|ActivationMethod.Interact:ActivationMethod.Cast;
                    d.points.Add(new PursuitWaypoint{position=new Vector3(0,k==1?7:1,5),curved=true,outgoing=new Vector3(10,0,10)});
                    d.points.Add(new PursuitWaypoint{position=new Vector3(8,10,35),curved=true,incoming=new Vector3(0,0,-10),outgoing=new Vector3(-10,0,10),airborne=true,eventId="ascent"});
                    d.points.Add(new PursuitWaypoint{position=new Vector3(-8,k==1?15:1,65),curved=true,incoming=new Vector3(0,0,-10),airborne=false,eventId="return"});
                    if(k==2){d.points[2].airborne=true;d.points[2].position=new Vector3(-8,14,65);d.points.Add(new PursuitWaypoint{position=new Vector3(-8,1,95),airborne=false,eventId="landing"});d.points.Add(new PursuitWaypoint{position=new Vector3(-8,1,110),airborne=false,eventId="ground-return"});}
                    var root=new GameObject(k==0?"B — Ground Pursuit":k==1?"C — Air Pursuit":"D — Hybrid Pursuit");root.transform.position=origin+new Vector3(20+k*25,0,0);
                    var target=GameObject.CreatePrimitive(PrimitiveType.Sphere);target.name="Pursuit target";target.transform.SetParent(root.transform,false);target.transform.localPosition=d.points[0].position;
                    var host=root.AddComponent<PursuitHost>();host.actor=actor;host.profile=profile;host.target=target.transform;host.caster=caster;host.sling=sling;host.startOnEnable=false;host.stableId=ids[k+1];host.prompt="Capture pursuit target";
                    if(k>0)host.castNode=target.AddComponent<AirNode>();hosts.Add(host);AssetDatabase.CreateAsset(profile,Folder+"/"+d.mode+"Pursuit.asset");
                }
                guide.pursuits=hosts.ToArray();guide.questIds=ids.Select(x=>x+"-quest").ToArray();
                foreach(string id in ids)
                {
                    catalog.definitions.challenges.Add(new ChallengeDefinition{id=id,displayName=id,resultKind=FactKind.TraversalCompleted,replayable=true});
                    catalog.definitions.quests.Add(new QuestDefinition{id=id+"-quest",displayName=id+" demonstration",kind=QuestKind.Optional,automaticTurnIn=true,
                        groups=new List<ObjectiveGroup>{new ObjectiveGroup{id="complete",objectives=new List<ObjectiveDefinition>{new ObjectiveDefinition{id="result",description="Complete the demonstration",kind=FactKind.TraversalCompleted,definitionId=id}}}}});
                }
                EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Demo save failed.");
                FinishGeneratedDemo();
                Debug.Log("RELAY_PURSUIT_DEMO "+ScenePath);
            }
            finally{if(setup.Length>0&&setup.All(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
        }
        public static void FinishGeneratedDemo()
        {
            // Only task-created demonstration assets. Existing authored worlds and profiles are never opened here.
            var hybrid=AssetDatabase.LoadAssetAtPath<PursuitProfile>(Folder+"/HybridPursuit.asset");
            if(!hybrid)throw new InvalidOperationException("Generate the isolated demonstration first.");
            if(hybrid.definition.points.Count==3){hybrid.definition.points.Add(new PursuitWaypoint{position=new Vector3(-8,1,95),airborne=false,eventId="ground-return"});EditorUtility.SetDirty(hybrid);AssetDatabase.SaveAssetIfDirty(hybrid);}
            if(hybrid.definition.points.Count==4){hybrid.definition.points[2].airborne=true;hybrid.definition.points[2].position=new Vector3(-8,14,65);hybrid.definition.points.Add(new PursuitWaypoint{position=new Vector3(-8,1,110),airborne=false,eventId="ground-return"});EditorUtility.SetDirty(hybrid);AssetDatabase.SaveAssetIfDirty(hybrid);}
            var catalog=AssetDatabase.LoadAssetAtPath<ProgressionCatalog>(Folder+"/Catalog.asset");var errors=ProgressionValidation.Validate(catalog.definitions);
            foreach(string guid in AssetDatabase.FindAssets("t:PursuitProfile",new[]{Folder}))errors.AddRange(AssetDatabase.LoadAssetAtPath<PursuitProfile>(AssetDatabase.GUIDToAssetPath(guid)).definition.Validate());
            errors.AddRange(AssetDatabase.LoadAssetAtPath<WindRelayProfile>(Folder+"/WindRelay.asset").definition.Validate());
            if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);var session=Object.FindAnyObjectByType<PlayerSession>();var actor=session.character.GetComponent<ProgressionActor>();
            var floor=GameObject.Find("Disposable activity lanes");if(floor.transform.position.y<100)
            {
                Vector3 offset=Vector3.up*990;floor.transform.position+=offset;session.arrival.position+=offset;session.character.transform.position+=offset;
                foreach(var node in Object.FindObjectsByType<RelayInteraction>(FindObjectsSortMode.None))node.transform.position+=offset;
                foreach(var host in Object.FindObjectsByType<PursuitHost>(FindObjectsSortMode.None))host.transform.position+=offset;
            }
            var interaction=actor.GetComponent<PlayerInteractionController>();if(!interaction)interaction=actor.gameObject.AddComponent<PlayerInteractionController>();interaction.actor=actor;interaction.view=session.orbit.GetComponent<Camera>();interaction.reach=12;
            foreach(var journal in Object.FindObjectsByType<ProgressionJournal>(FindObjectsSortMode.None))journal.interaction=interaction;
            var relay=Object.FindAnyObjectByType<WindRelayHost>();
            if(!GameObject.Find("START Wind Relay"))StartStation("Wind Relay",relay.nodes[0].transform.position+new Vector3(0,-2,-5),relay,null);
            foreach(var host in Object.FindObjectsByType<PursuitHost>(FindObjectsSortMode.None))if(!GameObject.Find("START "+host.name))StartStation(host.name,host.transform.position+Vector3.up, null,host);
            EditorSceneManager.SaveScene(scene);Debug.Log("RELAY_PURSUIT_DEMO_VALIDATED");
        }
        static void StartStation(string name,Vector3 position,WindRelayHost relay,PursuitHost pursuit)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="START "+name;go.transform.position=position;
            var start=go.AddComponent<ActivityStartInteraction>();start.stableId="demo-start-"+name;start.prompt="Begin / retry "+name;start.relay=relay;start.pursuit=pursuit;start.range=4;
            var label=new GameObject("Station label").AddComponent<TextMesh>();label.transform.SetParent(go.transform,false);label.transform.localPosition=Vector3.up*1.5f;label.text=name+"\nInteract to begin / retry";label.characterSize=.12f;label.fontSize=48;label.anchor=TextAnchor.MiddleCenter;
        }
        public static void BuildWindowsCandidate()
        {
            string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-windrunnerActivityBuildLabel");string label=index>=0&&index+1<args.Length?args[index+1]:"relay-pursuit-candidate";
            if(!System.Text.RegularExpressions.Regex.IsMatch(label,"^[a-zA-Z0-9_-]+$"))throw new ArgumentException("Invalid build label.");
            string output="Builds/"+label+"/Windrunner.exe";
            if(Directory.Exists("Builds/"+label))throw new InvalidOperationException("Candidate already exists; preserve its evidence.");
            Directory.CreateDirectory("Logs/RelayPursuit");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath,PlayerSession.TitleScene,PlayerSession.ZoneScene,PlayerSession.WorldScene},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            var warnings=new List<string>();foreach(var step in report.steps)foreach(var message in step.messages)if(message.type==LogType.Warning||message.type==LogType.Error)warnings.Add(message.content);
            File.WriteAllText("Logs/RelayPursuit/"+label+"-build.json",JsonUtility.ToJson(new CandidateResult{result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,seconds=report.summary.totalTime.TotalSeconds,bytes=report.summary.totalSize,messages=warnings.ToArray()},true));
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Candidate build failed.");
        }
        [Serializable] sealed class CandidateResult{public string result;public int errors,warnings;public double seconds;public ulong bytes;public string[] messages;}
        [MenuItem("Tools/Windrunner/Quest Authoring/Open Relay & Pursuit demonstration")]
        public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(ScenePath);}
    }
}
