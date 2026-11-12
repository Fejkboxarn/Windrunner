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
                actions.Action(0).clip = null; actions.Action(0).playbackSpeed = 2f;
                Assert.That(character.ValidateForLiveGraph(out string error), Is.True, error);
                Assert.That(actions.Action(0).IsPlayable, Is.False);
                Assert.That(source.standaloneActionProfile.Action(0).clip, Is.SameAs(original));
                Assert.That(source.standaloneActionProfile.Action(0).playbackSpeed, Is.EqualTo(1f));
                actions.Action(0).clip = incompatible;
                Assert.That(actions.Action(0).IsPlayable, Is.False);
                actions.Action(0).playbackSpeed = 0f;
                Assert.That(character.ValidateForLiveGraph(out error), Is.False);
            }
            finally { Object.DestroyImmediate(character); Object.DestroyImmediate(actions); Object.DestroyImmediate(incompatible); }
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
