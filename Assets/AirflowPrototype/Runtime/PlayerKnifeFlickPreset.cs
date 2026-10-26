using UnityEngine;

namespace AirflowPrototype
{
    public enum PlayerKnifeFlickPhase
    {
        Ready = 0,
        Prep = 1,
        Strike = 2,
        FollowThrough = 3
    }

    [CreateAssetMenu(
        fileName = "KnifeFlickPreset",
        menuName = "Airflow Prototype/Knife Flick Preset")]
    public sealed class PlayerKnifeFlickPreset : ScriptableObject
    {
        [Header("Hand Selection")]
        [Tooltip("Randomly chooses a hand but avoids immediately repeating the same hand.")]
        public bool avoidImmediateHandRepeat = true;

        [Range(0f, 100f)]
        [Tooltip("Chance to choose the right hand when both hands are available.")]
        public float rightHandChancePercent = 50f;

        [Header("Charge / Ready")]
        [Tooltip("Maximum amount of the Ready pose blended in while LMB is held.")]
        [Range(0f, 1f)]
        public float chargeReadyWeight = 0.30f;

        [Min(0.01f)]
        public float chargePoseResponse = 12f;

        [Header("Throw Timing")]
        [Min(0.01f)]
        public float prepDuration = 0.045f;

        [Min(0.01f)]
        public float strikeDuration = 0.070f;

        [Min(0.01f)]
        public float followThroughDuration = 0.135f;

        [Min(0.01f)]
        public float recoveryDuration = 0.140f;

        [Tooltip("Seconds after release when the visible knife leaves the hand. Keep this early for the Hela-like flick.")]
        [Min(0f)]
        public float knifeReleaseTime = 0.070f;

        [Tooltip("Seconds after release when the held knife first becomes visible.")]
        [Min(0f)]
        public float knifeMaterializeTime = 0.020f;

        [Header("Pose Strength")]
        [Range(0f, 1f)]
        public float chestWeight = 0.55f;

        [Range(0f, 1f)]
        public float armAimWeight = 1f;

        [Range(5f, 180f)]
        [Tooltip("Maximum world-space upper-arm correction used to follow the procedural hand path.")]
        public float maximumUpperArmCorrection = 115f;

        [Header("READY - Right Hand Reference")]
        [Tooltip("Desired hand direction from the shoulder in character-local space. Left throws mirror X automatically.")]
        public Vector3 readyHandDirection =
            new Vector3(0.58f, 0.08f, -0.28f);

        public float readyChestYaw = -2f;
        public float readyForearmSnap = 8f;
        public float readyWristSnap = -6f;

        [Header("PREP - Right Hand Reference")]
        public Vector3 prepHandDirection =
            new Vector3(0.78f, 0.14f, -0.48f);

        public float prepChestYaw = -5f;
        public float prepForearmSnap = 24f;
        public float prepWristSnap = -12f;

        [Header("STRIKE - Right Hand Reference")]
        public Vector3 strikeHandDirection =
            new Vector3(0.24f, 0.06f, 1.00f);

        public float strikeChestYaw = 5f;
        public float strikeForearmSnap = -20f;
        public float strikeWristSnap = -42f;

        [Header("FOLLOW THROUGH - Right Hand Reference")]
        public Vector3 followHandDirection =
            new Vector3(-0.20f, -0.10f, 1.06f);

        public float followChestYaw = 8f;
        public float followForearmSnap = -8f;
        public float followWristSnap = -24f;

        [Header("Thrown Knife Visual")]
        [Min(0.03f)]
        public float visualTravelDuration = 0.11f;

        [Min(0.001f)]
        public float trailWidth = 0.028f;

        public Color trailColor =
            new Color(0.72f, 0.94f, 1f, 0.95f);

        [Min(0f)]
        public float spinDegreesPerSecond = 1800f;

        [Header("Curves")]
        [Tooltip("Controls how quickly the strike pose attacks. 0..1 time to 0..1 pose progress.")]
        public AnimationCurve strikeCurve =
            new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.22f, 0.12f),
                new Keyframe(0.62f, 0.90f),
                new Keyframe(1f, 1f, 0f, 0f));

        [Tooltip("Controls follow-through progression.")]
        public AnimationCurve followCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Controls recovery back to the underlying locomotion animation.")]
        public AnimationCurve recoveryCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private void OnValidate()
        {
            rightHandChancePercent =
                Mathf.Clamp(rightHandChancePercent, 0f, 100f);

            chargeReadyWeight =
                Mathf.Clamp01(chargeReadyWeight);

            chargePoseResponse =
                Mathf.Max(0.01f, chargePoseResponse);

            prepDuration =
                Mathf.Max(0.01f, prepDuration);

            strikeDuration =
                Mathf.Max(0.01f, strikeDuration);

            followThroughDuration =
                Mathf.Max(0.01f, followThroughDuration);

            recoveryDuration =
                Mathf.Max(0.01f, recoveryDuration);

            knifeReleaseTime =
                Mathf.Max(0f, knifeReleaseTime);

            knifeMaterializeTime =
                Mathf.Max(0f, knifeMaterializeTime);

            maximumUpperArmCorrection =
                Mathf.Clamp(
                    maximumUpperArmCorrection,
                    5f,
                    180f);

            visualTravelDuration =
                Mathf.Max(0.03f, visualTravelDuration);

            trailWidth =
                Mathf.Max(0.001f, trailWidth);

            spinDegreesPerSecond =
                Mathf.Max(0f, spinDegreesPerSecond);
        }
    }
}
