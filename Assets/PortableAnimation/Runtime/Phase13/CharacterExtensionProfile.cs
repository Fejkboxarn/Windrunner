using System;
using System.Collections.Generic;
using UnityEngine;

namespace PortableAnimation
{
    public enum CharacterMotionDomain { Any, Grounded, Airborne, Flight }

    public enum CharacterExtensionRequestResult
    {
        Started, AlreadyActive, BlockedByPriority, InvalidDomain, Unknown
    }

    [Serializable]
    public sealed class CharacterMotionEntry
    {
        [SerializeField] private string id;
        [SerializeField] private AnimationClip clip;
        [SerializeField] private CharacterMotionDomain domain;
        [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;
        [SerializeField, Min(0f)] private float blendInSeconds = 0.18f;
        [SerializeField, Min(0f)] private float blendOutSeconds = 0.22f;
        [SerializeField, Min(0f)] private float autoExitSeconds;
        [SerializeField] private bool loop = true;
        [SerializeField] private int priority = 10;
        [SerializeField] private bool interruptible = true;

        public string Id => id;
        public AnimationClip Clip => clip;
        public CharacterMotionDomain Domain => domain;
        public float PlaybackSpeed => playbackSpeed;
        public float BlendIn => blendInSeconds;
        public float BlendOut => blendOutSeconds;
        public float AutoExitSeconds => autoExitSeconds;
        public bool Loop => loop;
        public int Priority => priority;
        public bool Interruptible => interruptible;

        public CharacterMotionEntry(string id, AnimationClip clip,
            CharacterMotionDomain domain, int priority, bool interruptible,
            bool loop, float autoExitSeconds, float blendIn = 0.18f,
            float blendOut = 0.22f, float playbackSpeed = 1f)
        {
            this.id = id;
            this.clip = clip;
            this.domain = domain;
            this.priority = priority;
            this.interruptible = interruptible;
            this.loop = loop;
            this.autoExitSeconds = autoExitSeconds;
            blendInSeconds = blendIn;
            blendOutSeconds = blendOut;
            this.playbackSpeed = playbackSpeed;
        }

        public void AssertValid()
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("Character extension ID is empty.");
            if (clip == null || clip.length <= 0f)
                throw new InvalidOperationException("Character extension clip missing/empty: " + id);
            if (playbackSpeed <= 0f || blendInSeconds < 0f || blendOutSeconds < 0f || autoExitSeconds < 0f)
                throw new InvalidOperationException("Invalid timing for character extension: " + id);
            if (!loop && autoExitSeconds <= 0f)
                throw new InvalidOperationException("Non-looping extension needs auto-exit timing: " + id);
        }
    }

    [CreateAssetMenu(menuName = "Portable Animation/Phase 13 Character Extensions",
        fileName = "CharacterExtensionProfile")]
    public sealed class CharacterExtensionProfile : ScriptableObject
    {
        [SerializeField] private AeroMotionProfile previousPipeline;
        [SerializeField] private string archetypeId = "CustomCharacter";
        [SerializeField] private List<CharacterMotionEntry> entries =
            new List<CharacterMotionEntry>();

        public AeroMotionProfile PreviousPipeline => previousPipeline;
        public string ArchetypeId => archetypeId;
        public IReadOnlyList<CharacterMotionEntry> Entries => entries;

        public void Configure(AeroMotionProfile baseProfile, string name,
            params CharacterMotionEntry[] extensions)
        {
            previousPipeline = baseProfile;
            archetypeId = name;
            entries = extensions == null
                ? new List<CharacterMotionEntry>()
                : new List<CharacterMotionEntry>(extensions);
        }

        public int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < entries.Count; i++)
                if (string.Equals(entries[i].Id, id, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        public void AssertValid()
        {
            if (previousPipeline == null)
                throw new InvalidOperationException("Character extensions require a Phase 12 profile.");
            previousPipeline.AssertValid();
            if (string.IsNullOrWhiteSpace(archetypeId) || entries == null || entries.Count == 0)
                throw new InvalidOperationException("Character extension profile requires a name and entries.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null) throw new InvalidOperationException("Null character extension entry.");
                entry.AssertValid();
                if (!ids.Add(entry.Id))
                    throw new InvalidOperationException("Duplicate character extension ID: " + entry.Id);
            }
        }
    }
}
