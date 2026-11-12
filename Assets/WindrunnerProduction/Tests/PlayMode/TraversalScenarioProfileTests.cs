using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace Windrunner.Production.Tests
{
    // Runs the same real component scenarios as regression tests. Results include
    // assertions, reflection, Editor and fixture costs; they are not shipping budgets.
    [Category("Profiling")]
    public sealed class TraversalScenarioProfileTests
    {
        [Serializable] private sealed class Result
        {
            public string scenario, utc, cpu, gpu, unity;
            public int samples;
            public double medianWallMs, p95WallMs, medianMainThreadMs, p95MainThreadMs;
            public long medianGcBytes, p95GcBytes, peakTotalUsedBytes;
        }
        private AbilityExecutionTests fixture;

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (fixture != null) { yield return fixture.Teardown(); fixture = null; }
        }

        [UnityTest] public IEnumerator TraversalAndLifecycleMeasurements()
        {
            var lifecycle = new GameplayLifecycleTests();
            yield return Measure("movement-jump-reversal-pause", lifecycle.MotorMovesJumpsAndStopsIntegrationWhilePaused());
            yield return Measure("live-animation-actions-restoration", lifecycle.GameplaySceneStartsAndPlayerRemainsFinite());
            yield return Measure("scene-reload-ui-handoff", lifecycle.GameplayReloadKeepsOneServiceOwnerAndRebindsSceneUi());
            for (int scenario = 0; scenario < 5; scenario++)
            {
                fixture = new AbilityExecutionTests(); yield return fixture.Setup();
                string name; IEnumerator execution;
                switch (scenario)
                {
                    case 0: name = "sling-charge-perfect-traverse-impact-launch"; execution = fixture.SlingPerfectInputChargeCurveImpactLaunchPreservesEventOrder(); break;
                    case 1: name = "sling-cancel-pause-normal-release"; execution = fixture.SlingCancellationPauseAndNormalReleaseRecoverCleanly(); break;
                    case 2: name = "dash-deplete-reward-reactivate-interrupt"; execution = fixture.DashDepletionRewardReactivationPauseAndInterruption(); break;
                    case 3: name = "cast-perfect-projectile-reward"; execution = fixture.CastPerfectReleaseProjectileAndRewardPreserveOrder(); break;
                    default: name = "water-swim-pause-exit-recovery"; execution = fixture.SwimmingPausesExitsAndRecoversThroughWaterRemoval(); break;
                }
                yield return Measure(name, execution);
                yield return fixture.Teardown(); fixture = null;
            }
        }

        private static IEnumerator Measure(string name, IEnumerator execution)
        {
            const int capacity = 8192;
            var wall = new double[capacity]; var cpu = new double[capacity]; var bytes = new long[capacity];
            var stack = new Stack<IEnumerator>(); stack.Push(execution);
            var result = new Result { scenario = name, utc = DateTime.UtcNow.ToString("O"),
                cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName, unity = Application.unityVersion };
            using (var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 4))
            using (var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 4))
            using (var memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory", 4))
            {
                Assert.That(gc.Valid && main.Valid && memory.Valid, Is.True, "Required native counters unavailable.");
                var clock = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    while (stack.Count > 0)
                    {
                        var top = stack.Peek();
                        if (!top.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                        if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                        yield return top.Current;
                        int i = result.samples++;
                        Assert.That(i, Is.LessThan(capacity), "Profile sample capacity exceeded.");
                        wall[i] = clock.Elapsed.TotalMilliseconds; clock.Restart();
                        cpu[i] = main.LastValue / 1000000.0; bytes[i] = gc.LastValue;
                        result.peakTotalUsedBytes = Math.Max(result.peakTotalUsedBytes, memory.LastValue);
                    }
                }
                finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
            }
            Assert.That(result.samples, Is.GreaterThan(0));
            Array.Sort(wall, 0, result.samples); Array.Sort(cpu, 0, result.samples); Array.Sort(bytes, 0, result.samples);
            int mid = result.samples / 2, high = (int)((result.samples - 1) * .95);
            result.medianWallMs = wall[mid]; result.p95WallMs = wall[high];
            result.medianMainThreadMs = cpu[mid]; result.p95MainThreadMs = cpu[high];
            result.medianGcBytes = bytes[mid]; result.p95GcBytes = bytes[high];
            File.WriteAllText("Logs/ProductionMigration/rendered-profile-" + name + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json", JsonUtility.ToJson(result, true));
        }
    }
}
