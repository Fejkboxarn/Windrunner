using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PortableAnimation;
using UnityEditor;
using UnityEngine;

namespace Windrunner.Production.Tests
{
    public sealed class VisualPlaybackTests
    {
        static VisualPlaybackSettings Settings(float speed = 1, bool reverse = false) =>
            new VisualPlaybackSettings { mode = VisualPlaybackMode.AuthoredSpeed, baseSpeed = speed, reverse = reverse };

        [TestCase(.5f, 4)]
        [TestCase(1, 2)]
        [TestCase(2, 1)]
        public void ConstantRateIsSourceSecondsPerSecond(float speed, float duration)
        {
            var clock = new VisualPlaybackClock(Settings(speed), 2);
            Assert.That(clock.Duration, Is.EqualTo(duration).Within(.00001));
            Assert.That(clock.Sample(.25).SourceSeconds, Is.EqualTo(.25 * speed).Within(.00001));
            Assert.That(clock.Sample(.25).EffectiveSpeed, Is.EqualTo(speed).Within(.00001));
        }

        [Test]
        public void CurveReverseLoopsAndLargeStepsUseTheSameSourceDomain()
        {
            var settings = Settings();
            settings.useCurve = true; settings.speedCurve = AnimationCurve.Linear(0, .5f, 1, 2);
            var forward = new VisualPlaybackClock(settings, 2);
            settings.reverse = true;
            var reverse = new VisualPlaybackClock(settings, 2);
            Assert.That(reverse.Sample(0).SourceSeconds, Is.EqualTo(2));
            Assert.That(reverse.Duration, Is.EqualTo(forward.Duration).Within(.00001));
            Assert.That(reverse.Sample(.2).SourceSeconds, Is.EqualTo(forward.Sample(forward.Duration - .2).SourceSeconds).Within(.001));
            Assert.Less(reverse.Sample(.2).EffectiveSpeed, 0);
            Assert.That(reverse.Sample(reverse.Duration * 1000 + .2, true).SourceSeconds,
                Is.EqualTo(reverse.Sample(.2).SourceSeconds).Within(.00001));
            Assert.AreEqual(0, reverse.Sample(100).SourceSeconds);
            double partition = 0;
            foreach (double dt in new[] { .016, .051, .23, .003, .2 }) partition += dt;
            Assert.That(reverse.Sample(partition).SourceSeconds, Is.EqualTo(reverse.Sample(.5).SourceSeconds).Within(.00001));
        }

        [Test]
        public void ZeroHoldFiniteEntryHoldSegmentsAndInvalidCurvesAreExplicit()
        {
            var settings = Settings(0, true);
            var held = new VisualPlaybackClock(settings, 4, .2f, .8f);
            Assert.IsTrue(double.IsInfinity(held.Duration));
            Assert.That(held.Sample(100).SourceSeconds, Is.EqualTo(3.2f).Within(.00001));
            Assert.AreEqual(0, held.Sample(100).EffectiveSpeed);
            settings.baseSpeed = 1; settings.entryHoldSeconds = .5f;
            var release = new VisualPlaybackClock(settings, 4, .2f, .8f);
            Assert.That(release.Sample(.4).SourceSeconds, Is.EqualTo(3.2f).Within(.00001));
            Assert.That(release.Sample(.75).SourceSeconds, Is.EqualTo(2.95f).Within(.00001));
            Assert.That(release.Sample(100).SourceSeconds, Is.EqualTo(.8f).Within(.00001));
            settings.useCurve = true; settings.speedCurve = AnimationCurve.Linear(0, -1, 1, 1);
            Assert.Throws<InvalidOperationException>(() => new VisualPlaybackClock(settings, 4));
            settings.speedCurve = AnimationCurve.Linear(0, 1, 1, 1);
            settings.baseSpeed = float.NaN;
            Assert.Throws<InvalidOperationException>(settings.AssertValid);
        }

