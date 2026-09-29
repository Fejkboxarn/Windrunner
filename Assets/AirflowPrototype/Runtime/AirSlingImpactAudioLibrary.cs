using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "AirSlingImpactAudioLibrary",
        menuName = "Airflow Prototype/Air Sling Impact Audio Library")]
    public sealed class AirSlingImpactAudioLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class ImpactEntry
        {
            [Tooltip(
                "Match this with Impact Audio ID on an AirSlingNode.")]
            public int id = 1;

            [Tooltip(
                "Optional human-readable label, e.g. Small Enemy, Stone Enemy.")]
            public string label;

            public AudioClip clip;

            [Range(0f, 1f)]
            public float volume = 1f;

            [Header("Random Pitch")]
            [Range(0.5f, 2f)]
            public float minimumPitch = 0.94f;

            [Range(0.5f, 2f)]
            public float maximumPitch = 1.06f;
        }

        [Header("Every Sling Node")]
        [Tooltip(
            "Played on every Sling impact together with the optional ID-specific sound.")]
        public AudioClip standardBloodSplash;

        [Range(0f, 1f)]
        public float standardBloodSplashVolume = 0.9f;

        [Header("Standard Splash Random Pitch")]
        [Range(0.5f, 2f)]
        public float standardMinimumPitch = 0.94f;

        [Range(0.5f, 2f)]
        public float standardMaximumPitch = 1.06f;

        [Header("Impact Audio By ID")]
        public List<ImpactEntry> entries =
            new List<ImpactEntry>();

        public void PlayImpact(
            Vector3 worldPosition,
            int impactAudioId)
        {
            if (standardBloodSplash != null)
            {
                PlayClipAtPoint(
                    standardBloodSplash,
                    worldPosition,
                    standardBloodSplashVolume,
                    standardMinimumPitch,
                    standardMaximumPitch,
                    "Sling Standard Impact SFX");
            }

            if (impactAudioId <= 0)
                return;

            ImpactEntry entry =
                FindEntry(
                    impactAudioId);

            if (entry == null ||
                entry.clip == null)
            {
                return;
            }

            PlayClipAtPoint(
                entry.clip,
                worldPosition,
                entry.volume,
                entry.minimumPitch,
                entry.maximumPitch,
                $"Sling Impact ID {impactAudioId}");
        }

        private static void PlayClipAtPoint(
            AudioClip clip,
            Vector3 worldPosition,
            float volume,
            float minimumPitch,
            float maximumPitch,
            string objectName)
        {
            if (clip == null)
                return;

            float low =
                Mathf.Clamp(
                    Mathf.Min(
                        minimumPitch,
                        maximumPitch),
                    0.5f,
                    2f);

            float high =
                Mathf.Clamp(
                    Mathf.Max(
                        minimumPitch,
                        maximumPitch),
                    0.5f,
                    2f);

            float pitch =
                UnityEngine.Random.Range(
                    low,
                    high);

            GameObject audioObject =
                new GameObject(
                    objectName);

            audioObject.transform.position =
                worldPosition;

            AudioSource source =
                audioObject.AddComponent<AudioSource>();

            source.clip = clip;
            source.volume =
                Mathf.Clamp01(
                    volume);

            source.pitch = pitch;
            source.spatialBlend = 1f;
            source.playOnAwake = false;

            source.Play();

            float lifetime =
                clip.length /
                Mathf.Max(
                    0.01f,
                    Mathf.Abs(
                        pitch));

            UnityEngine.Object.Destroy(
                audioObject,
                lifetime + 0.1f);
        }

        public ImpactEntry FindEntry(
            int id)
        {
            if (entries == null)
                return null;

            for (int i = 0;
                 i < entries.Count;
                 i++)
            {
                ImpactEntry entry =
                    entries[i];

                if (entry != null &&
                    entry.id == id)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
