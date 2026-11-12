using System;
using NUnit.Framework;
using Windrunner.Production.Core;

namespace Windrunner.Production.Tests
{
    public sealed class TimeScaleArbitrationTests
    {
        [TestCase(1f, 0.02f)]
        [TestCase(0.5f, 0.01f)]
        public void NestedPauseAndSlowdownsRestoreOnlyTheirOwnRequests(float baseline, float fixedStep)
        {
            var time = new TimeScaleArbiter(baseline, fixedStep);
            var slow = time.Slowdown(0.2f);
            var other = time.Slowdown(0.1f);
            var pause = time.Pause();
            var nested = time.Pause();
            Assert.That(time.EffectiveScale, Is.Zero);
            Assert.That(time.EffectiveFixedStep, Is.EqualTo(fixedStep * 0.1f / baseline).Within(0.000001));
            other.Dispose();
            pause.Dispose();
            Assert.That(time.EffectiveScale, Is.Zero);
            nested.Dispose();
            Assert.That(time.EffectiveScale, Is.EqualTo(0.2f));
            slow.SetScale(0.35f);
            Assert.That(time.EffectiveScale, Is.EqualTo(0.35f));
            slow.Dispose();
            slow.Dispose();
            slow.SetScale(0.01f);
            Assert.That(time.RequestCount, Is.Zero);
            Assert.That(time.EffectiveScale, Is.EqualTo(baseline));
            Assert.That(time.EffectiveFixedStep, Is.EqualTo(fixedStep));
        }

        [Test]
        public void HitStopMatchesLegacyCurveAndFreezesDuringPause()
        {
            var type = Type.GetType("AirflowPrototype.HitStopEnvelope, Assembly-CSharp", true);
            var start = type.GetMethod("Start");
            var advance = type.GetMethod("Advance");
            var random = new Random(7712);
            for (int scenario = 0; scenario < 400; scenario++)
            {
                var envelope = Activator.CreateInstance(type);
                float requested = 0.02f + (float)random.NextDouble() * 0.8f;
                float quality = (float)random.NextDouble();
                float hold = (float)random.NextDouble() * 0.2f;
                float recovery = 0.05f + (float)random.NextDouble() * 0.3f;
                float initial = UnityEngine.Mathf.Lerp(Math.Min(1f, requested * 1.18f), requested, quality);
                start.Invoke(envelope, new object[] { quality, hold, recovery, requested, 1f });
                float elapsed = 0f;
                for (int step = 0; step < 60; step++)
                {
                    bool paused = step >= 4 && step < 9;
                    if (!paused) elapsed += 1f / 120f;
                    advance.Invoke(envelope, new object[] { 1f / 120f, paused });
                    float t = UnityEngine.Mathf.Clamp01((elapsed - hold) / recovery);
                    float expected = elapsed <= hold ? initial : UnityEngine.Mathf.Lerp(initial, 1f, t * t * (3f - 2f * t));
                    Assert.That((float)type.GetProperty("Scale").GetValue(envelope), Is.EqualTo(expected).Within(0.000001f));
                    if (t >= 1f) break;
                }
            }
        }
    }
}
