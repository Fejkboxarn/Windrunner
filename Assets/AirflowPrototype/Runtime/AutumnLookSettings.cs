using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "AutumnLookSettings",
        menuName = "Airflow Prototype/Autumn Look Settings")]
    public sealed class AutumnLookSettings : ScriptableObject
    {
        [Header("Color Grade")]
        [Range(-100f, 100f)]
        public float temperature = 32f;

        [Range(-100f, 100f)]
        public float tint = 7f;

        [Range(-10f, 10f)]
        public float postExposure = 0.08f;

        [Range(-100f, 100f)]
        public float contrast = 18f;

        [Range(-180f, 180f)]
        public float hueShift = -4f;

        [Range(-100f, 100f)]
        public float saturation = 14f;

        [Tooltip("Warm multiplication over the final scene.")]
        public Color colorFilter =
            new Color(
                1f,
                0.88f,
                0.74f,
                1f);

        [Header("Bloom")]
        public bool bloomEnabled = true;

        [Min(0f)]
        public float bloomIntensity = 0.58f;

        [Min(0f)]
        public float bloomThreshold = 0.88f;

        [Range(0f, 1f)]
        public float bloomScatter = 0.72f;

        public Color bloomTint =
            new Color(
                1f,
                0.86f,
                0.68f,
                1f);

        [Header("Vignette")]
        public bool vignetteEnabled = true;

        [Range(0f, 1f)]
        public float vignetteIntensity = 0.22f;

        [Range(0.01f, 1f)]
        public float vignetteSmoothness = 0.48f;

        public Color vignetteColor =
            new Color(
                0.11f,
                0.045f,
                0.018f,
                1f);

        [Header("Film Grain")]
        public bool filmGrainEnabled = true;

        [Range(0f, 1f)]
        public float filmGrainIntensity = 0.10f;

        [Range(0f, 1f)]
        public float filmGrainResponse = 0.72f;

        [Header("Lens Character")]
        public bool chromaticAberrationEnabled = true;

        [Range(0f, 1f)]
        public float chromaticAberration = 0.035f;

        public bool lensDistortionEnabled = true;

        [Range(-1f, 1f)]
        public float lensDistortion = -0.035f;

        [Range(0f, 1f)]
        public float lensDistortionX = 1f;

        [Range(0f, 1f)]
        public float lensDistortionY = 1f;

        [Range(0.5f, 1.5f)]
        public float lensScale = 1.015f;

        [Header("Tonemapping")]
        public bool useACES = true;
    }
}
