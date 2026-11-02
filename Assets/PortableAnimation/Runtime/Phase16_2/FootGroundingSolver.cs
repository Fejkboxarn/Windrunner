using System;
using UnityEngine;

namespace PortableAnimation
{
    /// <summary>
    /// Post-PlayableGraph humanoid grounding. Contacts are in the SHOE rather
    /// than the skeleton: one at the toe outsole and one at the heel tip.
    /// The ankle bone is the IK end effector, NOT the ground contact point.
    /// Reuses the prior foot-release / velocity hysteresis and adds a bounded
    /// heel-to-toe support-line leveling correction when both are planted.
    /// No AnimationJob, graph ownership, or extra Animator is introduced.
    /// </summary>
    public sealed class FootGroundingSolver
    {
        private sealed class Leg
        {
            public Transform Thigh, Shin, Foot, Toes;
            public bool IsLeft, Initialized, Calibrated, Locked, ToeFound, HeelFound;
            public Vector3 ToeLocal, HeelLocal;
            public Vector3 RawToe, RawHeel, RawContact, PreviousContact;
            public Vector3 TargetSole, LockSole, BlendStart, AnkleTarget;
            public Vector3 GroundNormal;
            public Quaternion AnimatedFootRotation, DesiredFootRotation, LevelingDelta;
            public float ToeHeight, HeelHeight, Height;
            public float Weight, PlantAge, AnchorBlend, Cooldown;
            public float VerticalSpeed, PlanarSpeed;
            public FootSupportContact Contact;
            public RaycastHit ToeHit, HeelHit;
            public readonly RaycastHit[] ProbeHits = new RaycastHit[12];
        }

        private readonly FootGroundingProfile settings;
        private readonly Transform root, pelvis;
        private readonly Leg left, right;
        private float pelvisOffset;

        public bool LeftPlanted => left.Locked;
        public bool RightPlanted => right.Locked;
        public float LeftWeight => left.Weight;
        public float RightWeight => right.Weight;
        public float PelvisOffset => pelvisOffset;
        public Vector3 LeftTarget => left.TargetSole;
        public Vector3 RightTarget => right.TargetSole;
        public Vector3 LeftRawSole => left.RawContact;
        public Vector3 RightRawSole => right.RawContact;
        public Vector3 LeftAnkleTarget => left.AnkleTarget;
        public Vector3 RightAnkleTarget => right.AnkleTarget;
        public bool LeftGroundDetected => left.ToeFound || left.HeelFound;
        public bool RightGroundDetected => right.ToeFound || right.HeelFound;
        public float LeftPlanarSpeed => left.PlanarSpeed;
        public float RightPlanarSpeed => right.PlanarSpeed;
        public bool LeftHasToes => left.Toes != null;
        public bool RightHasToes => right.Toes != null;
        public Vector3 LeftToeSupport => left.RawToe;
        public Vector3 RightToeSupport => right.RawToe;
        public Vector3 LeftHeelSupport => left.RawHeel;
        public Vector3 RightHeelSupport => right.RawHeel;
        public FootSupportContact LeftContact => left.Contact;
        public FootSupportContact RightContact => right.Contact;

