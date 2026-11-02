using System.Collections.Generic;
using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Lightweight copy of the scored values needed by target selection.
    /// No Unity object references are stored in the selection policy.
    /// </summary>
    public readonly struct AirTargetSelectionCandidate
    {
        public readonly float Total;
        public readonly float Screen;
        public readonly bool IsCurrent;

        public AirTargetSelectionCandidate(float total, float screen, bool isCurrent)
        {
            Total = total;
            Screen = screen;
            IsCurrent = isCurrent;
        }
    }

    public readonly struct AirTargetSelectionRules
    {
        public readonly float MinimumScore;
        public readonly float CurrentTargetScoreGrace;
        public readonly float AimTakeoverScreenAdvantage;
        public readonly float AimTakeoverAllowedScoreDeficit;
        public readonly float SwitchScoreAdvantage;

        public AirTargetSelectionRules(float minimumScore, float currentTargetScoreGrace,
            float aimTakeoverScreenAdvantage, float aimTakeoverAllowedScoreDeficit,
            float switchScoreAdvantage)
        {
            MinimumScore = minimumScore;
            CurrentTargetScoreGrace = currentTargetScoreGrace;
            AimTakeoverScreenAdvantage = aimTakeoverScreenAdvantage;
            AimTakeoverAllowedScoreDeficit = aimTakeoverAllowedScoreDeficit;
            SwitchScoreAdvantage = switchScoreAdvantage;
        }
    }

    public enum AirTargetSelectionAction
    {
        KeepCurrent,
        SelectCandidate,
        ClearCurrent
    }

    public readonly struct AirTargetSelectionDecision
    {
        public readonly AirTargetSelectionAction Action;
        public readonly int CandidateIndex;
        public readonly float Score;

        public AirTargetSelectionDecision(
            AirTargetSelectionAction action, int candidateIndex, float score)
        {
            Action = action;
            CandidateIndex = candidateIndex;
            Score = score;
        }
    }

    /// <summary>
    /// Pure C# selection policy extracted verbatim in decision order from
    /// AirTargeter. It has no MonoBehaviour, transform, camera, scene event or
    /// collider dependency. Caller owns the sorted candidate list and applies
    /// selected-node visuals/events after the decision has been returned.
    /// </summary>
    public static class AirTargetSelectionPolicy
    {
        public static AirTargetSelectionDecision Choose(
            IReadOnlyList<AirTargetSelectionCandidate> candidates,
            bool hasCurrentTarget,
            AirTargetSelectionRules rules)
        {
            int bestTotal = -1;
            int mostCentered = -1;
            int current = -1;

            // Candidates arrive in exactly the legacy list's sorted order.
            // Strict > retains the original first-wins screen-score tie rule.
            for (int i = 0; i < candidates.Count; i++)
            {
                AirTargetSelectionCandidate candidate = candidates[i];

                if (candidate.Total >= rules.MinimumScore)
                {
                    if (bestTotal < 0)
                        bestTotal = i;

                    if (mostCentered < 0 ||
                        candidate.Screen > candidates[mostCentered].Screen)
                        mostCentered = i;
                }

                if (candidate.IsCurrent)
                    current = i;
            }

            if (hasCurrentTarget && current >= 0)
            {
                AirTargetSelectionCandidate currentCandidate = candidates[current];
                float graceMinimum = Mathf.Max(
                    0f, rules.MinimumScore - rules.CurrentTargetScoreGrace);

                if (currentCandidate.Total >= graceMinimum)
                {
                    if (mostCentered >= 0 && !candidates[mostCentered].IsCurrent)
                    {
                        AirTargetSelectionCandidate centered = candidates[mostCentered];
                        float screenAdvantage = centered.Screen - currentCandidate.Screen;
                        float totalDeficit = currentCandidate.Total - centered.Total;

                        bool deliberateAimTakeover =
                            screenAdvantage >= rules.AimTakeoverScreenAdvantage &&
                            totalDeficit <= rules.AimTakeoverAllowedScoreDeficit;

                        if (deliberateAimTakeover)
                            return new AirTargetSelectionDecision(
                                AirTargetSelectionAction.SelectCandidate,
                                mostCentered, centered.Total);
                    }

                    if (bestTotal < 0 || candidates[bestTotal].IsCurrent)
                        return new AirTargetSelectionDecision(
                            AirTargetSelectionAction.KeepCurrent, -1,
                            currentCandidate.Total);

                    if (candidates[bestTotal].Total <
                        currentCandidate.Total + rules.SwitchScoreAdvantage)
                        return new AirTargetSelectionDecision(
                            AirTargetSelectionAction.KeepCurrent, -1,
                            currentCandidate.Total);
                }
            }

            if (bestTotal >= 0)
                return new AirTargetSelectionDecision(
                    AirTargetSelectionAction.SelectCandidate,
                    bestTotal, candidates[bestTotal].Total);

            return new AirTargetSelectionDecision(
                AirTargetSelectionAction.ClearCurrent, -1, 0f);
        }
    }
}
