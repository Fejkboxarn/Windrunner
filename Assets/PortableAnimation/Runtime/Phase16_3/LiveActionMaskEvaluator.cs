using System;
using System.Collections.Generic;
using PortableAnimation.Authoring;
using UnityEngine;

namespace PortableAnimation
{
    /// <summary>
    /// Ordinary C# authoring-data reader. Applies fine-grained humanoid region
    /// settings by transform path, before Batch 10's grouped action job.
    /// No graph modifications, AnimationJobs or native allocations.
    /// </summary>
    public sealed class LiveActionMaskEvaluator
    {
        private readonly VisualActionAuthoringDatabase database;
        private readonly Dictionary<string, AuthoringBodyRegion> pathRegions =
            new Dictionary<string, AuthoringBodyRegion>(StringComparer.Ordinal);
        private readonly ActionMontageProfile montage;

        private static readonly (HumanBodyBones bone, AuthoringBodyRegion region)[] Mapping =
        {
            (HumanBodyBones.Hips, AuthoringBodyRegion.Pelvis),
            (HumanBodyBones.Spine, AuthoringBodyRegion.SpineLower),
            (HumanBodyBones.Chest, AuthoringBodyRegion.SpineMid),
            (HumanBodyBones.UpperChest, AuthoringBodyRegion.SpineUpper),
            (HumanBodyBones.Neck, AuthoringBodyRegion.Neck),
            (HumanBodyBones.Head, AuthoringBodyRegion.Head),
            (HumanBodyBones.LeftShoulder, AuthoringBodyRegion.LeftClavicle),
            (HumanBodyBones.LeftUpperArm, AuthoringBodyRegion.LeftUpperArm),
            (HumanBodyBones.LeftLowerArm, AuthoringBodyRegion.LeftForearm),
            (HumanBodyBones.LeftHand, AuthoringBodyRegion.LeftHand),
            (HumanBodyBones.RightShoulder, AuthoringBodyRegion.RightClavicle),
            (HumanBodyBones.RightUpperArm, AuthoringBodyRegion.RightUpperArm),
            (HumanBodyBones.RightLowerArm, AuthoringBodyRegion.RightForearm),
            (HumanBodyBones.RightHand, AuthoringBodyRegion.RightHand),
            (HumanBodyBones.LeftUpperLeg, AuthoringBodyRegion.LeftUpperLeg),
            (HumanBodyBones.LeftLowerLeg, AuthoringBodyRegion.LeftLowerLeg),
            (HumanBodyBones.LeftFoot, AuthoringBodyRegion.LeftFoot),
            (HumanBodyBones.LeftToes, AuthoringBodyRegion.LeftToes),
            (HumanBodyBones.RightUpperLeg, AuthoringBodyRegion.RightUpperLeg),
            (HumanBodyBones.RightLowerLeg, AuthoringBodyRegion.RightLowerLeg),
            (HumanBodyBones.RightFoot, AuthoringBodyRegion.RightFoot),
            (HumanBodyBones.RightToes, AuthoringBodyRegion.RightToes)
        };

        public LiveActionMaskEvaluator(Animator animator,
            VisualActionAuthoringDatabase source, ActionMontageProfile actionProfile)
        {
            database = source ?? throw new ArgumentNullException(nameof(source));
            montage = actionProfile ?? throw new ArgumentNullException(nameof(actionProfile));
            if (animator == null || !animator.isHuman)
                throw new InvalidOperationException("Live authoring needs a valid Humanoid Animator.");

            Transform root = animator.transform;
            var attached = new Dictionary<Transform, AuthoringBodyRegion>();
            foreach (var pair in Mapping)
            {
                Transform t = animator.GetBoneTransform(pair.bone);
                if (t != null) attached[t] = pair.region;
            }
            foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
            {
                string path = RelativePath(root, bone);
                AuthoringBodyRegion region = AuthoringBodyRegion.Root;
                Transform parent = bone;
                while (parent != null && parent != root)
                {
                    if (attached.TryGetValue(parent, out region)) break;
                    parent = parent.parent;
                }
                pathRegions[path] = region;
            }
        }

