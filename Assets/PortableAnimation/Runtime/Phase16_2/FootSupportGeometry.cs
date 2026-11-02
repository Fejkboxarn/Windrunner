using System;
using UnityEngine;

namespace PortableAnimation
{
    /// <summary>Support geometry is distinct from Humanoid Foot/Toes bone pivots.</summary>
    public enum ShoeSupportMode { FlatFoot = 0, HeelAndToe = 1 }
    public enum FootSupportContact { None = 0, Toe = 1, Heel = 2, Both = 3 }

    [Serializable]
    public struct ShoeSupportCalibration
    {
        public Vector3 ToeLocal;
        public Vector3 HeelLocal;
        public bool Valid;
        public ShoeSupportCalibration(Vector3 toe, Vector3 heel)
        {
            ToeLocal = toe;
            HeelLocal = heel;
            Valid = true;
        }
    }

    /// <summary>
    /// Calibrates TOE SOLE and HEEL TIP independently. The support line goes
    /// between them. The ankle joint is reconstructed from that rigid geometry.
    /// Position of the heel tip is not deducible from skeletal joints alone:
    /// it must be calibrated against visible footwear and may be fine-tuned.
    /// </summary>
    public static class FootSupportGeometry
    {
        public static ShoeSupportCalibration Estimate(
            Vector3 ankle, Quaternion ankleRotation, Vector3 toeBone,
            bool toeMapped, float toeForward, float toeDepth,
            float heelBackFromAnkle, float heelHeightAdjustment,
            float fallbackAnkleHeight)
        {
            Vector3 forward = Vector3.ProjectOnPlane(toeBone - ankle, Vector3.up);
            if (!toeMapped || forward.sqrMagnitude < 0.000001f)
                forward = Vector3.ProjectOnPlane(ankleRotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.000001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 toe = toeMapped
                ? toeBone + forward * Mathf.Max(0f, toeForward) -
                    Vector3.up * Mathf.Max(0f, toeDepth)
                : ankle + forward * 0.17f - Vector3.up *
                    Mathf.Max(0.01f, fallbackAnkleHeight);

            // A raised heel's TIP is generally below and behind the ankle.
            // At calibration its tip and toe outsole share one support plane.
            // HeelHeightAdjustment represents measured shoe geometry deviation.
            Vector3 heel = ankle - forward * Mathf.Max(0.01f, heelBackFromAnkle);
            heel.y = toe.y + heelHeightAdjustment;
            Quaternion inverse = Quaternion.Inverse(ankleRotation);
            return new ShoeSupportCalibration(inverse * (toe - ankle),
                inverse * (heel - ankle));
        }

        public static Vector3 WorldPoint(Vector3 ankle, Quaternion rotation,
            Vector3 local) => ankle + rotation * local;

        public static Vector3 AnkleFromPoint(Vector3 point, Quaternion rotation,
            Vector3 local) => point - rotation * local;

        /// <summary>
        /// Projects the support line onto the ground tangent plane. The
        /// Pythagorean relationship is sqrt(horizontal^2 + vertical^2),
        /// and the required pitch correction is atan2(vertical,horizontal).
        /// A bounded quaternion aligns that line while retaining yaw.
        /// </summary>
        public static Quaternion SupportLevelRotation(Vector3 worldHeel,
            Vector3 worldToe, Vector3 groundNormal, float maximumAngleDeg)
        {
            Vector3 line = worldToe - worldHeel;
            if (line.sqrMagnitude < 0.000001f) return Quaternion.identity;
            if (groundNormal.sqrMagnitude < 0.000001f) groundNormal = Vector3.up;
            groundNormal.Normalize();
            Vector3 flat = Vector3.ProjectOnPlane(line, groundNormal);
            if (flat.sqrMagnitude < 0.000001f) return Quaternion.identity;
            Quaternion desired = Quaternion.FromToRotation(line, flat);
            return Quaternion.RotateTowards(Quaternion.identity,
                desired, Mathf.Clamp(maximumAngleDeg, 0f, 45f));
        }

        public static float SupportTiltDegrees(Vector3 worldHeel,
            Vector3 worldToe, Vector3 groundNormal)
        {
            Vector3 line = worldToe - worldHeel;
            Vector3 vertical = Vector3.Project(line, groundNormal.normalized);
            Vector3 planar = Vector3.ProjectOnPlane(line, groundNormal.normalized);
            return Mathf.Atan2(vertical.magnitude, Mathf.Max(0.00001f,
                planar.magnitude)) * Mathf.Rad2Deg;
        }

        public static FootSupportContact Choose(bool toeFound, bool heelFound,
            float toeHeight, float heelHeight, float plantHeight,
            bool useHeelAndToe)
        {
            bool toe = toeFound && Mathf.Abs(toeHeight) <= plantHeight;
            bool heel = useHeelAndToe && heelFound &&
                Mathf.Abs(heelHeight) <= plantHeight;
            if (toe && heel) return FootSupportContact.Both;
            if (toe) return FootSupportContact.Toe;
            if (heel) return FootSupportContact.Heel;
            return FootSupportContact.None;
        }

        public static float TargetSupportHeight(Vector3 toe, Vector3 heel,
            bool useHeel) => useHeel ? Mathf.Min(toe.y, heel.y) : toe.y;
    }
}
