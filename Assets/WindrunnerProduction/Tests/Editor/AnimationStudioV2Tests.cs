using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using PortableAnimation;
using PortableAnimation.Authoring;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Windrunner.Production.Tests
{
    public sealed class AnimationStudioV2Tests
    {
        const BindingFlags Hidden=BindingFlags.Instance | BindingFlags.NonPublic;
        [Test]
        public void StudioDraftAndProductionSampleTheSameWeightedArm()
        {
            var type=Type.GetType("AirflowPrototype.Editor.DaggerMotionPreview, Assembly-CSharp-Editor",true);
            var window=(EditorWindow)ScriptableObject.CreateInstance(type);
            T Field<T>(string name)=>(T)type.GetField(name,Hidden).GetValue(window);
            void Set(string name,object value)=>type.GetField(name,Hidden).SetValue(window,value);
            void Call(string name,params object[] args)=>type.GetMethod(name,Hidden).Invoke(window,args);
            GameObject peer=null;
            try
            {
                Call("InitializeStudio");
                Assert.IsNull(Field<string>("issue"));
                var rig=Field<Animator>("previewRig");
                var profile=(PortableCharacterActionProfile)type.GetProperty("Actions",Hidden).GetValue(window);
                var ground=Field<GroundMotionProfile>("draftGround");
                string sourceBefore=EditorJsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<PortableCharacterActionProfile>("Assets/WindrunnerProduction/Animation/Generated/WindrunnerActionClips.asset"));
                profile.Action(4).regions = new RegionMaskSettings();
                var rule=new RegionInfluenceSettings{region=AuthoringBodyRegion.RightUpperArm,rotation=.5f};
                profile.Action(4).regions.enabled=true;profile.Action(4).regions.regions.Add(rule);
                Call("SelectAction",4,1);
                Call("BuildPreview");
                Assert.IsNull(Field<string>("issue"));
                Set("playhead",.25f);Call("AdvancePreview");
                var expected=rig.GetBoneTransform(HumanBodyBones.RightUpperArm).localRotation;
                peer=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AirflowPrototype/VampGirl_V2/Vamgirl_Windrunner.fbx"));
                var animator=peer.GetComponentInChildren<Animator>();
                animator.applyRootMotion=false;animator.fireEvents=false;animator.runtimeAnimatorController=null;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                using(var runtime=new GroundOnlyPipelineRuntime(animator,ground,profile))
                {
                    for(int f=0;f<90;f++)
                    {
                        if(f==75)Assert.IsTrue(runtime.RequestAction(4));
                        var motion=new AnimationFrameState(f,1f/60,Vector3.forward*6,6/8.5f,LocomotionMode.Grounded);
                        runtime.Evaluate(new GroundMotionFrame(motion,true,-2,0,false),6);
                    }
                    Assert.Less(Quaternion.Angle(expected,animator.GetBoneTransform(HumanBodyBones.RightUpperArm).localRotation),.3f);
                    Assert.That(runtime.FinalRotationContribution(4,ActionMotionContext.GroundedMoving,HumanBodyBones.RightUpperArm),
                        Is.EqualTo(.5f*profile.Action(4).blendWeight).Within(.002f));
                }
                rule.rotation=0;Call("BuildPreview");Call("AdvancePreview");
                Assert.Greater(Quaternion.Angle(expected,rig.GetBoneTransform(HumanBodyBones.RightUpperArm).localRotation),2);
                var saved=AssetDatabase.LoadAssetAtPath<PortableCharacterActionProfile>("Assets/WindrunnerProduction/Animation/Generated/WindrunnerActionClips.asset");
                Assert.AreEqual(sourceBefore,EditorJsonUtility.ToJson(saved));
                if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    rule.rotation=.5f;
                    Set("settingsTab",1);Set("skeleton",true);Set("playing",false);Set("rebuild",true);
                    for(int i=0;i<8;i++)Call("IndexProjectBatch");
                    CaptureWindow(window);
                }
            }
            finally
            {
                if(peer)Object.DestroyImmediate(peer);
                typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window,false);
                if(window && typeof(EditorWindow).GetField("m_Parent",Hidden).GetValue(window) is Object host && host)window.Close();
                if(window)Object.DestroyImmediate(window);
            }
        }

        [TestCase(GroundPose.Idle)]
        [TestCase(GroundPose.MoveForward)]
        public void GroundCandidateCanBeComparedStagedAndReverted(GroundPose slot)
        {
            var type=Type.GetType("AirflowPrototype.Editor.DaggerMotionPreview, Assembly-CSharp-Editor",true);
            var window=(EditorWindow)ScriptableObject.CreateInstance(type);
            object Get(string name)=>type.GetField(name,Hidden).GetValue(window);
            void Call(string name,params object[] args)=>type.GetMethod(name,Hidden).Invoke(window,args);
            try
            {
                Call("InitializeStudio");Call("SelectGround",(int)slot);
                var draft=(GroundMotionProfile)Get("draftGround");
                var root=(Windrunner.Production.Animation.WindrunnerAnimationProfile)Get("draftProfile");
                var current=draft.Clip(slot);
                var candidate=draft.Clip(slot==GroundPose.Idle?GroundPose.MoveForward:GroundPose.Idle);
                Assert.AreNotEqual(current,candidate);
                Call("SetPreviewClip",candidate);
                type.GetField("candidate",Hidden).SetValue(window,true);
                Call("BuildPreview");
                Assert.AreSame(candidate,((GroundMotionProfile)Get("previewGround")).Clip(slot));
                Assert.AreSame(current,draft.Clip(slot));
                type.GetField("candidate",Hidden).SetValue(window,false);Call("BuildPreview");
                Assert.AreSame(current,((GroundMotionProfile)Get("previewGround")).Clip(slot));
                using(var serialized=new SerializedObject(draft))
                {Call("StageCandidate",serialized);serialized.ApplyModifiedProperties();}
                Assert.AreSame(candidate,draft.Clip(slot));
                Assert.AreSame(current,root.standaloneGroundProfile.Clip(slot));
                var session=Get("draft");session.GetType().GetMethod("Revert").Invoke(session,null);
                Assert.AreSame(current,draft.Clip(slot));
            }
            finally{Object.DestroyImmediate(window);}
        }

        [Test]
        public void SavedAnimationModulesRoundTrip()
        {
            var id=Environment.GetEnvironmentVariable("WINDRUNNER_STUDIO_ROUNDTRIP");
            bool acrossProcesses=Guid.TryParseExact(id,"N",out _);
            string folder="Assets/AnimationStudioRoundTrip-"+(acrossProcesses?id:Guid.NewGuid().ToString("N"));
            string rootPath=folder+"/character.asset";
            bool readOnly=acrossProcesses && Environment.GetEnvironmentVariable("WINDRUNNER_STUDIO_READBACK")=="1";
            bool created=false,keep=false;
            var installed=AssetDatabase.LoadAssetAtPath<Windrunner.Production.Animation.WindrunnerAnimationProfile>(
                "Assets/WindrunnerProduction/Animation/Generated/WindrunnerAnimationProfile.asset");
            var expected=installed.standaloneActionProfile.Action(4).clip;
            var type=Type.GetType("AirflowPrototype.Editor.DaggerMotionPreview, Assembly-CSharp-Editor",true);
            EditorWindow window=null;
            try
            {
                if(!readOnly)
                {
                    Assert.IsFalse(AssetDatabase.IsValidFolder(folder));
                    AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));created=true;
                    var root=Object.Instantiate(installed);
                    root.standaloneGroundProfile=Object.Instantiate(installed.standaloneGroundProfile);
                    root.standaloneActionProfile=Object.Instantiate(installed.standaloneActionProfile);
                    AssetDatabase.CreateAsset(root.standaloneGroundProfile,folder+"/ground.asset");
                    AssetDatabase.CreateAsset(root.standaloneActionProfile,folder+"/actions.asset");
                    AssetDatabase.CreateAsset(root,rootPath);
                    window=(EditorWindow)ScriptableObject.CreateInstance(type);
                    type.GetField("laneProfile",Hidden).SetValue(window,root);
                    type.GetMethod("InitializeStudio",Hidden).Invoke(window,null);
                    Assert.IsNull(type.GetField("issue",Hidden).GetValue(window));
                    var draft=(PortableCharacterActionProfile)type.GetProperty("Actions",Hidden).GetValue(window);
                    var air=draft.Action(4).airborne;
                    air.clip=expected;air.regions.enabled=true;
                    air.playback.mode=VisualPlaybackMode.AuthoredSpeed;air.playback.baseSpeed=.7f;
                    air.playback.reverse=true;air.playback.useCurve=true;
                    air.playback.speedCurve=AnimationCurve.Linear(0,.5f,1,1.5f);
                    var ground=(GroundMotionProfile)type.GetField("draftGround",Hidden).GetValue(window);
                    ground.EditPlayback(GroundMotionProfile.TimingSlot(GroundPose.Idle)).mode=VisualPlaybackMode.AuthoredSpeed;
                    ground.EditPlayback(GroundMotionProfile.TimingSlot(GroundPose.Idle)).baseSpeed=.65f;
                    string adaptiveKey = GroundMotionProfile.TimingSlot(GroundPose.MoveForward);
                    ground.EditPlayback(adaptiveKey).mode = VisualPlaybackMode.AuthoredSpeed;
                    var adaptive = ground.EditAdaptive(adaptiveKey);
                    adaptive.enabled = true; adaptive.referenceSpeed = 7.25f;
                    adaptive.useResponseCurve = true; adaptive.responseCurve = AnimationCurve.Linear(0, .3f, 2, 1.4f);
                    adaptive.phaseGroup = 2; adaptive.leftContact = .2f;

                    air.regions.bones.Add(new BoneInfluence{bone=HumanBodyBones.RightHand,weight=.37f});
                    var session=type.GetField("draft",Hidden).GetValue(window);
                    session.GetType().GetMethod("Save").Invoke(session,null);
                    Assert.AreSame(expected,root.standaloneActionProfile.Action(4).airborne.clip);
                    if(acrossProcesses)
                    {
                        File.WriteAllText("Library/AnimationStudioRoundTrip-"+id+".txt",
                            System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
                        keep=true;
                        return;
                    }
                }
                if(readOnly)
                {
                    string marker="Library/AnimationStudioRoundTrip-"+id+".txt";
                    Assert.IsTrue(File.Exists(marker));Assert.AreNotEqual(
                        System.Diagnostics.Process.GetCurrentProcess().Id.ToString(),File.ReadAllText(marker));
                    created=true;File.Delete(marker);
                }
                var reloaded=AssetDatabase.LoadAssetAtPath<Windrunner.Production.Animation.WindrunnerAnimationProfile>(rootPath);
                var phase=reloaded.standaloneActionProfile.Action(4);
                Assert.AreSame(expected,phase.airborne.clip);
                Assert.IsTrue(phase.airborne.playback.reverse);
                Assert.AreEqual(.7f,phase.airborne.playback.baseSpeed);
                Assert.AreEqual(.75f,phase.airborne.playback.speedCurve.Evaluate(.25f),.0001);
                Assert.AreEqual(.65f,reloaded.standaloneGroundProfile.Playback(GroundMotionProfile.TimingSlot(GroundPose.Idle)).baseSpeed);
                var restored = reloaded.standaloneGroundProfile.Adaptive(GroundMotionProfile.TimingSlot(GroundPose.MoveForward));
                Assert.IsTrue(restored.enabled); Assert.AreEqual(7.25f, restored.referenceSpeed);
                Assert.IsTrue(restored.useResponseCurve); Assert.AreEqual(2, restored.phaseGroup);
                Assert.AreEqual(.2f, restored.leftContact);
                Assert.AreEqual(1.4f, restored.responseCurve.Evaluate(2), .0001);

                Assert.AreSame(installed.standaloneActionProfile.Action(4).stationary.clip,phase.stationary.clip);
                Assert.AreSame(expected,phase.clip);Assert.IsTrue(phase.airborne.regions.enabled);
                Assert.AreEqual(.37f,phase.airborne.regions.bones.Find(b=>b.bone==HumanBodyBones.RightHand).weight);
            }
            finally
            {
                if(window)Object.DestroyImmediate(window);
                if(created && !keep)AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void LaneSnapshotRetainsDraftWeightsAndReferencesOnDetachedModules()
        {
            var store=Type.GetType("AirflowPrototype.Editor.AnimationLaneDraft, Assembly-CSharp-Editor",true);
            if((bool)store.GetProperty("Pending").GetValue(null))Assert.Ignore("An author already owns a lane draft.");
            var root=AssetDatabase.LoadAssetAtPath<Windrunner.Production.Animation.WindrunnerAnimationProfile>(
                "Assets/WindrunnerProduction/Animation/Generated/WindrunnerAnimationProfile.asset");
            var draft=Object.Instantiate(root.standaloneActionProfile);
            try
            {
                draft.Action(4).regions.enabled=true;
                draft.Action(4).regions.bones.Add(new BoneInfluence{bone=HumanBodyBones.RightHand,weight=.61f});
                store.GetMethod("Store").Invoke(null,new object[]{root,root.standaloneGroundProfile,draft,false});
                var modules=((GroundMotionProfile ground,PortableCharacterActionProfile actions))store.GetMethod("Read").Invoke(null,null);
                try
                {
                    Assert.AreNotSame(draft,modules.actions);Assert.AreNotSame(root.standaloneGroundProfile,modules.ground);
                    Assert.IsFalse(EditorUtility.IsPersistent(modules.actions));
                    Assert.AreSame(draft.Action(4).clip,modules.actions.Action(4).clip);
                    Assert.AreEqual(.61f,modules.actions.Action(4).regions.bones[0].weight);
                    Assert.AreSame(root.standaloneGroundProfile.Clip(GroundPose.Idle),modules.ground.Clip(GroundPose.Idle));
                }
                finally{Object.DestroyImmediate(modules.ground);Object.DestroyImmediate(modules.actions);}
            }
            finally{store.GetMethod("Finish").Invoke(null,null);Object.DestroyImmediate(draft);}
        }

        [Test]
        public void ContextTimingPreservesPhaseAndUpdatesInterruptionPolicy()
        {
            var normal=new PortableActionPlayback.Timing(2,1,.05f,.1f,1);
            var changed=new PortableActionPlayback.Timing(1,2,.03f,.2f,5,interruptible:false);
            var clock=new PortableActionPlayback(new[]{normal,normal});
            Assert.IsTrue(clock.Request(0));clock.Tick(.5f);
            clock.ChangeTiming(0,changed);
            Assert.AreEqual(.25f,clock.ClipTime(0),.0001);
            Assert.IsFalse(clock.Request(1));
            clock.Tick(.1f);Assert.AreEqual(.45f,clock.ClipTime(0),.0001);
            clock.Cancel(0);clock.Tick(.1f);
            Assert.That(clock.Weight(0),Is.EqualTo(.5f).Within(.0001));
        }

        [Test]
        public void LocalRegionOverridesParentBoneAndBoneOverridesLocalRegion()
        {
            var basis=new RegionMaskSettings{enabled=true};
            basis.bones.Add(new BoneInfluence{bone=HumanBodyBones.RightHand,weight=.13f});
            var local=new RegionMaskSettings{enabled=true};
            float Resolve()=>local.Resolve(AuthoringBodyRegion.RightHand,"Hand",null,.5f,HumanBodyBones.RightHand,basis).rotation;
            Assert.AreEqual(.13f,Resolve());
            local.regions.Add(new RegionInfluenceSettings{region=AuthoringBodyRegion.RightHand,rotation=.6f});
            Assert.AreEqual(.6f,Resolve(),.0001);
            local.bones.Add(new BoneInfluence{bone=HumanBodyBones.RightHand,weight=.81f});
            Assert.AreEqual(.81f,Resolve());
        }

        [Test]
        public void TimingComparisonModesUseTheProductionSamplerAndKeepDraft()
        {
            var type=Type.GetType("AirflowPrototype.Editor.DaggerMotionPreview, Assembly-CSharp-Editor",true);
            var window=(EditorWindow)ScriptableObject.CreateInstance(type);
            object Get(string name)=>type.GetField(name,Hidden).GetValue(window);
            void Set(string name,object value)=>type.GetField(name,Hidden).SetValue(window,value);
            void Call(string name,params object[] args)=>type.GetMethod(name,Hidden).Invoke(window,args);
            try
            {
                Call("InitializeStudio");Call("SelectAction",4,1);
                var profile=(PortableCharacterActionProfile)type.GetProperty("Actions",Hidden).GetValue(window);
                var a=profile.Action(4);
                a.playback.mode=VisualPlaybackMode.AuthoredSpeed;a.playback.baseSpeed=.5f;
                a.playback.reverse=true;a.playback.useCurve=true;
                a.playback.speedCurve=AnimationCurve.Linear(0,.7f,1,1.3f);
                var oracle=new VisualPlaybackClock(a.playback,a.clip.length,a.segment.start,a.segment.end);
                Set("playhead",.25f);
                foreach(bool composed in new[]{true,false})
                {
                    Set("composed",composed);Set("timingComparison",0);Call("BuildPreview");Call("AdvancePreview");
                    Assert.IsNull(Get("issue"));
                    var sample=(VisualPlaybackSample)Get("timingSample");
                    Assert.That(sample.SourceSeconds,Is.EqualTo(oracle.Sample(.25).SourceSeconds).Within(.0002));
                    Assert.Less(sample.EffectiveSpeed,0);
                    Set("timingComparison",2);Call("BuildPreview");Call("AdvancePreview");
                    var original=(VisualPlaybackSample)Get("timingSample");
                    Assert.That(original.SourceSeconds,Is.EqualTo(.25f).Within(.0001));
                    Assert.That(original.EffectiveSpeed,Is.EqualTo(1).Within(.0001));
                    Set("timingComparison",1);Call("BuildPreview");Call("AdvancePreview");
                    var saved=(VisualPlaybackSample)Get("timingSample");
                    Assert.That(saved.SourceSeconds,Is.EqualTo(.25f*a.playbackSpeed).Within(.0001));
                }
                Assert.IsTrue(a.playback.reverse);Assert.AreEqual(.5f,a.playback.baseSpeed);
                if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    Set("timingComparison",0);Set("composed",true);Set("playing",false);Set("settingsTab",2);Set("rebuild",true);
                    for(int i=0;i<8;i++)Call("IndexProjectBatch");
                    foreach(int mode in new[]{0,1,2})
                    {
                        Set("timingComparison",mode);Set("rebuild",true);
                        CaptureWindow(window);
                        File.Copy("Logs/ProductionMigration/animation-studio-v2.png",
                            "Logs/ProductionMigration/animation-studio-speed-"+mode+".png",true);
                    }
                }
            }
            finally
            {
                typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window,false);
                if(typeof(EditorWindow).GetField("m_Parent",Hidden).GetValue(window) is Object host && host)window.Close();
                if(window)Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void AdaptiveStudioComparisonUsesRuntimeSpeedAndRetainsUnsavedCalibration()
        {
            var type = Type.GetType("AirflowPrototype.Editor.DaggerMotionPreview, Assembly-CSharp-Editor", true);
            var window = (EditorWindow)ScriptableObject.CreateInstance(type);
            object Get(string name) => type.GetField(name, Hidden).GetValue(window);
            void Set(string name, object value) => type.GetField(name, Hidden).SetValue(window, value);
            void Call(string name, params object[] args) => type.GetMethod(name, Hidden).Invoke(window, args);
            try
            {
                Call("InitializeStudio"); Call("SelectGround", (int)GroundPose.MoveForward);
                var ground = (GroundMotionProfile)Get("draftGround");
                string key = GroundMotionProfile.TimingSlot(GroundPose.MoveForward);
                var timing = ground.EditPlayback(key); timing.mode = VisualPlaybackMode.AuthoredSpeed;
                var adaptive = ground.EditAdaptive(key); adaptive.enabled = true;
                adaptive.referenceSpeed = 8; adaptive.smoothingSeconds = 0;
                Set("simulateGroundSpeed", true); Set("simulatedGroundSpeed", 6f); Set("playhead", .25f);
                foreach (bool composed in new[] { true, false })
                {
                    Set("composed", composed); Set("timingComparison", 0); Call("BuildPreview"); Call("AdvancePreview");
                    Assert.IsNull(Get("issue"));
                    var sample = (VisualPlaybackSample)Get("timingSample");
                    Assert.That(sample.EffectiveSpeed, Is.EqualTo(.75f).Within(.0001));
                    Assert.That(sample.SourceSeconds, Is.EqualTo(.25f*.75f).Within(.0001));
                    Set("timingComparison", 2); Call("BuildPreview"); Call("AdvancePreview");
                    Assert.That(((VisualPlaybackSample)Get("timingSample")).EffectiveSpeed, Is.EqualTo(1).Within(.0001));
                }
                Set("composed", true); Set("timingComparison", 0); Set("playing", false);
                for (int i = 0; i < 8; i++) Call("IndexProjectBatch");
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    foreach (float speed in new[] { 3f, 8f, 12f })
                    {
                        Set("simulatedGroundSpeed", speed); Set("rebuild", true);
                        CaptureWindow(window);
                        File.Copy("Logs/ProductionMigration/animation-studio-v2.png",
                            "Logs/ProductionMigration/adaptive-locomotion-"+speed+".png", true);
                    }
                Assert.IsTrue(adaptive.enabled); Assert.AreEqual(8, adaptive.referenceSpeed);
                var session = Get("draft"); session.GetType().GetMethod("Revert").Invoke(session, null);
                Assert.IsNull(ground.Adaptive(key));
            }
            finally
            {
                typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
                if (typeof(EditorWindow).GetField("m_Parent", Hidden).GetValue(window) is Object host && host) window.Close();
                if (window) Object.DestroyImmediate(window);
            }
        }

        static void CaptureWindow(EditorWindow window)
        {
            window.Show();window.position=new Rect(0,0,1240,780);
            typeof(EditorWindow).GetMethod("RepaintImmediately",Hidden).Invoke(window,null);
            var host=typeof(EditorWindow).GetField("m_Parent",Hidden).GetValue(window);
            var view=typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView");
            var rt=RenderTexture.GetTemporary(1240,780,24);
            var previous=RenderTexture.active;
            var pixels=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            try
            {
                view.GetMethod("GrabPixels",Hidden|BindingFlags.Public,null,new[]{typeof(RenderTexture),typeof(Rect)},null)
                    .Invoke(host,new object[]{rt,new Rect(0,0,1240,780)});
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();
                var colors=pixels.GetPixels32();
                for(int y=0;y<rt.height/2;y++)
                    for(int x=0;x<rt.width;x++)
                    {
                        int a=y*rt.width+x,b=(rt.height-1-y)*rt.width+x;
                        (colors[a],colors[b])=(colors[b],colors[a]);
                    }
                pixels.SetPixels32(colors);pixels.Apply();
                Directory.CreateDirectory("Logs/ProductionMigration");
                File.WriteAllBytes("Logs/ProductionMigration/animation-studio-v2.png",pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;Object.DestroyImmediate(pixels);RenderTexture.ReleaseTemporary(rt);}
        }
    }
}
