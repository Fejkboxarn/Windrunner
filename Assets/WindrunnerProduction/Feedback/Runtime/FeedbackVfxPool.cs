using System;
using System.Collections.Generic;
using UnityEngine;

namespace Windrunner.Production.Feedback
{
    /// <summary>Per-prefab pools with deterministic lifetime and pool cap.</summary>
    public sealed class FeedbackVfxPool : IDisposable
    {
        private sealed class Item
        {
            public GameObject Object;
            public bool Active;
            public float ReleaseAt;
            public Transform Follow;
            public Vector3 LocalOffset;
            public Quaternion RelativeRotation;
            public ParticleSystem[] Particles;
        }

        private readonly Transform owner;
        private readonly Dictionary<GameObject, List<Item>> pools =
            new Dictionary<GameObject, List<Item>>();

        public FeedbackVfxPool(Transform owner) { this.owner = owner; }

        public void Play(FeedbackEventDefinition def, FeedbackRequest request)
        {
            GameObject prefab = def.vfxPrefab;
            if (prefab == null) return;
            if (!pools.TryGetValue(prefab, out List<Item> pool))
            {
                pool = new List<Item>();
                pools.Add(prefab, pool);
            }
            Item item = null;
            for (int i = 0; i < pool.Count; i++)
                if (!pool[i].Active) { item = pool[i]; break; }
            if (item == null && pool.Count < Mathf.Max(1, def.maxVfxPoolSize))
            {
                GameObject clone = UnityEngine.Object.Instantiate(prefab, owner);
                clone.name = prefab.name + " (Windrunner VFX Pool)";
                clone.SetActive(false);
                item = new Item
                {
                    Object = clone,
                    Particles = clone.GetComponentsInChildren<ParticleSystem>(true)
                };
                pool.Add(item);
            }
            if (item == null) return;

            item.Active = true;
            item.Follow = def.attachVfxToTarget ? request.Target : null;
            item.LocalOffset = item.Follow != null ?
                item.Follow.InverseTransformPoint(request.Position) : Vector3.zero;
            item.RelativeRotation = item.Follow != null ?
                Quaternion.Inverse(item.Follow.rotation) * request.Rotation : Quaternion.identity;
            item.Object.transform.SetPositionAndRotation(request.Position, request.Rotation);
            item.ReleaseAt = Time.unscaledTime + Mathf.Max(0.05f, def.vfxLifetime);
            item.Object.SetActive(true);
            for (int i = 0; i < item.Particles.Length; i++)
            {
                item.Particles[i].Clear(true);
                item.Particles[i].Play(true);
            }
        }

        public void Tick()
        {
            foreach (List<Item> pool in pools.Values)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    Item item = pool[i];
                    if (!item.Active) continue;
                    if (item.Follow != null)
                        item.Object.transform.SetPositionAndRotation(
                            item.Follow.TransformPoint(item.LocalOffset),
                            item.Follow.rotation * item.RelativeRotation);
                    if (Time.unscaledTime >= item.ReleaseAt)
                    {
                        item.Object.SetActive(false);
                        item.Follow = null;
                        item.Active = false;
                    }
                }
            }
        }

        public void Dispose()
        {
            foreach (List<Item> pool in pools.Values)
                foreach (Item item in pool)
                    if (item.Object != null) UnityEngine.Object.Destroy(item.Object);
            pools.Clear();
        }
    }
}
