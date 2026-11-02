using UnityEngine;

namespace PortableAnimation
{
    /// <summary>Pure gating and geometry math. A sole contact is NOT an ankle joint.</summary>
    public static class FootContactMath
    {
        public static bool IsEligible(bool grounded, bool blocking, bool airborneState)
            => grounded && !blocking && !airborneState;

        public static bool ShouldPlant(bool eligible, bool hit, float soleHeight,
            float verticalFootSpeed, float plantHeight, float verticalSpeedLimit)
            => eligible && hit && Mathf.Abs(soleHeight) <= plantHeight &&
                Mathf.Abs(verticalFootSpeed) <= verticalSpeedLimit;

        public static bool ShouldPlantStable(bool eligible, bool hit, float soleHeight,
            float verticalFootSpeed, float horizontalFootSpeed,
            float plantHeight, float verticalSpeedLimit, float horizontalSpeedLimit)
            => ShouldPlant(eligible, hit, soleHeight, verticalFootSpeed,
                    plantHeight, verticalSpeedLimit) &&
                horizontalFootSpeed <= horizontalSpeedLimit;

        public static bool ShouldRelease(bool eligible, bool hit, float soleHeight,
            float horizontalDistance, float releaseHeight, float maxDistance)
            => !eligible || !hit || soleHeight > releaseHeight ||
                horizontalDistance > maxDistance;

        public static float PelvisTarget(float currentSoleY, float desiredSoleY,
            float maxDown, float maxUp)
            => Mathf.Clamp(desiredSoleY - currentSoleY,
                -Mathf.Abs(maxDown), Mathf.Abs(maxUp));

        /// <summary>
        /// Estimate sole contact in the FOOT'S local space, using the toe joint
        /// for height and a tunable fraction of ankle-to-toe travel for XZ.
        /// No mesh vertex/bounds assumptions. Optional toe-less fallback.
        /// </summary>
        public static Vector3 CalibrateSoleLocal(
            Vector3 ankleWorld, Quaternion footWorldRotation,
            Vector3 toeWorld, bool hasToe,
            float toeForwardFraction, float soleDepth, float fallbackAnkleHeight)
        {
            Vector3 soleWorld;
            if (hasToe)
            {
                Vector3 delta = toeWorld - ankleWorld;
                Vector3 horizontal = Vector3.ProjectOnPlane(delta, Vector3.up);
                soleWorld = ankleWorld + horizontal * Mathf.Clamp01(toeForwardFraction) +
                    Vector3.up * (delta.y - Mathf.Max(0f, soleDepth));
            }
            else
            {
                soleWorld = ankleWorld - Vector3.up *
                    (Mathf.Max(0f, fallbackAnkleHeight) + Mathf.Max(0f, soleDepth));
            }
            return Quaternion.Inverse(footWorldRotation) * (soleWorld - ankleWorld);
        }

        public static Vector3 SoleWorld(Vector3 ankleWorld, Quaternion footRotation,
            Vector3 localOffset)
            => ankleWorld + footRotation * localOffset;

        public static Vector3 AnkleFromSole(Vector3 targetSoleWorld,
            Quaternion footRotation, Vector3 localOffset)
            => targetSoleWorld - footRotation * localOffset;

        public static Vector3 GroundSoleTarget(Vector3 rawSoleWorld, float groundY,
            float clearance)
            => new Vector3(rawSoleWorld.x, groundY + Mathf.Max(0f, clearance),
                rawSoleWorld.z);

        public static float ExponentialResponse(float dt, float responseSeconds)
        {
            if (dt <= 0f) return 0f;
            return 1f - Mathf.Exp(-dt / Mathf.Max(0.0001f, responseSeconds));
        }
    }
}
