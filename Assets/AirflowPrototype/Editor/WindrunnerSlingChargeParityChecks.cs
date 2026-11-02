using System;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class WindrunnerSlingChargeParityChecks
    {
        [MenuItem("Tools/Windrunner/Production/Batch 5F/1. Run Charge Timing Parity Checks")]
        public static void Run()
        {
            try
            {
                var random = new System.Random(551906);
                var state = new AirSlingChargeState();
                int comparisons = 0;

                for (int i = 0; i < 5000; i++)
                {
                    float fullTime = i % 19 == 0 ? 0f : 0.05f + Next(random) * 1.5f;
                    float center = i % 23 == 0 ? 0f : Next(random) * 1.5f;
                    float leeway = i % 29 == 0 ? 0f : 0.01f + Next(random) * 0.25f;
                    float elapsed = Next(random) * 2f;
                    if (i % 11 == 0) elapsed = center;
                    if (i % 13 == 0) elapsed = center + leeway;
                    if (i % 17 == 0) elapsed = center - leeway;

                    state.Reset();
                    state.BeginCharge();
                    state.AdvanceCharge(elapsed);

                    float oldQuality = LegacyQuality(elapsed, center, leeway);
                    Equal("quality", i, oldQuality, state.EvaluatePerfectQuality(center, leeway));
                    Equal("progress", i,
                        Mathf.Clamp01(elapsed / Mathf.Max(0.05f, fullTime)),
                        state.Progress01(fullTime));
                    Equal("center", i,
                        fullTime > 0f ? Mathf.Clamp01(center / fullTime) : 0.5f,
                        AirSlingChargeState.PerfectCenter01(fullTime, center));
                    Equal("half-width", i,
                        fullTime > 0f ? Mathf.Clamp(leeway / fullTime, 0.001f, 0.49f) : 0.15f,
                        AirSlingChargeState.PerfectHalfWidth01(fullTime, leeway));
                    comparisons += 4;

                    bool acceptedPerfect = oldQuality > 0f;
                    float captured = acceptedPerfect ? oldQuality : 0f;
                    state.CapturePerfectQuality(captured);
                    Equal("captured quality", i, captured, state.CapturedPerfectQuality);
                    comparisons++;

                    float delay = i % 31 == 0 ? 0f : Next(random) * 0.8f;
                    float dt = Next(random) * 0.6f;
                    state.SetDelay(delay);
                    state.AdvanceDelay(dt);
                    Equal("pre-dash delay", i, delay - dt, state.DelayRemaining);
                    comparisons++;

                    state.ClearDelay();
                    Equal("clear delay", i, 0f, state.DelayRemaining);
                    Equal("retain captured quality through launch", i, captured,
                        state.CapturedPerfectQuality);
                    comparisons += 2;

                    state.Reset();
                    Equal("reset elapsed", i, 0f, state.ChargeElapsed);
                    Equal("reset quality", i, 0f, state.CapturedPerfectQuality);
                    Equal("reset delay", i, 0f, state.DelayRemaining);
                    comparisons += 3;
                }

                // Exact endpoint behavior: quality == 0 does not count as perfect.
                state.BeginCharge();
                state.AdvanceCharge(0.3f);
                Equal("perfect center peak", -1, 1f, state.EvaluatePerfectQuality(0.3f, 0.09f));
                state.BeginCharge();
                state.AdvanceCharge(0.3f + 0.09f);
                Equal("perfect outer edge rejected", -1, 0f,
                    state.EvaluatePerfectQuality(0.3f, 0.09f));
                comparisons += 2;

                Debug.Log("[Windrunner 5F] PASS — " + comparisons +
                    " legacy-vs-extracted timing comparisons. Charge window edges, " +
                    "perfect quality, progress UI, captured quality, pre-dash delay and reset verified. " +
                    "The gameplay event order still requires Play Mode testing.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[Windrunner 5F] FAIL — Stop before gameplay tuning or more migration.");
            }
        }

        private static float LegacyQuality(float elapsed, float perfectTime, float leeway)
        {
            float distance = Mathf.Abs(elapsed - perfectTime);
            if (distance > leeway)
                return 0f;
            return 1f - Mathf.Clamp01(distance / Mathf.Max(0.001f, leeway));
        }

        private static float Next(System.Random random) => (float)random.NextDouble();

        private static void Equal(string label, int caseNumber, float expected, float actual)
        {
            if (float.IsNaN(actual) || Mathf.Abs(expected - actual) > 0.00001f)
                throw new InvalidOperationException(label + " mismatch at case " + caseNumber +
                    ": legacy=" + expected + " extracted=" + actual);
        }
    }
}
