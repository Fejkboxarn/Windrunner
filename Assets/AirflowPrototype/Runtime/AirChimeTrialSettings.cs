using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "AirChimeTrialSettings",
        menuName = "Airflow Prototype/Air Chime Trial Settings")]
    public sealed class AirChimeTrialSettings : ScriptableObject
    {
        [Header("Trial")]
        [Min(0f)] public float groundedResetGrace = 0.55f;
        [Min(0.01f)] public float groundedResetConfirmTime = 0.10f;
        public bool resetWhenGrounded = true;

        [Header("Chime")]
        [Min(0.25f)] public float chimeRadius = 1.15f;
        [Min(0.05f)] public float triggerDepth = 0.75f;
        [Range(12, 96)] public int ringSegments = 48;
        [Min(0.001f)] public float ringWidth = 0.045f;

        [Header("Visual Motion")]
        [Min(0f)] public float idleRotationSpeed = 18f;
        [Min(0f)] public float activeRotationSpeed = 70f;
        [Range(0f, 0.5f)] public float activePulseAmount = 0.14f;
        [Min(0.1f)] public float activePulseSpeed = 4.5f;
        [Min(0.01f)] public float stateResponse = 10f;

        [Header("Colors")]
        public Color inactiveColor = new Color(0.42f, 0.64f, 0.72f, 0.22f);
        public Color activeColor = new Color(0.72f, 0.95f, 1f, 0.95f);
        public Color completedColor = new Color(0.56f, 0.90f, 1f, 0.36f);
        public Color finalColor = new Color(0.92f, 1f, 1f, 1f);

        [Header("Start Seal")]
        [Min(0.5f)] public float startSealRadius = 1.15f;
        [Min(0.01f)] public float startSealRingWidth = 0.06f;

        [Header("Procedural Audio")]
        public bool enableProceduralChimeAudio = true;
        [Range(0f, 1f)] public float chimeVolume = 0.42f;
        [Range(0f, 1f)] public float completionVolume = 0.62f;
        [Min(110f)] public float baseFrequency = 520f;
        [Min(0.1f)] public float chimeDuration = 0.34f;
    }
}
