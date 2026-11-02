using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    /// <summary>
    /// Deterministic reference-model comparison of AirTargeter selection policy.
    /// No scene objects are created or changed.
    /// </summary>
    public static class WindrunnerAirTargetSelectionParityChecks
    {
        private const string MenuPath =
            "Tools/Windrunner/Production/Batch 5K/Run Air Node Selection Parity";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            try
            {
                if (typeof(MonoBehaviour).IsAssignableFrom(typeof(AirTargetSelectionPolicy)))
                    throw new InvalidOperationException("Policy must be plain C#.");

                var random = new System.Random(5122026);
                var candidates = new List<AirTargetSelectionCandidate>(16);
                int[] actionCounts = new int[3];
                int scenarios = 0;

                for (int attempt = 0; attempt < 60000; attempt++)
                {
                    candidates.Clear();
                    int count = random.Next(0, 15);
                    int currentIndex = random.Next(0, count + 2) - 1;
                    bool hasCurrent = random.Next(0, 4) != 0;

                    for (int i = 0; i < count; i++)
                    {
                        float total = attempt % 17 == 0
                            ? 0.5f
                            : Range(random, -0.2f, 1.2f);
                        float screen = attempt % 23 == 0
                            ? 0.5f
                            : Range(random, 0f, 1f);
                        candidates.Add(new AirTargetSelectionCandidate(
                            total, screen, i == currentIndex));
                    }

                    // The runtime list is sorted before the decision. Preserve
                    // its strict b.Total.CompareTo(a.Total) tie semantics.
                    candidates.Sort((a, b) => b.Total.CompareTo(a.Total));

                    var rules = new AirTargetSelectionRules(
                        Range(random, 0f, 1.1f),
                        Range(random, 0f, 0.7f),
                        Range(random, 0f, 0.7f),
                        Range(random, -0.3f, 0.8f),
                        Range(random, 0f, 0.6f));

                    AirTargetSelectionDecision expected = LegacyChoose(
                        candidates, hasCurrent, rules);
                    AirTargetSelectionDecision actual = AirTargetSelectionPolicy.Choose(
                        candidates, hasCurrent, rules);

                    Verify(expected, actual, "seeded case=" + attempt);
                    actionCounts[(int)actual.Action]++;
                    scenarios++;
                }

                // Guarantee specific edge cases are covered, independent of
                // distribution of the seeded randomized candidate sets.
                RunEdges(candidates, ref scenarios, actionCounts);

                for (int action = 0; action < actionCounts.Length; action++)
                {
                    if (actionCounts[action] == 0)
                        throw new InvalidOperationException(
                            "No scenarios exercised action " + (AirTargetSelectionAction)action);
                }

                Debug.Log("[Windrunner 5K] PASS — " + scenarios +
                          " legacy-vs-new selection decisions. " +
                          "Keep=" + actionCounts[(int)AirTargetSelectionAction.KeepCurrent] +
                          ", Select=" + actionCounts[(int)AirTargetSelectionAction.SelectCandidate] +
                          ", Clear=" + actionCounts[(int)AirTargetSelectionAction.ClearCurrent] +
                          ". Tested score grace, hysteresis, deliberate aim takeover, " +
                          "ties and missing current targets. " +
                          "No scene objects were changed. This does not validate gameplay feel.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[Windrunner 5K] FAIL — restore the previous AirTargeter " +
                               "if this change is already imported, and send the full error.");
            }
        }

        private static void RunEdges(List<AirTargetSelectionCandidate> candidates,
            ref int scenarios, int[] actions)
        {
            var rules = new AirTargetSelectionRules(0.4f, 0.15f, 0.15f, 0.2f, 0.1f);

            candidates.Clear();
            CheckEdge(candidates, false, rules, "empty/no current", ref scenarios, actions);
            CheckEdge(candidates, true, rules, "empty/stale current", ref scenarios, actions);

            candidates.Add(new AirTargetSelectionCandidate(0.8f, 0.7f, false));
            CheckEdge(candidates, false, rules, "first target", ref scenarios, actions);

            candidates.Clear();
            candidates.Add(new AirTargetSelectionCandidate(0.65f, 0.15f, true));
            candidates.Add(new AirTargetSelectionCandidate(0.8f, 0.6f, false));
            CheckEdge(candidates, true, rules, "explicit aim takeover", ref scenarios, actions);

            candidates.Clear();
            candidates.Add(new AirTargetSelectionCandidate(0.72f, 0.65f, false));
            candidates.Add(new AirTargetSelectionCandidate(0.66f, 0.62f, true));
            CheckEdge(candidates, true, rules, "hysteresis keep", ref scenarios, actions);

            candidates.Clear();
            candidates.Add(new AirTargetSelectionCandidate(0.8f, 0.2f, false));
            candidates.Add(new AirTargetSelectionCandidate(0.62f, 0.25f, true));
            CheckEdge(candidates, true, rules, "higher total takes over", ref scenarios, actions);

            candidates.Clear();
            candidates.Add(new AirTargetSelectionCandidate(0.39f, 0.6f, true));
            CheckEdge(candidates, true, rules, "within score grace", ref scenarios, actions);

            candidates.Clear();
            candidates.Add(new AirTargetSelectionCandidate(0.2f, 0.6f, true));
            CheckEdge(candidates, true, rules, "below score grace", ref scenarios, actions);
        }

        private static void CheckEdge(List<AirTargetSelectionCandidate> candidates,
            bool hasCurrent, AirTargetSelectionRules rules, string label,
            ref int scenarios, int[] actions)
        {
            AirTargetSelectionDecision expected = LegacyChoose(candidates, hasCurrent, rules);
            AirTargetSelectionDecision actual = AirTargetSelectionPolicy.Choose(
                candidates, hasCurrent, rules);
            Verify(expected, actual, label);
            actions[(int)actual.Action]++;
            scenarios++;
        }

        // Independent index-based transcription of the pre-5K AirTargeter
        // flow: best total, most centered, current; grace; aim takeover;
        // hysteresis; best/clear. The runtime solver uses a different shape.
        private static AirTargetSelectionDecision LegacyChoose(
            IReadOnlyList<AirTargetSelectionCandidate> candidates,
            bool hasCurrent, AirTargetSelectionRules rules)
        {
            int? bestTotal = null;
            int? mostCentered = null;
            int? current = null;

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.Total >= rules.MinimumScore)
                {
                    if (!bestTotal.HasValue)
                        bestTotal = i;
                    if (!mostCentered.HasValue ||
                        candidate.Screen > candidates[mostCentered.Value].Screen)
                        mostCentered = i;
                }

                if (candidate.IsCurrent)
                    current = i;
            }

            if (hasCurrent && current.HasValue)
            {
                float minimum = Mathf.Max(
                    0f, rules.MinimumScore - rules.CurrentTargetScoreGrace);
                var existing = candidates[current.Value];

                if (existing.Total >= minimum)
                {
                    if (mostCentered.HasValue &&
                        !candidates[mostCentered.Value].IsCurrent)
                    {
                        var aim = candidates[mostCentered.Value];
                        float screenAdvantage = aim.Screen - existing.Screen;
                        float scoreDeficit = existing.Total - aim.Total;
                        if (screenAdvantage >= rules.AimTakeoverScreenAdvantage &&
                            scoreDeficit <= rules.AimTakeoverAllowedScoreDeficit)
                            return new AirTargetSelectionDecision(
                                AirTargetSelectionAction.SelectCandidate,
                                mostCentered.Value, aim.Total);
                    }

                    if (!bestTotal.HasValue || candidates[bestTotal.Value].IsCurrent)
                        return new AirTargetSelectionDecision(
                            AirTargetSelectionAction.KeepCurrent, -1, existing.Total);

                    if (candidates[bestTotal.Value].Total <
                        existing.Total + rules.SwitchScoreAdvantage)
                        return new AirTargetSelectionDecision(
                            AirTargetSelectionAction.KeepCurrent, -1, existing.Total);
                }
            }

            if (bestTotal.HasValue)
                return new AirTargetSelectionDecision(
                    AirTargetSelectionAction.SelectCandidate,
                    bestTotal.Value, candidates[bestTotal.Value].Total);

            return new AirTargetSelectionDecision(
                AirTargetSelectionAction.ClearCurrent, -1, 0f);
        }

        private static void Verify(AirTargetSelectionDecision expected,
            AirTargetSelectionDecision actual, string label)
        {
            if (expected.Action != actual.Action ||
                expected.CandidateIndex != actual.CandidateIndex ||
                Mathf.Abs(expected.Score - actual.Score) > 0.000001f)
                throw new InvalidOperationException(label + " differs: old=" +
                    expected.Action + " index=" + expected.CandidateIndex +
                    " score=" + expected.Score + ", new=" + actual.Action +
                    " index=" + actual.CandidateIndex + " score=" + actual.Score);
        }

        private static float Range(System.Random random, float min, float max) =>
            min + (float)random.NextDouble() * (max - min);
    }
}
