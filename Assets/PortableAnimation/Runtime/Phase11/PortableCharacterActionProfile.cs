using System;
using System.Collections.Generic;
using UnityEngine;

namespace PortableAnimation
{
    [Serializable]
    public sealed class PortableCharacterAction
    {
        public string id = "Action";
        public AnimationClip clip;
        [Tooltip("Optional bone mask. Empty means whole body; provide an AvatarMask for upper-body-only actions.")]
        public AvatarMask mask;
        [Min(0.001f)] public float blendInSeconds = 0.08f;
        [Min(0.001f)] public float blendOutSeconds = 0.14f;
        [Min(0)] public int priority;
        [Tooltip("When false, this action is rejected if a higher-priority action is currently playing.")]
        public bool allowSameActionRestart;
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
                if (action.clip == null || action.clip.length <= 0f)
                    throw new InvalidOperationException("Action '" + action.id +
                        "' has no usable animation clip.");
                if (action.blendInSeconds <= 0f || action.blendOutSeconds <= 0f)
                    throw new InvalidOperationException("Invalid fade timing for " + action.id);
            }
        }
    }
}
