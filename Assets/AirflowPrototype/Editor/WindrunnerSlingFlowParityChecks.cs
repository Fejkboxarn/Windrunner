using System;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    /// <summary>
    /// Compares the new regular C# flow owner with the exact old ordering of
    /// direct _state, ChargeState, DashProgression and impact-hold mutations.
    /// No scene, prefab, asset or Play Mode mutation is required.
    /// </summary>
    public static class WindrunnerSlingFlowParityChecks
    {
        private const string MenuPath =
            "Tools/Windrunner/Production/Batch 5I/Run State Parity Checks";

        private sealed class LegacyReference
        {
            public AirSlingPhase Phase = AirSlingPhase.Idle;
            public readonly AirSlingChargeState Charge = new AirSlingChargeState();
            public readonly AirSlingDashProgression Dash = new AirSlingDashProgression();
            public float Hold;
            public Vector3 ImpactRoot;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            AirSlingSettings settings = null;
            try
            {
                if (typeof(MonoBehaviour).IsAssignableFrom(typeof(AirSlingFlowState)))
                    throw new InvalidOperationException(
                        "Flow state must remain a plain C# class, not a MonoBehaviour.");

                int checks = 0;
                int cases = 0;
                var random = new System.Random(5102026);

                // First verify the full charging-input truth table, including
                // simultaneous pressed/released and missing input/target.
                var inputModel = new AirSlingFlowState();
                for (int combination = 0; combination < 16; combination++)
                {
                    bool available = (combination & 1) != 0;
                    bool mouse = (combination & 2) != 0;
                    bool pressed = (combination & 4) != 0;
                    bool released = (combination & 8) != 0;
                    AirSlingChargeCommand expected = !available || !mouse
                        ? AirSlingChargeCommand.Cancel
                        : pressed
                            ? AirSlingChargeCommand.Accumulate
                            : AirSlingChargeCommand.Release;
                    AirSlingChargeCommand actual = inputModel.DecideCharging(
                        available, mouse, pressed, released);
                    if (actual != expected)
                        throw new InvalidOperationException(
                            "Input priority drift for combination " + combination +
                            ": expected " + expected + ", got " + actual);
                    checks++;
                }

                settings = ScriptableObject.CreateInstance<AirSlingSettings>();
                settings.dashSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

                // Deterministic seeded data lets this catch changes across
                // future batches while keeping each run reproducible.
                for (int attempt = 0; attempt < 2500; attempt++)
                {
                    var old = new LegacyReference();
                    var current = new AirSlingFlowState();
                    Compare(old, current, "initial", ref checks);

                    old.Phase = AirSlingPhase.Charging;
                    old.Charge.BeginCharge();
                    current.BeginCharge();
                    Compare(old, current, "begin charge", ref checks);

                    int chargeSteps = random.Next(1, 7);
                    for (int step = 0; step < chargeSteps; step++)
                    {
                        float delta = Range(random, 0f, 0.08f);
                        old.Charge.AdvanceCharge(delta);
                        current.Charge.AdvanceCharge(delta);
                        Compare(old, current, "advance charge", ref checks);
                    }

                    // Cancellation after charge, delay, dash and impact hold
                    // is exercised here even though live 5H cancellation has
                    // not yet been recorded by the user.
                    if (attempt % 13 == 0)
                    {
                        ResetLegacy(old);
                        current.Reset();
                        Compare(old, current, "cancel from charge", ref checks);
                        cases++;
                        continue;
                    }

                    float quality = attempt % 2 == 0
                        ? Range(random, 0.01f, 1f)
                        : 0f;
                    old.Charge.CapturePerfectQuality(quality);
                    current.Charge.CapturePerfectQuality(quality);

                    float delay = attempt % 9 == 0
                        ? 0f
                        : Range(random, 0.02f, 0.3f);
                    old.Charge.SetDelay(delay);
                    old.Phase = AirSlingPhase.DelayBeforeDash;
                    current.BeginDelayBeforeDash(delay);
                    Compare(old, current, "enter delay", ref checks);

                    int delaySteps = random.Next(0, 7);
                    for (int step = 0; step < delaySteps; step++)
                    {
                        float delta = Range(random, 0f, 0.07f);
                        old.Charge.AdvanceDelay(delta);
                        current.Charge.AdvanceDelay(delta);
                        Compare(old, current, "advance delay", ref checks);
                    }

                    if (attempt % 17 == 0)
                    {
                        ResetLegacy(old);
                        current.Reset();
                        Compare(old, current, "cancel from delay", ref checks);
                        cases++;
                        continue;
                    }

                    settings.dashStartSpeed = Range(random, 3f, 40f);
                    settings.dashMaximumSpeed = Range(random, 10f, 65f);
                    settings.dashInitialKickSpeed = Range(random, 0f, 14f);
                    settings.dashAcceleration = Range(random, 1f, 250f);
                    settings.dashArrivalAccelerationStart = Range(random, 0f, 1f);
                    settings.dashArrivalAccelerationMultiplier = Range(random, 0.25f, 5f);

                    float distance = Range(random, 0.1f, 100f);
                    old.Dash.SetPathDistance(distance);
                    current.Dash.SetPathDistance(distance);

                    old.Dash.Begin(settings);
                    old.Phase = AirSlingPhase.DashingToNode;
                    current.BeginDash(settings);
                    Compare(old, current, "begin dash", ref checks);

                    int dashSteps = random.Next(1, 10);
                    for (int step = 0; step < dashSteps; step++)
                    {
                        float delta = Range(random, 0f, 0.05f);
                        old.Dash.AdvanceElapsed(delta);
                        current.Dash.AdvanceElapsed(delta);
                        float oldNext = old.Dash.PreviewNextProgress(delta, settings);
                        float newNext = current.Dash.PreviewNextProgress(delta, settings);
                        Equal(oldNext, newNext, "dash preview", ref checks);
                        old.Dash.CommitProgress(oldNext);
                        current.Dash.CommitProgress(newNext);
                        Compare(old, current, "advance dash", ref checks);
                    }

                    if (attempt % 19 == 0)
                    {
                        ResetLegacy(old);
                        current.Reset();
                        Compare(old, current, "cancel from dash", ref checks);
                        cases++;
                        continue;
                    }

                    var impact = new Vector3(
                        Range(random, -100f, 100f),
                        Range(random, -20f, 50f),
                        Range(random, -100f, 100f));
                    old.ImpactRoot = impact;
                    current.CaptureImpactRoot(impact);

                    float holdDuration = attempt % 5 == 0 && attempt % 23 != 0
                        ? 0f
                        : Range(random, 0.001f, 0.25f);
                    old.Hold = Mathf.Max(0f, holdDuration);
                    current.SetImpactHold(holdDuration);
                    Compare(old, current, "impact capture", ref checks);

                    if (old.Hold > 0f)
                    {
                        old.Phase = AirSlingPhase.ImpactHold;
                        current.EnterImpactHold();
                        Compare(old, current, "enter impact hold", ref checks);

                        int holdSteps = random.Next(0, 7);
                        for (int step = 0; step < holdSteps; step++)
                        {
                            float delta = Range(random, 0f, 0.1f);
                            old.Hold -= delta;
                            bool oldDone = old.Hold <= 0f;
                            bool newDone = current.AdvanceImpactHold(delta);
                            if (oldDone != newDone)
                                throw new InvalidOperationException("Impact hold completion drift");
                            checks++;
                            Compare(old, current, "advance impact hold", ref checks);
                        }
                    }

                    if (attempt % 23 == 0)
                    {
                        ResetLegacy(old);
                        current.Reset();
                        Compare(old, current, "cancel from hold", ref checks);
                        cases++;
                        continue;
                    }

                    old.Phase = AirSlingPhase.Idle;
                    old.Dash.ClearAfterLaunch();
                    old.Charge.ClearDelay();
                    old.Hold = 0f;
                    current.CompleteLaunch();
                    Compare(old, current, "complete launch", ref checks);

                    // Captured quality must remain available for event emission
                    // AFTER the phase becomes Idle, just as in Batch 5G.
                    Equal(quality, current.Charge.CapturedPerfectQuality,
                        "quality retained after launch", ref checks);

                    ResetLegacy(old);
                    current.Reset();
                    Compare(old, current, "reset after launch", ref checks);
                    cases++;
                }

                Debug.Log("[Windrunner 5I] PASS — " + cases +
                          " deterministic sling phase scenarios, " + checks +
                          " state/value checks, all 16 input-priority combinations. " +
                          "State transitions, zero/positive holds, cancellations, " +
                          "dash substate and launch cleanup match 5G reference. " +
                          "This is model parity, NOT a live-scene or feel test.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[Windrunner 5I] FAIL — keep your Batch 5G backup, " +
                               "do not accept the migration until investigated.");
            }
            finally
            {
                if (settings != null)
                    UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private static void ResetLegacy(LegacyReference old)
        {
            old.Phase = AirSlingPhase.Idle;
            old.Charge.Reset();
            old.Dash.Reset();
            old.Hold = 0f;
            old.ImpactRoot = Vector3.zero;
        }

        private static void Compare(LegacyReference old, AirSlingFlowState current,
            string context, ref int checks)
        {
            if (old.Phase != current.Phase ||
                (old.Phase == AirSlingPhase.Charging) != current.IsCharging ||
                (old.Phase != AirSlingPhase.Idle) != current.IsSlinging ||
                (old.Phase == AirSlingPhase.DashingToNode) != current.IsDashingToNode)
                throw new InvalidOperationException("Sling phase flag drift: " + context);
            checks += 4;

            if ((old.Hold <= 0f) != current.ShouldLaunchImmediately)
                throw new InvalidOperationException("Immediate-launch decision drift: " + context);
            checks++;

            Equal(old.Hold, current.ImpactHoldRemaining, context + "/hold", ref checks);
            Equal(old.ImpactRoot.x, current.ImpactRootPosition.x, context + "/root X", ref checks);
            Equal(old.ImpactRoot.y, current.ImpactRootPosition.y, context + "/root Y", ref checks);
            Equal(old.ImpactRoot.z, current.ImpactRootPosition.z, context + "/root Z", ref checks);
            Equal(old.Charge.ChargeElapsed, current.Charge.ChargeElapsed,
                context + "/charge", ref checks);
            Equal(old.Charge.CapturedPerfectQuality, current.Charge.CapturedPerfectQuality,
                context + "/quality", ref checks);
            Equal(old.Charge.DelayRemaining, current.Charge.DelayRemaining,
                context + "/delay", ref checks);
            Equal(old.Dash.Speed, current.Dash.Speed, context + "/dash speed", ref checks);
            Equal(old.Dash.Elapsed, current.Dash.Elapsed, context + "/dash elapsed", ref checks);
            Equal(old.Dash.Progress, current.Dash.Progress, context + "/dash progress", ref checks);
            Equal(old.Dash.PathDistance, current.Dash.PathDistance,
                context + "/dash distance", ref checks);
        }

        private static void Equal(float old, float current, string context, ref int checks)
        {
            if (float.IsNaN(old) != float.IsNaN(current) ||
                (float.IsNaN(old) ? false : Mathf.Abs(old - current) > 0.000001f))
                throw new InvalidOperationException(context + " drift: expected=" +
                                                    old + ", actual=" + current);
            checks++;
        }

        private static float Range(System.Random rng, float min, float max) =>
            min + (float)rng.NextDouble() * (max - min);
    }
}
