using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class WindrunnerAbilityCompositionChecks
    {
        private sealed class Budget : IAirPowerBudget
        {
            public readonly AirPowerState State = new AirPowerState();
            public readonly List<string> Events = new List<string>();
            public int Spends;
            public float Current => State.Current;
            public bool CanStartDash => !State.IsDepletedLocked && Current > 0.001f;
            public Budget()
            {
                State.Initialize(100f);
                State.Changed += (value, max) => Events.Add("changed");
                State.Depleted += () => Events.Add("depleted");
                State.RecoveredFromDepletion += () => Events.Add("recovered");
            }
            public bool SpendAndHasRemaining(float amount) { Spends++; return State.Spend(amount, 1f, 100f); }
        }
        public static void Run()
        {
            var power = new Budget();
            Require(!AbilityResourcePolicy.TryCommitSling(power, true, true, 101f) && power.Spends == 0, "Reject before spending");
            Require(AbilityResourcePolicy.TryCommitSling(power, true, true, 100f), "Exact depletion is a successful payment");
            Require(power.Current == 0f && string.Join(",", power.Events) == "changed,depleted", "Resource event order");
            Require(!AbilityResourcePolicy.TryCommitSling(null, true, true, 1f), "Required resource missing");
            Require(AbilityResourcePolicy.TryCommitSling(null, true, false, 1f), "Optional resource missing");
            Require(AbilityResourcePolicy.TryCommitSling(null, false, true, 1f), "Free ability");
            var dash = new AirDashSession();
            Require(!dash.EvaluateTick(true, true, 1f, power), "Depleted dash blocked");
            power.State.Restore(20f, 100f, 0.16f);
            dash.TryLatchReward(power.CanStartDash);
            bool active = dash.EvaluateTick(true, false, 1f, power);
            Require(active && dash.IsAutoDashLatched, "Reward reactivates dash");
            dash.SetDashing(active);
            Require(!dash.EvaluateTick(false, false, 1f, power) && !dash.IsAutoDashLatched, "Movement interruption clears latch");
            dash.SetDashing(false);
            var random = new System.Random(20261113);
            var rules = new AirCastReleaseRules(0.05f, 0.3f, 0.09f, 0.6f, 0.2f, 1f);
            for (int i = 0; i < 20000; i++)
            {
                var target = new object();
                var cast = new AirCastSession<object>();
                cast.Begin(target);
                float elapsed = (float)random.NextDouble();
                Require(cast.Advance(elapsed, true), "Target remains sticky");
                var expected = AirCastReleasePolicy.Evaluate(elapsed, elapsed, true, true, rules);
                var actual = cast.Release(true, true, rules);
                Require(actual.Cancel == expected.Cancel && actual.Perfect == expected.Perfect &&
                    actual.Ready == expected.Ready && actual.PerfectQuality == expected.PerfectQuality &&
                    actual.Charge01 == expected.Charge01, "Cast session release parity");
                Require(!cast.IsCharging && cast.ChargeTime == 0f && ReferenceEquals(cast.Target, target), "Release observer state");
                cast.ResetTargetLock();
                Require(cast.Target == null && cast.TargetLockTime == 0f, "Release cleanup");
                cast.Begin(target);
                Require(!cast.Advance(0.1f, false) && cast.Cancel() && !cast.Cancel(), "Lost-target cancellation is single edge");
            }
            Debug.Log("[Windrunner Ability Composition] PASS — 20000 cast sessions; payment/depletion/event order; dash reward reactivation/interruption.");
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
