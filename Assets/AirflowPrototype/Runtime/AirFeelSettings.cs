using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "AirFeelSettings",
        menuName = "Airflow Prototype/Air Feel Settings")]
    public sealed class AirFeelSettings : ScriptableObject
    {
        [Header("Speed Reference")]
        [Min(1f)] public float maxFeedbackSpeed = 20f;

        [Header("Camera Composition")]
        [Min(0f)] public float lookAheadStartSpeed = 7.5f;
        [Min(0f)] public float lookAheadDistanceAtMaxSpeed = 1.15f;
        [Min(0.01f)] public float lookAheadResponse = 6.5f;

        [Tooltip("Camera pullback is zero below this speed, then ramps toward Camera Pullback At Max Speed.")]
        [Min(0f)] public float cameraPullbackStartSpeed = 8.5f;

        [Min(0f)] public float cameraPullbackAtMaxSpeed = 0.45f;
        [Min(0.01f)] public float cameraPullbackResponse = 7f;

        [Header("Camera Turn Bank")]
        [Tooltip("Below this speed, camera banking is completely disabled.")]
        [Min(0f)] public float cameraTurnRollStartSpeed = 9.5f;

        [Tooltip("At this speed and above, the full configured camera turn roll is available.")]
        [Min(0.01f)] public float cameraTurnRollFullSpeed = 15.5f;

        [Range(0f, 10f)] public float maxCameraTurnRoll = 3.25f;
        [Min(1f)] public float turnRateForMaxCameraRoll = 170f;
        [Min(0.01f)] public float cameraTurnRollResponse = 10f;

        [Header("Character Forward Lean")]
        [Tooltip("Below this speed, forward lean is zero.")]
        [Min(0f)] public float characterForwardLeanStartSpeed = 6.5f;

        [Range(0f, 25f)] public float forwardLeanAtMaxSpeed = 10f;

        [Header("Character Turn Lean")]
        [Tooltip("Below this speed, character turn lean is disabled.")]
        [Min(0f)] public float characterTurnLeanStartSpeed = 7.5f;

        [Tooltip("At this speed and above, the full configured character turn lean is available.")]
        [Min(0.01f)] public float characterTurnLeanFullSpeed = 15.5f;

        [Range(0f, 25f)] public float maxCharacterTurnLean = 9f;
        [Min(1f)] public float turnRateForMaxCharacterLean = 170f;
        [Min(0.01f)] public float characterLeanResponse = 11f;

        [Header("Wind Streaks")]
        [Min(0f)] public float windStartSpeed = 9f;
        [Min(0.1f)] public float windFullSpeed = 18f;
        [Range(4, 40)] public int maxWindStreaks = 22;
        [Min(1f)] public float windTravelSpeed = 25f;
        [Min(0.5f)] public float windSpawnDistance = 8f;
        [Min(0.5f)] public float windSpreadX = 7f;
        [Min(0.5f)] public float windSpreadY = 4.5f;
        [Min(0.01f)] public float minWindLength = 0.15f;
        [Min(0.05f)] public float maxWindLength = 1.35f;
        [Min(0.001f)] public float minWindWidth = 0.008f;
        [Min(0.001f)] public float maxWindWidth = 0.022f;
        public Color windColor = new Color(0.82f, 0.95f, 1f, 0.55f);

        private void OnValidate()
        {
            maxFeedbackSpeed = Mathf.Max(1f, maxFeedbackSpeed);

            cameraPullbackStartSpeed =
                Mathf.Min(cameraPullbackStartSpeed, maxFeedbackSpeed);

            cameraTurnRollFullSpeed =
                Mathf.Max(
                    cameraTurnRollStartSpeed + 0.01f,
                    cameraTurnRollFullSpeed);

            characterForwardLeanStartSpeed =
                Mathf.Min(characterForwardLeanStartSpeed, maxFeedbackSpeed);

            characterTurnLeanFullSpeed =
                Mathf.Max(
                    characterTurnLeanStartSpeed + 0.01f,
                    characterTurnLeanFullSpeed);

            windFullSpeed =
                Mathf.Max(
                    windFullSpeed,
                    windStartSpeed + 0.1f);

            maxFeedbackSpeed =
                Mathf.Max(
                    maxFeedbackSpeed,
                    windFullSpeed);
        }
    }
}