        [Test]
        public void FixedDurationShowsOverrideAndHeldOneShotCannotStayActive()
        {
            var settings = Settings(.5f);
            settings.mode = VisualPlaybackMode.FixedVisualDuration; settings.fixedSeconds = .5f;
            var fit = new VisualPlaybackClock(settings, 2);
            Assert.That(fit.Sample(.1).EffectiveSpeed, Is.EqualTo(4).Within(.00001));
            settings = Settings(0); settings.maximumActionSeconds = .5f;
            var clock = new VisualPlaybackClock(settings, 2);
            var playback = new PortableActionPlayback(new[] { new PortableActionPlayback.Timing(2, 1, .05f, .1f, 0, visual: clock) });
            playback.Request(0); playback.Tick(.2f);
            float source = playback.ClipTime(0);
            playback.Tick(0); Assert.AreEqual(source, playback.ClipTime(0));
            playback.Tick(.25f); Assert.That(playback.Weight(0), Is.EqualTo(.5f).Within(.0001));
            playback.Tick(.1f); Assert.AreEqual(-1, playback.ActiveIndex); Assert.AreEqual(0, playback.Weight(0));
        }

        [Test]
        public void ContextSwitchKeepsSourceProgressAndIndependentFadeClock()
        {
            var forward = new VisualPlaybackClock(Settings(.5f), 2);
            var reverseSettings = Settings(2, true);
            reverseSettings.useCurve = true; reverseSettings.speedCurve = AnimationCurve.Linear(0, 1, 1, 2);
            var reverse = new VisualPlaybackClock(reverseSettings, 2);
            var playback = new PortableActionPlayback(new[] { new PortableActionPlayback.Timing(2, 1, .05f, .1f, 0, visual: forward) });
            playback.Request(0); playback.Tick(.5f);
            float before = playback.ClipTime(0);
            playback.ChangeTiming(0, new PortableActionPlayback.Timing(2, 1, .05f, .1f, 0, visual: reverse));
            Assert.That(playback.ClipTime(0), Is.EqualTo(before).Within(.00001));
            playback.Tick(.02f);
            Assert.Less(playback.ClipTime(0), before); Assert.That(playback.Weight(0), Is.GreaterThan(.9f));
            float priorWeight = playback.Weight(0);
            playback.Cancel(); playback.Tick(.05f);
            Assert.That(playback.Weight(0), Is.EqualTo(priorWeight - .5f).Within(.0001));
        }

        [Test]
        public void SwitchingToAHeldContextRetainsThePoseAndCanResume()
        {
            var moving=new PortableActionPlayback.Timing(2,1,.01f,.1f,0,visual:new VisualPlaybackClock(Settings(),2));
            var frozen=new PortableActionPlayback.Timing(2,1,.01f,.1f,0,visual:new VisualPlaybackClock(Settings(0,true),2));
            var playback=new PortableActionPlayback(new[]{moving});
            playback.Request(0);playback.Tick(.6f);float source=playback.ClipTime(0);
            playback.ChangeTiming(0,frozen);playback.Tick(.3f);
            Assert.That(playback.ClipTime(0),Is.EqualTo(source).Within(.00001));
            Assert.AreEqual(0,playback.VisualSample(0).EffectiveSpeed);
            Assert.IsFalse(float.IsNaN(playback.VisualSample(0).SourceSeconds));
            playback.ChangeTiming(0,moving);playback.Tick(.1f);
            Assert.That(playback.ClipTime(0),Is.EqualTo(source+.1f).Within(.00001));
        }

        [Test]
        public void MixedContextClocksCannotExtendTheOneShotSafetyDeadline()
        {
            var slow=new PortableActionPlayback.Timing(2,1,.01f,.1f,0,visual:new VisualPlaybackClock(Settings(.5f),2));
            var legacy=new PortableActionPlayback.Timing(2,1,.01f,.1f,0);
            var freeze=Settings(0);freeze.maximumActionSeconds=.5f;
            var frozen=new PortableActionPlayback.Timing(2,1,.01f,.1f,0,visual:new VisualPlaybackClock(freeze,2));
            var state=new PortableActionPlayback(new[]{slow});
            state.Request(0);state.Tick(.3f);
            state.ChangeTiming(0,legacy);state.Tick(.1f);
            state.ChangeTiming(0,frozen);state.Tick(.11f);
            Assert.AreEqual(-1,state.ActiveIndex);
        }

