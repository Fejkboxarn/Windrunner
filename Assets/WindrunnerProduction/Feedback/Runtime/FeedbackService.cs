using System;
using System.Collections.Generic;
using UnityEngine;

namespace Windrunner.Production.Feedback
{
    /// <summary>
    /// Project-scoped semantic dispatch. Observe first; production playback is
    /// gated per event until the matching legacy effects are explicitly muted.
    /// </summary>
    public sealed class FeedbackService : IFeedbackService, IDisposable
    {
        private readonly Dictionary<FeedbackKey, FeedbackEventDefinition> catalog =
            new Dictionary<FeedbackKey, FeedbackEventDefinition>();
        private readonly Dictionary<FeedbackKey, float> nextAllowed =
            new Dictionary<FeedbackKey, float>();
        private readonly FeedbackSettingsService settings;
        private readonly FeedbackAudioPool audio;
        private readonly FeedbackVfxPool vfx;
        private readonly IFeedbackRecipeBackend feel;
        private readonly bool logObserved;
        private bool disposed;

        public event Action<FeedbackKey, FeedbackRequest> Observed;
        public int ObservedCount { get; private set; }
        public int PlayedCount { get; private set; }

        public FeedbackService(FeedbackCatalog source, FeedbackSettingsService settings,
            FeedbackAudioPool audio, FeedbackVfxPool vfx,
            IFeedbackRecipeBackend feel, bool logObserved)
        {
            this.settings = settings;
            this.audio = audio;
            this.vfx = vfx;
            this.feel = feel;
            this.logObserved = logObserved;
            if (source == null || source.Entries == null) return;
            foreach (FeedbackCatalogItem entry in source.Entries)
            {
                if (entry == null || entry.key == FeedbackKey.None || entry.definition == null) continue;
                if (catalog.ContainsKey(entry.key))
                    Debug.LogWarning("[Windrunner Feedback] Duplicate catalog key " + entry.key);
                else
                    catalog.Add(entry.key, entry.definition);
            }

            if (feel != null)
            {
                foreach (FeedbackEventDefinition definition in catalog.Values)
                {
                    if (!definition.enableProductionPlayback ||
                        !definition.legacyEffectsMutedAndTested || definition.feelRecipes == null)
                        continue;
                    foreach (FeedbackRecipeEntry recipe in definition.feelRecipes)
                        if (recipe != null && recipe.prefab != null)
                        {
                            try { feel.Prewarm(recipe); }
                            catch (Exception ex)
                            {
                                Debug.LogException(ex);
                            }
                        }
                }
            }
        }

        public void Publish(FeedbackKey key, FeedbackRequest request)
        {
            if (disposed || key == FeedbackKey.None) return;
            ObservedCount++;
            if (logObserved) Debug.Log("[Windrunner Feedback] Observed " + key);
            try { Observed?.Invoke(key, request); }
            catch (Exception ex) { Debug.LogException(ex); }

            if (!catalog.TryGetValue(key, out FeedbackEventDefinition def) || def == null ||
                !def.enableProductionPlayback || !def.legacyEffectsMutedAndTested)
                return;

            float now = Time.unscaledTime;
            if (nextAllowed.TryGetValue(key, out float next) && now < next) return;
            nextAllowed[key] = now + Mathf.Max(0f, def.cooldownSeconds);
            float intensity = Mathf.Max(0f, def.baseIntensity * request.Intensity);
            if (intensity <= 0f) return;
            PlayedCount++;

            try { audio?.Play(def, request, intensity, settings.Scale(FeedbackCategory.Audio)); }
            catch (Exception ex) { Debug.LogException(ex); }
            try
            {
                if (settings.Scale(FeedbackCategory.Vfx) > 0f) vfx?.Play(def, request);
            }
            catch (Exception ex) { Debug.LogException(ex); }
            if (feel != null && def.feelRecipes != null)
            {
                foreach (FeedbackRecipeEntry recipe in def.feelRecipes)
                {
                    if (recipe == null || recipe.prefab == null) continue;
                    float scaled = intensity * recipe.intensityMultiplier * settings.Scale(recipe.category);
                    if (scaled > 0.001f)
                    {
                        try { feel.Play(recipe, request, scaled); }
                        catch (Exception ex) { Debug.LogException(ex); }
                    }
                }
            }
        }

        public void Tick()
        {
            if (disposed) return;
            audio?.Tick();
            vfx?.Tick();
            feel?.Tick();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Observed = null;
            audio?.Dispose();
            vfx?.Dispose();
            feel?.Dispose();
            catalog.Clear();
            nextAllowed.Clear();
        }
    }
}
