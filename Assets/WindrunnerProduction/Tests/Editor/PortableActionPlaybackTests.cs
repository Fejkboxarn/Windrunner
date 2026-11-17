using NUnit.Framework;
using PortableAnimation;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Windrunner.Production.Animation;

namespace Windrunner.Production.Tests
{
    public sealed class PortableActionPlaybackTests
    {
        [Test]
        public void ClipPreviewRetargetsBothHandsWithoutMovingTheSceneAvatar()
        {
            const string path="Assets/WindrunnerProduction/World/Scenes/World_Zone01.unity";
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool opened=!scene.isLoaded;
            if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
            var type=System.Type.GetType("AirflowPrototype.Editor.DaggerMotionPreview, Assembly-CSharp-Editor",true);
            var window=ScriptableObject.CreateInstance(type);
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            try
            {
                type.GetMethod("Bind",flags).Invoke(window,null);
                Assert.IsNull(type.GetField("issue",flags).GetValue(window));
                var rig=(Animator)type.GetField("previewRig",flags).GetValue(window);
                var bridge=(Component)type.GetField("bridge",flags).GetValue(window);
                var live=(Animator)bridge.GetType().GetProperty("HumanoidAnimator").GetValue(bridge);
                Assert.AreSame(live.avatar,rig.avatar);
                Assert.AreNotEqual(rig.gameObject.scene,live.gameObject.scene);
                Assert.IsFalse(rig.applyRootMotion);
                var root=rig.transform.position;
                var hand=rig.GetBoneTransform(HumanBodyBones.RightHand);
                var other=rig.GetBoneTransform(HumanBodyBones.LeftHand);
                var original=live.GetBoneTransform(HumanBodyBones.RightHand).rotation;
                var sample=type.GetMethod("EvaluatePose",flags);
                sample.Invoke(window,new object[]{0f,1f});
                var first=hand.rotation;var second=other.rotation;
                sample.Invoke(window,new object[]{.39f,1f});
                Assert.Greater(Quaternion.Angle(first,hand.rotation)+Quaternion.Angle(second,other.rotation),.05f);
                Assert.Less(Vector3.Distance(root,rig.transform.position),.001f);
                var profile=(WindrunnerAnimationProfile)bridge.GetType().GetProperty("Profile").GetValue(bridge);
                var assigned=new HashSet<AnimationClip>();
                var ground=profile.standaloneGroundProfile;
                for(int i=0;i<(int)GroundPose.Count;i++) assigned.Add(ground.Clip((GroundPose)i));
                for(int i=0;i<8;i++){assigned.Add(ground.RunDirectionalClip(i));assigned.Add(ground.WalkDirectionalClip(i));}
                for(int i=0;i<4;i++)assigned.Add(ground.StopClip(i));
                for(int i=0;i<profile.standaloneActionProfile.Count;i++)assigned.Add(profile.standaloneActionProfile.Action(i).clip);
                // Exercise production socket creation on the isolated Avatar, then sample
                // the actual model axes through every assigned clip on both hands.
                var daggerType=System.Type.GetType("Windrunner.Daggers.DaggerPlayer, Assembly-CSharp",true);
                var fixture=rig.gameObject.AddComponent(daggerType);
                daggerType.GetField("rig").SetValue(fixture,rig);
                var mounts=new List<(Transform model,Transform index,Transform little)>();
                var pairs=AssetDatabase.FindAssets("t:DaggerPair",new[]{"Assets/WindrunnerFrontend/Daggers/Defaults"});
                Assert.AreEqual(2,pairs.Length);
                foreach(bool left in new[]{true,false})
                {
                    var wrist=left?other:hand;
                    var middle=left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal;
                    var socket=(Transform)daggerType.GetMethod("CreateGrip",flags).Invoke(fixture,new object[]{wrist,middle,wrist});
                    var index=rig.GetBoneTransform(left?HumanBodyBones.LeftIndexProximal:HumanBodyBones.RightIndexProximal);
                    var little=rig.GetBoneTransform(left?HumanBodyBones.LeftLittleProximal:HumanBodyBones.RightLittleProximal);
                    foreach(var guid in pairs)
                    {
                        var pair=AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                        string side=left?"left":"right";
                        object Field(string suffix)=>pair.GetType().GetField(side+suffix).GetValue(pair);
                        var model=(GameObject)daggerType.GetMethod("Mount").Invoke(null,new[]{Field("Model"),socket,Field("Offset"),Field("Euler")});
                        mounts.Add((model.transform,index,little));
                    }
                }
                var turn=rig.transform.rotation;
                foreach(var clip in assigned)
                {
                    Assert.IsNotNull(clip);
                    type.GetMethod("SetPreviewClip",flags).Invoke(window,new object[]{clip});
                    foreach(float fraction in new[]{0f,.25f,.5f,.75f,.99f})
                    {
                        sample.Invoke(window,new object[]{clip.length*fraction,1f});
                        Assert.Less(Vector3.Distance(root,rig.transform.position),.001f,clip.name);
                        Assert.Less(Quaternion.Angle(turn,rig.transform.rotation),.1f,clip.name);
                        foreach(var grip in mounts)
                        {
                            Assert.Greater(Vector3.Dot(grip.model.up,(grip.index.position-grip.little.position).normalized),
                                .90f,clip.name+" blade direction at "+fraction);
                            Assert.Less(Vector3.Distance(grip.model.position,(grip.index.position+grip.little.position)*.5f),
                                rig.humanScale*.085f,clip.name+" handle to palm at "+fraction);
                        }
                        // Catch nonfinite samples and retargeting scale explosions in either wrist.
                        Assert.Less(Vector3.Distance(hand.position,rig.transform.position),rig.humanScale*3,clip.name);
                        Assert.Less(Vector3.Distance(other.position,rig.transform.position),rig.humanScale*3,clip.name);
                    }
                }
                Assert.Less(Quaternion.Angle(original,live.GetBoneTransform(HumanBodyBones.RightHand).rotation),.01f);
            }
            finally
            {
                Object.DestroyImmediate(window);
                if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
            }
        }

