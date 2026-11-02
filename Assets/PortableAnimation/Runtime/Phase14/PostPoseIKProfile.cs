using System;
using System.Collections.Generic;
using UnityEngine;

namespace PortableAnimation
{
    public enum IKArm { Left, Right }
    public enum IKChainMode { SingleBone, TwoBone }

    [Serializable]
    public sealed class IKArmDefinition
    {
        [SerializeField] private IKArm side;
        [SerializeField] private IKChainMode mode;
        [SerializeField] private string upperPath;
        [SerializeField] private string lowerPath;
        [SerializeField] private string handPath;
        [SerializeField] private Vector3 localAimAxis = Vector3.down;
        [SerializeField, Min(0.01f)] private float virtualTipLength = 0.7f;
        [SerializeField, Range(0f, 180f)] private float maxSingleBoneTurn = 85f;
        [SerializeField, Range(0.7f, 0.999f)] private float maxReachFraction = 0.995f;
        [SerializeField, Range(0f, 1f)] private float handOrientationWeight = 0f;

        public IKArm Side => side;
        public IKChainMode Mode => mode;
        public string UpperPath => upperPath;
        public string LowerPath => lowerPath;
        public string HandPath => handPath;
        public Vector3 LocalAimAxis => localAimAxis;
        public float VirtualTipLength => virtualTipLength;
        public float MaxSingleBoneTurn => maxSingleBoneTurn;
        public float MaxReachFraction => maxReachFraction;
        public float HandOrientationWeight => handOrientationWeight;

        public IKArmDefinition(IKArm side, IKChainMode mode, string upper,
            string lower, string hand, Vector3 axis, float tipLength,
            float maxTurn, float reach, float handOrientationWeight)
        {
            this.side = side;
            this.mode = mode;
            upperPath = upper;
            lowerPath = lower;
            handPath = hand;
            localAimAxis = axis;
            virtualTipLength = tipLength;
            maxSingleBoneTurn = maxTurn;
            maxReachFraction = reach;
            this.handOrientationWeight = handOrientationWeight;
        }

        public void AssertValid()
        {
            if (string.IsNullOrWhiteSpace(upperPath))
                throw new InvalidOperationException("IK upper arm path is empty: " + side);
            if (localAimAxis.sqrMagnitude < 0.0001f || virtualTipLength <= 0f)
                throw new InvalidOperationException("Invalid aim axis/tip length: " + side);
            if (mode == IKChainMode.TwoBone &&
                (string.IsNullOrWhiteSpace(lowerPath) || string.IsNullOrWhiteSpace(handPath)))
                throw new InvalidOperationException("Two-bone IK needs elbow and hand paths: " + side);
        }
    }

    [Serializable]
    public sealed class IKWeaponRule
    {
        [SerializeField] private WeaponPoseKind kind;
        [SerializeField, Range(0f, 1f)] private float leftWeight;
        [SerializeField, Range(0f, 1f)] private float rightWeight;
        public WeaponPoseKind Kind => kind;
        public float LeftWeight => leftWeight;
        public float RightWeight => rightWeight;
        public IKWeaponRule(WeaponPoseKind kind, float left, float right)
        {
            this.kind = kind;
            leftWeight = Mathf.Clamp01(left);
            rightWeight = Mathf.Clamp01(right);
        }
    }

    [CreateAssetMenu(menuName = "Portable Animation/Phase 14 Post-Pose IK",
        fileName = "PostPoseIKProfile")]
    public sealed class PostPoseIKProfile : ScriptableObject
    {
        [SerializeField] private CharacterExtensionProfile previousPipeline;
        [SerializeField] private IKArmDefinition leftArm;
        [SerializeField] private IKArmDefinition rightArm;
        [SerializeField] private List<IKWeaponRule> rules = new List<IKWeaponRule>();
        [SerializeField, Min(0.1f)] private float weightSpeed = 4f;
        [SerializeField, Range(0f, 1f)] private float montageMultiplier = 0.2f;
        [SerializeField] private bool suppressDuringBlockingHeroState = true;

        public CharacterExtensionProfile PreviousPipeline => previousPipeline;
        public IKArmDefinition LeftArm => leftArm;
        public IKArmDefinition RightArm => rightArm;
        public IReadOnlyList<IKWeaponRule> Rules => rules;
        public float WeightSpeed => weightSpeed;
        public float MontageMultiplier => montageMultiplier;
        public bool SuppressDuringBlockingHeroState => suppressDuringBlockingHeroState;

        public void Configure(CharacterExtensionProfile previous, IKArmDefinition left,
            IKArmDefinition right, params IKWeaponRule[] equipmentRules)
        {
            previousPipeline = previous;
            leftArm = left;
            rightArm = right;
            rules = equipmentRules == null ? new List<IKWeaponRule>() :
                new List<IKWeaponRule>(equipmentRules);
        }

        public float GetWeight(WeaponPoseKind kind, IKArm side)
        {
            foreach (var rule in rules)
                if (rule.Kind == kind)
                    return side == IKArm.Left ? rule.LeftWeight : rule.RightWeight;
            return 0f;
        }

        public void AssertValid()
        {
            if (previousPipeline == null)
                throw new InvalidOperationException("Phase 14 requires a Phase 13 profile.");
            previousPipeline.AssertValid();
            if (leftArm == null || rightArm == null)
                throw new InvalidOperationException("Both IK arm definitions are required.");
            leftArm.AssertValid();
            rightArm.AssertValid();
            if (leftArm.Side != IKArm.Left || rightArm.Side != IKArm.Right)
                throw new InvalidOperationException("IK arm sides are incorrectly configured.");
            if (rules == null || rules.Count == 0 || weightSpeed <= 0f)
                throw new InvalidOperationException("IK equipment policies/speed missing.");
            var seen = new HashSet<WeaponPoseKind>();
            foreach (var rule in rules)
            {
                if (rule == null || !seen.Add(rule.Kind))
                    throw new InvalidOperationException("Null or duplicate IK equipment rule.");
            }
        }
    }
}
