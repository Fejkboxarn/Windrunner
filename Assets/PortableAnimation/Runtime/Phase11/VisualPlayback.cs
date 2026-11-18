using System;
using UnityEngine;

namespace PortableAnimation
{
    public enum VisualPlaybackMode { ExistingTiming, AuthoredSpeed, FixedVisualDuration }

    [Serializable]
    public sealed class VisualPlaybackSettings
    {
        public VisualPlaybackMode mode;
        [Min(0)] public float baseSpeed = 1;
        public bool reverse;
        public bool useCurve;
        public AnimationCurve speedCurve = AnimationCurve.Linear(0, 1, 1, 1);
        [Min(0)] public float entryHoldSeconds;
        [Min(.001f)] public float fixedSeconds = 1;
        [Min(.001f)] public float maximumActionSeconds = 10;

        public void AssertValid()
        {
            if (!Enum.IsDefined(typeof(VisualPlaybackMode), mode) ||
                !Finite(baseSpeed, 0, 100) || !Finite(entryHoldSeconds, 0, 300) ||
                !Finite(fixedSeconds, .001f, 300) || !Finite(maximumActionSeconds, .001f, 300))
                throw new InvalidOperationException("Playback needs finite speed 0–100 and durations within 0–300 seconds.");
            if (!useCurve) return;
            if (speedCurve == null || speedCurve.length == 0)
                throw new InvalidOperationException("An enabled speed curve needs keys.");
            foreach (var key in speedCurve.keys)
                if (!Finite(key.time, 0, 1) || !Finite(key.value, 0, 100) ||
                    float.IsNaN(key.inTangent) || float.IsNaN(key.outTangent))
                    throw new InvalidOperationException("Speed curves use source progress 0–1 and nonnegative multipliers 0–100. Use Reverse for backward playback.");
            for (int i = 0; i <= VisualPlaybackClock.Resolution; i++)
                if (!Finite(speedCurve.Evaluate((float)i / VisualPlaybackClock.Resolution), 0, 100))
                    throw new InvalidOperationException("A speed-curve tangent overshoots the supported 0–100 range.");
        }
        static bool Finite(float value, float minimum, float maximum) =>
            value >= minimum && value <= maximum && !float.IsInfinity(value);
    }

    public readonly struct VisualPlaybackSample
    {
        public readonly float SourceSeconds, Progress, BaseSpeed, CurveMultiplier, EffectiveSpeed, SourceLength;
        public readonly double EstimatedSeconds;
        public readonly VisualPlaybackMode Mode;
        public readonly bool Held;
        public VisualPlaybackSample(float source, float length, float basis, float curve, float effective,
            double estimate, VisualPlaybackMode mode, bool held = false)
        {
            SourceSeconds = source; SourceLength = length; Progress = length > 0 ? source / length : 0;
            BaseSpeed = basis; CurveMultiplier = curve; EffectiveSpeed = effective;
            EstimatedSeconds = estimate; Mode = mode; Held = held;
        }
    }

    /// <summary>
    /// Integrates reciprocal speed over source progress at construction, then
    /// inverts the immutable table. Sampling is allocation-free and independent
    /// of frame partition. Zero is a barrier; a finite entry hold releases by time.
    /// </summary>
    public sealed class VisualPlaybackClock
    {
        public const int Resolution = 1024;
        readonly double[] seconds = new double[Resolution + 1];
        readonly float[] multipliers = new float[Resolution + 1];
        readonly float sourceLength, start, end, basis, hold, sign;
        readonly VisualPlaybackMode mode;
        readonly bool reverse;
        readonly double scale;
        public double Duration { get; }
        public float MaximumActionSeconds { get; }
        public float StartProgress => reverse ? 1 : 0;

