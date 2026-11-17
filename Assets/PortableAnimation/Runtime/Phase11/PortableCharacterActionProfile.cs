using System;
using System.Collections.Generic;
using UnityEngine;

namespace PortableAnimation
{
    public enum ActionMotionContext { GroundedMoving, Stationary, Airborne }

    [Serializable]
    public sealed class ActionPoseOverride
    {
        public bool enabled;
        [Tooltip("An empty or incompatible clip uses the complete base action mapping.")]
        public AnimationClip clip;
        [Tooltip("Empty applies the full-body pose.")]
        public AvatarMask mask;
        [Range(0,1)] public float weight = 1f;
        public bool inheritClip;
        public bool inheritMask, inheritWeight;
        public RegionMaskSettings regions = new RegionMaskSettings();
        public ActionVisualTiming timing = new ActionVisualTiming();
        public bool overrideTiming, muteWhenMissing;
        public VisualTimingFields timingFields=VisualTimingFields.All;
        public bool Available => enabled && clip && clip.humanMotion && !clip.legacy && clip.length > 0;
    }

    [Serializable]
    public sealed class PortableCharacterAction
    {
        public string id = "Action";
        [Tooltip("Related phases, e.g. Sling or Cast. Cancellation can target this group.")]
        public string group = "";
        public AnimationClip clip;
        [Tooltip("Optional bone mask. Empty means whole body; provide an AvatarMask for upper-body-only actions.")]
        public AvatarMask mask;
        [Range(0f, 1f)] public float blendWeight = 1f;
        [Min(0.001f)] public float blendInSeconds = 0.08f;
        [Min(0.001f)] public float blendOutSeconds = 0.14f;
        [Min(0)] public int priority;
        [Tooltip("Allow a request for this same action to restart its clip while it is active. Priority and interruption policy still apply.")]
        public bool allowSameActionRestart;
        [Min(0.01f)] public float playbackSpeed = 1f;
        [Tooltip("Loop this phase until its configured end/cancel event.")]
        public bool loopUntilCanceled;
        [Tooltip("When false, only explicit cancellation can interrupt this action.")]
        public bool interruptible = true;

        public RegionMaskSettings regions = new RegionMaskSettings();
        public ActionVisualTiming segment = new ActionVisualTiming();
        public ActionPoseOverride stationary = new ActionPoseOverride();
        public ActionPoseOverride airborne = new ActionPoseOverride();

        public bool IsPlayable => clip != null && clip.length > 0f && !clip.legacy && clip.humanMotion;
    }

    /// <summary>
    /// Optional, character-local action module. No weapon poses, montage graph,
    /// gameplay dependencies or mandatory action list are required.
    /// </summary>
    [CreateAssetMenu(menuName = "Portable Animation/Character Action Clips",
        fileName = "CharacterActionClips")]
    public sealed class PortableCharacterActionProfile : ScriptableObject
    {
        [SerializeField] private List<PortableCharacterAction> actions =
            new List<PortableCharacterAction>();

        [Min(0)] public float stationaryEnterSpeed = .15f;
        [Min(0)] public float stationaryExitSpeed = .45f;
        [Min(.001f)] public float contextFadeSeconds = .12f;

        public ActionMotionContext SelectContext(bool grounded,float metresPerSecond, ActionMotionContext previous)
        {
            if (!grounded) return ActionMotionContext.Airborne;
            float threshold = previous == ActionMotionContext.Stationary ? stationaryExitSpeed : stationaryEnterSpeed;
            return metresPerSecond <= threshold ? ActionMotionContext.Stationary : ActionMotionContext.GroundedMoving;
        }
        public int Count => actions != null ? actions.Count : 0;
        public PortableCharacterAction Action(int index) => actions[index];

        public void AssertValid()
        {
            if (!(stationaryEnterSpeed >= 0 && stationaryExitSpeed > stationaryEnterSpeed &&
                  !float.IsInfinity(stationaryExitSpeed) && contextFadeSeconds > 0 && !float.IsInfinity(contextFadeSeconds)))
                throw new InvalidOperationException("Invalid action movement thresholds or context fade.");
            if (actions == null)
                throw new InvalidOperationException("Action list is missing.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < actions.Count; i++)
            {
                PortableCharacterAction action = actions[i];
                if (action == null || string.IsNullOrWhiteSpace(action.id))
                    throw new InvalidOperationException("Action " + i + " has no ID.");
                if(action.segment!=null && !action.segment.Valid)throw new InvalidOperationException("Invalid source segment for "+action.id);
                foreach (var variant in new[] {action.stationary, action.airborne})
                    if (variant != null && variant.enabled && (!(variant.weight >= 0 && variant.weight <= 1) || (variant.overrideTiming && (variant.timing==null || !variant.timing.Valid))))
                        throw new InvalidOperationException("Invalid context influence for "+action.id);
                if (!ids.Add(action.id))
                    throw new InvalidOperationException("Duplicate action ID: " + action.id);
                // Unassigned/incompatible actions are safe reserved slots. The
                // Inspector reports them; locomotion and gameplay keep running.
                if (float.IsNaN(action.blendInSeconds) || float.IsInfinity(action.blendInSeconds) ||
                    float.IsNaN(action.blendOutSeconds) || float.IsInfinity(action.blendOutSeconds) ||
                    action.blendInSeconds <= 0f || action.blendOutSeconds <= 0f)
                    throw new InvalidOperationException("Invalid fade timing for " + action.id);
                if (!(action.blendWeight >= 0f && action.blendWeight <= 1f))
                    throw new InvalidOperationException("Invalid action weight for " + action.id);
                if (float.IsNaN(action.playbackSpeed) || float.IsInfinity(action.playbackSpeed) || action.playbackSpeed <= 0f)
                    throw new InvalidOperationException("Invalid playback speed for " + action.id);
            }
        }
    }
}
