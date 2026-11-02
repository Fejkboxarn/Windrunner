using System;
using UnityEngine;

namespace Windrunner.Production.Feedback
{
    /// <summary>
    /// Synthetic FEEL test routed through a real FeedbackService. It owns an
    /// in-memory catalog, not WindrunnerFeedbackHost's live catalog. The backend
    /// is borrowed and MUST NOT be disposed with this disposable test session.
    /// </summary>
    public sealed class FeelValidationProbe : IDisposable
    {
        private sealed class BorrowedBackend : IFeedbackRecipeBackend
        {
            private readonly IFeedbackRecipeBackend shared;

            public BorrowedBackend(IFeedbackRecipeBackend shared) => this.shared = shared;
            public void Play(FeedbackRecipeEntry recipe, FeedbackRequest request, float intensity) =>
                shared.Play(recipe, request, intensity);
            public void Prewarm(FeedbackRecipeEntry recipe) => shared.Prewarm(recipe);
            public void Tick() { } // The scene's WindrunnerFeedbackHost owns ticking.
            public void Dispose() { } // Never dispose the scene-owned FEEL pool.
        }

        private FeedbackCatalog catalog;
        private FeedbackEventDefinition definition;
        private FeedbackService service;

        public int ObservedCount => service != null ? service.ObservedCount : 0;
        public int AcceptedCount => service != null ? service.PlayedCount : 0;
        public FeedbackSettingsService Settings { get; }

        public FeelValidationProbe(IFeedbackRecipeBackend sharedBackend, FeedbackRecipeEntry recipe)
        {
            if (sharedBackend == null) throw new ArgumentNullException(nameof(sharedBackend));
            if (recipe == null || recipe.prefab == null)
                throw new ArgumentException("A FEEL recipe prefab is required.", nameof(recipe));

            definition = ScriptableObject.CreateInstance<FeedbackEventDefinition>();
            definition.hideFlags = HideFlags.HideAndDontSave;
            definition.key = FeedbackKey.Jump;
            definition.enableProductionPlayback = true;
            definition.legacyEffectsMutedAndTested = true; // Synthetic channel only.
            definition.baseIntensity = 1f;
            definition.cooldownSeconds = 0f;
            definition.feelRecipes = new[] { recipe };

            catalog = ScriptableObject.CreateInstance<FeedbackCatalog>();
            catalog.hideFlags = HideFlags.HideAndDontSave;
            catalog.Configure(new[]
            {
                new FeedbackCatalogItem { key = FeedbackKey.Jump, definition = definition }
            });

            Settings = new FeedbackSettingsService();
            service = new FeedbackService(catalog, Settings, null, null,
                new BorrowedBackend(sharedBackend), false);
        }

        public void Fire(Vector3 worldPosition, Quaternion rotation, float intensity)
        {
            if (service == null) throw new ObjectDisposedException(nameof(FeelValidationProbe));
            service.Publish(FeedbackKey.Jump, new FeedbackRequest(
                worldPosition, rotation, intensity: intensity));
        }

        public void Dispose()
        {
            service?.Dispose();
            service = null;
            if (catalog != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(catalog);
                else UnityEngine.Object.DestroyImmediate(catalog);
                catalog = null;
            }
            if (definition != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(definition);
                else UnityEngine.Object.DestroyImmediate(definition);
                definition = null;
            }
        }
    }
}
