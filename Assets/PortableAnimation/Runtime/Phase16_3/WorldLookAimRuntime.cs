using System;
using System.Collections.Generic;
using PortableAnimation.Authoring;
using UnityEngine;

namespace PortableAnimation
{
    /// <summary>Pure authoring rule evaluator; no MonoBehaviour, Playable or animation jobs.</summary>
    public static class Phase1634WorldLookRules
    {
        // Matches Batch 16.3.1's blend windows exactly. A missing specific phase
        // inherits WholeClip; all opt-out flags are false on legacy assets.
        public static float PhasePermission(VisualActionEntry action,
            AuthoringMovementContext movement, float time)
        {
            if (action == null) return 1f;
            float fallback = Permit(action, movement, AuthoringActionPhase.WholeClip, 1f);
            float windup = Permit(action, movement, AuthoringActionPhase.Windup, fallback);
            float active = Permit(action, movement, AuthoringActionPhase.Active, fallback);
            float recovery = Permit(action, movement, AuthoringActionPhase.Recovery, fallback);
            float a = Mathf.Clamp(action.windupEnd, 0.01f, 0.94f);
            float b = Mathf.Clamp(action.activeEnd, a + 0.05f, 0.99f);
            float half = Mathf.Min(0.04f, (b - a) * 0.25f);
            float t = Mathf.Clamp01(time);
            if (t < a - half) return windup;
            if (t < a + half)
                return Mathf.Lerp(windup, active,
                    Mathf.SmoothStep(0f, 1f, (t - a + half) / (2f * half)));
            if (t < b - half) return active;
            if (t < b + half)
                return Mathf.Lerp(active, recovery,
                    Mathf.SmoothStep(0f, 1f, (t - b + half) / (2f * half)));
            return recovery;
        }

        private static float Permit(VisualActionEntry action,
            AuthoringMovementContext movement, AuthoringActionPhase phase, float fallback)
        {
            if (action.contexts == null) return fallback;
            foreach (var entry in action.contexts)
            {
                if (entry != null && entry.movementContext == movement && entry.phase == phase)
                    return entry.suppressWorldLookAim ? 0f : 1f;
            }
            return fallback;
        }

        // Position OR rotation occupied by the action reduces the amount of look
        // influence. Scale is intentionally ignored for head/torso aim.
        public static float RegionAvailability(float position, float rotation,
            float montageWeight, float movementPermission, float phasePermission)
        {
            float occupied = Mathf.Clamp01(Mathf.Max(position, rotation));
            return Mathf.Clamp01(movementPermission) * Mathf.Clamp01(phasePermission) *
                   (1f - Mathf.Clamp01(montageWeight) * occupied);
        }

