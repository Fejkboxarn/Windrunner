using System;

namespace PortableAnimation
{
    /// <summary>Deterministic action timing/priority/blending, independent of Playables.</summary>
    public sealed class PortableActionPlayback
    {
        public readonly struct Timing
        {
            public readonly float Length, Speed, BlendIn, BlendOut;
            public readonly int Priority;
            public readonly bool Available, Loop, Restart, Interruptible;
            public Timing(float length, float speed, float blendIn, float blendOut, int priority,
                bool available = true, bool loop = false, bool restart = false, bool interruptible = true)
            {
                Length = length; Speed = speed; BlendIn = blendIn; BlendOut = blendOut;
                Priority = priority; Available = available; Loop = loop; Restart = restart; Interruptible = interruptible;
            }
        }
        private readonly Timing[] timings;
        private readonly float[] ages, weights;
        public int ActiveIndex { get; private set; } = -1;
        public int Count => timings.Length;
        public PortableActionPlayback(Timing[] timings)
        {
            this.timings = (Timing[])timings.Clone();
            ages = new float[timings.Length]; weights = new float[timings.Length];
        }
        public bool Request(int index)
        {
            if (index < 0 || index >= Count || !timings[index].Available) return false;
            var next = timings[index];
            if (ActiveIndex >= 0)
            {
                var previous = timings[ActiveIndex];
                if (!previous.Interruptible || previous.Priority > next.Priority ||
                    (index == ActiveIndex && !next.Restart)) return false;
            }
            ActiveIndex = index; ages[index] = 0f;
            return true;
        }
        public void Cancel(int index = -1)
        {
            if (index < 0 || index == ActiveIndex) ActiveIndex = -1;
        }
        public float Weight(int index) => weights[index];
        public float ClipTime(int index)
        {
            var timing = timings[index];
            float time = ages[index] * timing.Speed;
            return timing.Loop && timing.Length > 0f ? time % timing.Length : Math.Min(time, timing.Length);
        }
        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;
            for (int i = 0; i < Count; i++)
            {
                var timing = timings[i];
                if (i == ActiveIndex || weights[i] > 0f) ages[i] += deltaTime;
                if (i == ActiveIndex)
                {
                    float remaining = timing.Length / timing.Speed - ages[i];
                    if (!timing.Loop && remaining <= 0f) { ActiveIndex = -1; weights[i] = 0f; continue; }
                    float fadeIn = Math.Min(1f, ages[i] / timing.BlendIn);
                    float fadeOut = timing.Loop ? 1f : Math.Min(1f, remaining / timing.BlendOut);
                    weights[i] = Math.Max(0f, Math.Min(fadeIn, fadeOut));
                }
                else weights[i] = Math.Max(0f, weights[i] - deltaTime / timing.BlendOut);
            }
        }
    }
}
