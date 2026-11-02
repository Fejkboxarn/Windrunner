using UnityEngine;

namespace PortableAnimation
{
    /// <summary>
    /// Button commands ramp toward speed targets. Direct Inspector edits remain
    /// immediate and cancel the ramp. Pure C#, independent of MonoBehaviour.
    /// </summary>
    public sealed class MotionCommandSmoother
    {
        private float targetForward, targetLateral;
        private float expectedForward, expectedLateral;
        public bool Active { get; private set; }

        public void Request(float forward, float lateral,
            float currentForward, float currentLateral)
        {
            targetForward = Mathf.Clamp01(forward);
            targetLateral = Mathf.Clamp(lateral, -1f, 1f);
            expectedForward = currentForward;
            expectedLateral = currentLateral;
            Active = true;
        }

        public void Cancel() { Active = false; }

        public void Tick(float dt, ref float forward, ref float lateral,
            float acceleration, float deceleration)
        {
            if (!Active) return;
            if (Mathf.Abs(forward - expectedForward) > 0.00001f ||
                Mathf.Abs(lateral - expectedLateral) > 0.00001f)
            { Active = false; return; }
            dt = Mathf.Clamp(dt, 0f, .05f);
            forward = Mathf.MoveTowards(forward, targetForward,
                dt * (targetForward < forward ? deceleration : acceleration));
            lateral = Mathf.MoveTowards(lateral, targetLateral,
                dt * (Mathf.Abs(targetLateral) < Mathf.Abs(lateral)
                    ? deceleration : acceleration));
            expectedForward = forward;
            expectedLateral = lateral;
            if (Mathf.Approximately(forward, targetForward) &&
                Mathf.Approximately(lateral, targetLateral)) Active = false;
        }
    }
}
