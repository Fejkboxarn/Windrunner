using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "SpeedPostProcessSettings",
        menuName = "Airflow Prototype/Speed Post Process Settings")]
    public sealed class SpeedPostProcessSettings : ScriptableObject
    {
        [Header("Activation")]
        public bool enabled = true;

        [Tooltip("Below this planar speed, the speed lens is inactive.")]
        [Min(0f)]
        public float startSpeed = 8.5f;

        [Tooltip("At this planar speed, all speed effects reach their maximum.")]
        [Min(0.1f)]
        public float fullSpeed = 18f;

        [Tooltip("How quickly the visual effect reacts to speed changes.")]
        [Min(0.1f)]
        public float response = 6.5f;

        [Header("Motion Blur")]
        public bool motionBlurEnabled = true;

        [Tooltip("Maximum URP motion-blur intensity at Full Speed.")]
        [Range(0f, 1f)]
        public float motionBlurIntensity = 0.34f;

        [Tooltip("Maximum motion-vector length URP considers. Lower is cheaper/subtler.")]
        [Range(0f, 0.2f)]
        public float motionBlurClamp = 0.055f;

        [Header("Peripheral Tunnel")]
        public bool speedVignetteEnabled = true;

        [Tooltip("Additional vignette intensity added at Full Speed.")]
        [Range(0f, 0.6f)]
        public float additionalVignette = 0.15f;

        [Tooltip("Higher values push the vignette farther toward the screen edge.")]
        [Range(0.01f, 1f)]
        public float speedVignetteSmoothness = 0.58f;

        [Header("Speed Lens Distortion")]
        public bool speedLensDistortionEnabled = true;

        [Tooltip("Additional distortion at Full Speed. Negative values give a subtle tunnel/wide-angle pull.")]
        [Range(-0.5f, 0.5f)]
        public float additionalLensDistortion = -0.11f;

        [Tooltip("Extra zoom used to hide distorted screen edges.")]
        [Range(0f, 0.15f)]
        public float additionalLensScale = 0.025f;

        [Header("Speed Chromatic Aberration")]
        public bool speedChromaticAberrationEnabled = true;

        [Range(0f, 0.5f)]
        public float additionalChromaticAberration = 0.12f;

        [Header("Speed Bloom")]
        public bool speedBloomEnabled = true;

        [Tooltip("Small bloom increase at high speed to make air/reward effects feel brighter.")]
        [Range(0f, 1.5f)]
        public float additionalBloom = 0.18f;

        [Header("Debug")]
        [Tooltip("Useful while tuning. Forces the speed lens to Full Speed in Play Mode.")]
        public bool previewAtFullStrength;
    }
}
