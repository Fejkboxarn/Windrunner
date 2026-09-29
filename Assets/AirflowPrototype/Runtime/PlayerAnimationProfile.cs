using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "PlayerAnimationProfile",
        menuName = "Airflow Prototype/Player Animation Profile")]
    public sealed class PlayerAnimationProfile : ScriptableObject
    {
        public const string EmptyPlaceholderName = "AF_Empty";
        public const string IdlePlaceholderName = "AF_Idle";
        public const string LocomotionPlaceholderName = "AF_Locomotion";
        public const string JumpPlaceholderName = "AF_Jump";
        public const string FallPlaceholderName = "AF_Fall";
        public const string LandPlaceholderName = "AF_Land";
        public const string FlipAPlaceholderName = "AF_FlipA";
        public const string FlipBPlaceholderName = "AF_FlipB";
        public const string FlipCPlaceholderName = "AF_FlipC";

        [Header("Drop Your Clips Here")]
        public AnimationClip idle;
        public AnimationClip locomotion;
        public AnimationClip jump;
        public AnimationClip fall;
        public AnimationClip land;

        [InspectorName("Front Flip")]
        [Tooltip("Optional. Leave empty to use the improved procedural 360 front flip.")]
        public AnimationClip flipA;

        [HideInInspector] public AnimationClip flipB;
        [HideInInspector] public AnimationClip flipC;

        [Header("Playback")]
        [Min(0.1f)] public float locomotionReferenceSpeed = 7f;
        [Min(0.05f)] public float minimumLocomotionPlayback = 0.55f;
        [Min(0.05f)] public float maximumLocomotionPlayback = 2.2f;
        [Range(0f, 0.35f)] public float crossFadeDuration = 0.08f;
        [Range(0.1f, 1f)] public float landingLockFraction = 0.72f;

        [SerializeField, HideInInspector]
        private RuntimeAnimatorController templateController;

        public RuntimeAnimatorController TemplateController =>
            templateController;

        public bool HasIdle => idle != null;
        public bool HasLocomotion => locomotion != null;
        public bool HasJump => jump != null;
        public bool HasFall => fall != null;
        public bool HasLand => land != null;

        public bool HasFlip(int variant)
        {
            return variant == 0 && flipA != null;
        }

        public AnimationClip GetFlip(int variant)
        {
            return variant == 0 ? flipA : null;
        }

        public void SetTemplateController(
            RuntimeAnimatorController controller)
        {
            templateController = controller;
        }

        private void OnValidate()
        {
            locomotionReferenceSpeed =
                Mathf.Max(0.1f, locomotionReferenceSpeed);

            minimumLocomotionPlayback =
                Mathf.Max(0.05f, minimumLocomotionPlayback);

            maximumLocomotionPlayback =
                Mathf.Max(
                    minimumLocomotionPlayback,
                    maximumLocomotionPlayback);
        }
    }
}