        public FootGroundingSolver(Animator animator, FootGroundingProfile profile)
        {
            if (animator == null || !animator.isHuman || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("A valid Humanoid Avatar is required.");
            settings = profile ?? throw new ArgumentNullException(nameof(profile));
            root = animator.transform;
            pelvis = animator.GetBoneTransform(HumanBodyBones.Hips);
            left = MakeLeg(animator, true, HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                HumanBodyBones.LeftToes);
            right = MakeLeg(animator, false, HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
                HumanBodyBones.RightToes);
            if (pelvis == null || !left.Shin.IsChildOf(left.Thigh) ||
                !left.Foot.IsChildOf(left.Shin) ||
                !right.Shin.IsChildOf(right.Thigh) ||
                !right.Foot.IsChildOf(right.Shin))
                throw new InvalidOperationException("Humanoid leg hierarchy is incomplete.");
        }

        private static Leg MakeLeg(Animator animator, bool isLeft,
            HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones foot,
            HumanBodyBones toe)
        {
            var result = new Leg
            {
                IsLeft = isLeft,
                Thigh = animator.GetBoneTransform(upper),
                Shin = animator.GetBoneTransform(lower),
                Foot = animator.GetBoneTransform(foot),
                Toes = animator.GetBoneTransform(toe)
            };
            if (result.Thigh == null || result.Shin == null || result.Foot == null)
                throw new InvalidOperationException("Missing leg bones for " + upper);
            return result;
        }

        public void Reset()
        {
            ResetLeg(left);
            ResetLeg(right);
            pelvisOffset = 0f;
        }

        private static void ResetLeg(Leg leg)
        {
            leg.Locked = false;
            leg.Initialized = false;
            leg.Calibrated = false;
            leg.Weight = 0f;
            leg.Contact = FootSupportContact.None;
            leg.PlantAge = 0f;
            leg.Cooldown = 0f;
            leg.LevelingDelta = Quaternion.identity;
        }

        public void Apply(float dt, bool eligible, bool enabled)
        {
            dt = Mathf.Clamp(dt, 0f, 0.05f);
            bool allowed = eligible && enabled;
            Prepare(left, dt, allowed);
            Prepare(right, dt, allowed);

            float pelvisGoal = 0f;
            bool any = false;
            CollectPelvis(left, ref pelvisGoal, ref any);
            CollectPelvis(right, ref pelvisGoal, ref any);
            pelvisOffset = Mathf.MoveTowards(pelvisOffset,
                pelvisGoal * settings.PelvisStrength, settings.PelvisSpeed * dt);
            if (Mathf.Abs(pelvisOffset) > 0.00001f)
                pelvis.position += Vector3.up * pelvisOffset;

            if (allowed)
            {
                ApplyLeg(left);
                ApplyLeg(right);
            }
        }

        private void Calibrate(Leg leg)
        {
            ShoeSupportCalibration saved = leg.IsLeft
                ? settings.LeftSupportCalibration : settings.RightSupportCalibration;
            if (!saved.Valid)
                saved = FootSupportGeometry.Estimate(leg.Foot.position,
                    leg.Foot.rotation, leg.Toes == null ? Vector3.zero :
                    leg.Toes.position, settings.EstimateSolesFromToes && leg.Toes != null,
                    settings.ToeTipForwardMeters, settings.SoleDepth,
                    settings.HeelBackFromAnkleMeters,
                    settings.HeelTipHeightAdjustment, settings.AnkleHeight);

            Vector3 common = leg.IsLeft
                ? settings.LeftSoleLocalAdjustment : settings.RightSoleLocalAdjustment;
            leg.ToeLocal = saved.ToeLocal + common + (leg.IsLeft
                ? settings.LeftToeTipLocalAdjustment :
                    settings.RightToeTipLocalAdjustment);
            leg.HeelLocal = saved.HeelLocal + common + (leg.IsLeft
                ? settings.LeftHeelTipLocalAdjustment :
                    settings.RightHeelTipLocalAdjustment);
            leg.LevelingDelta = Quaternion.identity;
            leg.Calibrated = true;
        }

        private void Prepare(Leg leg, float dt, bool eligible)
        {
            if (!leg.Calibrated) Calibrate(leg);
            Vector3 ankle = leg.Foot.position;
            leg.AnimatedFootRotation = leg.Foot.rotation;
            leg.DesiredFootRotation = leg.AnimatedFootRotation;
            leg.RawToe = FootSupportGeometry.WorldPoint(ankle,
                leg.AnimatedFootRotation, leg.ToeLocal);
            leg.RawHeel = FootSupportGeometry.WorldPoint(ankle,
                leg.AnimatedFootRotation, leg.HeelLocal);

            leg.ToeFound = Probe(leg, leg.RawToe, out leg.ToeHit);
            bool useHeel = settings.SupportMode == ShoeSupportMode.HeelAndToe;
            leg.HeelFound = useHeel && Probe(leg, leg.RawHeel, out leg.HeelHit);
            leg.ToeHeight = leg.ToeFound ? leg.RawToe.y -
                (leg.ToeHit.point.y + settings.SoleClearance) : float.PositiveInfinity;
            leg.HeelHeight = leg.HeelFound ? leg.RawHeel.y -
                (leg.HeelHit.point.y + settings.SoleClearance) : float.PositiveInfinity;

            bool heightsCompatible = leg.ToeFound && leg.HeelFound &&
                Mathf.Abs(leg.ToeHit.point.y - leg.HeelHit.point.y) <=
                settings.MaximumDualGroundHeightDifference;

            FootSupportContact candidate = FootSupportGeometry.Choose(
                leg.ToeFound, heightsCompatible,
                leg.ToeHeight, leg.HeelHeight, settings.PlantHeight, useHeel);

            // Once contact is active, keep the same support reference through
            // release. Changing a reference point mid-contact causes a pop.
            FootSupportContact active = leg.Locked || leg.Weight > 0.00001f
                ? leg.Contact : candidate;
            if (active == FootSupportContact.None)
                active = FootSupportContact.Toe;
            Vector3 localAnchor = AnchorLocal(leg, active);
            Vector3 rawAnchor = FootSupportGeometry.WorldPoint(ankle,
                leg.AnimatedFootRotation, localAnchor);
            Vector3 previousTarget = leg.TargetSole;
            leg.RawContact = rawAnchor;
            leg.TargetSole = rawAnchor;
            leg.AnkleTarget = ankle;
            leg.Cooldown = Mathf.Max(0f, leg.Cooldown - dt);

            bool hadPrevious = leg.Initialized;
            if (hadPrevious && dt > 0.00001f)
            {
                Vector3 velocity = (rawAnchor - leg.PreviousContact) / dt;
                float a = FootContactMath.ExponentialResponse(
                    dt, settings.VelocityFilterSeconds);
                leg.VerticalSpeed = Mathf.Lerp(leg.VerticalSpeed, velocity.y, a);
                leg.PlanarSpeed = Mathf.Lerp(leg.PlanarSpeed,
                    Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude, a);
            }
            else
            {
                leg.VerticalSpeed = 0f;
                leg.PlanarSpeed = 0f;
            }
            leg.PreviousContact = rawAnchor;
            leg.Initialized = true;
            leg.Height = ActiveHeight(leg, active);
            bool hasGround = ActiveGround(leg, active);
            float drift = leg.Locked ? Vector3.ProjectOnPlane(
                rawAnchor - leg.LockSole, Vector3.up).magnitude : 0f;

            bool release = leg.Locked && FootContactMath.ShouldRelease(
                eligible, hasGround, leg.Height, drift,
                settings.ReleaseHeight, settings.MaxHorizontalLock);
            if (release)
            {
                bool hardRelease = !eligible || !hasGround ||
                    drift >= settings.MaxHorizontalLock ||
                    leg.Height >= settings.ReleaseHeight * 1.65f;
                if (hardRelease || leg.PlantAge >= settings.MinimumPlantTime)
                {
                    leg.Locked = false;
                    leg.Cooldown = 0.06f;
                }
            }

            if (leg.Locked) leg.PlantAge += dt;
            if (!leg.Locked && leg.Cooldown <= 0f && hadPrevious &&
                candidate != FootSupportContact.None &&
                FootContactMath.ShouldPlantStable(eligible, hasGround,
                    leg.Height, leg.VerticalSpeed, leg.PlanarSpeed,
                    settings.PlantHeight, settings.MaxPlantVerticalSpeed,
                    settings.MaxPlantPlanarSpeed))
            {
                // On first acquire candidate can differ from the old anchor.
                // Recompute velocity/anchor next frame without popping pose.
                if (active != candidate && leg.Weight <= 0.00001f)
                {
                    active = candidate;
                    localAnchor = AnchorLocal(leg, active);
                    rawAnchor = FootSupportGeometry.WorldPoint(ankle,
                        leg.AnimatedFootRotation, localAnchor);
                    leg.RawContact = rawAnchor;
                    leg.PreviousContact = rawAnchor;
                }
                leg.Contact = active;
                leg.BlendStart = leg.Weight > 0.001f
                    ? previousTarget : rawAnchor;
                float groundY = ActiveGroundY(leg, active);
                leg.LockSole = FootContactMath.GroundSoleTarget(rawAnchor,
                    groundY, settings.SoleClearance);
                leg.GroundNormal = ActiveNormal(leg, active);
                leg.Locked = true;
                leg.PlantAge = 0f;
                leg.AnchorBlend = 0f;
            }

            leg.Weight = Mathf.MoveTowards(leg.Weight,
                leg.Locked && eligible ? settings.IKStrength : 0f,
                (leg.Locked ? settings.FootWeightSpeed :
                    settings.ReleaseWeightSpeed) * dt);

            if (leg.Locked)
            {
                leg.AnchorBlend = Mathf.MoveTowards(leg.AnchorBlend, 1f,
                    dt / Mathf.Max(0.01f, settings.AnchorTransitionSeconds));
                float t = leg.AnchorBlend;
                t = t * t * (3f - 2f * t);
                leg.TargetSole = Vector3.Lerp(leg.BlendStart,
                    leg.LockSole, t);
            }
            else if (leg.Weight > 0.00001f)
            {
                float alpha = 1f - Mathf.Clamp01(leg.Weight /
                    Mathf.Max(0.001f, settings.IKStrength));
                leg.TargetSole = Vector3.Lerp(leg.LockSole,
                    rawAnchor, alpha);
            }
            else
            {
                leg.Contact = FootSupportContact.None;
            }

            if (leg.Contact == FootSupportContact.Both && leg.Locked &&
                leg.ToeFound && leg.HeelFound)
            {
                Quaternion correction = FootSupportGeometry.SupportLevelRotation(
                    leg.RawHeel, leg.RawToe, leg.GroundNormal,
                    settings.MaxSupportLinePitchCorrection);
                correction = Quaternion.Slerp(Quaternion.identity,
                    correction, settings.SupportLineLevelingStrength);
                leg.LevelingDelta = correction;
            }
            else if (leg.Weight <= 0.00001f ||
                leg.Contact != FootSupportContact.Both)
            {
                leg.LevelingDelta = Quaternion.identity;
            }
            // Preserve the last leveling delta while IK influence fades;
            // otherwise the shoe rotation itself would snap on release.
            leg.DesiredFootRotation = leg.LevelingDelta *
                leg.AnimatedFootRotation;
            leg.AnkleTarget = FootSupportGeometry.AnkleFromPoint(
                leg.TargetSole, leg.DesiredFootRotation,
                AnchorLocal(leg, leg.Contact));
        }

        private static Vector3 AnchorLocal(Leg leg, FootSupportContact contact)
        {
            return contact == FootSupportContact.Heel ||
                contact == FootSupportContact.Both ? leg.HeelLocal : leg.ToeLocal;
        }

        private static bool ActiveGround(Leg leg, FootSupportContact contact)
        {
            switch (contact)
            {
                case FootSupportContact.Heel: return leg.HeelFound;
                case FootSupportContact.Both: return leg.HeelFound && leg.ToeFound;
                default: return leg.ToeFound;
            }
        }

        private static float ActiveHeight(Leg leg, FootSupportContact contact)
        {
            switch (contact)
            {
                case FootSupportContact.Heel: return leg.HeelHeight;
                case FootSupportContact.Both: return Mathf.Max(leg.ToeHeight, leg.HeelHeight);
                default: return leg.ToeHeight;
            }
        }

        private static float ActiveGroundY(Leg leg, FootSupportContact contact)
        {
            return contact == FootSupportContact.Toe ? leg.ToeHit.point.y :
                leg.HeelHit.point.y;
        }

        private static Vector3 ActiveNormal(Leg leg, FootSupportContact contact)
        {
            if (contact == FootSupportContact.Both)
                return (leg.ToeHit.normal + leg.HeelHit.normal).normalized;
            return contact == FootSupportContact.Toe
                ? leg.ToeHit.normal : leg.HeelHit.normal;
        }

        private bool Probe(Leg leg, Vector3 sole, out RaycastHit closestHit)
        {
            closestHit = default(RaycastHit);
            Vector3 origin = sole + Vector3.up * settings.ProbeHeight;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down,
                leg.ProbeHits, settings.ProbeDistance,
                settings.GroundLayers, QueryTriggerInteraction.Ignore);
            bool found = false;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = leg.ProbeHits[i];
                if (hit.collider == null ||
                    hit.collider.transform.IsChildOf(root)) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
                closestHit = hit;
                found = true;
            }
            return found;
        }

