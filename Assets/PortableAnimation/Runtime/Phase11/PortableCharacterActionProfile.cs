using System;
using System.Collections.Generic;
using UnityEngine;

namespace PortableAnimation
{
    [Serializable]
    public sealed class PortableCharacterAction
    {
        public string id = "Action";
        [Tooltip("Related phases, e.g. Sling or Cast. Cancellation can target this group.")]
        public string group = "";
        public AnimationClip clip;
        [Tooltip("Optional bone mask. Empty means whole body; provide an AvatarMask for upper-body-only actions.")]
        public AvatarMask mask;
        [Min(0.001f)] public float blendInSeconds = 0.08f;
        [Min(0.001f)] public float blendOutSeconds = 0.14f;
        [Min(0)] public int priority;
        [Tooltip("When false, this action is rejected if a higher-priority action is currently playing.")]
        public bool allowSameActionRestart;
        [Min(0.01f)] public float playbackSpeed = 1f;
        [Tooltip("Loop this phase until its configured end/cancel event.")]
        public bool loopUntilCanceled;
        [Tooltip("When false, only explicit cancellation can interrupt this action.")]
        public bool interruptible = true;

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

        public int Count => actions != null ? actions.Count : 0;
        public PortableCharacterAction Action(int index) => actions[index];

        public void AssertValid()
        {
            if (actions == null)
                throw new InvalidOperationException("Action list is missing.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < actions.Count; i++)
            {
                PortableCharacterAction action = actions[i];
                if (action == null || string.IsNullOrWhiteSpace(action.id))
                    throw new InvalidOperationException("Action " + i + " has no ID.");
                if (!ids.Add(action.id))
                    throw new InvalidOperationException("Duplicate action ID: " + action.id);
                // Unassigned/incompatible actions are safe reserved slots. The
                // Inspector reports them; locomotion and gameplay keep running.
                if (float.IsNaN(action.blendInSeconds) || float.IsInfinity(action.blendInSeconds) ||
                    float.IsNaN(action.blendOutSeconds) || float.IsInfinity(action.blendOutSeconds) ||
                    action.blendInSeconds <= 0f || action.blendOutSeconds <= 0f)
                    throw new InvalidOperationException("Invalid fade timing for " + action.id);
                if (float.IsNaN(action.playbackSpeed) || float.IsInfinity(action.playbackSpeed) || action.playbackSpeed <= 0f)
                    throw new InvalidOperationException("Invalid playback speed for " + action.id);
            }
        }
    }
}
