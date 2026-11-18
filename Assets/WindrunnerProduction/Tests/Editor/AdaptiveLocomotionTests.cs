using System;
using NUnit.Framework;
using PortableAnimation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Windrunner.Production.Tests
{
    public sealed class AdaptiveLocomotionTests
    {
        const string Folder = "Assets/WindrunnerProduction/Animation/Generated/";
        [Test]
        public void ProductionSessionUsesResolvedMotionWithoutScalingReferenceSpeed()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Windrunner.Production.Animation.WindrunnerAnimationProfile>(Folder+"WindrunnerAnimationProfile.asset"));
            var ground = Object.Instantiate(root.standaloneGroundProfile); root.standaloneGroundProfile = ground;
            root.motionSpeedMultiplier = 2;
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AirflowPrototype/VampGirl_V2/Vamgirl_Windrunner.fbx"));
            try
            {
                string key = GroundMotionProfile.TimingSlot(GroundPose.MoveForward);
                ground.EditPlayback(key).mode = VisualPlaybackMode.AuthoredSpeed;
                var settings = ground.EditAdaptive(key); settings.enabled = true; settings.referenceSpeed = 6; settings.smoothingSeconds = 0;
                using var session = new Windrunner.Production.Animation.WindrunnerAnimationSession(actor.GetComponentInChildren<Animator>(), root, Array.Empty<Behaviour>());
                Assert.IsTrue(session.TryStartPortable(), session.LastError);
                int frame = 0;
                void Step(Vector3 resolved)
                {
                    session.Evaluate(new Windrunner.Production.Animation.WindrunnerMotionFrame(frame++, .02f,
                        Vector3.forward*8.5f, 8.5f, 0, 0, 0, 0, true, false, false, resolved));
                }
                for (int i = 0; i < 60; i++) Step(Vector3.forward*6);
                Assert.IsTrue(session.ReadRecordingPose(out var moving));
                Assert.IsTrue(moving.adaptive.Enabled);
                Assert.That(moving.adaptive.ActualSpeed, Is.EqualTo(6).Within(.0001));
                Assert.That(moving.groundTiming.EffectiveSpeed, Is.EqualTo(1).Within(.0001));
                for (int i = 0; i < 60; i++) Step(Vector3.zero);
                session.ReadRecordingPose(out var blocked);
                Assert.AreEqual(GroundMotionState.Idle, blocked.state);
                Assert.AreEqual(0, blocked.actualGroundSpeed);
                Assert.AreEqual(ActionMotionContext.GroundedMoving, blocked.context);
                Assert.That(actor.transform.position.sqrMagnitude, Is.LessThan(.000001));
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(ground); Object.DestroyImmediate(root); }
        }
        [TestCase(0, .65f)]
        [TestCase(3, .65f)]
        [TestCase(6, 1)]
        [TestCase(9, 1.5f)]
        [TestCase(100, 1.5f)]
        public void ActualMetresDetermineClampedCadence(float speed, float expected)
        {
            var settings = new AdaptiveLocomotionSettings { smoothingSeconds = 0 };
            var sample = new AdaptiveLocomotionClock(settings).Advance(speed, .02f);
            Assert.That(sample.Multiplier, Is.EqualTo(expected).Within(.00001));
        }
        [Test]
        public void ResponseDesignerAndExponentialSmoothingHaveExplicitContributions()
        {
            var settings = new AdaptiveLocomotionSettings { referenceSpeed = 5, smoothingSeconds = .1f,
                designerMultiplier = .8f, useResponseCurve = true, responseCurve = AnimationCurve.Linear(0, 0, 2, 3) };
            var one = new AdaptiveLocomotionClock(settings);
            var many = new AdaptiveLocomotionClock(settings);
            var a = one.Advance(5, .2f);
            AdaptiveLocomotionSample b = default;
            for (int i = 0; i < 20; i++) b = many.Advance(5, .01f);
            Assert.That(a.Ratio, Is.EqualTo(1)); Assert.That(a.Response, Is.EqualTo(1.5f).Within(.0001));
            Assert.That(a.Multiplier, Is.EqualTo(b.Multiplier).Within(.00001));
            Assert.That(a.Multiplier, Is.EqualTo(Mathf.Lerp(.65f, 1.2f, 1-Mathf.Exp(-2))).Within(.00001));
            var stopped = one.Advance(0, .2f);
            Assert.That(stopped.Multiplier, Is.LessThan(a.Multiplier).And.GreaterThan(.65f));
            Assert.That(one.Advance(100, 0).Multiplier, Is.EqualTo(stopped.Multiplier));
            Assert.Throws<ArgumentOutOfRangeException>(() => one.Advance(float.NaN, .1f));
        }
        [Test]
        public void InvalidCalibrationAndIncompatibleSynchronizationAreRejected()
        {
            var profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GroundMotionProfile>(Folder+"WindrunnerGroundMotion.asset"));
            try
            {
                string key = GroundMotionProfile.TimingSlot(GroundPose.MoveForward);
                var speed = profile.EditPlayback(key); speed.mode = VisualPlaybackMode.AuthoredSpeed;
                var adaptive = profile.EditAdaptive(key); adaptive.enabled = true;
                adaptive.referenceSpeed = 0; Assert.Throws<InvalidOperationException>(profile.AssertStandaloneValid);
                adaptive.referenceSpeed = 6; adaptive.phaseGroup = 1; speed.reverse = true;
                Assert.Throws<InvalidOperationException>(profile.AssertStandaloneValid);
                adaptive.phaseGroup = 0; Assert.DoesNotThrow(profile.AssertStandaloneValid);
                speed.mode = VisualPlaybackMode.FixedVisualDuration;
                Assert.Throws<InvalidOperationException>(profile.AssertStandaloneValid);
                speed.mode = VisualPlaybackMode.AuthoredSpeed; speed.reverse = false;
                adaptive.phaseGroup = 1; adaptive.minimumSpeed = adaptive.maximumSpeed = 1;
                string other = GroundMotionProfile.TimingSlot(GroundPose.MoveBackward);
                profile.EditPlayback(other).mode = VisualPlaybackMode.AuthoredSpeed;
                var peer = profile.EditAdaptive(other); peer.enabled = true; peer.phaseGroup = 1;
                peer.minimumSpeed = peer.maximumSpeed = 10;
                var graph = PlayableGraph.Create("Conflicting calibrated cadence limits");
                try { Assert.Throws<InvalidOperationException>(() => new GroundMotionSource(graph, profile, true)); }
                finally { graph.Destroy(); }

            }
            finally { Object.DestroyImmediate(profile); }
        }
        [Test]
        public void EightWayGroupsPreservePhaseThroughDirectionAndStateTransitions()
        {
            var profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GroundMotionProfile>(Folder+"WindrunnerGroundMotion.asset"));
            var graph = PlayableGraph.Create("Adaptive directional regression");
            try
            {
                for (int i = 0; i < 33; i++)
                {
                    string key = GroundMotionSource.TimingKey(i);
                    if (!GroundMotionProfile.IsDirectionalSlot(key)) continue;
                    profile.EditPlayback(key).mode = VisualPlaybackMode.AuthoredSpeed;
                    var settings = profile.EditAdaptive(key); settings.enabled = true; settings.phaseGroup = 1;
                    settings.referenceSpeed = i % 2 == 0 ? 5 : 7; settings.smoothingSeconds = 0;
                }
                var source = new GroundMotionSource(graph, profile, true);
                int frame = 0;
                void Tick(float speed, Vector3 direction, bool grounded = true, bool jump = false)
                {
                    var motion = new AnimationFrameState(frame++, .02f, direction*speed, speed/8.5f,
                        grounded ? LocomotionMode.Grounded : LocomotionMode.Airborne);
                    source.Tick(new GroundMotionFrame(motion, grounded, grounded ? 0 : jump ? 3 : -3, 0, jump, speed));
                }
                for (int direction = 0; direction < 8; direction++)
                {
                    Vector3 heading = Quaternion.Euler(0, direction*45, 0)*Vector3.forward;
                    for (int i = 0; i < 40; i++) Tick(8.5f, heading);
                    var forward = source.ReadTiming(GroundMotionProfile.TimingSlot(GroundPose.MoveForward));
                    var backward = source.ReadTiming(GroundMotionProfile.TimingSlot(GroundPose.MoveBackward));
                    Assert.That(forward.Progress, Is.EqualTo(backward.Progress).Within(.0003));
                    Assert.That(forward.EffectiveSpeed/forward.SourceLength,
                        Is.EqualTo(backward.EffectiveSpeed/backward.SourceLength).Within(.0001));
                    float phase = forward.Progress;
                    Tick(8.5f, -heading);
                    float next = source.ReadTiming(GroundMotionProfile.TimingSlot(GroundPose.MoveForward)).Progress;
                    Assert.That(Mathf.Repeat(next-phase, 1), Is.LessThan(.15f), "Direction change restarted the cycle.");
                }
                Tick(8.5f, Vector3.forward, false, true);
                for (int i = 0; i < 30; i++) Tick(8.5f, Vector3.forward, false);
                Assert.AreEqual(GroundMotionState.Falling, source.State);
                float airbornePhase = source.ReadTiming(GroundMotionProfile.TimingSlot(GroundPose.MoveForward)).Progress;
                Tick(8.5f, Vector3.forward, false);
                Assert.AreEqual(airbornePhase, source.ReadTiming(GroundMotionProfile.TimingSlot(GroundPose.MoveForward)).Progress);
                for (int i = 0; i < 40; i++) Tick(8.5f, Vector3.forward);
                Assert.AreEqual(GroundMotionState.Moving, source.State);
                for (int i = 0; i < 60; i++) Tick(0, Vector3.forward);
                Assert.AreEqual(GroundMotionState.Idle, source.State);
                Assert.That(source.IdleBlend, Is.GreaterThan(.99));
            }
            finally { graph.Destroy(); Object.DestroyImmediate(profile); }
        }
        [Test]
        public void AdaptiveReverseUsesSourceCurveOnceAndOriginalComparisonBypassesEveryMultiplier()
        {
            var profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GroundMotionProfile>(Folder+"WindrunnerGroundMotion.asset"));
            var graph = PlayableGraph.Create("Adaptive reverse regression");
            try
            {
                string key = GroundMotionProfile.TimingSlot(GroundPose.MoveForward);
                var timing = profile.EditPlayback(key); timing.mode = VisualPlaybackMode.AuthoredSpeed;
                timing.baseSpeed = .5f; timing.reverse = true; timing.useCurve = true;
                timing.speedCurve = AnimationCurve.Linear(0, 2, 1, 2);
                var settings = profile.EditAdaptive(key); settings.enabled = true;
                settings.referenceSpeed = 6; settings.smoothingSeconds = 0;
                var adaptive = new GroundMotionSource(graph, profile, true);
                var original = new GroundMotionSource(graph, profile, true, true);
                for (int i = 0; i < 60; i++)
                {
                    var motion = new AnimationFrameState(i, .02f, Vector3.forward, 1, LocomotionMode.Grounded);
                    var frame = new GroundMotionFrame(motion, true, 0, 0, false, 9);
                    adaptive.Tick(frame); original.Tick(frame);
                }
                Assert.That(adaptive.ReadTiming(key).EffectiveSpeed, Is.EqualTo(-1.5f).Within(.0001));
                Assert.That(original.ReadTiming(key).EffectiveSpeed, Is.EqualTo(1).Within(.0001));
                Assert.IsFalse(original.ReadAdaptive(key).Enabled);
            }
            finally { graph.Destroy(); Object.DestroyImmediate(profile); }
        }
    }
}
