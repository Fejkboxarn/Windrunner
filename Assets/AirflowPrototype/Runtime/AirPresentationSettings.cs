using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "AirPresentationSettings",
        menuName = "Airflow Prototype/Air Presentation Settings")]
    public sealed class AirPresentationSettings : ScriptableObject
    {
        [Header("Charge Compression")]
        [Range(4, 24)] public int chargeStreakCount = 10;
        [Min(0.1f)] public float chargeStreakRadius = 0.85f;
        [Min(0.05f)] public float chargeStreakLength = 0.28f;
        [Min(0f)] public float chargeOrbitSpeed = 90f;
        [Min(0.001f)] public float chargeStreakWidth = 0.012f;
        [Min(0.05f)] public float chargeRingStartRadius = 0.62f;
        [Min(0.02f)] public float chargeRingReadyRadius = 0.17f;
        [Min(0.001f)] public float chargeRingWidth = 0.022f;

        [Header("Release Pressure")]
        [Range(1, 5)] public int releaseRingCount = 3;
        [Min(0.05f)] public float releaseStartRadius = 0.16f;
        [Min(0.1f)] public float releaseEndRadius = 1.15f;
        [Min(0.03f)] public float releaseDuration = 0.16f;
        [Min(0.001f)] public float releaseRingWidth = 0.028f;

        [Header("Node Impact")]
        [Range(1, 5)] public int nodeImpactRingCount = 3;
        [Min(0.05f)] public float nodeImpactStartRadius = 0.18f;
        [Min(0.1f)] public float nodeImpactEndRadius = 1.35f;
        [Min(0.03f)] public float nodeImpactDuration = 0.20f;
        [Min(0.001f)] public float nodeImpactRingWidth = 0.024f;

        [Header("Reward Arrival")]
        [Range(1, 5)] public int rewardRingCount = 3;
        [Min(0.05f)] public float rewardStartRadius = 0.24f;
        [Min(0.1f)] public float rewardEndRadius = 1.55f;
        [Min(0.03f)] public float rewardDuration = 0.22f;
        [Min(0.001f)] public float rewardRingWidth = 0.032f;

        [Header("Return Stream Shape")]
        [Min(0f)] public float returnCurveSide = 1.15f;
        [Min(0f)] public float returnCurveUp = 0.75f;
        [Range(1f, 5f)] public float returnEasePower = 2.4f;

        [Header("Color")]
        public Color softAirColor = new Color(0.66f, 0.88f, 1f, 0.55f);
        public Color brightAirColor = new Color(0.90f, 0.99f, 1f, 0.95f);
    }
}
