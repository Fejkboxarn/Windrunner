using System;
using UnityEngine;

namespace Windrunner.Production.Feedback
{
    [Serializable]
    public sealed class FeedbackRecipeEntry
    {
        [Tooltip("Prefab containing an MMF_Player. FEEL stays outside this portable assembly.")]
        public GameObject prefab;
        public FeedbackCategory category = FeedbackCategory.General;
        [Range(0f, 3f)] public float intensityMultiplier = 1f;
        public bool attachToTarget;
        public Vector3 localPositionOffset;
        public Vector3 localEulerOffset;
        [Min(0)] public int prewarmCount = 1;
        [Min(1)] public int maxPoolSize = 8;
        public FeedbackPoolOverflow overflow = FeedbackPoolOverflow.RejectNewest;
        [Min(0.1f)] public float safetyReleaseSeconds = 5f;
    }

}
