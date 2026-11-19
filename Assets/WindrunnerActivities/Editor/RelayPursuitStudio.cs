using System;
using AirflowPrototype;
using Windrunner.Production.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Windrunner.Production.Progression.Activities;
namespace Windrunner.Activities.Editor
{
    public sealed class RelayPursuitStudio:EditorWindow
    {
        UnityEngine.Object selected;
        ProgressionActor actor;
        ProgressionCatalog catalog;
        GameObject relayPrefab;
        ScriptableObject draft;
        UnityEditor.Editor draftInspector;
        Vector2 scroll;
        PreviewRenderUtility preview;
        GameObject ghost;
        Vector3 previewCenter;float previewExtent;
        PursuitRoute route;
        bool playing;
        float distance,previewSpeed=6;
        int direction=1;
        double last;
        string error;
        [MenuItem("Tools/Windrunner/Quest Authoring/Relay & Pursuit Studio")]
        public static void Open()=>GetWindow<RelayPursuitStudio>("Relay & Pursuit");
        void OnEnable(){EditorApplication.update+=Tick;last=EditorApplication.timeSinceStartup;}
        void OnDisable(){EditorApplication.update-=Tick;Release();}
        void Release()
        {
            playing=false;route=null;if(draftInspector)DestroyImmediate(draftInspector);if(draft)DestroyImmediate(draft);draft=null;
            if(preview!=null){preview.Cleanup();preview=null;}ghost=null;
        }
        void Load(UnityEngine.Object asset)
        {
            Release();selected=asset;error=null;
            if(asset is WindRelayProfile||asset is PursuitProfile){draft=Instantiate((ScriptableObject)asset);draft.hideFlags=HideFlags.HideAndDontSave;}
        }
        void Create<T>() where T:ScriptableObject
        {
            string path=EditorUtility.SaveFilePanelInProject("Create activity",typeof(T).Name,"asset","Choose a project-owned authoring location.");if(string.IsNullOrEmpty(path))return;
            var asset=CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);Selection.activeObject=asset;Load(asset);
        }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Draft edits are isolated. Save explicitly to update the profile. Scene handles use Undo; save the profile explicitly. Preview has no gameplay, input or save service.",MessageType.Info);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("New Wind Relay"))Create<WindRelayProfile>();if(GUILayout.Button("New Pursuit"))Create<PursuitProfile>();
            }
            var next=EditorGUILayout.ObjectField("Activity profile",selected,typeof(ScriptableObject),false);
            if(next!=selected)Load(next);
            if(!draft)return;
            actor=(ProgressionActor)EditorGUILayout.ObjectField("Player binding",actor,typeof(ProgressionActor),true);
            catalog=(ProgressionCatalog)EditorGUILayout.ObjectField("Objective catalog",catalog,typeof(ProgressionCatalog),false);
            relayPrefab=(GameObject)EditorGUILayout.ObjectField("Relay prefab (optional)",relayPrefab,typeof(GameObject),false);
            using(new EditorGUILayout.HorizontalScope()){if(GUILayout.Button("Create bound scene objects"))CreateHost();if(GUILayout.Button("Register challenge in catalog"))RegisterChallenge();}
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Validate"))Validate();
                if(GUILayout.Button("Save draft"))
                {
                    SaveDraft();
                }
                if(GUILayout.Button("Discard / reload"))Load(selected);
                if(GUILayout.Button("Select bound hosts"))SelectHosts();
            }
            if(!string.IsNullOrEmpty(error))EditorGUILayout.HelpBox(error,MessageType.Warning);
            scroll=EditorGUILayout.BeginScrollView(scroll);
            if(draft is WindRelayProfile relay)RelayForm(relay);
            if(draft is PursuitProfile pursuit)PursuitForm(pursuit);
            EditorGUILayout.EndScrollView();
        }
        bool SaveDraft()
        {
            if(!selected||!draft||!Validate())return false;
            Undo.RecordObject(selected,"Save activity profile");var flags=selected.hideFlags;string name=selected.name;
            EditorUtility.CopySerialized(draft,selected);selected.hideFlags=flags;selected.name=name;EditorUtility.SetDirty(selected);AssetDatabase.SaveAssetIfDirty(selected);return true;
        }
        bool Validate()
        {
            var errors=draft is WindRelayProfile r?r.definition.Validate():((PursuitProfile)draft).definition.Validate();
            error=errors.Count==0?"Configuration is valid. Verify the referenced challenge exists in the player's catalog.":string.Join("\n",errors);return errors.Count==0;
        }
        void RelayForm(WindRelayProfile profile)
        {
            var d=profile.definition;d.challengeId=EditorGUILayout.TextField("Challenge ID",d.challengeId);d.regionId=EditorGUILayout.TextField("Region ID",d.regionId);
            d.pattern=(RelayPattern)EditorGUILayout.EnumPopup("Activation pattern",d.pattern);d.reset=(RelayReset)EditorGUILayout.EnumPopup("Expiry reset",d.reset);
            d.overallSeconds=EditorGUILayout.FloatField("Overall seconds (0 = off)",d.overallSeconds);d.timerOnEntry=EditorGUILayout.Toggle("Timer starts on entry",d.timerOnEntry);d.retryLimit=EditorGUILayout.IntField("Retry limit (-1 = unlimited)",d.retryLimit);
            for(int i=0;i<d.nodes.Count;i++)
            {
                var n=d.nodes[i];EditorGUILayout.BeginVertical(EditorStyles.helpBox);EditorGUILayout.LabelField("Relay "+(i+1),EditorStyles.boldLabel);
                n.id=EditorGUILayout.TextField("Stable ID",n.id);n.methods=(ActivationMethod)EditorGUILayout.EnumFlagsField("Activation",n.methods);
                n.playerState=(RelayPlayerState)EditorGUILayout.EnumPopup("Required player state",n.playerState);
                n.radius=EditorGUILayout.FloatField("Activation radius",n.radius);n.delay=EditorGUILayout.FloatField("Availability delay",n.delay);n.window=EditorGUILayout.FloatField("Active seconds (0 = unlimited)",n.window);n.warning=EditorGUILayout.FloatField("Expiry warning seconds",n.warning);
                n.airPowerReward=EditorGUILayout.FloatField("Extra main AirPower (no reserve)",n.airPowerReward);
                n.overrideExpiryReset=EditorGUILayout.Toggle("Override expiry reset",n.overrideExpiryReset);if(n.overrideExpiryReset)n.expiryReset=(RelayReset)EditorGUILayout.EnumPopup("This relay expiry reset",n.expiryReset);
                n.required=EditorGUILayout.Toggle("Required for completion",n.required);n.prerequisites=(RelayJoin)EditorGUILayout.EnumPopup("Incoming branches",n.prerequisites);
                if(d.pattern==RelayPattern.Graph)for(int j=0;j<d.nodes.Count;j++)if(i!=j)
                {string id=d.nodes[j].id;bool linked=n.destinations.Contains(id);bool use=EditorGUILayout.Toggle("Link to "+id,linked);if(use&&!linked)n.destinations.Add(id);if(!use&&linked)n.destinations.Remove(id);}
                if(GUILayout.Button("Remove relay")){d.nodes.RemoveAt(i);EditorGUILayout.EndVertical();break;}EditorGUILayout.EndVertical();
            }
            if(GUILayout.Button("Add relay"))d.nodes.Add(new RelayNodeDefinition{id="relay-"+Guid.NewGuid().ToString("N").Substring(0,8)});
            EditorGUILayout.HelpBox("Place RelayInteraction objects beneath a WindRelayHost. Bind production AirNode/AirSlingNode only for enabled channels. Author rewards through the existing AirNode/quest reward configuration.",MessageType.None);
        }
        void PursuitForm(PursuitProfile profile)
        {
            var d=profile.definition;profile.targetPrefab=(GameObject)EditorGUILayout.ObjectField("Target prefab",profile.targetPrefab,typeof(GameObject),false);
            // Grouped, labelled serialized fields preserve Unity curve editing and nested waypoint Undo semantics on the isolated draft.
            var so=new SerializedObject(profile);so.Update();var def=so.FindProperty("definition");
            foreach(string name in new[]{"challengeId","regionId","mode","end","speed","minSpeed","maxSpeed","acceleration","deceleration","turnDegrees","startDistance","startDelay","maxSeconds","escapeDistance","reverse","distanceResponse","response","capture","captureRadius","teleportDistance","nearMiss","missRadius","approachSpeed","retreatDistance","minimumApproachTime","reactionDelay","reactionProbability","cooldown","maxReactions","evasion","burstSpeed","burstAcceleration","burstSeconds"})EditorGUILayout.PropertyField(def.FindPropertyRelative(name),true);
            so.ApplyModifiedProperties();
            for(int i=0;i<d.points.Count;i++)
            {
                var p=d.points[i];EditorGUILayout.BeginVertical(EditorStyles.helpBox);EditorGUILayout.LabelField("Waypoint "+i,EditorStyles.boldLabel);
                p.position=EditorGUILayout.Vector3Field("Local position (metres)",p.position);p.curved=EditorGUILayout.Toggle("Curved outgoing segment",p.curved);
                p.incoming=EditorGUILayout.Vector3Field("Incoming tangent",p.incoming);p.outgoing=EditorGUILayout.Vector3Field("Outgoing tangent",p.outgoing);
                p.airborne=EditorGUILayout.Toggle("Airborne outgoing segment",p.airborne);p.speedOverride=EditorGUILayout.FloatField("Speed override (-1 = base)",p.speedOverride);p.pause=EditorGUILayout.FloatField("Pause seconds",p.pause);
                p.allowReversal=EditorGUILayout.Toggle("Allow reversal",p.allowReversal);p.capturable=EditorGUILayout.Toggle("Capture eligible",p.capturable);p.eventId=EditorGUILayout.TextField("Checkpoint/event ID",p.eventId);
                if(GUILayout.Button("Remove waypoint")){d.points.RemoveAt(i);EditorGUILayout.EndVertical();break;}EditorGUILayout.EndVertical();
            }
            if(GUILayout.Button("Add waypoint"))d.points.Add(new PursuitWaypoint{position=d.points.Count>0?d.points[d.points.Count-1].position+Vector3.forward*10:Vector3.zero});
            if(GUILayout.Button("Rebuild isolated preview"))BuildPreview(d);
            if(route!=null&&preview!=null)
            {
                EditorGUILayout.LabelField($"Length {route.Length:0.00} m; base-speed travel {route.Length/Mathf.Max(.01f,d.speed):0.00} s (excludes pauses/acceleration)");
                using(new EditorGUILayout.HorizontalScope()){playing=GUILayout.Toggle(playing,"Play / Pause","Button");if(GUILayout.Button("Reverse"))direction=-direction;if(GUILayout.Button("Reset")){distance=0;playing=false;direction=1;}}
                previewSpeed=EditorGUILayout.Slider("Simulated speed",previewSpeed,0,60);distance=EditorGUILayout.Slider("Route distance",distance,0,route.Length);
                Rect rect=GUILayoutUtility.GetRect(200,220,GUILayout.ExpandWidth(true));DrawPreview(rect);
                EditorGUILayout.HelpBox("Isolated static-mesh preview (sphere fallback for skinned-only prefabs): authored local elevations, no terrain raycasts or gameplay scripts. Ground adherence must be tested in the disposable Play Mode demo.",MessageType.None);
            }
        }
        void BuildPreview(PursuitDefinition definition)
        {
            if(!Validate())return;if(preview!=null)preview.Cleanup();preview=new PreviewRenderUtility();
            ghost=new GameObject("Isolated preview target");ghost.hideFlags=HideFlags.HideAndDontSave;preview.AddSingleGO(ghost);
            var source=((PursuitProfile)draft).targetPrefab;int count=0;
            if(source)foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(!filter.sharedMesh||!renderer)continue;
                var child=new GameObject("Preview mesh",typeof(MeshFilter),typeof(MeshRenderer));child.transform.SetParent(ghost.transform,false);child.transform.localPosition=source.transform.InverseTransformPoint(filter.transform.position);child.transform.localRotation=Quaternion.Inverse(source.transform.rotation)*filter.transform.rotation;child.transform.localScale=filter.transform.lossyScale;child.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;child.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;count++;
            }
            if(count==0){var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);DestroyImmediate(marker.GetComponent<Collider>());marker.transform.SetParent(ghost.transform,false);}
            route=new PursuitRoute(definition);var bounds=new Bounds(definition.points[0].position,Vector3.zero);foreach(var point in definition.points)bounds.Encapsulate(point.position);previewCenter=bounds.center;previewExtent=Mathf.Max(8,bounds.size.magnitude*.65f);distance=0;last=EditorApplication.timeSinceStartup;
        }
        void Tick(){double now=EditorApplication.timeSinceStartup;if(playing&&route!=null){distance=Mathf.Repeat(distance+(float)(now-last)*previewSpeed*direction,route.Length);Repaint();}last=now;}
        void DrawPreview(Rect rect)
        {
            if(Event.current.type!=EventType.Repaint)return;ghost.transform.position=route.Evaluate(distance,out var tangent,out _);ghost.transform.rotation=Quaternion.LookRotation(tangent);
            preview.camera.transform.position=previewCenter+new Vector3(.6f,.7f,-1)*previewExtent;preview.camera.transform.LookAt(previewCenter);preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=500;
            preview.lights[0].intensity=1;preview.BeginPreview(rect,GUIStyle.none);preview.Render();GUI.DrawTexture(rect,preview.EndPreview(),ScaleMode.StretchToFill,false);
        }
        void RegisterChallenge()
        {
            if(!catalog||!Validate())return;
            string id=draft is WindRelayProfile r?r.definition.challengeId:((PursuitProfile)draft).definition.challengeId;
            if(catalog.definitions.challenges.Exists(c=>c.id==id)){error="Challenge already registered. Use Progression Studio for quest/objective bindings.";return;}
            Undo.RecordObject(catalog,"Register activity challenge");catalog.definitions.challenges.Add(new ChallengeDefinition{id=id,displayName=id,resultKind=FactKind.TraversalCompleted,replayable=true});EditorUtility.SetDirty(catalog);
            error="Challenge added with Undo. Save the catalog explicitly in Progression Studio; whole-activity objectives use TraversalCompleted and this challenge ID.";
        }
        void CreateHost()
        {
            if(!actor||!selected||!Validate()||EditorApplication.isPlaying){error="Select a saved valid profile and explicit scene player; exit Play Mode.";return;}
            if(draft is WindRelayProfile && !(selected is WindRelayProfile))return;
            var root=new GameObject(selected.name);Undo.RegisterCreatedObjectUndo(root,"Create activity host");root.transform.position=actor.transform.position+actor.transform.forward*8;
            if(selected is WindRelayProfile r)
            {
                var host=root.AddComponent<WindRelayHost>();host.profile=r;host.actor=actor;host.caster=actor.GetComponent<AirCaster>();host.airPower=actor.GetComponent<AirPower>();host.motor=actor.GetComponent<PlayerMotor>();host.sling=actor.GetComponent<AirSlingController>();host.startOnEnable=false;
                var list=new System.Collections.Generic.List<RelayInteraction>();
                for(int i=0;i<r.definition.nodes.Count;i++)
                {
                    var go=relayPrefab?(GameObject)PrefabUtility.InstantiatePrefab(relayPrefab):GameObject.CreatePrimitive(PrimitiveType.Sphere);Undo.RegisterCreatedObjectUndo(go,"Place relay");go.transform.SetParent(root.transform,false);go.transform.localPosition=new Vector3(i*8,2,0);
                    var n=go.GetComponent<RelayInteraction>();if(!n)n=go.AddComponent<RelayInteraction>();n.relay=host;n.relayId=r.definition.nodes[i].id;n.stableId=n.relayId;n.castNode=go.GetComponent<AirNode>();n.slingNode=go.GetComponent<AirSlingNode>();n.range=r.definition.nodes[i].radius;n.visual=go.GetComponentInChildren<Renderer>();list.Add(n);
                }
                host.nodes=list.ToArray();
            }
            else if(selected is PursuitProfile p)
            {
                var h=root.AddComponent<PursuitHost>();h.profile=p;h.actor=actor;h.caster=actor.GetComponent<AirCaster>();h.sling=actor.GetComponent<AirSlingController>();h.startOnEnable=false;h.stableId=p.definition.challengeId;
                var go=p.targetPrefab?(GameObject)PrefabUtility.InstantiatePrefab(p.targetPrefab):GameObject.CreatePrimitive(PrimitiveType.Sphere);Undo.RegisterCreatedObjectUndo(go,"Place pursuit target");go.transform.SetParent(root.transform,false);h.target=go.transform;h.castNode=go.GetComponent<AirNode>();h.slingNode=go.GetComponent<AirSlingNode>();
            }
            Selection.activeGameObject=root;error="Created from the SAVED profile with Undo. Verify bindings/colliders and ground mask, then save the scene explicitly. Begin/Cancel may be wired through existing interaction UnityEvents.";
        }
        void SelectHosts()
        {
            var found=new System.Collections.Generic.List<UnityEngine.Object>();foreach(var h in FindObjectsByType<WindRelayHost>(FindObjectsSortMode.None))if(h.profile==selected)found.Add(h.gameObject);
            foreach(var h in FindObjectsByType<PursuitHost>(FindObjectsSortMode.None))if(h.profile==selected)found.Add(h.gameObject);Selection.objects=found.ToArray();
        }
    }
    [CustomEditor(typeof(PursuitHost))]
    public sealed class PursuitHostEditor:UnityEditor.Editor
    {
        bool show=true;
        public override void OnInspectorGUI(){DrawDefaultInspector();show=EditorGUILayout.Toggle("Show route handles",show);if(GUILayout.Button("Open Relay & Pursuit Studio"))RelayPursuitStudio.Open();if(GUILayout.Button("Save profile")&&((PursuitHost)target).profile)AssetDatabase.SaveAssetIfDirty(((PursuitHost)target).profile);}
        void OnSceneGUI()
        {
            var host=(PursuitHost)target;if(!show||!host.profile||EditorApplication.isPlaying)return;var d=host.profile.definition;if(d.Validate().Count>0)return;
            var route=new PursuitRoute(d);Handles.color=Color.cyan;Vector3 previous=host.transform.TransformPoint(route.Evaluate(0,out _,out _));
            for(int i=1;i<=128;i++){Vector3 next=host.transform.TransformPoint(route.Evaluate(route.Length*i/128,out var tangent,out _));Handles.DrawLine(previous,next);if(i%16==0)Handles.ArrowHandleCap(0,next,Quaternion.LookRotation(host.transform.TransformDirection(tangent)*(d.reverse?-1:1)),1.5f,EventType.Repaint);previous=next;}
            for(int i=0;i<d.points.Count;i++)
            {
                var p=d.points[i];Vector3 world=host.transform.TransformPoint(p.position);Handles.color=p.airborne?Color.cyan:Color.green;
                Handles.Label(world,$"{i}: {(p.airborne?"Air":"Ground")} / {(p.speedOverride<0?d.speed:p.speedOverride):0.0} m/s / reverse {p.allowReversal}");
                EditorGUI.BeginChangeCheck();Vector3 moved=Handles.PositionHandle(world,host.transform.rotation);
                if(EditorGUI.EndChangeCheck()){Undo.RecordObject(host.profile,"Move pursuit waypoint");p.position=host.transform.InverseTransformPoint(moved);EditorUtility.SetDirty(host.profile);}
                if(p.curved)
                {
                    EditorGUI.BeginChangeCheck();Vector3 outgoing=Handles.PositionHandle(host.transform.TransformPoint(p.position+p.outgoing),host.transform.rotation);Vector3 incoming=Handles.PositionHandle(host.transform.TransformPoint(p.position+p.incoming),host.transform.rotation);
                    if(EditorGUI.EndChangeCheck()){Undo.RecordObject(host.profile,"Edit pursuit curve tangents");p.outgoing=host.transform.InverseTransformPoint(outgoing)-p.position;p.incoming=host.transform.InverseTransformPoint(incoming)-p.position;EditorUtility.SetDirty(host.profile);}
                    Handles.DrawDottedLine(world,outgoing,3);Handles.DrawDottedLine(world,incoming,3);
                }
                Handles.DrawWireDisc(world,Vector3.up,d.captureRadius);Handles.color=Color.yellow;Handles.DrawWireDisc(world,Vector3.up,d.missRadius);
            }
        }
    }
    [CustomEditor(typeof(WindRelayHost))]
    public sealed class WindRelayHostEditor:UnityEditor.Editor
    {
        void OnSceneGUI()
        {
            var h=(WindRelayHost)target;if(!h.profile)return;
            foreach(var n in h.nodes)if(n)
            {
                Handles.color=Color.cyan;Handles.Label(n.transform.position,n.relayId);Handles.DrawWireDisc(n.transform.position,Vector3.up,n.range);
                var def=h.profile.definition.nodes.Find(x=>x.id==n.relayId);if(def==null)continue;
                System.Collections.Generic.IEnumerable<string> links=def.destinations;
                if(h.profile.definition.pattern==RelayPattern.Sequential){int index=h.profile.definition.nodes.IndexOf(def);links=index+1<h.profile.definition.nodes.Count?new[]{h.profile.definition.nodes[index+1].id}:Array.Empty<string>();}
                else if(h.profile.definition.pattern==RelayPattern.Unordered)links=Array.Empty<string>();
                foreach(string dest in links)foreach(var other in h.nodes)if(other&&other.relayId==dest){Vector3 direction=other.transform.position-n.transform.position;Handles.DrawDottedLine(n.transform.position,other.transform.position,4);if(direction.sqrMagnitude>.0001f)Handles.ArrowHandleCap(0,Vector3.Lerp(n.transform.position,other.transform.position,.7f),Quaternion.LookRotation(direction),1,EventType.Repaint);}
            }
        }
    }
}
