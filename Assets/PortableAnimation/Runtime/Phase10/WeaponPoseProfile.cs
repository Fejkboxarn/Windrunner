using System;
using System.Collections.Generic;
using UnityEngine;

namespace PortableAnimation
{
    // The researched PhysicsWeapon node is primarily an equipment-dependent
    // idle-pose overlay, NOT a rigidbody weapon simulation.
    public enum WeaponPoseKind
    {
        Unarmed = 0,
        OneHanded = 1,
        LargeWeapon = 2,
        Shield = 3,
        DualWield = 4
    }

    [Serializable]
    public sealed class WeaponPoseDefinition
    {
        [SerializeField] private WeaponPoseKind kind;
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private BoneWeightMask bodyMask;
        [SerializeField, Min(0f)] private float equipSeconds = 0.3f;
        [SerializeField, Min(0f)] private float unequipSeconds = 0.25f;

        public WeaponPoseKind Kind => kind;
        public AnimationClip IdleClip => idleClip;
        public BoneWeightMask BodyMask => bodyMask;
        public float EquipSeconds => equipSeconds;
        public float UnequipSeconds => unequipSeconds;

        public WeaponPoseDefinition(WeaponPoseKind kind, AnimationClip idle,
            BoneWeightMask mask, float equipSeconds, float unequipSeconds)
        {
            this.kind = kind;
            idleClip = idle;
            bodyMask = mask;
            this.equipSeconds = Mathf.Max(0f, equipSeconds);
            this.unequipSeconds = Mathf.Max(0f, unequipSeconds);
        }

        public void AssertValid()
        {
            if (kind == WeaponPoseKind.Unarmed || idleClip == null || bodyMask == null)
                throw new InvalidOperationException("Invalid weapon pose definition: " + kind);
            if (idleClip.length <= 0f || equipSeconds < 0f || unequipSeconds < 0f)
                throw new InvalidOperationException("Invalid weapon clip or timing: " + kind);
        }
    }

    [CreateAssetMenu(menuName = "Portable Animation/Phase 10 Weapon Pose",
        fileName = "WeaponPoseProfile")]
    public sealed class WeaponPoseProfile : ScriptableObject
    {
        [SerializeField] private HeroStateProfile previousPipeline;
        [SerializeField] private List<WeaponPoseDefinition> poses =
            new List<WeaponPoseDefinition>();
        [SerializeField] private bool weaponFeedsAimOffset = true;

        public HeroStateProfile PreviousPipeline => previousPipeline;
        public IReadOnlyList<WeaponPoseDefinition> Poses => poses;
        public bool WeaponFeedsAimOffset => weaponFeedsAimOffset;

        public void Configure(HeroStateProfile heroProfile,
            params WeaponPoseDefinition[] definitions)
        {
            previousPipeline = heroProfile;
            poses = definitions == null
                ? new List<WeaponPoseDefinition>()
                : new List<WeaponPoseDefinition>(definitions);
            weaponFeedsAimOffset = true;
        }

        public int IndexOf(WeaponPoseKind kind)
        {
            if (kind == WeaponPoseKind.Unarmed) return -1;
            for (int i = 0; i < poses.Count; i++)
                if (poses[i].Kind == kind) return i;
            return -1;
        }

        public void AssertValid()
        {
            if (previousPipeline == null)
                throw new InvalidOperationException("Phase 9 HeroState profile is not assigned.");
            previousPipeline.AssertValid();
            if (poses == null || poses.Count != 4)
                throw new InvalidOperationException("Phase 10 demo expects four weapon poses.");
            var unique = new HashSet<WeaponPoseKind>();
            foreach (var pose in poses)
            {
                if (pose == null) throw new InvalidOperationException("Null weapon pose entry.");
                pose.AssertValid();
                if (!unique.Add(pose.Kind))
                    throw new InvalidOperationException("Duplicate weapon mode: " + pose.Kind);
            }
            for (int i = 1; i <= 4; i++)
                if (!unique.Contains((WeaponPoseKind)i))
                    throw new InvalidOperationException("Missing weapon mode: " + (WeaponPoseKind)i);
        }
    }
}
