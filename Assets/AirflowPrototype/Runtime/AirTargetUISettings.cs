using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "AirTargetUISettings",
        menuName = "Airflow Prototype/Air Target UI Settings")]
    public sealed class AirTargetUISettings : ScriptableObject
    {
        [Header("Size")]
        [Min(32f)] public float idleSize = 108f;
        [Min(32f)] public float chargingSize = 132f;
        [Range(1f, 1.8f)] public float perfectBloomScale = 1.34f;
        [Min(0.01f)] public float sizeResponse = 13f;

        [Header("Air Ring Geometry")]
        [Range(2, 8)] public int outerArcCount = 4;
        [Range(15f, 80f)] public float outerArcDegrees = 56f;
        [Range(0.5f, 8f)] public float outerArcThickness = 3f;

        [Tooltip("Thickness of the fixed timing seam. Its radius is derived from AirCastSettings.Perfect Timing Center.")]
        [Range(0.5f, 7f)] public float sweetSpotThickness = 2.2f;

        [Range(0.08f, 0.45f)] public float compressionMinRadius01 = 0.17f;
        [Range(0.45f, 0.95f)] public float compressionMaxRadius01 = 0.78f;
        [Range(0.5f, 8f)] public float compressionThickness = 3.2f;

        [Range(0f, 180f)] public float idleRotationSpeed = 24f;
        [Range(0f, 360f)] public float chargingRotationSpeed = 78f;

        [Header("Visibility")]
        [Min(0.01f)] public float appearResponse = 14f;
        [Min(0.01f)] public float disappearResponse = 18f;

        [Header("Perfect Flash")]
        [Min(0.03f)] public float perfectFlashDuration = 0.24f;
        [Min(0.01f)] public float perfectFlashResponse = 18f;

        [Header("Perfect Time Slow")]
        [Range(0.05f, 1f)] public float perfectTimeScale = 0.24f;
        [Min(0f)] public float perfectSlowHoldDuration = 0.055f;
        [Min(0.01f)] public float perfectSlowRecoveryDuration = 0.16f;

        [Header("Colors")]
        public Color idleColor = new Color(0.63f, 0.90f, 1f, 0.72f);
        public Color chargeColor = new Color(0.78f, 0.96f, 1f, 0.95f);
        public Color sweetSpotColor = new Color(0.92f, 0.99f, 1f, 0.56f);
        public Color perfectColor = new Color(1f, 1f, 1f, 1f);
    }
}
