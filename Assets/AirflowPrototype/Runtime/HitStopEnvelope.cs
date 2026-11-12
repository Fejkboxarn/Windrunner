using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>Deterministic legacy hold/smoothstep recovery; pause freezes its clock.</summary>
    public sealed class HitStopEnvelope
    {
        public bool Active { get; private set; }
        public float Scale { get; private set; }
        public float Elapsed { get; private set; }
        private float baseline, initial, hold, recovery;

        public void Start(float quality, float holdDuration, float recoveryDuration, float requested, float baselineScale)
        {
            baseline = baselineScale;
            hold = Mathf.Max(0f, holdDuration);
            recovery = Mathf.Max(0.01f, recoveryDuration);
            requested = Mathf.Clamp(requested, 0.01f, 1f);
            initial = Mathf.Lerp(Mathf.Min(1f, requested * 1.18f), requested, Mathf.Clamp01(quality));
            Scale = Mathf.Clamp(initial, 0.01f, Mathf.Max(0.01f, baseline));
            Elapsed = 0f;
            Active = true;
        }
        public void Advance(float unscaledDeltaTime, bool paused)
        {
            if (!Active || paused) return;
            Elapsed += unscaledDeltaTime;
            if (Elapsed <= hold) return;
            float t = Mathf.Clamp01((Elapsed - hold) / recovery);
            if (t >= 1f) { Stop(); return; }
            float smooth = t * t * (3f - 2f * t);
            Scale = Mathf.Clamp(Mathf.Lerp(initial, baseline, smooth), 0.01f, Mathf.Max(0.01f, baseline));
        }
        public void Stop() { Active = false; Elapsed = 0f; Scale = baseline; }
    }
}
