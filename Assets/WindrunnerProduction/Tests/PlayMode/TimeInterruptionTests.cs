using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Windrunner.Production.Input;
using Windrunner.Production.Unity;

namespace Windrunner.Production.Tests
{
    public sealed class TimeInterruptionTests
    {
        [UnityTest]
        public IEnumerator MenuPauseOwnsZeroUntilAllPausesReleaseAndHitStopRecovers()
        {
            float baseline = Time.timeScale, fixedStep = Time.fixedDeltaTime, capture = Time.captureDeltaTime;
            var template = WindrunnerInputActionsFactory.CreateDefaults();
            var input = new WindrunnerInputService(template);
            var menu = new WindrunnerPauseMenuController(input, null, null);
            var settings = ScriptableObject.CreateInstance(Type.GetType("AirflowPrototype.AirTargetUISettings, Assembly-CSharp", true));
            var first = new GameObject("First hit stop");
            var second = new GameObject("Second hit stop");
            Windrunner.Production.Core.TimeScaleArbiter.Request nested = null;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                var type = Type.GetType("AirflowPrototype.AirPerfectTimingFeedback, Assembly-CSharp", true);
                var a = (Behaviour)first.AddComponent(type);
                var b = (Behaviour)second.AddComponent(type);
                type.GetMethod("Configure").Invoke(a, new object[] { null, settings });
                type.GetMethod("Configure").Invoke(b, new object[] { null, settings });
                var trigger = type.GetMethod("TriggerPerfectFeedback", new[] { typeof(float), typeof(float), typeof(float), typeof(float) });
                trigger.Invoke(a, new object[] { 1f, 0.1f, 0.2f, 0.2f });
                trigger.Invoke(b, new object[] { 1f, 0.1f, 0.2f, 0.4f });
                Assert.That(Time.timeScale, Is.EqualTo(0.2f).Within(0.00001f));
                menu.Pause();
                nested = UnityTimeAuthority.Pause();
                for (int i = 0; i < 8; i++) { yield return null; Assert.That(Time.timeScale, Is.Zero); }
                a.enabled = false;
                menu.Resume();
                Assert.That(Time.timeScale, Is.Zero);
                nested.Dispose();
                Assert.That(Time.timeScale, Is.EqualTo(0.4f).Within(0.00001f));
                for (int i = 0; i < 60; i++) yield return null;
                Assert.That(Time.timeScale, Is.EqualTo(baseline).Within(0.00001f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep).Within(0.00001f));
            }
            finally
            {
                first.SetActive(false); second.SetActive(false);
                nested?.Dispose(); menu.Dispose(); input.Dispose();
                Time.captureDeltaTime = capture;
                UnityEngine.Object.Destroy(first); UnityEngine.Object.Destroy(second);
                UnityEngine.Object.Destroy(settings); UnityEngine.Object.Destroy(template);
            }
            yield return null;
        }
    }
}
