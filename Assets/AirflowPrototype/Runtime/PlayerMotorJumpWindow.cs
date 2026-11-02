using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Coyote time, jump input buffering and one-jump-per-ground-contact rules.
    /// No MonoBehaviour, InputAction or scene lookup. Caller supplies the clock.
    /// </summary>
    public sealed class PlayerMotorJumpWindow
    {
        private float _lastGroundedTime = float.NegativeInfinity;
        private float _lastJumpPressedTime = float.NegativeInfinity;
        private bool _jumpConsumed;

        public float CoyoteRemaining(float now, float coyoteTime) =>
            Mathf.Max(0f, coyoteTime - (now - _lastGroundedTime));

        public float BufferRemaining(float now, float bufferTime) =>
            Mathf.Max(0f, bufferTime - (now - _lastJumpPressedTime));

        public void ReportGrounded(float now)
        {
            _lastGroundedTime = now;
            _jumpConsumed = false;
        }

        public void ReportJumpPressed(float now)
        {
            _lastJumpPressedTime = now;
        }

        public bool TryConsume(float now, float bufferTime, float coyoteTime)
        {
            if (_jumpConsumed)
                return false;

            bool buffered = now - _lastJumpPressedTime <= bufferTime;
            bool coyoteValid = now - _lastGroundedTime <= coyoteTime;
            if (!buffered || !coyoteValid)
                return false;

            _jumpConsumed = true;
            _lastJumpPressedTime = float.NegativeInfinity;
            _lastGroundedTime = float.NegativeInfinity;
            return true;
        }
    }
}
