using UnityEngine;

namespace AirflowPrototype
{
    public enum AirSlingReticleShape
    {
        Diamond,
        Square,
        Hexagon,
        Ring
    }

    [CreateAssetMenu(
        fileName = "AirSlingTargetUISettings",
        menuName = "Airflow Prototype/Air Sling Target UI Settings")]
    public sealed class AirSlingTargetUISettings : ScriptableObject
    {
        [Header("Shape")]
        public AirSlingReticleShape shape =
            AirSlingReticleShape.Diamond;

        [Range(0f, 45f)]
        public float shapeAngleOffset = 0f;

        [Range(0.45f, 0.95f)]
        public float outerRadius01 = 0.82f;

        [Range(0.5f, 10f)]
        public float outerThickness = 3.5f;

        [Tooltip("Only used by Ring mode.")]
        [Range(2, 10)]
        public int ringArcCount = 4;

        [Tooltip("Only used by Ring mode.")]
        [Range(12f, 85f)]
        public float ringArcDegrees = 48f;

        [Header("Size")]
        [Min(32f)]
        public float idleSize = 116f;

        [Min(32f)]
        public float chargingSize = 142f;

        [Range(1f, 1.8f)]
        public float perfectBloomScale = 1.34f;

        [Min(0.01f)]
        public float sizeResponse = 13f;

        [Header("Timing Geometry")]
        [Range(0.08f, 0.45f)]
        public float compressionMinRadius01 = 0.17f;

        [Range(0.45f, 0.95f)]
        public float compressionMaxRadius01 = 0.78f;

        [Range(0.5f, 10f)]
        public float compressionThickness = 3.5f;

        [Range(0.5f, 10f)]
        public float perfectBandThickness = 2.5f;

        [Header("Rotation")]
        [Range(-180f, 180f)]
        public float idleRotationSpeed = -18f;

        [Range(-360f, 360f)]
        public float chargingRotationSpeed = -90f;

        [Header("Visibility")]
        [Min(0.01f)]
        public float appearResponse = 14f;

        [Min(0.01f)]
        public float disappearResponse = 18f;

        [Header("Perfect Flash")]
        [Min(0.03f)]
        public float perfectFlashDuration = 0.24f;

        [Min(0.01f)]
        public float perfectFlashResponse = 18f;

        [Header("Colors")]
        public Color idleColor =
            new Color(
                0.72f,
                0.08f,
                0.06f,
                0.78f);

        public Color chargeColor =
            new Color(
                1f,
                0.16f,
                0.10f,
                0.98f);

        public Color perfectBandColor =
            new Color(
                1f,
                0.58f,
                0.18f,
                0.82f);

        public Color perfectColor =
            new Color(
                1f,
                0.95f,
                0.84f,
                1f);

        [Header("Canvas")]
        public int sortingOrder = 510;
    }
}
