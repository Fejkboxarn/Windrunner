using System;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Windrunner.Production.Feedback;

namespace Windrunner.Production.Tests
{
    public sealed class RuntimeAllocationTests
    {
        [Test]
        public void MeasureResourceUiReadAndFeedbackDispatch()
        {
            var owner = new GameObject("Allocation fixture"); owner.SetActive(false);
            var settings = ScriptableObject.CreateInstance(Type.GetType("AirflowPrototype.AirflowMovementSettings, Assembly-CSharp", true));
            try
            {
                var powerType = Type.GetType("AirflowPrototype.AirPower, Assembly-CSharp", true);
                var power = owner.AddComponent(powerType);
                powerType.GetMethod("Configure").Invoke(power, new object[] { settings, true });
                var uiType = Type.GetType("AirflowPrototype.AirPowerProgressBarUI, Assembly-CSharp", true);
                var ui = owner.AddComponent(uiType);
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                uiType.GetField("airPower", flags).SetValue(ui, power);
                uiType.GetMethod("BuildAirPowerReader", flags).Invoke(ui, null);
                var read = (Func<float>)uiType.GetField("_readNormalized", flags).GetValue(ui);
                for (int i = 0; i < 1000; i++) read();
                var stopwatch = new Stopwatch();
                long calibrationStart = GC.GetAllocatedBytesForCurrentThread();
                var calibration = new byte[8192];
                long calibrationBytes = GC.GetAllocatedBytesForCurrentThread() - calibrationStart;
                GC.KeepAlive(calibration);
                bool allocationsSupported = calibrationBytes >= 8192;
                UnityEngine.Debug.Log("[Runtime profile] ALLOCATION_COUNTER_SUPPORTED=" + allocationsSupported + " calibrationBytes=" + calibrationBytes);
                long before = GC.GetAllocatedBytesForCurrentThread(); stopwatch.Start();
                float total = 0f;
                for (int i = 0; i < 100000; i++) total += read();
                stopwatch.Stop(); long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                UnityEngine.Debug.Log("[Runtime profile] UI_RESOURCE_READ iterations=100000 bytes=" + bytes + " milliseconds=" + stopwatch.Elapsed.TotalMilliseconds);
                Assert.That(total, Is.EqualTo(100000f));
                var direct = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), power, powerType.GetProperty("Normalized").GetGetMethod());
                for (int i = 0; i < 1000; i++) direct();
                stopwatch.Restart();
                total = 0f;
                for (int i = 0; i < 100000; i++) total += direct();
                stopwatch.Stop();
                UnityEngine.Debug.Log("[Runtime profile] DIRECT_RESOURCE_READ iterations=100000 milliseconds=" + stopwatch.Elapsed.TotalMilliseconds);
                Assert.That(total, Is.EqualTo(100000f));
                using (var feedback = new FeedbackService(null, null, null, null, null, false))
                {
                    int count = 0; feedback.Observed += (key, request) => count++;
                    for (int i = 0; i < 1000; i++) feedback.Publish(FeedbackKey.Jump, default);
                    stopwatch.Reset(); before = GC.GetAllocatedBytesForCurrentThread(); stopwatch.Start();
                    for (int i = 0; i < 100000; i++) feedback.Publish(FeedbackKey.Jump, default);
                    stopwatch.Stop(); long eventBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                    UnityEngine.Debug.Log("[Runtime profile] FEEDBACK_DISPATCH iterations=100000 bytes=" + eventBytes + " milliseconds=" + stopwatch.Elapsed.TotalMilliseconds);
                    Assert.That(count, Is.EqualTo(101000));
                    if (allocationsSupported)
                        Assert.That(eventBytes, Is.Zero, "Stable feedback subscriptions should not allocate per publication.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(settings); }
        }
    }
}
