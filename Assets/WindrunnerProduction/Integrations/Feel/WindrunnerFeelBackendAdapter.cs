// The Feel asset's MMF_Player is in Assembly-CSharp in the reference project.
// Keep this optional adapter outside Feedback's asmdef to avoid a hard dependency.
// Install licensed Feel 5.9.1 first, then add WINDRUNNER_FEEL to Scripting Define Symbols.
#if WINDRUNNER_FEEL
using System;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Feedbacks;
using Windrunner.Production.Feedback;

namespace Windrunner.Production.Integrations.Feel
{
    /// <summary>Unity object lifetime bridge only; driver owns pooling/playback.</summary>
    [DisallowMultipleComponent]
    public sealed class WindrunnerFeelBackendAdapter : MonoBehaviour,
        IFeedbackRecipeBackend, IFeedbackRecipeDiagnostics
    {
        private FeelRecipeDriver driver;

        private void EnsureDriver()
        {
            if (driver == null) driver = new FeelRecipeDriver(transform);
        }

        public void Play(FeedbackRecipeEntry recipe, FeedbackRequest request, float intensity)
        {
            EnsureDriver();
            driver.Play(recipe, request, intensity);
        }

        public void Prewarm(FeedbackRecipeEntry recipe)
        {
            EnsureDriver();
            driver.Prewarm(recipe);
        }

        public FeedbackBackendDiagnostics Snapshot()
        {
            return driver != null ? driver.Snapshot() : default;
        }

        public void Tick() => driver?.Tick();
        public void Dispose()
        {
            driver?.Dispose();
            driver = null;
        }
        private void OnDestroy() => Dispose();
    }

    /// <summary>Plain C# bounded FEEL object pools; no per-event Instantiate/Destroy.</summary>
    internal sealed class FeelRecipeDriver : IDisposable
    {
        private sealed class Instance
        {
            public GameObject Object;
            public MMF_Player Player;
            public bool Initialized;
            public bool Active;
            public float Started;
            public float ReleaseAt;
            public Transform Follow;
            public Vector3 Offset;
            public Quaternion RotOffset;
            public Vector3 OriginalScale;
        }

        private readonly Transform owner;
        private readonly GameObject inactiveContainer;
        private readonly Dictionary<GameObject, List<Instance>> pools =
            new Dictionary<GameObject, List<Instance>>();
        private readonly HashSet<GameObject> prewarmed = new HashSet<GameObject>();
        private int requests;
        private int started;
        private int created;
        private int prewarmedCount;
        private int reused;
        private int rejected;
        private int stolen;
        private int released;
        private int peakActive;
        private float lastIntensity;
        private Vector3 lastPosition;
        private bool disposed;

        public FeelRecipeDriver(Transform owner)
        {
            this.owner = owner;
            inactiveContainer = new GameObject("Windrunner Inactive FEEL Pool");
            inactiveContainer.transform.SetParent(owner, false);
            inactiveContainer.SetActive(false);
        }

        public void Play(FeedbackRecipeEntry recipe, FeedbackRequest request, float intensity)
        {
            if (disposed || recipe == null || recipe.prefab == null || intensity <= 0f) return;
            requests++;
            Prewarm(recipe);
            List<Instance> pool = pools[recipe.prefab];

            Instance active = null;
            for (int i = 0; i < pool.Count; i++)
                if (!pool[i].Active) { active = pool[i]; break; }
            if (active != null) reused++;
            if (active == null && pool.Count < Mathf.Max(1, recipe.maxPoolSize))
            {
                active = CreateInstance(recipe.prefab);
                if (active != null) pool.Add(active);
            }
            if (active == null && recipe.overflow == FeedbackPoolOverflow.StealOldest)
            {
                for (int i = 0; i < pool.Count; i++)
                    if (active == null || pool[i].Started < active.Started) active = pool[i];
                if (active != null)
                {
                    stolen++;
                    Release(active);
                    reused++;
                }
            }
            if (active == null)
            {
                rejected++;
                return;
            }

            active.Follow = recipe.attachToTarget ? request.Target : null;
            active.Offset = recipe.localPositionOffset;
            active.RotOffset = Quaternion.Euler(recipe.localEulerOffset);
            Transform t = active.Object.transform;
            t.SetParent(owner, false);
            t.localScale = active.OriginalScale;
            if (active.Follow != null)
                t.SetPositionAndRotation(active.Follow.TransformPoint(active.Offset),
                    active.Follow.rotation * active.RotOffset);
            else
                t.SetPositionAndRotation(request.Position + request.Rotation * active.Offset,
                    request.Rotation * active.RotOffset);

            t.gameObject.SetActive(true);
            if (!active.Initialized)
            {
                active.Player.Initialization(active.Object);
                active.Initialized = true;
            }
            active.Started = Time.unscaledTime;
            active.ReleaseAt = active.Started + Mathf.Max(0.1f, recipe.safetyReleaseSeconds);
            active.Active = true;
            peakActive = Mathf.Max(peakActive, CountActive());
            try
            {
                active.Player.PlayFeedbacks(request.Position, intensity, false);
                started++;
                lastIntensity = intensity;
                lastPosition = request.Position;
            }
            catch
            {
                Release(active);
                throw;
            }
        }

