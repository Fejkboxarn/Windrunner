using System;
using UnityEngine;

namespace PortableAnimation
{
    public enum WeaponPoseSignalKind { Equipped, Switched, Unequipped }

    public readonly struct WeaponPoseSignal
    {
        public readonly WeaponPoseSignalKind Kind;
        public readonly WeaponPoseKind Previous;
        public readonly WeaponPoseKind Current;
        public WeaponPoseSignal(WeaponPoseSignalKind kind,
            WeaponPoseKind previous, WeaponPoseKind current)
        {
            Kind = kind;
            Previous = previous;
            Current = current;
        }
        public override string ToString() => Kind + ": " + Previous + " -> " + Current;
    }

    /// <summary>
    /// Pure C# equipment-pose transition controller. Interrupted switches
    /// blend from CURRENT weights, not the completed old preset. Unarmed
    /// contributes no overlay and restores the incoming animation pose.
    /// </summary>
    public sealed class WeaponPoseController
    {
        private readonly WeaponPoseProfile profile;
        private readonly float[] current;
        private readonly float[] start;
        private readonly float[] target;
        private float elapsed;
        private float duration;
        private WeaponPoseKind requested = WeaponPoseKind.Unarmed;

        public event Action<WeaponPoseSignal> SignalRaised;
        public WeaponPoseKind Requested => requested;
        public bool IsBlending => duration > 0f && elapsed < duration;
        public int Count => current.Length;
        public bool IsEquipped => requested != WeaponPoseKind.Unarmed || HasActiveWeight;
        public bool HasActiveWeight
        {
            get
            {
                for (int i = 0; i < current.Length; i++)
                    if (current[i] > 0.001f) return true;
                return false;
            }
        }

        public WeaponPoseController(WeaponPoseProfile profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            profile.AssertValid();
            current = new float[profile.Poses.Count];
            start = new float[current.Length];
            target = new float[current.Length];
        }

        public float Weight(int index) => current[index];

        public void Select(WeaponPoseKind kind)
        {
            if (kind == requested) return;
            int next = profile.IndexOf(kind);
            if (kind != WeaponPoseKind.Unarmed && next < 0)
                throw new ArgumentException("Unknown equipment mode: " + kind);

            WeaponPoseKind previous = requested;
            int previousIndex = profile.IndexOf(previous);
            requested = kind;
            Array.Copy(current, start, current.Length);
            Array.Clear(target, 0, target.Length);
            if (next >= 0) target[next] = 1f;

            float outTime = previousIndex >= 0
                ? profile.Poses[previousIndex].UnequipSeconds : 0f;
            float inTime = next >= 0
                ? profile.Poses[next].EquipSeconds : 0f;
            duration = Mathf.Max(outTime, inTime);
            elapsed = 0f;
            if (duration <= 0f) Array.Copy(target, current, current.Length);

            var signal = kind == WeaponPoseKind.Unarmed
                ? WeaponPoseSignalKind.Unequipped
                : previous == WeaponPoseKind.Unarmed
                    ? WeaponPoseSignalKind.Equipped
                    : WeaponPoseSignalKind.Switched;
            SignalRaised?.Invoke(new WeaponPoseSignal(signal, previous, kind));
        }

        public void Advance(float dt)
        {
            if (!IsBlending) return;
            elapsed = Mathf.Min(duration, elapsed + Mathf.Max(0f, dt));
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - 2f * t);
            for (int i = 0; i < current.Length; i++)
                current[i] = Mathf.Lerp(start[i], target[i], smooth);
        }
    }
}
