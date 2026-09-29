using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "PlayerHumanoidAnimationProfile",
        menuName = "Airflow Prototype/Player Humanoid Animation Profile")]
    public sealed class PlayerHumanoidAnimationProfile : ScriptableObject
    {
        [Header("Grounded Locomotion")]
        public AnimationClip idle;
        public AnimationClip walk;
        public AnimationClip run;

        [Header("Airborne")]
        public AnimationClip jump;
        public AnimationClip fall;
        public AnimationClip land;

        [Header("Sling / Death From Above")]
        public AnimationClip slingStart;
        public AnimationClip slingAir;
        public AnimationClip slingImpact;

        [Header("Locomotion Blend Thresholds")]
        [Range(0.05f, 0.8f)]
        public float walkThreshold = 0.35f;

        [Range(0.2f, 1.5f)]
        public float runThreshold = 1f;

        [Header("Transition Feel")]
        [Range(0f, 0.3f)]
        public float locomotionTransitionDuration = 0.08f;

        [Range(0f, 0.3f)]
        public float jumpTransitionDuration = 0.05f;

        [Range(0f, 0.3f)]
        public float fallTransitionDuration = 0.08f;

        [Range(0f, 0.3f)]
        public float landTransitionDuration = 0.04f;

        [Range(0f, 0.3f)]
        public float slingTransitionDuration = 0.04f;

        [Range(0.1f, 1f)]
        public float landExitTime = 0.82f;

        [Range(0.1f, 1f)]
        public float slingImpactExitTime = 0.88f;

        private void OnValidate()
        {
            walkThreshold =
                Mathf.Clamp(
                    walkThreshold,
                    0.05f,
                    0.8f);

            runThreshold =
                Mathf.Max(
                    walkThreshold + 0.05f,
                    runThreshold);

            locomotionTransitionDuration =
                Mathf.Max(
                    0f,
                    locomotionTransitionDuration);

            jumpTransitionDuration =
                Mathf.Max(
                    0f,
                    jumpTransitionDuration);

            fallTransitionDuration =
                Mathf.Max(
                    0f,
                    fallTransitionDuration);

            landTransitionDuration =
                Mathf.Max(
                    0f,
                    landTransitionDuration);

            slingTransitionDuration =
                Mathf.Max(
                    0f,
                    slingTransitionDuration);
        }
    }
}