        public void Prewarm(FeedbackRecipeEntry recipe)
        {
            if (disposed || recipe == null || recipe.prefab == null) return;
            if (!pools.TryGetValue(recipe.prefab, out List<Instance> pool))
            {
                pool = new List<Instance>();
                pools.Add(recipe.prefab, pool);
            }
            if (prewarmed.Add(recipe.prefab))
            {
                int desired = Mathf.Clamp(recipe.prewarmCount, 0, Mathf.Max(1, recipe.maxPoolSize));
                for (int i = 0; i < desired; i++)
                {
                    Instance instance = CreateInstance(recipe.prefab);
                    if (instance != null)
                    {
                        pool.Add(instance);
                        prewarmedCount++;
                    }
                }
            }
        }

        private Instance CreateInstance(GameObject prefab)
        {
            GameObject clone = UnityEngine.Object.Instantiate(prefab, inactiveContainer.transform);
            clone.name = prefab.name + " (Windrunner FEEL Pool)";
            MMF_Player player = clone.GetComponentInChildren<MMF_Player>(true);
            if (player == null)
            {
                Debug.LogError("[Windrunner FEEL] Prefab has no MMF_Player: " + prefab.name, prefab);
                UnityEngine.Object.Destroy(clone);
                return null;
            }
            // Pooled players are triggered explicitly. Never allow their
            // OnEnable/Start callbacks to trigger an extra FEEL sequence.
            player.AutoPlayOnEnable = false;
            player.AutoPlayOnStart = false;
            clone.SetActive(false);
            created++;
            return new Instance { Object = clone, Player = player, OriginalScale = clone.transform.localScale };
        }

        private int CountActive()
        {
            int count = 0;
            foreach (List<Instance> pool in pools.Values)
                foreach (Instance instance in pool)
                    if (instance.Active) count++;
            return count;
        }

        public FeedbackBackendDiagnostics Snapshot()
        {
            int playerPlayCount = 0;
            foreach (List<Instance> pool in pools.Values)
                foreach (Instance instance in pool)
                    if (instance.Player != null)
                        playerPlayCount += instance.Player.PlayCount;
            return new FeedbackBackendDiagnostics(requests, started, created,
                prewarmedCount, reused, rejected, stolen, released, CountActive(),
                peakActive, playerPlayCount, lastIntensity, lastPosition);
        }

        public void Tick()
        {
            if (disposed) return;
            float now = Time.unscaledTime;
            foreach (List<Instance> pool in pools.Values)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    Instance instance = pool[i];
                    if (!instance.Active) continue;
                    if (instance.Follow != null)
                        instance.Object.transform.SetPositionAndRotation(
                            instance.Follow.TransformPoint(instance.Offset),
                            instance.Follow.rotation * instance.RotOffset);

                    // Allow one short startup grace. FEEL's own playback state ends
                    // the instance early; the safety timeout prevents leaked loops.
                    if (now >= instance.ReleaseAt ||
                        (now > instance.Started + 0.15f && !instance.Player.HasFeedbackStillPlaying()))
                        Release(instance);
                }
            }
        }

        private void Release(Instance instance)
        {
            if (!instance.Active) return;
            instance.Player.StopFeedbacks();
            instance.Object.SetActive(false);
            instance.Object.transform.SetParent(inactiveContainer.transform, false);
            instance.Object.transform.localScale = instance.OriginalScale;
            instance.Follow = null;
            instance.Active = false;
            released++;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (List<Instance> pool in pools.Values)
            {
                foreach (Instance instance in pool)
                {
                    if (instance.Active) Release(instance);
                    if (instance.Object != null) UnityEngine.Object.Destroy(instance.Object);
                }
            }
            pools.Clear();
            prewarmed.Clear();
            if (inactiveContainer != null) UnityEngine.Object.Destroy(inactiveContainer);
        }
    }
}
#endif
