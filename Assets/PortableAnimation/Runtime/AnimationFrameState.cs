using UnityEngine;
namespace PortableAnimation
{
    public enum LocomotionMode { Grounded, Airborne, Hovering, FreeFlight }
    public readonly struct AnimationFrameState
    {
        public readonly int FrameId;
        public readonly float DeltaTime;
        public readonly Vector3 LocalVelocity;
        public readonly float NormalizedSpeed;
        public readonly LocomotionMode Mode;
        public AnimationFrameState(int frameId, float deltaTime, Vector3 localVelocity, float normalizedSpeed, LocomotionMode mode)
        {
            FrameId = frameId;
            DeltaTime = Mathf.Max(0f, deltaTime);
            LocalVelocity = localVelocity;
            NormalizedSpeed = Mathf.Clamp01(normalizedSpeed);
            Mode = mode;
        }
    }
}
