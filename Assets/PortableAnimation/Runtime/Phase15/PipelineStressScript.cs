using UnityEngine;

namespace PortableAnimation
{
    public readonly struct PipelineStressStep
    {
        public readonly bool Grounded;
        public readonly bool Flight;
        public readonly bool Jump;
        public readonly bool Boost;
        public readonly Vector3 Velocity;
        public readonly float Speed;
        public readonly float Vertical;
        public readonly float YawRate;
        public readonly float Yaw;
        public readonly float Pitch;
        public readonly string Label;
        public PipelineStressStep(bool grounded, bool flight, bool jump, bool boost,
            Vector3 velocity, float speed, float vertical, float yawRate,
            float yaw, float pitch, string label)
        {
            Grounded = grounded; Flight = flight; Jump = jump; Boost = boost;
            Velocity = velocity; Speed = speed; Vertical = vertical;
            YawRate = yawRate; Yaw = yaw; Pitch = pitch; Label = label;
        }
    }

    /// <summary>Reproducible movement scenario. No physics or scene objects.</summary>
    public static class PipelineStressScript
    {
        public static PipelineStressStep Get(int tick, int seed)
        {
            int phase = ((tick + (seed & 7)) % 600 + 600) % 600;
            bool ground = phase < 130 || phase >= 400;
            bool flight = phase >= 180 && phase < 400;
            bool jump = phase == 130;
            bool boost = flight && phase >= 280 && phase < 325;
            float a = tick * 0.041f;
            float speed = ground ? Mathf.PingPong(tick * 0.016f, 1f) :
                flight ? 0.8f : 0.3f;
            Vector3 movement = new Vector3(Mathf.Sin(a) * speed,
                0f, Mathf.Cos(a * 0.7f) * speed);
            float vertical = 0f;
            if (!ground) vertical = phase < 155 ? 2.1f :
                phase < 180 ? -1.3f : phase < 265 ? 1.7f : -2.1f;
            string label = flight ? "Flight" :
                ground ? "Ground" : "JumpFall";
            return new PipelineStressStep(ground, flight, jump, boost,
                movement, Mathf.Clamp01(speed), vertical,
                Mathf.Cos(a * 0.31f) * 130f,
                Mathf.Sin(a * 0.55f) * 75f,
                Mathf.Cos(a * 0.9f) * 38f, label);
        }
    }
}