        [Test]
        public void DaggerProductionMappingsUseHumanoidChannelsAndTraversalSafeMasks()
        {
            var profile=AssetDatabase.LoadAssetAtPath<WindrunnerAnimationProfile>("Assets/WindrunnerProduction/Animation/Generated/WindrunnerAnimationProfile.asset");
            Assert.IsTrue(profile.ValidateForLiveGraph(out var error),error);
            void Check(AnimationClip clip)
            {
                Assert.IsNotNull(clip);
                Assert.IsTrue(clip.humanMotion && !clip.legacy && clip.length>0,clip.name);
                StringAssert.StartsWith("Assets/Dagger Animation Pack/Animations/Humanoid/",AssetDatabase.GetAssetPath(clip));
                Assert.Zero(AnimationUtility.GetAnimationEvents(clip).Length,clip.name);
            }
            for(int i=0;i<(int)GroundPose.Count;i++)Check(profile.standaloneGroundProfile.Clip((GroundPose)i));
            for(int i=0;i<8;i++)
            {
                Check(profile.standaloneGroundProfile.RunDirectionalClip(i));
                Check(profile.standaloneGroundProfile.WalkDirectionalClip(i));
            }
            foreach(var index in new[]{0,3,4,5,6,7})
            {
                var action=profile.standaloneActionProfile.Action(index);
                Check(action.clip);
                if(index==0)continue;
                Assert.IsFalse(action.mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root));
                Assert.IsFalse(action.mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg));
                Assert.IsFalse(action.mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg));
                Assert.IsTrue(action.mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm));
                Assert.IsTrue(action.mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm));
            }
        }

        [Test]
        public void CharacterOverridesAndMissingClipsDoNotMutateTheSharedProfile()
        {
            var source = AssetDatabase.LoadAssetAtPath<WindrunnerAnimationProfile>("Assets/WindrunnerProduction/Animation/Generated/WindrunnerAnimationProfile.asset");
            var character = Object.Instantiate(source);
            var actions = Object.Instantiate(source.standaloneActionProfile);
            var incompatible = new AnimationClip { legacy = true };
            try
            {
                character.standaloneActionProfile = actions;
                var original = source.standaloneActionProfile.Action(0).clip;
                float originalSpeed = source.standaloneActionProfile.Action(0).playbackSpeed;
                actions.Action(0).clip = null; actions.Action(0).playbackSpeed = 2f;
                Assert.That(character.ValidateForLiveGraph(out string error), Is.True, error);
                Assert.That(actions.Action(0).IsPlayable, Is.False);
                Assert.That(source.standaloneActionProfile.Action(0).clip, Is.SameAs(original));
                Assert.That(source.standaloneActionProfile.Action(0).playbackSpeed, Is.EqualTo(originalSpeed));
                actions.Action(0).clip = incompatible;
                Assert.That(actions.Action(0).IsPlayable, Is.False);
                actions.Action(0).playbackSpeed = 0f;
                Assert.That(character.ValidateForLiveGraph(out error), Is.False);
            }
            finally { Object.DestroyImmediate(character); Object.DestroyImmediate(actions); Object.DestroyImmediate(incompatible); }
        }

        [Test]
        public void ProductionDirectionalMixerFollowsAllHeadingsDespiteFastFacingChanges()
        {
            var profile = AssetDatabase.LoadAssetAtPath<GroundMotionProfile>("Assets/WindrunnerProduction/Animation/Generated/WindrunnerGroundMotion.asset");
            var graph = UnityEngine.Playables.PlayableGraph.Create("Directional recording probe");
            try
            {
                var source = new GroundMotionSource(graph, profile, true);
                int frame = 0;
                foreach (int heading in new[] { -1, 0, 4, 1, 5, 2, 6, 3, 7 })
                {
                    var velocity = heading < 0 ? Vector3.zero : Quaternion.Euler(0, heading * 45, 0) * Vector3.forward;
                    for (int i = 0; i < 90; i++)
                    {
                        source.Tick(new GroundMotionFrame(new AnimationFrameState(frame++, 1f/60, velocity,
                            velocity.magnitude, LocomotionMode.Grounded), true, 0, i%2 == 0 ? 400 : -400, false));
                        Assert.AreNotEqual(GroundMotionState.Turning, source.State);
                        Assert.AreNotEqual(GroundMotionState.Pivoting, source.State);
                    }
                    if (heading < 0) continue;
                    source.ReadDominant(out var clip, out var weight, out int actual);
                    Assert.AreEqual(heading, actual);
                    Assert.AreSame(profile.RunDirectionalClip(heading), clip);
                    Assert.Greater(weight, .99f);
                    Assert.That(source.CyclesPerSecond * clip.length, Is.EqualTo(1.25f).Within(.002f));
                }
                const float walkingSpeed = .4f;
                for (int i=0; i<120; i++)
                    source.Tick(new GroundMotionFrame(new AnimationFrameState(frame++, 1f/60,
                        Vector3.forward*walkingSpeed, walkingSpeed, LocomotionMode.Grounded), true, 0, 0, false));
                source.ReadDominant(out var walk, out float walkWeight, out int walkHeading);
                Assert.AreEqual(0,walkHeading);
                Assert.Greater(walkWeight,.99f);
                Assert.AreSame(profile.WalkDirectionalClip(0),walk);
                Assert.That(source.CyclesPerSecond*walk.length,
                    Is.EqualTo(Mathf.Lerp(.55f,1.25f,walkingSpeed)).Within(.002f));
            }
            finally { graph.Destroy(); }
        }

        [Test]
        public void InstalledLocomotionHandlesReversalsAirborneAndLandingWithNormalizedWeights()
        {
            var profile = AssetDatabase.LoadAssetAtPath<GroundMotionProfile>("Assets/WindrunnerProduction/Animation/Generated/WindrunnerGroundMotion.asset");
            var controller = new GroundMotionController(profile, true);
            var visited = new HashSet<GroundMotionState>();
            for (int frame = 0; frame < 900; frame++)
            {
                bool airborne = frame >= 500 && frame < 700;
                float vertical = frame < 600 ? 5f : -5f;
                Vector3 velocity = frame >= 100 && frame < 350 ? (frame < 220 ? Vector3.forward : Vector3.back) : Vector3.zero;
                var motion = new GroundMotionFrame(new AnimationFrameState(frame, 1f / 60f, velocity,
                    velocity.magnitude, airborne ? LocomotionMode.Airborne : LocomotionMode.Grounded), !airborne, vertical, 0f, frame == 500);
                controller.Advance(motion); visited.Add(controller.State);
                float sum = 0f;
                for (int i = 0; i < (int)GroundPose.Count; i++)
                {
                    float weight = controller.Weight((GroundPose)i);
                    Assert.That(float.IsNaN(weight), Is.False); Assert.That(weight, Is.GreaterThanOrEqualTo(0f)); sum += weight;
                }
                Assert.That(sum, Is.EqualTo(1f).Within(.001f));
            }
            foreach (var state in new[] { GroundMotionState.Idle, GroundMotionState.Starting, GroundMotionState.Moving,
                GroundMotionState.Stopping, GroundMotionState.JumpStarting, GroundMotionState.Rising, GroundMotionState.Falling, GroundMotionState.Landing })
                Assert.That(visited.Contains(state), Is.True, state.ToString());
        }

        [Test]
        public void PriorityLoopCancellationAndMissingSlotsHaveDeterministicRecovery()
        {
            var state = new PortableActionPlayback(new[] {
                new PortableActionPlayback.Timing(1f, 2f, .1f, .2f, 10),
                new PortableActionPlayback.Timing(.5f, 1f, .1f, .2f, 20, loop: true),
                new PortableActionPlayback.Timing(0f, 1f, .1f, .2f, 30, available: false) });
            Assert.That(state.Request(2), Is.False);
            Assert.That(state.Request(0), Is.True); state.Tick(.1f);
            Assert.That(state.ClipTime(0), Is.EqualTo(.2f).Within(.0001));
            Assert.That(state.Request(0), Is.False);
            Assert.That(state.Request(1), Is.True); state.Tick(.1f);
            Assert.That(state.Weight(0), Is.GreaterThan(0f), "Interrupted pose must fade rather than disappear.");
            Assert.That(state.Request(0), Is.False);
            state.Tick(5f); Assert.That(state.ActiveIndex, Is.EqualTo(1));
            state.Cancel(0); Assert.That(state.ActiveIndex, Is.EqualTo(1));
            state.Cancel(1); state.Tick(.3f);
            Assert.That(state.Weight(1), Is.Zero);
            Assert.That(state.Request(0), Is.True); state.Tick(.51f);
            Assert.That(state.ActiveIndex, Is.EqualTo(-1));
        }
        [Test]
        public void NonInterruptibleActionStillAllowsExplicitCancellationAndFreshReplay()
        {
            var state = new PortableActionPlayback(new[] {
                new PortableActionPlayback.Timing(1f, 1f, .1f, .2f, 1, interruptible: false),
                new PortableActionPlayback.Timing(1f, 1f, .1f, .2f, 100) });
            Assert.That(state.Request(0), Is.True); state.Tick(.2f);
            Assert.That(state.Request(1), Is.False);
            state.Cancel(); Assert.That(state.Request(1), Is.True);
            state.Tick(float.NaN); Assert.That(state.ClipTime(1), Is.Zero);
            state.Tick(0f); Assert.That(state.ActiveIndex, Is.EqualTo(1));
        }
    }
}