        [Test]
        public void CompiledClockSamplingDoesNotAllocateManagedMemory()
        {
            var settings=Settings(1,true);settings.useCurve=true;
            settings.speedCurve=AnimationCurve.EaseInOut(0,.5f,1,2);
            var clock=new VisualPlaybackClock(settings,2);
            double sum=0;
            for(int i=0;i<100;i++)sum+=clock.Sample(i*.02,true).SourceSeconds;
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<10000;i++)sum+=clock.Sample(i*.02,true).SourceSeconds;
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.Greater(sum,0);
            Assert.AreEqual(0,allocated,"Only compiled-clock Sample calls are measured.");
            Debug.Log("Compiled clock sampling: 10000 samples, "+allocated+" managed bytes on the calling thread.");
        }

        [Test]
        public void TimingAuditUsesInstalledClipsAndActualGraphSamples()
        {
            const string folder = "Assets/WindrunnerProduction/Animation/Generated/";
            var ground = AssetDatabase.LoadAssetAtPath<GroundMotionProfile>(folder + "WindrunnerGroundMotion.asset");
            var actions = AssetDatabase.LoadAssetAtPath<PortableCharacterActionProfile>(folder + "WindrunnerActionClips.asset");
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/AirflowPrototype/VampGirl_V2/Vamgirl_Windrunner.fbx"));
            var rig = model.GetComponentInChildren<Animator>();
            rig.applyRootMotion = false; rig.fireEvents = false; rig.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var rows = new System.Collections.Generic.List<string> { "Assignment,Context,Clip,SourceSeconds,EffectiveSpeed,VisualSeconds,SourcePath" };
            void Add(string id, string context, AnimationClip clip, VisualPlaybackSample sample)
            {
                if (clip) rows.Add(string.Join(",", id, context, clip.name, clip.length.ToString("F6",System.Globalization.CultureInfo.InvariantCulture),
                    sample.EffectiveSpeed.ToString("F6",System.Globalization.CultureInfo.InvariantCulture),
                    sample.EstimatedSeconds.ToString("F6",System.Globalization.CultureInfo.InvariantCulture), AssetDatabase.GetAssetPath(clip)));
            }
            try
            {
                var auditGraph = UnityEngine.Playables.PlayableGraph.Create("Timing audit ground");
                try
                {
                    var source = new GroundMotionSource(auditGraph, ground, true);
                    for (int f = 0; f < 120; f++) source.Tick(1, 1f/60);
                    using var serialized = new SerializedObject(ground);
                    for (int i = 0; i < 33; i++)
                    {
                        var clip = serialized.FindProperty(GroundMotionSource.TimingKey(i))?.objectReferenceValue as AnimationClip;
                        Add(GroundMotionSource.TimingKey(i), "full-run-input", clip, source.ReadTiming(i));
                    }
                }
                finally { auditGraph.Destroy(); }
                for (int i = 0; i < actions.Count; i++)
                    foreach (var context in new[] { ActionMotionContext.Stationary, ActionMotionContext.GroundedMoving, ActionMotionContext.Airborne })
                    {
                        using var graph = new GroundOnlyPipelineRuntime(rig, ground, actions);
                        float speed = context == ActionMotionContext.Stationary ? 0 : 6;
                        for (int f = 0; f < 15; f++)
                        {
                            if (f == 1) graph.RequestAction(i);
                            graph.Evaluate(new GroundMotionFrame(new AnimationFrameState(f,1f/120,Vector3.forward*speed,speed/8.5f,
                                context == ActionMotionContext.Airborne ? LocomotionMode.Airborne : LocomotionMode.Grounded),
                                context != ActionMotionContext.Airborne, 0, 0),speed);
                        }
                        graph.ReadRecordingPose(out var pose);Add(actions.Action(i).id,context.ToString(),pose.actionClip,pose.timing);
                    }
                Directory.CreateDirectory("Logs/ProductionMigration");
                File.WriteAllLines("Logs/ProductionMigration/animation-timing-audit.csv",rows);
                Assert.Greater(rows.Count,40);
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }
    }
}
