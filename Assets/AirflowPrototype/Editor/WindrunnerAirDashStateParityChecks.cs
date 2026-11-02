using System;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    /// <summary>
    /// Compares the new plain-C# dash owner with an independent transcription
    /// of the old AirDash transition flow. Does not touch scene objects.
    /// </summary>
    public static class WindrunnerAirDashStateParityChecks
    {
        private const string MenuPath =
            "Tools/Windrunner/Production/Batch 5N/Run Dash State Parity";

        private sealed class LegacyState
        {
            public bool Dashing;
            public bool Latched;
            public int Starts;
            public int Stops;

            public void Reward(bool canStart)
            {
                if (canStart)
                    Latched = true;
            }

            public void Cancel()
            {
                Latched = false;
            }

            public void Disable()
            {
                Latched = false;
                SetDashing(false);
            }

            public bool Tick(bool configured, bool hasMove,
                bool held, bool canStart, bool spendResult)
            {
                if (!configured)
                {
                    Latched = false;
                    SetDashing(false);
                    return false;
                }

                if (Latched && !hasMove)
                    Latched = false;

                bool manual = held && hasMove;
                bool automatic = Latched && hasMove;
                bool wants = manual || automatic;

                if (!wants)
                {
                    SetDashing(false);
                    return false;
                }

                if (!Dashing && !canStart)
                {
                    Latched = false;
                    SetDashing(false);
                    return false;
                }

                if (!spendResult)
                    Latched = false;

                SetDashing(spendResult);
                return true;
            }

            private void SetDashing(bool value)
            {
                if (Dashing == value)
                    return;

                Dashing = value;
                if (value) Starts++;
                else Stops++;
            }
        }

        private sealed class ExtractedState
        {
            public readonly AirDashSession Session = new AirDashSession();
            public int Starts;
            public int Stops;

            public void Reward(bool canStart)
            {
                Session.TryLatchReward(canStart);
            }

            public void Cancel()
            {
                Session.CancelAutoDash();
            }

            public void Disable()
            {
                Session.CancelAutoDash();
                SetDashing(false);
            }

            public bool Tick(bool configured, bool hasMove,
                bool held, bool canStart, bool spendResult)
            {
                if (!configured)
                {
                    Session.CancelAutoDash();
                    SetDashing(false);
                    return false;
                }

                if (!Session.WantsDash(hasMove, held))
                {
                    SetDashing(false);
                    return false;
                }

                if (!Session.IsDashing && !Session.CanBeginDash(canStart))
                {
                    SetDashing(false);
                    return false;
                }

                Session.AfterSpend(spendResult);
                SetDashing(spendResult);
                return true;
            }

            private void SetDashing(bool value)
            {
                if (!Session.SetDashing(value))
                    return;

                if (value) Starts++;
                else Stops++;
            }
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            try
            {
                if (typeof(MonoBehaviour).IsAssignableFrom(typeof(AirDashSession)))
                    throw new InvalidOperationException("AirDashSession must remain a normal C# class.");

                var random = new System.Random(20261102);
                int ticks = 0;
                int rewardAttempts = 0;
                int manualCancels = 0;
                int disableCalls = 0;
                int attemptedSpends = 0;
                int starts = 0;
                int stops = 0;

                // Independent reset per sequence exercises re-entering a level,
                // missing dependencies, reward loops and rapid input changes.
                for (int sequence = 0; sequence < 25000; sequence++)
                {
                    var old = new LegacyState();
                    var current = new ExtractedState();

                    for (int step = 0; step < 48; step++)
                    {
                        bool canStart = random.Next(0, 9) != 0;
                        if (random.Next(0, 9) == 0)
                        {
                            old.Reward(canStart);
                            current.Reward(canStart);
                            rewardAttempts++;
                        }

                        if (random.Next(0, 31) == 0)
                        {
                            old.Cancel();
                            current.Cancel();
                            manualCancels++;
                        }

                        if (random.Next(0, 73) == 0)
                        {
                            old.Disable();
                            current.Disable();
                            disableCalls++;
                        }

                        bool configured = random.Next(0, 41) != 0;
                        bool move = random.Next(0, 5) != 0;
                        bool held = random.Next(0, 3) != 0;
                        bool stillHasPower = random.Next(0, 19) != 0;

                        bool legacySpend = old.Tick(configured, move, held,
                            canStart, stillHasPower);
                        bool extractedSpend = current.Tick(configured, move, held,
                            canStart, stillHasPower);

                        if (legacySpend != extractedSpend ||
                            old.Dashing != current.Session.IsDashing ||
                            old.Latched != current.Session.IsAutoDashLatched ||
                            old.Starts != current.Starts || old.Stops != current.Stops)
                        {
                            throw new InvalidOperationException(
                                "Mismatch at sequence=" + sequence + ", step=" + step +
                                ", configured=" + configured + ", move=" + move +
                                ", held=" + held + ", power=" + canStart +
                                ", spendSuccess=" + stillHasPower +
                                ", spend old/new=" + legacySpend + "/" + extractedSpend +
                                ", dashing old/new=" + old.Dashing + "/" +
                                current.Session.IsDashing + ", latched old/new=" +
                                old.Latched + "/" + current.Session.IsAutoDashLatched);
                        }

                        ticks++;
                        if (legacySpend) attemptedSpends++;
                    }

                    starts += old.Starts;
                    stops += old.Stops;
                }

                if (attemptedSpends == 0 || starts == 0 || stops == 0 ||
                    rewardAttempts == 0 || manualCancels == 0 || disableCalls == 0)
                    throw new InvalidOperationException("Incomplete test coverage.");

                Debug.Log("[Windrunner 5N] PASS — dash ticks=" + ticks +
                          ", attempted spends=" + attemptedSpends +
                          ", starts=" + starts + ", stops=" + stops +
                          ", rewards=" + rewardAttempts +
                          ", manual cancels=" + manualCancels +
                          ", disables=" + disableCalls +
                          ". Legacy and extracted sessions agree on dash, latch, " +
                          "power-spend decisions and transitions. No scene changes. " +
                          "Manual traversal/feel checks are still required.");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Debug.LogError("[Windrunner 5N] FAIL — stop, send the Console output and " +
                               "restore the pre-5N AirDash if already imported.");
            }
        }
    }
}
