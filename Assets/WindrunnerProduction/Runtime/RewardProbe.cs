using System;
using System.Diagnostics;

namespace Windrunner.Production.Diagnostics
{
    // Opt-in timings on the gameplay thread. Calls compile out of release variants.
    public static class RewardProbe
    {
        public enum Phase { Main, OverflowRules, Notify, ReserveHud, Lock, Inspect, Validate, Serialize, TempWrite, Flush, Publish }
#if UNITY_EDITOR || ENABLE_PROFILER
        public static bool Active;
        public static readonly long[] Ticks = new long[Enum.GetValues(typeof(Phase)).Length];
        private static readonly long[] Started = new long[Ticks.Length];
        public static int Writes;
#endif
        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_PROFILER")]
        public static void Begin(Phase phase)
        {
#if UNITY_EDITOR || ENABLE_PROFILER
            if (Active) Started[(int)phase] = Stopwatch.GetTimestamp();
#endif
        }
        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_PROFILER")]
        public static void End(Phase phase)
        {
#if UNITY_EDITOR || ENABLE_PROFILER
            if (!Active) return;
            Ticks[(int)phase] += Stopwatch.GetTimestamp() - Started[(int)phase];
            if (phase == Phase.Publish) Writes++;
#endif
        }
    }
}
