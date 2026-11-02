using UnityEngine;

namespace Windrunner.Production.Feedback
{
    [CreateAssetMenu(menuName = "Windrunner/Feedback/Event Definition")]
    public sealed class FeedbackEventDefinition : ScriptableObject
    {
        public FeedbackKey key;

        [Header("Safe Migration Gate")]
        [Tooltip("OFF by default. Events are observed, but no new effects play.")]
        public bool enableProductionPlayback;
        [Tooltip("Confirm ALL corresponding legacy effects are muted before enabling playback.")]
        public bool legacyEffectsMutedAndTested;

        [Header("Playback")]
        [Min(0f)] public float cooldownSeconds;
        [Range(0f, 3f)] public float baseIntensity = 1f;

        [Header("Audio")]
        public AudioClip[] audioVariants;
        [Range(0f, 1f)] public float audioVolume = 1f;
        [Range(0.5f, 2f)] public float minPitch = 0.95f;
        [Range(0.5f, 2f)] public float maxPitch = 1.05f;
        public bool spatialAudio = true;
        public bool followTargetForAudio;

        [Header("Simple Pooled VFX")]
        public GameObject vfxPrefab;
        [Min(0.05f)] public float vfxLifetime = 2.5f;
        [Min(1)] public int maxVfxPoolSize = 12;
        public bool attachVfxToTarget;

        [Header("FEEL Recipes — optional, pooled")]
        public FeedbackRecipeEntry[] feelRecipes;
    }
}
