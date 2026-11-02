using System;
using UnityEngine;

namespace PortableAnimation
{
    /// <summary>
    /// Post-graph Transform corrections. Called once AFTER manual graph.Evaluate,
    /// so it does not add nested AnimationScriptPlayable/GetInputStream jobs.
    /// The next graph evaluation restores the authored animation base pose.
    /// </summary>
    public sealed class PostPoseIKRuntime
    {
        private sealed class Chain
        {
            public IKArmDefinition Profile;
            public Transform Upper, Lower, Hand, Target, Pole;
            public float Weight;
            public bool IsTwoBone => Profile.Mode == IKChainMode.TwoBone;
        }

        private readonly Animator animator;
        private readonly PostPoseIKProfile profile;
        private readonly Chain left;
        private readonly Chain right;
        private bool enabled = true;
        private bool manualOverride;
        private float manualLeft, manualRight;

        public float LeftWeight => left.Weight;
        public float RightWeight => right.Weight;
        public bool Enabled => enabled;
        public bool ManualOverride => manualOverride;
        public bool LeftTwoBone => left.IsTwoBone;
        public bool RightTwoBone => right.IsTwoBone;

        public PostPoseIKRuntime(Animator animator, PostPoseIKProfile profile)
        {
            this.animator = animator ?? throw new ArgumentNullException(nameof(animator));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            profile.AssertValid();
            left = Bind(profile.LeftArm);
            right = Bind(profile.RightArm);
        }

        private Chain Bind(IKArmDefinition settings)
        {
            Transform root = animator.transform;
            var chain = new Chain
            {
                Profile = settings,
                Upper = root.Find(settings.UpperPath)
            };
            if (chain.Upper == null)
                throw new InvalidOperationException("IK bone missing: " + settings.UpperPath);
            if (settings.Mode == IKChainMode.TwoBone)
            {
                chain.Lower = root.Find(settings.LowerPath);
                chain.Hand = root.Find(settings.HandPath);
                if (chain.Lower == null || chain.Hand == null ||
                    !chain.Lower.IsChildOf(chain.Upper) ||
                    !chain.Hand.IsChildOf(chain.Lower))
                    throw new InvalidOperationException("IK requires upper > lower > hand: " +
                        settings.Side);
                if (Vector3.Distance(chain.Upper.position, chain.Lower.position) < 0.001f ||
                    Vector3.Distance(chain.Lower.position, chain.Hand.position) < 0.001f)
                    throw new InvalidOperationException("IK arm has zero-length segment: " +
                        settings.Side);
            }
            return chain;
        }

        public void SetEnabled(bool value) => enabled = value;
        public void SetManualWeights(bool active, float l, float r)
        {
            manualOverride = active;
            manualLeft = Mathf.Clamp01(l);
            manualRight = Mathf.Clamp01(r);
        }

        public void SetTargets(Transform leftTarget, Transform rightTarget,
            Transform leftPole, Transform rightPole)
        {
            ValidateExternal(leftTarget);
            ValidateExternal(rightTarget);
            ValidateExternal(leftPole);
            ValidateExternal(rightPole);
            left.Target = leftTarget; left.Pole = leftPole;
            right.Target = rightTarget; right.Pole = rightPole;
        }

        private void ValidateExternal(Transform t)
        {
            if (t != null && t.IsChildOf(animator.transform))
                throw new InvalidOperationException(
                    "IK target/pole must be outside the Animator rig: " + t.name);
        }

        public void Apply(float deltaTime, WeaponPoseKind weapon,
            bool blockingState, bool montageActive)
        {
            float targetL = manualOverride ? manualLeft :
                profile.GetWeight(weapon, IKArm.Left);
            float targetR = manualOverride ? manualRight :
                profile.GetWeight(weapon, IKArm.Right);
            if (!enabled || (blockingState && profile.SuppressDuringBlockingHeroState))
            { targetL = 0f; targetR = 0f; }
            if (montageActive)
            {
                targetL *= profile.MontageMultiplier;
                targetR *= profile.MontageMultiplier;
            }
            if (left.Target == null || (left.IsTwoBone && left.Pole == null)) targetL = 0f;
            if (right.Target == null || (right.IsTwoBone && right.Pole == null)) targetR = 0f;

            float step = Mathf.Max(0f, deltaTime) * profile.WeightSpeed;
            left.Weight = Mathf.MoveTowards(left.Weight, targetL, step);
            right.Weight = Mathf.MoveTowards(right.Weight, targetR, step);
            Solve(left);
            Solve(right);
        }

        private static void Solve(Chain chain)
        {
            if (chain.Weight <= 0f) return;
            if (chain.IsTwoBone)
                IKGeometry.SolveTwoBone(chain.Upper, chain.Lower, chain.Hand,
                    chain.Target, chain.Pole, chain.Profile.MaxReachFraction,
                    chain.Weight, chain.Profile.HandOrientationWeight);
            else
                IKGeometry.SolveSingleBone(chain.Upper, chain.Target,
                    chain.Profile.LocalAimAxis, chain.Profile.MaxSingleBoneTurn,
                    chain.Weight);
        }

        public float TipError(IKArm side)
        {
            Chain c = side == IKArm.Left ? left : right;
            if (c.Target == null) return 0f;
            Vector3 end = c.IsTwoBone ? c.Hand.position :
                c.Upper.position + c.Upper.TransformDirection(
                    c.Profile.LocalAimAxis.normalized) * c.Profile.VirtualTipLength;
            return Vector3.Distance(end, c.Target.position);
        }
    }
}
