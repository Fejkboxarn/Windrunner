#if UNITY_EDITOR
using System;
using PortableAnimation;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Windrunner.Production.Input;
using Windrunner.Production.Unity;
using Object=UnityEngine.Object;

namespace Windrunner.Production.Tests
{
    public sealed partial class MainMenuFlowTests
    {
        static IEnumerator CaptureRecording(string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            var file=Path.GetFullPath("Logs/ProductionMigration/"+name+"-"+DateTime.UtcNow.Ticks+".png");
            var view=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            view.Repaint();yield return null;
            ScreenCapture.CaptureScreenshot(file);
            for(int i=0;i<120 && !File.Exists(file);i++){view.Repaint();yield return null;}
            Assert.IsTrue(File.Exists(file));
        }
        [UnityTest]
        public IEnumerator DraftLaneRunsDetachedProfilesAndReleasesThemOnReload()
        {
            var store=Type.GetType("AirflowPrototype.Editor.AnimationLaneDraft, Assembly-CSharp-Editor",true);
            const string launchKey="Windrunner.Acceptance.Launch";
            if((bool)store.GetProperty("Pending").GetValue(null) || UnityEditor.SessionState.GetBool(launchKey,false))
                Assert.Ignore("An author owns the current lane session.");
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Legacy,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var bridgeType=Type.GetType("AirflowPrototype.AirflowPortableAnimationBridge, Assembly-CSharp",true);
            var bridge=(Component)Object.FindAnyObjectByType(bridgeType);
            var source=Prop<Windrunner.Production.Animation.WindrunnerAnimationProfile>(bridge,"Profile");
            var draft=Object.Instantiate(source.standaloneActionProfile);
            try
            {
                draft.Action(4).regions.enabled=true;
                draft.Action(4).regions.bones.Add(new BoneInfluence{bone=HumanBodyBones.RightHand,weight=.51f});
                store.GetMethod("Store").Invoke(null,new object[]{source,source.standaloneGroundProfile,draft,true});
                UnityEditor.SessionState.SetBool(launchKey,true);
                var launcher=Type.GetType("Windrunner.Frontend.Editor.TraversalAcceptanceLauncher, Assembly-CSharp-Editor",true);
                launcher.GetMethod("OnPlayMode",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)
                    .Invoke(null,new object[]{UnityEditor.PlayModeStateChange.EnteredPlayMode});
                var bound=Prop<Windrunner.Production.Animation.WindrunnerAnimationProfile>(bridge,"Profile");
                var actions=bound.standaloneActionProfile;var ground=bound.standaloneGroundProfile;
                Assert.AreNotSame(source,bound);Assert.AreNotSame(draft,actions);
                Assert.AreEqual(.51f,actions.Action(4).regions.bones[0].weight);
                Assert.IsTrue(Prop<bool>(bridge,"PortableActive"));
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Legacy,new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                Assert.IsTrue(!bound && !actions && !ground,"The lane retained detached profiles after scene replacement.");
                var next=(Component)Object.FindAnyObjectByType(bridgeType);
                Assert.AreSame(source,Prop<Windrunner.Production.Animation.WindrunnerAnimationProfile>(next,"Profile"));
                Assert.IsFalse(source.standaloneActionProfile.Action(4).regions.enabled);
            }
            finally
            {
                UnityEditor.SessionState.EraseBool(launchKey);
                UnityEditor.SessionState.EraseString("Windrunner.Acceptance.MotionProfile");
                store.GetMethod("Finish").Invoke(null,null);
                Object.Destroy(draft);
            }
        }
        [UnityTest]
        public IEnumerator CustomArmMaskHasIndependentMeasuredJointInfluence()
        {
            const string folder="Assets/WindrunnerProduction/Animation/Generated/";
            var ground=UnityEditor.AssetDatabase.LoadAssetAtPath<GroundMotionProfile>(folder+"WindrunnerGroundMotion.asset");
            var source=UnityEditor.AssetDatabase.LoadAssetAtPath<PortableCharacterActionProfile>(folder+"WindrunnerActionClips.asset");
            var model=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AirflowPrototype/VampGirl_V2/Vamgirl_Windrunner.fbx");
            Quaternion[,] samples=new Quaternion[3,2];
            for(int k=0;k<3;k++)
            {
                var avatar=Object.Instantiate(model);var rig=avatar.GetComponentInChildren<Animator>();
                rig.runtimeAnimatorController=null;rig.fireEvents=false;rig.applyRootMotion=false;rig.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var profile=Object.Instantiate(source);
                try
                {
                    var settings=profile.Action(4).regions;
                    settings.enabled=true;
                    settings.regions.Add(new PortableAnimation.Authoring.RegionInfluenceSettings{
                        region=PortableAnimation.Authoring.AuthoringBodyRegion.RightUpperArm,rotation=k*.5f});
                    using var graph=new GroundOnlyPipelineRuntime(rig,ground,profile);
                    graph.RequestAction(4);
                    for(int f=0;f<30;f++)
                        graph.Evaluate(new GroundMotionFrame(new AnimationFrameState(f,1f/120,Vector3.forward,.8f,LocomotionMode.Grounded),true,0,0,false),5);
                    samples[k,0]=rig.GetBoneTransform(HumanBodyBones.RightUpperArm).localRotation;
                    samples[k,1]=rig.GetBoneTransform(HumanBodyBones.LeftUpperArm).localRotation;
                }
                finally{Object.Destroy(profile);Object.Destroy(avatar);}
            }
            float half=Quaternion.Angle(samples[0,0],samples[1,0]);
            float full=Quaternion.Angle(samples[0,0],samples[2,0]);
            Debug.Log($"Independent upper arm influence: half {half:F3}, full {full:F3}, opposite {Quaternion.Angle(samples[0,1],samples[2,1]):F3} degrees");
            Assert.Greater(full,2f);Assert.That(half,Is.EqualTo(full*.5f).Within(full*.20f));
            Assert.Less(Quaternion.Angle(samples[0,1],samples[2,1]),.15f);
            yield return null;
        }
        [UnityTest]
        public IEnumerator ContextChangesPreserveOneActionClockAndRecoverItsLayers()
        {
            const string folder="Assets/WindrunnerProduction/Animation/Generated/";
            var ground=UnityEditor.AssetDatabase.LoadAssetAtPath<GroundMotionProfile>(folder+"WindrunnerGroundMotion.asset");
            var source=UnityEditor.AssetDatabase.LoadAssetAtPath<PortableCharacterActionProfile>(folder+"WindrunnerActionClips.asset");
            var profile=Object.Instantiate(source);
            var avatar=Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/AirflowPrototype/VampGirl_V2/Vamgirl_Windrunner.fbx"));
            var rig=avatar.GetComponentInChildren<Animator>();
            rig.applyRootMotion=false;rig.fireEvents=false;rig.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var peer=Object.Instantiate(avatar);
            var baseRig=peer.GetComponentInChildren<Animator>();
            var legs=new[]{HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg};
            float LegDifference()
            {
                float maximum=0;
                foreach(var bone in legs)
                    maximum=Mathf.Max(maximum,Quaternion.Angle(rig.GetBoneTransform(bone).localRotation,
                        baseRig.GetBoneTransform(bone).localRotation));
                return maximum;
            }
            try
            {
                int frame=0;
                foreach(bool missing in new[]{false,true})
                {
                    if(missing)profile.Action(4).airborne.clip=null;
                    using var graph=new GroundOnlyPipelineRuntime(rig,ground,profile);
                    using var baseGraph=new GroundOnlyPipelineRuntime(baseRig,ground,null);
                    GroundOnlyPipelineRuntime.RecordingPose Step(int frames,bool grounded,float speed)
                    {
                        for(int i=0;i<frames;i++)
                        {
                            var state=new AnimationFrameState(frame++,1f/120,Vector3.forward*speed,
                                Mathf.Clamp01(speed/8.5f),grounded?LocomotionMode.Grounded:LocomotionMode.Airborne);
                            var sample=new GroundMotionFrame(state,grounded,0,0,false);
                            graph.Evaluate(sample,speed);
                            baseGraph.Evaluate(sample,speed);
                        }
                        graph.ReadRecordingPose(out var pose);return pose;
                    }
                    var position=avatar.transform.position;var rotation=avatar.transform.rotation;
                    Step(2,true,0);Assert.IsTrue(graph.RequestAction(4));
                    var idle=Step(12,true,0);
                    Assert.AreSame(profile.Action(4).stationary.clip,idle.actionClip);
                    Assert.Greater(idle.actionWeight,.98f);Assert.IsFalse(idle.mask);
                    float standingLegDegrees=LegDifference();
                    Assert.Greater(standingLegDegrees,2f,"Stationary throw should visibly own retargeted leg joints.");
                    var moving=Step(18,true,4);
                    Assert.AreSame(profile.Action(4).clip,moving.actionClip);
                    Assert.AreSame(profile.Action(4).mask,moving.mask);
                    float movingLegDegrees=LegDifference();
                    Debug.Log($"Context lower-body comparison: standing {standingLegDegrees:F2} deg, moving {movingLegDegrees:F2} deg");
                    Assert.Less(movingLegDegrees,3f,"Moving legs should converge to the locomotion-only peer.");
                    var air=Step(18,false,4);
                    Assert.AreSame(missing?profile.Action(4).clip:profile.Action(4).airborne.clip,air.actionClip);
                    Assert.That(air.actionSeconds/air.actionClip.length,Is.EqualTo(
                        48f/120*profile.Action(4).playbackSpeed/profile.Action(4).clip.length).Within(.002));
                    var landed=Step(18,true,0);
                    Assert.AreSame(idle.actionClip,landed.actionClip);
                    Assert.Greater(landed.actionSeconds,idle.actionSeconds);
                    Assert.Less(Vector3.Distance(position,avatar.transform.position),.001f);
                    Assert.Less(Quaternion.Angle(rotation,avatar.transform.rotation),.1f);
                    graph.CancelGroup("Cast");
                    Assert.IsNull(Step(32,true,0).action);
                    Assert.IsTrue(graph.RequestAction(4));
                    Assert.IsTrue(graph.RequestAction(1));
                    Assert.IsFalse(graph.RequestAction(4));
                    Assert.AreEqual("SlingTravel",graph.ActiveAction);
                }
            }
            finally{Object.Destroy(peer);Object.Destroy(avatar);Object.Destroy(profile);}
            yield return null;
        }
        [UnityTest] public IEnumerator DaggerCastRetargetsProductionRigAndRecoversAfterRelease()
        {
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Legacy,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var settings=InputSystem.settings;
            var flags=settings.hideFlags;
            if(flags==HideFlags.HideAndDontSave) settings.hideFlags=HideFlags.DontSave;
            var isolated=Object.Instantiate(settings);
            isolated.hideFlags=HideFlags.DontSave;
            isolated.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            isolated.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings=isolated;
            var mouse=InputSystem.AddDevice<Mouse>();
            var keyboard=InputSystem.AddDevice<Keyboard>();
            var pad=InputSystem.AddDevice<Gamepad>();
            float capture=Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime=1f/60;
                var lane=Type.GetType("Windrunner.Frontend.TraversalAcceptance, Assembly-CSharp",true);
                var deck=lane.GetMethod("Install").Invoke(null,new object[]{Session()});
                var bridge=(Component)Object.FindAnyObjectByType(Type.GetType("AirflowPrototype.AirflowPortableAnimationBridge, Assembly-CSharp",true));
                var animator=Prop<Animator>(bridge,"HumanoidAnimator");
                // Batch Game view can be culled between screenshots. Sample the actual pose every fixture frame.
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                var input=WindrunnerInputHost.Active.Input;
                input.Actions.RemoveAllBindingOverrides();
                for(int i=0;i<24;i++)yield return null;
                var overlayType=Type.GetType("AirflowPrototype.AnimationRecordingOverlay, Assembly-CSharp",true);
                var overlay=bridge.GetComponent(overlayType);
                Assert.IsNotNull(overlay);
                Assert.IsFalse(Prop<bool>(overlay,"Visible"));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Insert));
                yield return null; yield return null;
                Assert.IsTrue(Prop<bool>(overlay,"Visible"));
                var move=input.GetAction("Gameplay","Move");
                int up=-1;
                for(int i=0;i<move.bindings.Count;i++) if(string.Equals(move.bindings[i].name,"up",StringComparison.OrdinalIgnoreCase)){up=i;break;}
                Assert.GreaterOrEqual(up,0);
                move.ApplyBindingOverride(up,"<Keyboard>/u");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.U,Key.D));
                yield return null; yield return null;
                var reading=Prop<Vector2>(overlay,"Movement");
                Assert.AreEqual(input.Axis("Move"),reading);
                Assert.Greater(reading.y,.6f); Assert.Greater(reading.x,.6f);
                var privateFields=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var directions=(bool[])overlayType.GetField("lit",privateFields).GetValue(overlay);
                var labels=(string[])overlayType.GetField("keys",privateFields).GetValue(overlay);
                Assert.IsTrue(directions[0] && directions[3]);
                Assert.That(labels[0],Does.Contain("U").IgnoreCase);
                for(int i=0;i<40;i++)yield return null;
                yield return CaptureRecording("dagger-diagonal-recording");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=new Vector2(-.8f,.2f)});
                yield return null; yield return null;
                Assert.AreEqual(input.Axis("Move"),Prop<Vector2>(overlay,"Movement"));
                Assert.Less(Prop<Vector2>(overlay,"Movement").x,-.4f);
                InputSystem.RemoveDevice(pad);
                move.RemoveBindingOverride(up);
                yield return null;yield return null;
                Assert.AreEqual(Vector2.zero,Prop<Vector2>(overlay,"Movement"));
                lane.GetMethod("ThrowLedge",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(deck,null);
                var owner=(Behaviour)bridge;
                owner.enabled=false; owner.enabled=true;
                Assert.AreEqual(1,bridge.GetComponents(overlayType).Length);
                Assert.IsFalse(animator.applyRootMotion);
                for(int i=0;i<40;i++)yield return null;
                var position=bridge.transform.position;
                var wrist=hand.rotation;
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left,true));
                for(int i=0;i<36;i++)yield return null;
                Assert.AreEqual("CastCharge",Prop<string>(bridge,"ActiveAnimationAction"));
                var rotationDelta=Quaternion.Angle(wrist,hand.rotation);
                Debug.Log($"Cast charge wrist delta {rotationDelta}, rig {animator.name}, enabled {animator.enabled}, active {animator.gameObject.activeInHierarchy}, culling {animator.cullingMode}, dt {Time.deltaTime}, hand {hand.rotation}, scale {bridge.transform.lossyScale}.");
                Assert.Greater(rotationDelta,1f);
                Assert.Less(Vector2.Distance(new Vector2(position.x,position.z),
                    new Vector2(bridge.transform.position.x,bridge.transform.position.z)),.05f);
                Assert.IsFalse(animator.applyRootMotion);
                Assert.IsNull(animator.runtimeAnimatorController);
                // Hold the real Cast input while a transient semantic presentation interrupts it.
                var feedback=Windrunner.Production.Feedback.WindrunnerFeedbackHost.Active.Service;
                int observed=feedback.ObservedCount;
                feedback.Publish(Windrunner.Production.Feedback.FeedbackKey.Flip,
                    Windrunner.Production.Feedback.FeedbackRequest.At(bridge.transform));
                Assert.AreEqual("DoubleJumpFlip",Prop<string>(bridge,"ActiveAnimationAction"));
                for(int i=0;i<70;i++)yield return null;
                Assert.AreEqual("CastCharge",Prop<string>(bridge,"ActiveAnimationAction"));
                Assert.AreEqual(observed+1,feedback.ObservedCount,"Pose recovery must not replay gameplay feedback.");
                var caster=bridge.GetComponent(Type.GetType("AirflowPrototype.AirCaster, Assembly-CSharp",true));
                var pulse=caster.GetType().GetEvent("PulseReleased");
                int releaseCount=0;
                Action<float,bool> countRelease=(amount,perfect)=>releaseCount++;
                pulse.AddEventHandler(caster,countRelease);
                InputSystem.QueueStateEvent(mouse,new MouseState());
                yield return null;yield return null;
                Assert.AreEqual("CastRelease",Prop<string>(bridge,"ActiveAnimationAction"));
                for(int i=0;i<10;i++)yield return null;
                yield return CaptureRecording("context-stationary-throw");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));
                for(int i=0;i<9;i++)yield return null;
                yield return CaptureRecording("context-moving-throw");
                GroundOnlyPipelineRuntime.RecordingPose ActualPose()
                {
                    object[] args={null};
                    Assert.IsTrue((bool)bridge.GetType().GetMethod("ReadRecordingPose").Invoke(bridge,args));
                    return (GroundOnlyPipelineRuntime.RecordingPose)args[0];
                }
                bool airborneThrow=false;
                for(int i=0;i<35;i++)
                {
                    var pose=ActualPose();
                    if(pose.context==ActionMotionContext.Airborne && pose.actionClip && pose.actionClip.name=="Air_throw_01")
                    {airborneThrow=true;break;}
                    yield return null;
                }
                Assert.IsTrue(airborneThrow,"Walking off the ledge should crossfade the same release to the authored air throw.");
                yield return CaptureRecording("context-airborne-throw");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                for(int i=0;i<75;i++)yield return null;
                Assert.AreEqual(1,releaseCount,"Changing animation context must not deliver a second projectile release.");
                pulse.RemoveEventHandler(caster,countRelease);
                Assert.AreNotEqual("CastRelease",Prop<string>(bridge,"ActiveAnimationAction"));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Insert));
                yield return null;yield return null;
                Assert.IsFalse(Prop<bool>(overlay,"Visible"));
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Legacy,new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                Assert.IsTrue(overlay==null);
                var replacements=Object.FindObjectsByType(overlayType);
                Assert.AreEqual(1,replacements.Length);
                Assert.IsFalse(Prop<bool>((Component)replacements[0],"Visible"));
            }
            finally
            {
                Time.captureDeltaTime=capture;
                if(pad.added)InputSystem.RemoveDevice(pad);
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings=settings;settings.hideFlags=flags;Object.Destroy(isolated);
                if(ProjectRoot.Active)Object.Destroy(ProjectRoot.Active.gameObject);
            }
            yield return null;
            var prior=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Dagger motion teardown"));
            yield return SceneManager.UnloadSceneAsync(prior);
        }
    }
}
#endif
