using System.Collections.Generic;
using UnityEngine;

namespace Windrunner.Production.Feedback
{
    /// <summary>Plain C# settings; keys remain stable across releases.</summary>
    public sealed class FeedbackSettingsService
    {
        private const string Prefix = "Windrunner.Feedback.Category.v1.";
        private readonly Dictionary<FeedbackCategory, float> scales =
            new Dictionary<FeedbackCategory, float>();

        public FeedbackSettingsService()
        {
            foreach (FeedbackCategory category in System.Enum.GetValues(typeof(FeedbackCategory)))
                scales[category] = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + category, 1f));
        }

        public float Scale(FeedbackCategory category)
        {
            return scales.TryGetValue(category, out float scale) ? scale : 1f;
        }

        public void SetScale(FeedbackCategory category, float scale)
        {
            float value = Mathf.Clamp01(scale);
            scales[category] = value;
            PlayerPrefs.SetFloat(Prefix + category, value);
            PlayerPrefs.Save();
        }
    }
}
