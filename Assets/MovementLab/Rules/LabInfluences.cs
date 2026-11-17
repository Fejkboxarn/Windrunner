using System;
using UnityEngine;

namespace Windrunner.MovementLab
{
    public enum WindMode { Continuous, EntryImpulse }

    [Serializable]
    public sealed class WindSettings
    {
        public WindMode mode;
        [Min(0)] public float strength = 80, speedCap = 150, minimumSpeed = 1, rampSeconds = .25f;
        [Range(0, 1)] public float edgeFalloff = .25f, verticalWeight = 1;
    }

    // Per actor and volume. Sample once per motor frame; no trigger callback ownership.
    public struct WindContact
    {
        private bool inside, eligible;
        private float elapsed, delivered;
        public bool Inside => inside;
        public bool Eligible => eligible;

        public Vector3 Sample(WindSettings settings, Vector3 velocity, bool grounded, float depth, float dt)
        {
            if (dt <= 0) return Vector3.zero;
            if (depth < 0) { this = default; return Vector3.zero; }
            Vector3 movement = velocity;
            if (grounded) movement.y = 0;
            if (!inside || settings.mode == WindMode.Continuous)
                eligible = movement.sqrMagnitude > .0001f && movement.magnitude >= settings.minimumSpeed;
            inside = true;
            if (!eligible) return Vector3.zero;
            elapsed += dt;
            float ramp = settings.rampSeconds <= 0 ? 1 : Mathf.Clamp01(elapsed / settings.rampSeconds);
            float weight = settings.edgeFalloff <= 0 ? 1 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(depth / settings.edgeFalloff));
            float amount = settings.strength * ramp * dt;
            if (settings.mode == WindMode.EntryImpulse)
            {
                amount = settings.strength * (ramp - delivered);
                delivered = ramp;
            }
            return LabVelocity.Boost(velocity, grounded, settings.verticalWeight, amount * weight, settings.speedCap);
        }
    }

    public static class LabVelocity
    {
        public static Vector3 Boost(Vector3 velocity, bool grounded, float verticalWeight, float amount, float cap)
        {
            var direction = velocity;
            direction.y *= grounded ? 0 : verticalWeight;
            if (direction.sqrMagnitude < .0001f) return Vector3.zero;
            return direction.normalized * Mathf.Clamp(amount, 0, Mathf.Max(0, cap - velocity.magnitude));
        }

        // Strongest environmental contribution wins. Overlapping identical boxes cannot multiply it.
        public static Vector3 Strongest(Vector3 current, Vector3 candidate) =>
            candidate.sqrMagnitude > current.sqrMagnitude ? candidate : current;
    }

    [Serializable]
    public sealed class OverdriveSettings
    {
        [Min(.01f)] public float drain = 8;
        [Min(0)] public float acceleration = 95, activationPower = 2, rampSeconds = .3f, speedCap = 170;
        [Min(0)] public float groundMultiplier = 1, airMultiplier = .85f;
    }

    public sealed class OverdriveSession
    {
        private bool requireRelease;
        private float heldTime;
        public bool Active { get; private set; }
        public void Interrupt() { Active = false; heldTime = 0; requireRelease = true; }

        public float Request(OverdriveSettings s, bool held, bool allowed, float available, float dt)
        {
            if (!held) { requireRelease = false; Active = false; heldTime = 0; return 0; }
            if (!allowed || available <= 0) { Interrupt(); return 0; }
            if (dt <= 0 || requireRelease || (!Active && available < s.activationPower)) return 0;
            Active = true;
            return Mathf.Min(available, Mathf.Max(.01f, s.drain) * dt);
        }

        public Vector3 Apply(OverdriveSettings s, Vector3 velocity, bool grounded, float paid, bool remaining)
        {
            float seconds = paid / Mathf.Max(.01f, s.drain);
            heldTime += seconds;
            float ramp = s.rampSeconds > 0 ? Mathf.Clamp01(heldTime / s.rampSeconds) : 1;
            Vector3 delta = LabVelocity.Boost(velocity, grounded, 1, seconds * s.acceleration * ramp *
                (grounded ? s.groundMultiplier : s.airMultiplier), s.speedCap);
            if (!remaining) Interrupt();
            return delta;
        }
    }
}
