using System;
using UnityEngine;

namespace PortableAnimation
{
    public enum CharacterExtensionSignalKind { Started, Exited, Rejected, Completed }

    public readonly struct CharacterExtensionSignal
    {
        public readonly CharacterExtensionSignalKind Kind;
        public readonly string Id;
        public CharacterExtensionSignal(CharacterExtensionSignalKind kind, string id)
        { Kind = kind; Id = id; }
        public override string ToString() => Kind + ": " + Id;
    }

    /// <summary>
    /// Pure C# pose-branch policy. Index 0 is the existing locomotion output;
    /// indices 1..N are character-specific clips. Weights always sum to 1.
    /// Interrupting a fade uses current weights, never a default preset.
    /// This controller deliberately never moves the gameplay character.
    /// </summary>
    public sealed class CharacterExtensionController
    {
        private readonly CharacterExtensionProfile profile;
        private readonly float[] weights;
        private readonly float[] starting;
        private readonly float[] destination;
        private readonly float[] clipTimes;
        private int active = -1;
        private float elapsed;
        private float transitionSeconds;
        private float activeTime;

        public event Action<CharacterExtensionSignal> SignalRaised;
        public int ActiveIndex => active;
        public string ActiveId => active < 0 ? "Base" : profile.Entries[active].Id;
        public int EntryCount => profile.Entries.Count;
        public float BaseWeight => weights[0];
        public float ActiveTime => activeTime;
        public bool IsTransitioning => transitionSeconds > 0f && elapsed < transitionSeconds;

        public CharacterExtensionController(CharacterExtensionProfile profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            profile.AssertValid();
            weights = new float[profile.Entries.Count + 1];
            starting = new float[weights.Length];
            destination = new float[weights.Length];
            clipTimes = new float[profile.Entries.Count];
            weights[0] = destination[0] = 1f;
        }

        public static bool Allows(CharacterMotionDomain domain, bool grounded, bool flight)
        {
            switch (domain)
            {
                case CharacterMotionDomain.Grounded: return grounded;
                case CharacterMotionDomain.Airborne: return !grounded;
                case CharacterMotionDomain.Flight: return flight && !grounded;
                default: return true;
            }
        }

        public float Weight(int clipIndex) => weights[clipIndex + 1];
        public float ClipTime(int clipIndex) => clipTimes[clipIndex];

        public CharacterExtensionRequestResult Request(string id, bool grounded, bool flight)
        {
            int index = profile.IndexOf(id);
            if (index < 0) return CharacterExtensionRequestResult.Unknown;
            if (active == index) return CharacterExtensionRequestResult.AlreadyActive;

            var next = profile.Entries[index];
            if (!Allows(next.Domain, grounded, flight))
            {
                SignalRaised?.Invoke(new CharacterExtensionSignal(
                    CharacterExtensionSignalKind.Rejected, id));
                return CharacterExtensionRequestResult.InvalidDomain;
            }

            if (active >= 0)
            {
                var old = profile.Entries[active];
                if (!old.Interruptible && next.Priority <= old.Priority)
                {
                    SignalRaised?.Invoke(new CharacterExtensionSignal(
                        CharacterExtensionSignalKind.Rejected, id));
                    return CharacterExtensionRequestResult.BlockedByPriority;
                }
            }

            active = index;
            activeTime = 0f;
            clipTimes[index] = 0f;
            BeginTransition(index + 1, next.BlendIn);
            SignalRaised?.Invoke(new CharacterExtensionSignal(
                CharacterExtensionSignalKind.Started, id));
            return CharacterExtensionRequestResult.Started;
        }

        public void Exit()
        {
            if (active < 0) return;
            var entry = profile.Entries[active];
            string id = entry.Id;
            active = -1;
            activeTime = 0f;
            BeginTransition(0, entry.BlendOut);
            SignalRaised?.Invoke(new CharacterExtensionSignal(
                CharacterExtensionSignalKind.Exited, id));
        }

        public void Advance(float dt, bool grounded, bool flight, bool blocked)
        {
            dt = Mathf.Max(0f, dt);
            if (active >= 0)
            {
                var entry = profile.Entries[active];
                if (blocked || !Allows(entry.Domain, grounded, flight))
                    Exit();
                else
                {
                    activeTime += dt;
                    float sampled = clipTimes[active] + dt * entry.PlaybackSpeed;
                    clipTimes[active] = entry.Loop
                        ? Mathf.Repeat(sampled, entry.Clip.length)
                        : Mathf.Min(sampled, Mathf.Max(0f, entry.Clip.length - 0.0001f));
                    if (entry.AutoExitSeconds > 0f && activeTime >= entry.AutoExitSeconds)
                    {
                        SignalRaised?.Invoke(new CharacterExtensionSignal(
                            CharacterExtensionSignalKind.Completed, entry.Id));
                        Exit();
                    }
                }
            }

            if (transitionSeconds <= 0f) return;
            elapsed = Mathf.Min(transitionSeconds, elapsed + dt);
            float t = Mathf.Clamp01(elapsed / transitionSeconds);
            float smooth = t * t * (3f - 2f * t);
            for (int i = 0; i < weights.Length; i++)
                weights[i] = Mathf.Lerp(starting[i], destination[i], smooth);
        }

        private void BeginTransition(int targetIndex, float seconds)
        {
            Array.Copy(weights, starting, weights.Length);
            Array.Clear(destination, 0, destination.Length);
            destination[targetIndex] = 1f;
            elapsed = 0f;
            transitionSeconds = Mathf.Max(0f, seconds);
            if (transitionSeconds <= 0f)
                Array.Copy(destination, weights, weights.Length);
        }
    }
}
