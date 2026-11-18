using System;
using NUnit.Framework;
using PortableAnimation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Windrunner.Production.Tests
{
    public sealed class VisualPlaybackPoseTests
    {
        const string Folder = "Assets/WindrunnerProduction/Animation/Generated/";
        [TestCase(.5f, false, false)]
        [TestCase(1f, false, false)]
        [TestCase(2f, true, false)]
        [TestCase(1f, true, true)]
        [TestCase(0f, true, false)]
        public void ProductionSamplesExactReverseOrCurvePoseWithWeightedArm(float speed, bool reverse, bool curve)
        {
            var ground = AssetDatabase.LoadAssetAtPath<GroundMotionProfile>(Folder + "WindrunnerGroundMotion.asset");
            var original = AssetDatabase.LoadAssetAtPath<PortableCharacterActionProfile>(Folder + "WindrunnerActionClips.asset");
            var profile = Object.Instantiate(original);
            var a = profile.Action(4);
            a.stationary.enabled = a.airborne.enabled = false;
            a.blendWeight = 1; a.blendInSeconds = .001f; a.loopUntilCanceled = true;
            a.regions = new RegionMaskSettings { enabled = true };
            a.regions.regions.Add(new PortableAnimation.Authoring.RegionInfluenceSettings {
                region = PortableAnimation.Authoring.AuthoringBodyRegion.RightUpperArm, rotation = 1 });
            a.playback.mode = VisualPlaybackMode.AuthoredSpeed; a.playback.baseSpeed = speed;
            a.playback.reverse = reverse; a.playback.useCurve = curve;
            a.playback.speedCurve = AnimationCurve.Linear(0, .5f, 1, 2);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AirflowPrototype/VampGirl_V2/Vamgirl_Windrunner.fbx");
            var actor = Object.Instantiate(model); var reference = Object.Instantiate(model);
            Animator Configure(GameObject subject)
            {
                var rig = subject.GetComponentInChildren<Animator>();
                rig.runtimeAnimatorController = null; rig.fireEvents = false; rig.applyRootMotion = false;
                rig.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                return rig;
            }
            var rig = Configure(actor); var peer = Configure(reference);
            var graph = PlayableGraph.Create("Direct source pose oracle");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var clip = AnimationClipPlayable.Create(graph, a.clip); clip.SetSpeed(0);
            var output = AnimationPlayableOutput.Create(graph, "Direct source", peer); output.SetSourcePlayable(clip);
            graph.Play();
            try
            {
                using var runtime = new GroundOnlyPipelineRuntime(rig, ground, profile);
                var clock = new VisualPlaybackClock(a.playback, a.clip.length);
                runtime.RequestAction(4);
                double elapsed = 0;
                for (int i = 0; i < 15; i++)
                {
                    float dt = i == 7 ? .31f : .02f; elapsed += dt;
                    var motion = new AnimationFrameState(i, dt, Vector3.forward*6, 6/8.5f, LocomotionMode.Grounded);
                    runtime.Evaluate(new GroundMotionFrame(motion, true, 0, 0), 6);
                    var expected = clock.Sample(elapsed, true);
                    runtime.ReadRecordingPose(out var pose);
                    Assert.That(pose.actionSeconds, Is.EqualTo(Mathf.Min(expected.SourceSeconds,a.clip.length-.0001f)).Within(.0001));
                    clip.SetTime(Math.Min(expected.SourceSeconds, a.clip.length-.0001f)); graph.Evaluate(0);
                    Assert.Less(Quaternion.Angle(rig.GetBoneTransform(HumanBodyBones.RightUpperArm).localRotation,
                        peer.GetBoneTransform(HumanBodyBones.RightUpperArm).localRotation), .4f);
                }
                Assert.That(actor.transform.position.sqrMagnitude, Is.LessThan(.000001));
                Assert.That(Quaternion.Angle(actor.transform.rotation, Quaternion.identity), Is.LessThan(.001));
            }
            finally { graph.Destroy(); Object.DestroyImmediate(actor); Object.DestroyImmediate(reference); Object.DestroyImmediate(profile); }
        }

        [Test]
        public void GroundSlotOverrideAndOriginalComparisonDoNotStackGaitMultipliers()
        {
            var source = AssetDatabase.LoadAssetAtPath<GroundMotionProfile>(Folder + "WindrunnerGroundMotion.asset");
            var profile = Object.Instantiate(source);
            string key = GroundMotionProfile.TimingSlot(GroundPose.MoveForward);
            var settings = profile.EditPlayback(key);
            settings.mode = VisualPlaybackMode.AuthoredSpeed; settings.baseSpeed = .5f; settings.reverse = true;
            var graph = PlayableGraph.Create("Ground timing");
            try
            {
                var normal = new GroundMotionSource(graph,profile,true);
                var original = new GroundMotionSource(graph,profile,true,true);
                for(int i=0;i<120;i++){normal.Tick(1,1f/60);original.Tick(1,1f/60);}
                var current = normal.ReadTiming((int)GroundPose.MoveForward);
                Assert.That(current.EffectiveSpeed,Is.EqualTo(-.5f).Within(.00001));
                Assert.That(original.ReadTiming((int)GroundPose.MoveForward).EffectiveSpeed,Is.EqualTo(1).Within(.00001));
                Assert.That(original.ReadTiming((int)GroundPose.MoveLeft).EffectiveSpeed,Is.EqualTo(1).Within(.00001));
                Assert.IsNull(source.Playback(key));
            }
            finally{graph.Destroy();Object.DestroyImmediate(profile);}
        }
    }
}
