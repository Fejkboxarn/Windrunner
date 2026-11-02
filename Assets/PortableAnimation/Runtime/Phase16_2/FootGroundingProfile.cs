using UnityEngine;

namespace PortableAnimation
{
    /// <summary>Pure configuration. Existing serialized settings are preserved.</summary>
    [CreateAssetMenu(menuName = "Portable Animation/Phase 16.2 Foot Grounding", fileName = "FootGroundingProfile")]
    public sealed class FootGroundingProfile : ScriptableObject
    {
        [Header("Ground Probes")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.1f)] private float probeHeight = 0.65f;
        [SerializeField, Min(0.1f)] private float probeDistance = 1.8f;
        [SerializeField, Range(-0.1f, 0.3f)] private float ankleHeight = 0.075f;

        [Header("NEW - Sole Contact Calibration (not ankle joint)")]
        [Tooltip("Use Humanoid toes to estimate sole height and foot-forward direction.")]
        [SerializeField] private bool estimateSolesFromToes = true;
        [Tooltip("Fraction of ankle-to-toe distance in XZ used as sole contact.")]
        [SerializeField, Range(0f, 1f)] private float toeForwardFraction = 0.62f;
        [Tooltip("Additional distance below the toe joint to place the contact on the sole.")]
        [SerializeField, Range(0f, 0.15f)] private float soleDepth = 0.025f;
        [Tooltip("Small distance of locked sole above the collider.")]
        [SerializeField, Range(0f, 0.08f)] private float soleClearance = 0.008f;
        [Tooltip("Fine-tuning adjustment in foot local space, right and left may differ.")]
        [SerializeField] private Vector3 leftSoleLocalAdjustment = Vector3.zero;
        [SerializeField] private Vector3 rightSoleLocalAdjustment = Vector3.zero;

        [Header("Heel / Toe Support Model — Batch 16.2.2")]
        [Tooltip("FlatFoot uses a single toe/sole anchor. HeelAndToe tracks the heel tip AND front outsole.")]
        [SerializeField] private ShoeSupportMode shoeSupportMode = ShoeSupportMode.HeelAndToe;
        [Tooltip("Additional forward distance from the toe joint to the actual shoe contact point.")]
        [SerializeField, Range(0f, 0.20f)] private float toeTipForwardMeters = 0.04f;
        [Tooltip("Horizontal distance from the ankle pivot BACK to the heel tip.")]
        [SerializeField, Range(0.01f, 0.35f)] private float heelBackFromAnkleMeters = 0.10f;
        [Tooltip("Difference in height between heel tip and toe contact DURING CALIBRATION. Set 0 for the horizontal orange support line.")]
        [SerializeField, Range(-0.15f, 0.15f)] private float heelTipHeightAdjustment = 0f;
        [Tooltip("Saved in foot-bone local space. Calibrate in an editor rest/idle pose, then fine-tune local offsets below.")]
        [SerializeField] private ShoeSupportCalibration leftSupportCalibration;
        [SerializeField] private ShoeSupportCalibration rightSupportCalibration;
        [SerializeField] private Vector3 leftToeTipLocalAdjustment;
        [SerializeField] private Vector3 rightToeTipLocalAdjustment;
        [SerializeField] private Vector3 leftHeelTipLocalAdjustment;
        [SerializeField] private Vector3 rightHeelTipLocalAdjustment;
        [Tooltip("While both points touch, smoothly level the shoe's heel-to-toe support line against ground.")]
        [SerializeField, Range(0f, 1f)] private float supportLineLevelingStrength = 0.7f;
        [SerializeField, Range(0f, 35f)] private float maxSupportLinePitchCorrection = 14f;
        [Tooltip("How far apart the two sampled ground heights may be to accept a dual-support plant.")]
        [SerializeField, Range(0.005f, 0.3f)] private float maximumDualGroundHeightDifference = 0.06f;

