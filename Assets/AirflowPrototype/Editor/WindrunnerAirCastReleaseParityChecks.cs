using System;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    /// <summary>
    /// Verifies the pure release policy against the pre-5M AirCaster release
    /// arithmetic. No scene, assets, or input bindings are modified.
    /// </summary>
    public static class WindrunnerAirCastReleaseParityChecks
    {
        private const string MenuPath =
            "Tools/Windrunner/Production/Batch 5M/Run Air Cast Release Parity";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            try
            {
                if (typeof(MonoBehaviour).IsAssignableFrom(typeof(AirCastReleasePolicy)))
                    throw new InvalidOperationException("Release policy must remain plain C#.");

                int compared = 0;
                int canceled = 0;
                int perfect = 0;
                int normal = 0;
                int ready = 0;

                var random = new System.Random(11022026);
                for (int i = 0; i < 100000; i++)
                {
                    float minimum = Range(random, 0f, 0.4f);
                    float timing = Range(random, 0.05f, 0.8f);
                    float leeway = Range(random, 0.001f, 0.28f);
                    float full = Range(random, 0.2f, 1.2f);

                    var rules = new AirCastReleaseRules(
                        minimum,
                        timing,
                        leeway,
                        full,
                        Range(random, 0f, 1f),
                        Range(random, 0f, 1f));

                    float charge = Range(random, 0f, 1.6f);
                    float locked = Range(random, 0f, 1.6f);
                    bool hasTarget = random.Next(0, 7) != 0;
                    bool sameTarget = hasTarget && random.Next(0, 5) != 0;

                    Check(charge, locked, hasTarget, sameTarget, rules,
                        "random case " + i, ref compared, ref canceled,
                        ref perfect, ref normal, ref ready);
                }

                var edgeRules = new AirCastReleaseRules(
                    0.07f, 0.30f, 0.09f, 0.60f, 0.25f, 1f);
                Check(0f, 0f, false, false, edgeRules, "idle tap",
                    ref compared, ref canceled, ref perfect, ref normal, ref ready);
                Check(0.0699f, 0.0699f, true, true, edgeRules, "early cancel",
                    ref compared, ref canceled, ref perfect, ref normal, ref ready);
                Check(0.07f, 0.07f, true, true, edgeRules, "minimum fire time",
                    ref compared, ref canceled, ref perfect, ref normal, ref ready);
                Check(0.3f, 0.3f, false, false, edgeRules, "lost target",
                    ref compared, ref canceled, ref perfect, ref normal, ref ready);
                Check(0.3f, 0.3f, true, false, edgeRules, "changed target",
                    ref compared, ref canceled, ref perfect, ref normal, ref ready);
                Check(0.3f, 0.3f, true, true, edgeRules, "perfect center",
                    ref compared, ref canceled, ref perfect, ref normal, ref ready);
                Check(0.21f, 0.21f, true, true, edgeRules, "perfect start edge",
                    ref compared, ref canceled, ref perfect, ref normal, ref ready);
                Check(0.39f, 0.39f, true, true, edgeRules, "perfect end edge",
                    ref compared, ref canceled, ref perfect, ref normal, ref ready);
                Check(0.4f, 0.4f, true, true, edgeRules, "past perfect",
                    ref compared, ref canceled, ref perfect, ref normal, ref ready);
                Check(1.6f, 0f, true, true, edgeRules, "no lock",
                    ref compared, ref canceled, ref perfect, ref normal, ref ready);
                Check(1.6f, 1.6f, true, true, edgeRules, "overcharged",
                    ref compared, ref canceled, ref perfect, ref normal, ref ready);
                Check(0.5f, 0.2f, true, true,
                    new AirCastReleaseRules(0.07f, 0.3f, 0.09f, 0f, 0f, 1f),
                    "zero full timing", ref compared, ref canceled,
                    ref perfect, ref normal, ref ready);

                if (canceled == 0 || perfect == 0 || normal == 0 || ready == 0)
                    throw new InvalidOperationException("Coverage incomplete: one or more outcomes never occurred.");

                Debug.Log("[Windrunner 5M] PASS — legacy-vs-policy release cases=" +
                          compared + ", canceled=" + canceled + ", normal=" + normal +
                          ", perfect=" + perfect + ", ready=" + ready +
                          ". Includes target loss, target switching, perfect boundaries, " +
                          "minimum-fire cancellation, aim assist and overcharge. " +
                          "No scene objects were changed. Manual gameplay testing is still required.");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Debug.LogError("[Windrunner 5M] FAIL — stop and send the full Console output. " +
                               "Restore the pre-5M AirCaster if already imported.");
            }
        }

        private static void Check(
            float charge, float locked, bool hasTarget, bool sameTarget,
            AirCastReleaseRules rules, string label,
            ref int compared, ref int canceled, ref int perfect,
            ref int normal, ref int ready)
        {
            AirCastReleaseDecision actual = AirCastReleasePolicy.Evaluate(
                charge, locked, hasTarget, sameTarget, rules);
            AirCastReleaseDecision expected = LegacyEvaluate(
                charge, locked, hasTarget, sameTarget, rules);

            if (actual.Cancel != expected.Cancel || actual.Perfect != expected.Perfect ||
                actual.Ready != expected.Ready ||
                !Equal(actual.PerfectQuality, expected.PerfectQuality) ||
                !Equal(actual.Charge01, expected.Charge01) ||
                (!actual.Cancel && !Equal(actual.AimAssist, expected.AimAssist)))
            {
                throw new InvalidOperationException("Mismatch in " + label +
                    " (charge=" + charge + ", lock=" + locked +
                    ", target=" + hasTarget + ", sticky=" + sameTarget +
                    ") expected: cancel=" + expected.Cancel +
                    ", perfect=" + expected.Perfect + ", ready=" + expected.Ready +
                    ", quality=" + expected.PerfectQuality + ", charge01=" +
                    expected.Charge01 + ", aim=" + expected.AimAssist +
                    "; actual: cancel=" + actual.Cancel + ", perfect=" +
                    actual.Perfect + ", ready=" + actual.Ready +
                    ", quality=" + actual.PerfectQuality + ", charge01=" +
                    actual.Charge01 + ", aim=" + actual.AimAssist);
            }

            compared++;
            if (actual.Cancel) canceled++;
            else if (actual.Perfect) perfect++;
            else normal++;
            if (!actual.Cancel && actual.Ready) ready++;
        }

        // Independent transcription of the pre-5M AirCaster.Release method,
        // intentionally retaining the original operation order and clamps.
        private static AirCastReleaseDecision LegacyEvaluate(
            float charge, float locked, bool hasTarget, bool sameTarget,
            AirCastReleaseRules rules)
        {
            float charge01 = rules.FullTimingTime > 0f
                ? Mathf.Clamp01(charge / rules.FullTimingTime)
                : 0f;

            float effective = sameTarget ? Mathf.Min(charge, locked) : 0f;
            float quality = 0f;
            if (sameTarget)
            {
                float distance = Mathf.Abs(effective - rules.PerfectTime);
                if (distance <= rules.PerfectLeeway)
                    quality = 1f - Mathf.Clamp01(
                        distance / Mathf.Max(0.001f, rules.PerfectLeeway));
            }

            bool isPerfect = quality > 0f;
            bool normallyReady = sameTarget && charge >= rules.PerfectTime &&
                                 locked >= rules.PerfectTime;
            bool isReady = normallyReady || isPerfect;
            bool isCanceled = charge < rules.MinimumFireTime || !hasTarget;

            float accuracy = 0f;
            if (!isCanceled)
            {
                if (isPerfect)
                {
                    accuracy = rules.ReadyAimAssist;
                }
                else
                {
                    float air = Mathf.InverseLerp(
                        rules.MinimumFireTime, rules.PerfectTime, charge);
                    float target = Mathf.InverseLerp(
                        0f, rules.PerfectTime, sameTarget ? locked : 0f);
                    float alpha = Mathf.Min(air, target);
                    accuracy = Mathf.Lerp(
                        rules.MinimumAimAssist, rules.ReadyAimAssist, alpha);
                }
            }

            return new AirCastReleaseDecision(
                isCanceled, isPerfect, isReady, quality, charge01, accuracy);
        }

        private static bool Equal(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.000001f;
        }

        private static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }
    }
}
