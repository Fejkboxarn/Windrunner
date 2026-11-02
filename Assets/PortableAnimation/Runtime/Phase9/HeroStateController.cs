using System;
using UnityEngine;

namespace PortableAnimation
{
    public enum HeroStateRequestResult { Started, AlreadyActive, Rejected, TerminalLocked }

    /// <summary>
    /// Standalone high-level animation state controller. No Animator/MonoBehaviour dependencies.
    /// Fade interruptions interpolate from CURRENT per-state weights.
    /// Outgoing clips freeze at their last sample time while weights fade away.
    /// </summary>
    public sealed class HeroStateController
    {
        private readonly HeroStateProfile profile;
        private readonly float[] weights, start, target;
        private readonly double[] times;
        private int active = -1;
        private float blendDuration, blendElapsed;
        private bool terminalLocked;

        public event Action<HeroStateSignal> SignalRaised;
        public HeroPoseState ActiveState => active < 0 ? HeroPoseState.Normal : profile.States[active].State;
        public int ActiveIndex => active;
        public int Count => weights.Length;
        public bool IsTerminal => terminalLocked;
        public bool IsTransitioning => blendDuration > 0f && blendElapsed < blendDuration;
        public float ActiveTime => active < 0 ? 0f : (float)times[active];
        public float GetWeight(int index) => weights[index];
        public double GetClipTime(int index) => times[index];
        public HeroStateEntry Entry(int index) => profile.States[index];

        public bool BlocksActions => AnyEffectiveFlag(e => e.BlocksActions);
        public bool BlocksNewHits => AnyEffectiveFlag(e => e.BlocksNewHits);
        public bool SuppressesAimOffset => AnyEffectiveFlag(e => e.SuppressesAimOffset);

        public HeroStateController(HeroStateProfile profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            profile.AssertValid();
            int count = profile.States.Count;
            weights = new float[count];
            start = new float[count];
            target = new float[count];
            times = new double[count];
        }

        public HeroStateRequestResult Request(HeroPoseState kind)
        {
            if (terminalLocked) return HeroStateRequestResult.TerminalLocked;
            if (kind == ActiveState) return HeroStateRequestResult.AlreadyActive;
            if (kind == HeroPoseState.Normal)
            {
                ChangeTo(-1);
                return HeroStateRequestResult.Started;
            }

            int incoming = profile.IndexOf(kind);
            if (incoming < 0) throw new ArgumentException("No configured state: " + kind);

            if (active >= 0)
            {
                HeroStateEntry oldState = profile.States[active];
                HeroStateEntry newState = profile.States[incoming];
                if (kind != HeroPoseState.Dead &&
                    (!oldState.Interruptible || newState.Priority <= oldState.Priority))
                {
                    Emit(HeroStateSignalKind.Rejected, ActiveState, kind);
                    return HeroStateRequestResult.Rejected;
                }
            }
            ChangeTo(incoming);
            return HeroStateRequestResult.Started;
        }

        /// <summary>Explicit demonstration revive. Never called by automatic completion.</summary>
        public void ResetTerminal()
        {
            if (!terminalLocked) return;
            HeroPoseState previous = ActiveState;
            terminalLocked = false;
            ChangeTo(-1);
            Emit(HeroStateSignalKind.Reset, previous, HeroPoseState.Normal);
        }

        public void Advance(float deltaTime)
        {
            float remaining = Mathf.Max(0f, deltaTime);
            // Allows a single large test timestep to traverse knockdown -> recovery -> normal.
            for (int guard = 0; guard < 8; guard++)
            {
                if (active < 0 || terminalLocked)
                {
                    AdvanceFade(remaining);
                    break;
                }

                HeroStateEntry state = profile.States[active];
                if (state.Duration <= 0f)
                {
                    AdvanceFade(remaining);
                    times[active] += remaining;
                    break;
                }

                float untilExit = Mathf.Max(0f, state.Duration - (float)times[active]);
                float step = Mathf.Min(remaining, untilExit);
                times[active] += step;
                AdvanceFade(step);
                remaining -= step;

                if ((float)times[active] + 0.000001f < state.Duration)
                    break;

                HeroPoseState previous = state.State;
                HeroPoseState next = state.Next;
                Emit(HeroStateSignalKind.Completed, previous, next);
                ChangeTo(profile.IndexOf(next));

                if (remaining <= 0f) break;
            }
        }

        private void ChangeTo(int next)
        {
            int previousIndex = active;
            HeroPoseState previous = ActiveState;
            HeroPoseState incoming = next < 0 ? HeroPoseState.Normal : profile.States[next].State;
            if (previousIndex >= 0 && previous != incoming)
                Emit(HeroStateSignalKind.Interrupted, previous, incoming);

            float outSeconds = previousIndex < 0 ? 0f : profile.States[previousIndex].BlendOut;
            float inSeconds = next < 0 ? 0f : profile.States[next].BlendIn;
            blendDuration = Mathf.Max(outSeconds, inSeconds);
            blendElapsed = 0f;
            Array.Copy(weights, start, weights.Length);
            Array.Clear(target, 0, target.Length);
            if (next >= 0)
            {
                target[next] = 1f;
                times[next] = 0d;
            }
            active = next;
            terminalLocked = next >= 0 && profile.States[next].Terminal;
            if (blendDuration <= 0f) Array.Copy(target, weights, weights.Length);
            Emit(HeroStateSignalKind.Entered, previous, incoming);
        }

        private void AdvanceFade(float dt)
        {
            if (blendDuration <= 0f || blendElapsed >= blendDuration) return;
            blendElapsed = Mathf.Min(blendDuration, blendElapsed + dt);
            float t = Mathf.Clamp01(blendElapsed / blendDuration);
            float smooth = t * t * (3f - 2f * t);
            for (int i = 0; i < weights.Length; i++)
                weights[i] = Mathf.Lerp(start[i], target[i], smooth);
        }

        private bool AnyEffectiveFlag(Func<HeroStateEntry, bool> predicate)
        {
            if (active >= 0 && predicate(profile.States[active])) return true;
            for (int i = 0; i < weights.Length; i++)
                if (weights[i] > 0.01f && predicate(profile.States[i])) return true;
            return false;
        }

        private void Emit(HeroStateSignalKind kind, HeroPoseState previous, HeroPoseState current)
        { SignalRaised?.Invoke(new HeroStateSignal(kind, previous, current)); }
    }
}
