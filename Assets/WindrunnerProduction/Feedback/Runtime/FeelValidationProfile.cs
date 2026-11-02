using UnityEngine;

namespace Windrunner.Production.Feedback
{
    /// <summary>
    /// Developer-only recipe data, created by Batch 3.5 tooling outside the
    /// production event catalog. Editing this asset cannot enable gameplay FX.
    /// </summary>
    [CreateAssetMenu(menuName = "Windrunner/Diagnostics/FEEL Validation Profile")]
    public sealed class FeelValidationProfile : ScriptableObject
    {
        public FeedbackRecipeEntry recipe = new FeedbackRecipeEntry
        {
            category = FeedbackCategory.General,
            prewarmCount = 2,
            maxPoolSize = 4,
            overflow = FeedbackPoolOverflow.RejectNewest,
            safetyReleaseSeconds = 1.25f
        };

        [Range(0.05f, 2f)] public float firstIntensity = 0.8f;
        [Range(0.05f, 2f)] public float burstIntensity = 0.3f;
        [Range(2, 128)] public int burstSize = 24;
        [Range(0.5f, 10f)] public float releaseWaitSeconds = 1.5f;
    }
}
