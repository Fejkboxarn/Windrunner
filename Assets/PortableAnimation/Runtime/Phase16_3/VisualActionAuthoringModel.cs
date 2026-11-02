using System;
using System.Collections.Generic;
using UnityEngine;

namespace PortableAnimation.Authoring
{
    public enum AuthoringMovementContext
    {
        Idle,
        Start,
        Walk,
        Run,
        Stop,
        JumpStart,
        Rising,
        Falling,
        Landing,
        Hover,
        Flight,
        Stunned,
        KnockedDown,
        Dead,
        Custom
    }

    public enum AuthoringActionPhase
    {
        WholeClip,
        Windup,
        Active,
        Recovery
    }

    public enum AuthoringBodyRegion
    {
        Root,
        Pelvis,
        SpineLower,
        SpineMid,
        SpineUpper,
        Neck,
        Head,
        LeftClavicle,
        LeftUpperArm,
        LeftForearm,
        LeftHand,
        RightClavicle,
        RightUpperArm,
        RightForearm,
        RightHand,
        LeftUpperLeg,
        LeftLowerLeg,
        LeftFoot,
        LeftToes,
        RightUpperLeg,
        RightLowerLeg,
        RightFoot,
        RightToes
    }

    [Serializable]
    public sealed class RegionInfluenceSettings
    {
        public AuthoringBodyRegion region;
        [Range(0f, 1f)] public float position = 0f;
        [Range(0f, 1f)] public float rotation = 1f;
        [Range(0f, 1f)] public float scale = 0f;
        public AnimationCurve clipInfluence = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    }

    [Serializable]
    public sealed class ActionContextEntry
    {
        public string label = "Context";
        public AuthoringMovementContext movementContext = AuthoringMovementContext.Idle;
        public AuthoringActionPhase phase = AuthoringActionPhase.WholeClip;
        // Opt-out semantics keep old saved authoring assets enabled by default.
        // This is independent for every movement context and action phase.
        public bool suppressWorldLookAim;
        public List<RegionInfluenceSettings> regions = new List<RegionInfluenceSettings>();
    }

    [Serializable]
    public sealed class MovementLookAimRule
    {
        public AuthoringMovementContext movementContext;
        // Defaults to false, including when loading an older database.
        public bool suppressWorldLookAim;
    }

    [Serializable]
    public sealed class WorldLookAimSettings
    {
        public bool enabled = true;
        [Range(5f, 120f)] public float maxYaw = 75f;
        [Range(5f, 90f)] public float maxPitch = 45f;
        [Range(0.01f, 1f)] public float smoothingSeconds = 0.18f;
        [Range(0f, 1f)] public float lowerSpine = 0.08f;
        [Range(0f, 1f)] public float middleSpine = 0.12f;
        [Range(0f, 1f)] public float upperSpine = 0.20f;
        [Range(0f, 1f)] public float neck = 0.20f;
        [Range(0f, 1f)] public float head = 0.40f;
        // Alternative rig orientations can be calibrated in the authoring UI.
        [Range(-180f, 180f)] public float facingYawOffset;
        [Range(-90f, 90f)] public float facingPitchOffset;
    }

    [Serializable]
    public sealed class VisualActionEntry
    {
        public string actionId = "Action";
        // Explicit runtime slot mapping. -1 uses matching montage actionId.
        public int runtimeActionIndex = -1;
        [Range(0.01f, 0.95f)] public float windupEnd = 0.2f;
        [Range(0.05f, 0.99f)] public float activeEnd = 0.7f;
        public AnimationClip previewClip;
        [Range(0.01f, 5f)] public float previewSpeed = 1f;
        public bool loopingPreview = false;
        public List<ActionContextEntry> contexts = new List<ActionContextEntry>();
    }

    /// <summary>
    /// Compatibility type for databases authored before the file-name correction.
    /// Keep this class in VisualActionAuthoringModel.cs so Unity can resolve
    /// existing assets that still reference that script's original .meta GUID.
    /// New assets should use VisualActionAuthoringDatabase directly.
    /// </summary>
    [Obsolete("Legacy authoring asset type. New assets use VisualActionAuthoringDatabase.")]
    public sealed class VisualActionAuthoringModel : VisualActionAuthoringDatabase
    {
    }
}
