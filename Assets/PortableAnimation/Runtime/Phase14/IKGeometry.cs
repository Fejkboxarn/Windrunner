using System;
using UnityEngine;

namespace PortableAnimation
{
    /// <summary>
    /// Stateless, project-independent analytic solver. Never allocates NativeArrays,
    /// binds animation streams or creates Playables.
    /// </summary>
    public static class IKGeometry
    {
        public static bool CalculateElbow(Vector3 origin, Vector3 target,
            Vector3 pole, float upperLength, float lowerLength,
            float reachFraction, out Vector3 reachableTarget, out Vector3 elbow)
        {
            reachableTarget = target;
            elbow = origin;
            if (upperLength <= 0.0001f || lowerLength <= 0.0001f)
                return false;
            Vector3 displacement = target - origin;
            float rawDistance = displacement.magnitude;
            if (rawDistance <= 0.0001f) return false;
            Vector3 axis = displacement / rawDistance;
            float minimum = Mathf.Abs(upperLength - lowerLength) + 0.0001f;
            float maximum = (upperLength + lowerLength) *
                Mathf.Clamp(reachFraction, 0.7f, 0.999f);
            if (maximum <= minimum) maximum = minimum + 0.0001f;
            float distance = Mathf.Clamp(rawDistance, minimum, maximum);
            reachableTarget = origin + axis * distance;

            float along = (upperLength * upperLength - lowerLength * lowerLength +
                distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f,
                upperLength * upperLength - along * along));

            Vector3 perpendicular = Vector3.ProjectOnPlane(pole - origin, axis);
            if (perpendicular.sqrMagnitude < 0.000001f)
                perpendicular = Vector3.Cross(axis,
                    Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.95f
                        ? Vector3.up : Vector3.right);
            perpendicular.Normalize();
            elbow = origin + axis * along + perpendicular * height;
            return IsFinite(reachableTarget) && IsFinite(elbow);
        }

        public static bool SolveTwoBone(Transform upper, Transform lower,
            Transform hand, Transform target, Transform pole,
            float reachFraction, float weight, float handOrientationWeight)
        {
            weight = Mathf.Clamp01(weight);
            if (weight <= 0f || upper == null || lower == null ||
                hand == null || target == null || pole == null) return false;
            if (!lower.IsChildOf(upper) || !hand.IsChildOf(lower)) return false;

            float lenUpper = Vector3.Distance(upper.position, lower.position);
            float lenLower = Vector3.Distance(lower.position, hand.position);
            if (!CalculateElbow(upper.position, target.position, pole.position,
                lenUpper, lenLower, reachFraction,
                out Vector3 correctedTarget, out Vector3 desiredElbow)) return false;

            Vector3 oldUpper = lower.position - upper.position;
            Vector3 newUpper = desiredElbow - upper.position;
            if (oldUpper.sqrMagnitude < 0.000001f ||
                newUpper.sqrMagnitude < 0.000001f) return false;

            Quaternion upperRotation = upper.rotation;
            Quaternion upperGoal = Quaternion.FromToRotation(oldUpper, newUpper) * upperRotation;
            upper.rotation = Quaternion.Slerp(upperRotation, upperGoal, weight);

            Vector3 oldLower = hand.position - lower.position;
            Vector3 newLower = correctedTarget - lower.position;
            if (oldLower.sqrMagnitude > 0.000001f && newLower.sqrMagnitude > 0.000001f)
            {
                Quaternion lowerRotation = lower.rotation;
                Quaternion lowerGoal = Quaternion.FromToRotation(oldLower, newLower) * lowerRotation;
                lower.rotation = Quaternion.Slerp(lowerRotation, lowerGoal, weight);
            }
            float orientAlpha = weight * Mathf.Clamp01(handOrientationWeight);
            if (orientAlpha > 0f)
                hand.rotation = Quaternion.Slerp(hand.rotation, target.rotation, orientAlpha);
            return true;
        }

        public static bool SolveSingleBone(Transform bone, Transform target,
            Vector3 localAxis, float maxDegrees, float weight)
        {
            weight = Mathf.Clamp01(weight);
            if (bone == null || target == null || weight <= 0f ||
                localAxis.sqrMagnitude < 0.0001f) return false;
            Vector3 direction = target.position - bone.position;
            if (direction.sqrMagnitude < 0.000001f) return false;
            Quaternion current = bone.rotation;
            Vector3 from = bone.TransformDirection(localAxis.normalized);
            Quaternion desired = Quaternion.FromToRotation(from, direction.normalized);
            Quaternion limited = Quaternion.RotateTowards(
                Quaternion.identity, desired, Mathf.Clamp(maxDegrees, 0f, 180f));
            bone.rotation = Quaternion.Slerp(current, limited * current, weight);
            return true;
        }

        public static bool IsFinite(Vector3 value)
        {
            return Finite(value.x) && Finite(value.y) && Finite(value.z);
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