        public BoneChannelWeights Resolve(int slot, string bonePath,
            ContextualMotionState context, float actionTime, BoneChannelWeights fallback)
        {
            if (slot < 0 || slot >= montage.Actions.Count ||
                !pathRegions.TryGetValue(bonePath, out var region))
                return fallback;

            VisualActionEntry action = FindAction(slot);
            if (action == null) return fallback;

            AuthoringMovementContext desired = ToAuthoringContext(context);
            float t = Mathf.Clamp01(actionTime);
            // WholeClip is the baseline; explicit phase entries override it.
            // Smooth blending around authored windup/active boundaries.
            var baseline = PhaseRule(action, desired, AuthoringActionPhase.WholeClip,
                region, t, fallback);
            var windup = PhaseRule(action, desired, AuthoringActionPhase.Windup,
                region, t, baseline);
            var active = PhaseRule(action, desired, AuthoringActionPhase.Active,
                region, t, baseline);
            var recovery = PhaseRule(action, desired, AuthoringActionPhase.Recovery,
                region, t, baseline);

            float a = Mathf.Clamp(action.windupEnd, 0.01f, 0.94f);
            float b = Mathf.Clamp(action.activeEnd, a + 0.05f, 0.99f);
            float half = Mathf.Min(0.04f, (b - a) * 0.25f);
            if (t < a - half) return windup;
            if (t < a + half) return Blend(windup, active,
                Mathf.SmoothStep(0f, 1f, (t - a + half) / (2f * half)));
            if (t < b - half) return active;
            if (t < b + half) return Blend(active, recovery,
                Mathf.SmoothStep(0f, 1f, (t - b + half) / (2f * half)));
            return recovery;
        }

        private static BoneChannelWeights PhaseRule(VisualActionEntry action,
            AuthoringMovementContext state, AuthoringActionPhase phase,
            AuthoringBodyRegion region, float time, BoneChannelWeights fallback)
        {
            foreach (var context in action.contexts)
            {
                if (context == null || context.movementContext != state ||
                    context.phase != phase) continue;
                foreach (var rule in context.regions)
                {
                    if (rule == null || rule.region != region) continue;
                    float envelope = rule.clipInfluence == null || rule.clipInfluence.length == 0
                        ? 1f : Mathf.Clamp01(rule.clipInfluence.Evaluate(time));
                    return new BoneChannelWeights(
                        Mathf.Clamp01(rule.position * envelope),
                        Mathf.Clamp01(rule.rotation * envelope),
                        Mathf.Clamp01(rule.scale * envelope));
                }
            }
            return fallback;
        }

        private static BoneChannelWeights Blend(BoneChannelWeights a,
            BoneChannelWeights b, float t)
        {
            return new BoneChannelWeights(
                Mathf.Lerp(a.position, b.position, t),
                Mathf.Lerp(a.rotation, b.rotation, t),
                Mathf.Lerp(a.scale, b.scale, t));
        }

        public VisualActionEntry FindAction(int slot)
        {
            string id = montage.Actions[slot].Id;
            foreach (var action in database.actions)
                if (action != null && action.runtimeActionIndex == slot)
                    return action;
            foreach (var action in database.actions)
                if (action != null && string.Equals(action.actionId, id, StringComparison.Ordinal))
                    return action;
            return null;
        }

        public static AuthoringMovementContext ToAuthoringContext(ContextualMotionState state)
        {
            switch (state)
            {
                case ContextualMotionState.Starting: return AuthoringMovementContext.Start;
                case ContextualMotionState.Moving: return AuthoringMovementContext.Run;
                case ContextualMotionState.Stopping: return AuthoringMovementContext.Stop;
                case ContextualMotionState.JumpStarting: return AuthoringMovementContext.JumpStart;
                case ContextualMotionState.Rising: return AuthoringMovementContext.Rising;
                case ContextualMotionState.Falling: return AuthoringMovementContext.Falling;
                case ContextualMotionState.Landing: return AuthoringMovementContext.Landing;
                case ContextualMotionState.Hovering: return AuthoringMovementContext.Hover;
                case ContextualMotionState.Flying: return AuthoringMovementContext.Flight;
                case ContextualMotionState.Stunned: return AuthoringMovementContext.Stunned;
                case ContextualMotionState.KnockedDown: return AuthoringMovementContext.KnockedDown;
                case ContextualMotionState.Dead: return AuthoringMovementContext.Dead;
                case ContextualMotionState.Walking: return AuthoringMovementContext.Walk;
                case ContextualMotionState.Running: return AuthoringMovementContext.Run;
                default: return AuthoringMovementContext.Idle;
            }
        }

        private static string RelativePath(Transform root, Transform target)
        {
            if (target == root) return "";
            var parts = new Stack<string>();
            Transform t = target;
            while (t != null && t != root)
            { parts.Push(t.name); t = t.parent; }
            return t == root ? string.Join("/", parts.ToArray()) : "";
        }
    }
}