        public VisualPlaybackClock(VisualPlaybackSettings settings, float length, float rangeStart = 0, float rangeEnd = 1)
        {
            settings.AssertValid();
            if (!(length > 0) || !(rangeStart >= 0 && rangeEnd <= 1 && rangeEnd > rangeStart))
                throw new ArgumentException("Playback requires a positive source length and valid normalized source range.");
            sourceLength = length; start = rangeStart; end = rangeEnd;
            basis = settings.baseSpeed; hold = settings.entryHoldSeconds; reverse = settings.reverse;
            sign = reverse ? -1 : 1; mode = settings.mode;
            MaximumActionSeconds = settings.maximumActionSeconds;
            float span = length * (end - start);
            for (int i = 0; i <= Resolution; i++)
            {
                float source = Mathf.Lerp(start, end, reverse ? 1 - (float)i / Resolution : (float)i / Resolution);
                multipliers[i] = settings.useCurve ? settings.speedCurve.Evaluate(source) : 1;
                if (i == 0) continue;
                float middle = Mathf.Lerp(start, end, reverse ? 1 - (i - .5f) / Resolution : (i - .5f) / Resolution);
                float rate = basis * (settings.useCurve ? settings.speedCurve.Evaluate(middle) : 1);
                seconds[i] = seconds[i - 1] + (rate <= 0 || multipliers[i - 1] <= 0 || multipliers[i] <= 0
                    ? double.PositiveInfinity : span / (Resolution * (double)rate));
            }
            double travel = seconds[Resolution];
            scale = mode == VisualPlaybackMode.FixedVisualDuration && !double.IsInfinity(travel)
                ? settings.fixedSeconds / travel : 1;
            Duration = hold + travel * scale;
        }

        public double ElapsedAtProgress(float sourceProgress)
        {
            float position = Mathf.Clamp01(reverse ? 1 - sourceProgress : sourceProgress) * Resolution;
            int lo = Math.Min(Resolution - 1, (int)position);
            if (double.IsInfinity(seconds[lo + 1]))
                return position > lo || double.IsInfinity(seconds[lo]) ? double.PositiveInfinity : hold + seconds[lo] * scale;
            return hold + (seconds[lo] + (seconds[lo + 1] - seconds[lo]) * (position - lo)) * scale;
        }

        public VisualPlaybackSample HeldSample(float progress)
        {
            progress=Mathf.Clamp01(progress);
            int index=Mathf.Clamp(Mathf.RoundToInt((reverse?1-progress:progress)*Resolution),0,Resolution);
            return new VisualPlaybackSample(Mathf.Lerp(start,end,progress)*sourceLength,sourceLength,
                basis,multipliers[index],0,double.PositiveInfinity,mode,true);
        }

        public VisualPlaybackSample Sample(double elapsed, bool loop = false)
        {
            if(double.IsNaN(elapsed) || double.IsInfinity(elapsed))
                throw new ArgumentOutOfRangeException(nameof(elapsed),"Visual elapsed time must be finite.");
            elapsed = Math.Max(0, elapsed);
            if (loop && !double.IsInfinity(Duration) && Duration > 0) elapsed %= Duration;
            double travelTime = Math.Max(0, elapsed - hold) / scale;
            int lo = 0, hi = Resolution;
            while (lo + 1 < hi)
            {
                int mid = (lo + hi) / 2;
                if (seconds[mid] <= travelTime) lo = mid; else hi = mid;
            }
            double interval = seconds[hi] - seconds[lo];
            float fraction = double.IsInfinity(interval) ? 0 : (float)Math.Min(1, (travelTime - seconds[lo]) / interval);
            float phase = (lo + fraction) / Resolution;
            float curve = Mathf.Lerp(multipliers[lo], multipliers[hi], fraction);
            bool held = elapsed < hold || double.IsInfinity(interval);
            float effective = held || interval <= 0 ? 0 :
                (float)(sign * sourceLength * (end-start) / (Resolution * interval * scale));
            if (!loop && elapsed >= Duration) effective = 0;
            float source = Mathf.Lerp(start, end, reverse ? 1 - phase : phase) * sourceLength;
            return new VisualPlaybackSample(source, sourceLength, basis, curve, effective, Duration, mode, held);
        }

        public float RangeProgress(double elapsed, bool loop) =>
            Mathf.InverseLerp(start * sourceLength, end * sourceLength, Sample(elapsed, loop).SourceSeconds);
    }
}
