using System;
using System.Collections.Generic;
using UnityEngine;

namespace Windrunner.Production.Feedback
{
    /// <summary>One AudioSource per active one-shot, reused without Instantiate/Destroy per event.</summary>
    public sealed class FeedbackAudioPool : IDisposable
    {
        private sealed class Voice
        {
            public GameObject Object;
            public AudioSource Source;
            public Transform Follow;
            public float Started;
            public bool Active;
        }

        private readonly Transform owner;
        private readonly int maxVoices;
        private readonly List<Voice> voices = new List<Voice>();

        public FeedbackAudioPool(Transform owner, int maxVoices)
        {
            this.owner = owner;
            this.maxVoices = Mathf.Clamp(maxVoices, 1, 128);
        }

        public void Play(FeedbackEventDefinition definition, FeedbackRequest request,
            float intensity, float categoryScale)
        {
            AudioClip[] variants = definition.audioVariants;
            if (variants == null || variants.Length == 0 || categoryScale <= 0f) return;
            int start = UnityEngine.Random.Range(0, variants.Length);
            AudioClip clip = null;
            for (int i = 0; i < variants.Length; i++)
            {
                clip = variants[(start + i) % variants.Length];
                if (clip != null) break;
            }
            if (clip == null) return;

            Voice voice = null;
            for (int i = 0; i < voices.Count; i++)
            {
                if (!voices[i].Active) { voice = voices[i]; break; }
            }
            if (voice == null && voices.Count < maxVoices)
            {
                var go = new GameObject("Windrunner Pooled Audio Voice");
                go.transform.SetParent(owner, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                voice = new Voice { Object = go, Source = source };
                voices.Add(voice);
            }
            if (voice == null) return; // Stable cap under heavy footstep/impact storms.

            voice.Follow = definition.followTargetForAudio ? request.Target : null;
            voice.Object.transform.position = voice.Follow != null ? voice.Follow.position : request.Position;
            voice.Source.Stop();
            voice.Source.clip = clip;
            voice.Source.spatialBlend = definition.spatialAudio ? 1f : 0f;
            voice.Source.pitch = UnityEngine.Random.Range(
                Mathf.Min(definition.minPitch, definition.maxPitch),
                Mathf.Max(definition.minPitch, definition.maxPitch));
            voice.Source.volume = Mathf.Clamp01(definition.audioVolume * intensity * categoryScale);
            voice.Started = Time.unscaledTime;
            voice.Active = true;
            voice.Source.Play();
        }

        public void Tick()
        {
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                if (!voice.Active) continue;
                if (voice.Follow != null) voice.Object.transform.position = voice.Follow.position;
                if (!voice.Source.isPlaying && Time.unscaledTime > voice.Started + 0.025f)
                {
                    voice.Source.clip = null;
                    voice.Follow = null;
                    voice.Active = false;
                }
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < voices.Count; i++)
                if (voices[i].Object != null) UnityEngine.Object.Destroy(voices[i].Object);
            voices.Clear();
        }
    }
}