        public static float SmoothFactor(float dt, float seconds) =>
            dt <= 0f ? 0f : 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, seconds));
    }

    /// <summary>
    /// Visual preview solver executed AFTER PlayableGraph evaluation, once per frame.
    /// Samples rendered Humanoid pose, adds a bounded world-target look correction
    /// and fades each body-region weight. Does not create a custom animation job.
    /// </summary>
    public sealed class Phase1634WorldLookSolver
    {
        private readonly Transform character;
        private readonly Animator animator;
        private readonly List<LookJoint> joints = new List<LookJoint>(5);
        private readonly VisualActionAuthoringDatabase database;
        private readonly LiveActionMaskEvaluator liveMasks;
        private readonly ActionMontageProfile montage;
        private float yawSmooth;
        private float pitchSmooth;
        private readonly Transform headTransform;
        private readonly Vector3 neutralFaceLocalForward;
        public float CurrentHeadInfluence { get; private set; }
        public float MaximumLookWeight { get; private set; }
        public float CurrentYaw => yawSmooth;
        public float CurrentPitch => pitchSmooth;

        private sealed class LookJoint
        {
            public Transform bone;
            public string path;
            public int index;
            public float filteredAvailability;
        }

        public Phase1634WorldLookSolver(Animator source,
            VisualActionAuthoringDatabase authoring,
            LiveActionMaskEvaluator masks, ActionMontageProfile montageProfile)
        {
            if (source == null || !source.isHuman)
                throw new InvalidOperationException("World Look Aim needs a Humanoid Animator.");
            animator = source;
            character = source.transform;
            database = authoring ?? throw new ArgumentNullException(nameof(authoring));
            liveMasks = masks;
            montage = montageProfile ?? throw new ArgumentNullException(nameof(montageProfile));
            Add(HumanBodyBones.Spine, 0);
            Add(HumanBodyBones.Chest, 1);
            Add(HumanBodyBones.UpperChest, 2);
            Add(HumanBodyBones.Neck, 3);
            Add(HumanBodyBones.Head, 4);
            headTransform = source.GetBoneTransform(HumanBodyBones.Head);
            if (headTransform == null)
                throw new InvalidOperationException("World Look Aim needs a mapped Humanoid Head.");
            // Calibrate the local direction that was facing the character's
            // forward when the graph was bound. Never assume Head.forward is
            // anatomically pointing out of the character's face.
            neutralFaceLocalForward = headTransform.InverseTransformDirection(character.forward);
            if (joints.Count < 2)
                throw new InvalidOperationException("World Look Aim needs Head and at least one torso bone.");
        }

        private void Add(HumanBodyBones bone, int index)
        {
            var t = animator.GetBoneTransform(bone);
            if (t == null) return; // UpperChest is optional in many valid Humanoid rigs.
            var joint = new LookJoint { bone = t, path = RelativePath(character, t), index = index };
            joints.Add(joint);
        }

        public void Apply(Vector3 worldTarget, float dt, AuthoringMovementContext movement,
            string montageId, float montageTime, float montageLength,
            float montageWeight, bool heroBlocksLook, bool useAuthoredMasks,
            bool externallyEnabled)
        {
            var settings = database.worldLook;
            if (settings == null) settings = new WorldLookAimSettings();
            bool globallyAllowed = settings.enabled && externallyEnabled &&
                                   database.WorldLookAllowed(movement) &&
                                   !heroBlocksLook;

            int slot = -1;
            if (!string.IsNullOrEmpty(montageId) && montageId != "None")
                for (int i = 0; i < montage.Actions.Count; i++)
                    if (string.Equals(montage.Actions[i].Id, montageId, StringComparison.Ordinal))
                    { slot = i; break; }
            float normalizedTime = montageLength > 0.001f ? Mathf.Clamp01(montageTime / montageLength) : 0f;
            VisualActionEntry action = slot >= 0 && liveMasks != null ? liveMasks.FindAction(slot) : null;
            float phaseAllowed = Phase1634WorldLookRules.PhasePermission(action, movement, normalizedTime);
            float speed = Phase1634WorldLookRules.SmoothFactor(dt, settings.smoothingSeconds);

            // Target direction is measured in WORLD SPACE and converted into the
            // character's root-facing frame. Horizontal and vertical rotations
            // remain independent to avoid the look direction becoming unstable.
            Vector3 origin = headTransform.position;
            Vector3 toTarget = worldTarget - origin;
            Vector3 rootForward = Vector3.ProjectOnPlane(character.forward, Vector3.up);
            if (rootForward.sqrMagnitude < 1e-5f) rootForward = Vector3.forward;
            Quaternion facing = Quaternion.LookRotation(rootForward.normalized, Vector3.up);
            Vector3 local = Quaternion.Inverse(facing) * toTarget;
            bool validDirection = toTarget.sqrMagnitude > 0.0025f;
            // Aim from the ACTUAL evaluated head pose, not merely the root
            // forward axis. This lets idle/walk/attack poses look at the same
            // point even when their authored head orientation differs.
            Vector3 currentFacing = Quaternion.Inverse(facing) *
                headTransform.TransformDirection(neutralFaceLocalForward);
            float desiredYaw = validDirection ? Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg : 0f;
            float desiredPitch = validDirection ? Mathf.Atan2(local.y,
                Mathf.Sqrt(local.x * local.x + local.z * local.z)) * Mathf.Rad2Deg : 0f;
            float currentYaw = Mathf.Atan2(currentFacing.x, currentFacing.z) * Mathf.Rad2Deg;
            float currentPitch = Mathf.Atan2(currentFacing.y,
                Mathf.Sqrt(currentFacing.x * currentFacing.x + currentFacing.z * currentFacing.z)) * Mathf.Rad2Deg;
            float targetYaw = Mathf.Clamp(Mathf.DeltaAngle(currentYaw,
                desiredYaw + settings.facingYawOffset), -settings.maxYaw, settings.maxYaw);
            float targetPitch = Mathf.Clamp(desiredPitch + settings.facingPitchOffset -
                currentPitch, -settings.maxPitch, settings.maxPitch);
            yawSmooth = Mathf.LerpAngle(yawSmooth, targetYaw, speed);
            pitchSmooth = Mathf.Lerp(pitchSmooth, targetPitch, speed);

            float sum = 0f;
            for (int j = 0; j < joints.Count; j++)
                sum += Distribution(settings, joints[j].index);
            sum = Mathf.Max(0.001f, sum);

            CurrentHeadInfluence = 0f;
            MaximumLookWeight = 0f;
            foreach (var joint in joints)
            {
                float avail = globallyAllowed && validDirection ? 1f : 0f;
                if (avail > 0f && slot >= 0 && montageWeight > 0f)
                {
                    // Fall back to the ACTUAL static runtime policy if this
                    // region has no authored live value. Never assume unmasked.
                    var policy = montage.Actions[slot].Policy.Resolve(
                        montage.BaseProfile.RegionMap.Resolve(joint.path));
                    BoneChannelWeights actionMask = !useAuthoredMasks || liveMasks == null ? policy :
                        liveMasks.Resolve(slot, joint.path, AuthoringContextMapper.ToContext(movement),
                            normalizedTime, policy);
                    avail = Phase1634WorldLookRules.RegionAvailability(
                        actionMask.position, actionMask.rotation, montageWeight, 1f, phaseAllowed);
                }
                else
                    avail *= phaseAllowed;

                joint.filteredAvailability = Mathf.Lerp(joint.filteredAvailability,
                    Mathf.Clamp01(avail), speed);
                MaximumLookWeight = Mathf.Max(MaximumLookWeight, joint.filteredAvailability);
                if (joint.index == 4) CurrentHeadInfluence = joint.filteredAvailability;
                float fraction = Distribution(settings, joint.index) / sum;
                float yaw = yawSmooth * fraction * joint.filteredAvailability;
                float pitch = pitchSmooth * fraction * joint.filteredAvailability;
                // World-up yaw and character-right pitch are applied over the
                // evaluated base pose, largest (torso) bones first.
                Quaternion correction = Quaternion.AngleAxis(yaw, Vector3.up) *
                    Quaternion.AngleAxis(-pitch, facing * Vector3.right);
                joint.bone.rotation = correction * joint.bone.rotation;
            }
        }

        private static float Distribution(WorldLookAimSettings s, int i)
        {
            switch (i)
            {
                case 0: return Mathf.Clamp01(s.lowerSpine);
                case 1: return Mathf.Clamp01(s.middleSpine);
                case 2: return Mathf.Clamp01(s.upperSpine);
                case 3: return Mathf.Clamp01(s.neck);
                default: return Mathf.Clamp01(s.head);
            }
        }

        private static string RelativePath(Transform root, Transform t)
        {
            if (t == root) return "";
            var stack = new Stack<string>();
            while (t != null && t != root) { stack.Push(t.name); t = t.parent; }
            return t == root ? string.Join("/", stack.ToArray()) : "";
        }
    }
}