        [Header("Plant / Release")]
        [SerializeField, Range(0.005f, 0.3f)] private float plantHeight = 0.09f;
        [SerializeField, Range(0.01f, 0.5f)] private float releaseHeight = 0.17f;
        [SerializeField, Range(0.05f, 1f)] private float maxHorizontalLock = 0.40f;
        [SerializeField, Min(0f)] private float maxPlantVerticalSpeed = 2.5f;
        [SerializeField, Range(0.05f, 5f)] private float maxPlantPlanarSpeed = 0.95f;
        [SerializeField, Range(0f, 0.3f)] private float minimumPlantTime = 0.09f;
        [SerializeField, Range(0f, 1f)] private float legIKStrength = 0.88f;
        [SerializeField, Min(0.1f)] private float footWeightSpeed = 8f;
        [SerializeField, Min(0.1f)] private float releaseWeightSpeed = 5.5f;
        [SerializeField, Range(0.01f, 0.3f)] private float anchorTransitionSeconds = 0.08f;
        [SerializeField, Range(0.01f, 0.3f)] private float velocityFilterSeconds = 0.06f;
        [SerializeField, Range(0.90f, 0.999f)] private float maxReachFraction = 0.997f;
        [Header("Pelvis")]
        [SerializeField, Range(0f, 0.25f)] private float maxPelvisDrop = 0.10f;
        [SerializeField, Range(0f, 0.25f)] private float maxPelvisRaise = 0.06f;
        [SerializeField, Range(0f, 1f)] private float pelvisStrength = 0.45f;
        [SerializeField, Min(0.1f)] private float pelvisSpeed = 4f;
        [Header("In-place physics test (no root motion)")]
        [SerializeField, Range(0.2f, 6f)] private float forwardMetersPerSecond = 2.5f;
        [SerializeField, Range(0.2f, 6f)] private float sidewaysMetersPerSecond = 1.8f;
        [SerializeField, Range(0f, 1f)] private float jumpWindupSeconds = 0.62f;
        [SerializeField, Range(0.5f, 8f)] private float jumpVelocity = 4.2f;
        [SerializeField, Range(1f, 30f)] private float gravity = 9.5f;
        [SerializeField, Range(0.5f, 5f)] private float maxTravelFromOrigin = 2.7f;

        public ShoeSupportMode SupportMode => shoeSupportMode;
        public float ToeTipForwardMeters => toeTipForwardMeters;
        public float HeelBackFromAnkleMeters => heelBackFromAnkleMeters;
        public float HeelTipHeightAdjustment => heelTipHeightAdjustment;
        public float SupportLineLevelingStrength => supportLineLevelingStrength;
        public float MaxSupportLinePitchCorrection => maxSupportLinePitchCorrection;
        public float MaximumDualGroundHeightDifference => maximumDualGroundHeightDifference;
        public ShoeSupportCalibration LeftSupportCalibration => leftSupportCalibration;
        public ShoeSupportCalibration RightSupportCalibration => rightSupportCalibration;
        public Vector3 LeftToeTipLocalAdjustment => leftToeTipLocalAdjustment;
        public Vector3 RightToeTipLocalAdjustment => rightToeTipLocalAdjustment;
        public Vector3 LeftHeelTipLocalAdjustment => leftHeelTipLocalAdjustment;
        public Vector3 RightHeelTipLocalAdjustment => rightHeelTipLocalAdjustment;

        public void SetSupportCalibration(bool isLeft, ShoeSupportCalibration calibration)
        {
            if (isLeft) leftSupportCalibration = calibration;
            else rightSupportCalibration = calibration;
        }

        public LayerMask GroundLayers => groundLayers;
        public float ProbeHeight => probeHeight;
        public float ProbeDistance => probeDistance;
        public float AnkleHeight => Mathf.Max(0f, ankleHeight); // fallback only
        public bool EstimateSolesFromToes => estimateSolesFromToes;
        public float ToeForwardFraction => toeForwardFraction;
        public float SoleDepth => soleDepth;
        public float SoleClearance => soleClearance;
        public Vector3 LeftSoleLocalAdjustment => leftSoleLocalAdjustment;
        public Vector3 RightSoleLocalAdjustment => rightSoleLocalAdjustment;
        public float PlantHeight => plantHeight;
        public float ReleaseHeight => Mathf.Max(plantHeight + 0.01f, releaseHeight);
        public float MaxHorizontalLock => maxHorizontalLock;
        public float MaxPlantVerticalSpeed => maxPlantVerticalSpeed;
        public float MaxPlantPlanarSpeed => maxPlantPlanarSpeed;
        public float MinimumPlantTime => minimumPlantTime;
        public float IKStrength => legIKStrength;
        public float FootWeightSpeed => footWeightSpeed;
        public float ReleaseWeightSpeed => releaseWeightSpeed;
        public float AnchorTransitionSeconds => anchorTransitionSeconds;
        public float VelocityFilterSeconds => velocityFilterSeconds;
        public float MaxReachFraction => maxReachFraction;
        public float MaxPelvisDrop => maxPelvisDrop;
        public float MaxPelvisRaise => maxPelvisRaise;
        public float PelvisStrength => pelvisStrength;
        public float PelvisSpeed => pelvisSpeed;
        public float ForwardMetersPerSecond => forwardMetersPerSecond;
        public float SidewaysMetersPerSecond => sidewaysMetersPerSecond;
        public float JumpWindupSeconds => jumpWindupSeconds;
        public float JumpVelocity => jumpVelocity;
        public float Gravity => gravity;
        public float MaxTravelFromOrigin => maxTravelFromOrigin;
    }
}
