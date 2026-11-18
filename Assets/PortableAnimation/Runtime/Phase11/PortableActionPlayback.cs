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
            public readonly VisualPlaybackClock Visual;
            public Timing(float length, float speed, float blendIn, float blendOut, int priority,
                bool available = true, bool loop = false, bool restart = false, bool interruptible = true, VisualPlaybackClock visual = null)
            {
                Visual = visual;
                Length = length; Speed = speed; BlendIn = blendIn; BlendOut = blendOut;
                Priority = priority; Available = available; Loop = loop; Restart = restart; Interruptible = interruptible;
            }
        }
        private readonly Timing[] timings;
        private readonly float[] ages, weights;
        private readonly double[] visualAges;
        private readonly float[] heldProgress;
        public int ActiveIndex { get; private set; } = -1;
        public int Count => timings.Length;
        public PortableActionPlayback(Timing[] timings)
        {
            this.timings = (Timing[])timings.Clone();
            visualAges = new double[timings.Length];
            heldProgress = new float[timings.Length];
            for(int i=0;i<heldProgress.Length;i++)heldProgress[i]=-1;
            ages = new float[timings.Length]; weights = new float[timings.Length];
        }
        public void ChangeTiming(int index,Timing timing)
        {
            var old=timings[index];
            float phase = old.Length > 0 ? ClipTime(index) / old.Length : 0;
            heldProgress[index]=-1;
            if (timing.Visual != null)
            {
                double elapsed=ages[index] == 0 && weights[index] == 0 ? 0 : timing.Visual.ElapsedAtProgress(phase);
                if(double.IsInfinity(elapsed))
                {
                    // A newly selected zero-speed barrier must retain the current pose.
                    heldProgress[index]=phase;
                    elapsed=0;
                }
                visualAges[index]=elapsed;
            }
            else if(old.Length>0 && old.Speed>0 && timing.Speed>0)
                visualAges[index] = phase * timing.Length / timing.Speed;
            timings[index]=timing;
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
            ActiveIndex = index; ages[index] = 0f; visualAges[index] = 0; heldProgress[index]=-1;
            return true;
        }
        public void Cancel(int index = -1)
        {
            if (index < 0 || index == ActiveIndex) ActiveIndex = -1;
        }
        public float Weight(int index) => weights[index];
        public bool IsLoopingSlot(int index) => timings[index].Available && timings[index].Loop;
        public float ClipTime(int index)
        {
            var timing = timings[index];
            if (timing.Visual != null)
                return (heldProgress[index]>=0 ? heldProgress[index] :
                    timing.Visual.RangeProgress(visualAges[index], timing.Loop)) * timing.Length;
            float time = (float)(visualAges[index] * timing.Speed);
            return timing.Loop && timing.Length > 0f ? time % timing.Length : Math.Min(time, timing.Length);
        }
        public VisualPlaybackSample VisualSample(int index) =>
            heldProgress[index]>=0 ? timings[index].Visual.HeldSample(heldProgress[index]) :
                timings[index].Visual.Sample(visualAges[index], timings[index].Loop);

        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;
            for (int i = 0; i < Count; i++)
            {
                var timing = timings[i];
                if (i == ActiveIndex || weights[i] > 0f)
                {
                    ages[i] += deltaTime;
                    visualAges[i] += deltaTime;
                }
                if (i == ActiveIndex)
                {
                    float remaining = timing.Length / timing.Speed - (float)visualAges[i];
                    if (timing.Visual != null)
                    {
                        remaining = (float)Math.Min(timing.Visual.Duration - visualAges[i],
                            timing.Visual.MaximumActionSeconds - ages[i]);
                        // Ability-owned loops terminate through the existing cancellation path.
                    }
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
