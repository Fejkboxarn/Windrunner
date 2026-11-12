using UnityEngine;
using Windrunner.Production.Core;

namespace Windrunner.Production.Unity
{
    /// <summary>
    /// Single adapter for the engine-global clock, not a gameplay service locator.
    /// Owners hold disposable requests. With no requests, the next scope captures
    /// the current engine baseline (including old scenes without ProjectRoot).
    /// </summary>
    public static class UnityTimeAuthority
    {
        private static TimeScaleArbiter arbiter;
        public static bool IsPaused => arbiter != null && arbiter.IsPaused;
        public static float BaselineScale => Current.BaselineScale;
        private static TimeScaleArbiter Current
        {
            get
            {
                if (arbiter == null || arbiter.RequestCount == 0)
                {
                    if (arbiter != null) arbiter.Changed -= Apply;
                    arbiter = new TimeScaleArbiter(Time.timeScale, Time.fixedDeltaTime);
                    arbiter.Changed += Apply;
                }
                return arbiter;
            }
        }
        public static TimeScaleArbiter.Request Pause() => Current.Pause();
        public static TimeScaleArbiter.Request Slowdown(float scale) => Current.Slowdown(scale);
        private static void Apply()
        {
            Time.timeScale = arbiter.EffectiveScale;
            Time.fixedDeltaTime = arbiter.EffectiveFixedStep;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            if (arbiter != null)
            {
                arbiter.Changed -= Apply;
                if (arbiter.RequestCount > 0)
                {
                    Time.timeScale = arbiter.BaselineScale;
                    Time.fixedDeltaTime = arbiter.BaselineFixedStep;
                }
            }
            arbiter = null;
        }
    }
}
