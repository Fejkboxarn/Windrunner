using System;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class WindrunnerAirPowerStateChecks
    {
        [MenuItem("Tools/Windrunner/Production/Batch 5B/3. Check Air Power Rules")]
        public static void Run()
        {
            try
            {
                var state = new AirPowerState();
                int changed = 0;
                int depleted = 0;
                int recovered = 0;
                float lastMax = -1f;
                state.Changed += (_, max) => { changed++; lastMax = max; };
                state.Depleted += () => depleted++;
                state.RecoveredFromDepletion += () => recovered++;

                state.Initialize(100f);
                Expect("initial fill", Approximately(state.Current, 100f) && changed == 0);

                Expect("valid spend", state.Spend(40f, 2f, 100f));
                Expect("spend event", Approximately(state.Current, 60f) && changed == 1 &&
                                      Approximately(lastMax, 100f));

                Expect("overspend reports depleted", !state.Spend(100f, 3f, 100f));
                Expect("depletion lockout", state.IsDepletedLocked && depleted == 1 &&
                                           Approximately(state.Current, 0f));

                state.Restore(10f, 100f, 0.25f);
                Expect("below restart threshold", state.IsDepletedLocked && recovered == 0);

                state.Restore(15f, 100f, 0.25f);
                Expect("restart threshold", !state.IsDepletedLocked && recovered == 1 &&
                                            Approximately(state.Current, 25f));

                state.Tick(3.5f, 1f, 100f, true, 1f, 20f, 0.25f);
                Expect("passive recovery delay", Approximately(state.Current, 25f));

                state.Tick(4.1f, 1f, 100f, true, 1f, 20f, 0.25f);
                Expect("passive recovery", Approximately(state.Current, 45f));

                state.Refill(100f);
                Expect("refill", Approximately(state.Current, 100f) && !state.IsDepletedLocked);
                Expect("no duplicate recovery", recovered == 1);

                Debug.Log("[Windrunner 5B] AirPowerState PASS — initial fill, spending, " +
                          "events, depletion lockout, delayed recovery, threshold and refill. " +
                          "This checks the extracted rules, not full in-game mechanics parity.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[Windrunner 5B] AirPowerState FAIL. Stop migration and report this error.");
            }
        }

        private static bool Approximately(float a, float b) => Mathf.Abs(a - b) < 0.0001f;

        private static void Expect(string check, bool condition)
        {
            if (!condition)
                throw new InvalidOperationException("AirPowerState check failed: " + check);
        }
    }
}