        private void CollectPelvis(Leg leg, ref float goal, ref bool any)
        {
            if (leg.Weight <= 0.00001f || !ActiveGround(leg, leg.Contact))
                return;
            float correction = FootContactMath.PelvisTarget(
                leg.RawContact.y, leg.TargetSole.y,
                settings.MaxPelvisDrop, settings.MaxPelvisRaise);
            correction *= Mathf.Clamp01(leg.Weight /
                Mathf.Max(0.001f, settings.IKStrength));
            goal = any ? Mathf.Min(goal, correction) : correction;
            any = true;
        }

        private void ApplyLeg(Leg leg)
        {
            if (leg.Weight <= 0.00001f) return;
            Vector3 hip = leg.Thigh.position;
            Vector3 knee = leg.Shin.position;
            Vector3 ankle = leg.Foot.position;
            float upper = Vector3.Distance(hip, knee);
            float lower = Vector3.Distance(knee, ankle);
            Vector3 ankleTarget = leg.AnkleTarget;
            if (!IKGeometry.CalculateElbow(hip, ankleTarget, knee,
                upper, lower, settings.MaxReachFraction,
                out Vector3 reached, out Vector3 desiredKnee)) return;
            Vector3 fromUpper = knee - hip;
            Vector3 toUpper = desiredKnee - hip;
            if (fromUpper.sqrMagnitude <= 0.000001f ||
                toUpper.sqrMagnitude <= 0.000001f) return;
            Quaternion targetThigh = Quaternion.FromToRotation(
                fromUpper, toUpper) * leg.Thigh.rotation;
            leg.Thigh.rotation = Quaternion.Slerp(
                leg.Thigh.rotation, targetThigh, leg.Weight);
            Vector3 fromShin = leg.Foot.position - leg.Shin.position;
            Vector3 toShin = reached - leg.Shin.position;
            if (fromShin.sqrMagnitude > 0.000001f &&
                toShin.sqrMagnitude > 0.000001f)
            {
                Quaternion targetShin = Quaternion.FromToRotation(
                    fromShin, toShin) * leg.Shin.rotation;
                leg.Shin.rotation = Quaternion.Slerp(
                    leg.Shin.rotation, targetShin, leg.Weight);
            }
            // Counteract the unintended world rotation inherited from IK
            // parents, then apply ONLY the bounded support-line leveling.
            leg.Foot.rotation = Quaternion.Slerp(
                leg.AnimatedFootRotation, leg.DesiredFootRotation, leg.Weight);
        }
    }
}
