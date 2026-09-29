using System;
using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Tiny runtime synthesizer for prototype-only fallback sounds.
    /// Assign real AudioClips in AirAudioSettings to replace any of these.
    /// </summary>
    public static class PrototypeAudioSynthesis
    {
        private const int SampleRate = 44100;

        public static AudioClip CreateFootstep()
        {
            return CreateThump(
                "Prototype Footstep",
                0.12f,
                105f,
                42f,
                0.22f);
        }

        public static AudioClip CreateJump()
        {
            return CreateWhoosh(
                "Prototype Jump",
                0.26f,
                0.62f,
                0.25f);
        }

        public static AudioClip CreateLanding()
        {
            return CreateThump(
                "Prototype Landing",
                0.22f,
                92f,
                38f,
                0.35f);
        }

        public static AudioClip CreateFlip()
        {
            return CreateWhoosh(
                "Prototype Flip",
                0.34f,
                0.78f,
                0.45f);
        }

        public static AudioClip CreateDashStart()
        {
            return CreateWhoosh(
                "Prototype Dash Start",
                0.30f,
                0.82f,
                0.58f);
        }

        public static AudioClip CreateCastRelease()
        {
            return CreateWhoosh(
                "Prototype Air Release",
                0.25f,
                0.95f,
                0.78f);
        }

        public static AudioClip CreateNodeHit()
        {
            return CreateThump(
                "Prototype Node Hit",
                0.14f,
                150f,
                62f,
                0.28f);
        }

        public static AudioClip CreateReturnLaunch()
        {
            return CreateWhoosh(
                "Prototype Return Launch",
                0.24f,
                0.72f,
                0.62f);
        }

        public static AudioClip CreateRewardArrival()
        {
            return CreateRewardWhump();
        }

        public static AudioClip CreateDepleted()
        {
            return CreateToneSweep(
                "Prototype Air Empty",
                0.28f,
                220f,
                105f,
                0.28f);
        }

        public static AudioClip CreateReadyCue()
        {
            return CreateToneSweep(
                "Prototype Target Ready",
                0.11f,
                620f,
                910f,
                0.22f);
        }

        public static AudioClip CreateChargeLoop()
        {
            return CreatePeriodicWindLoop(
                "Prototype Charge Loop",
                1.6f,
                0.20f,
                18,
                42,
                240);
        }

        public static AudioClip CreateDashWindLoop()
        {
            return CreatePeriodicWindLoop(
                "Prototype Dash Wind Loop",
                2.0f,
                0.24f,
                24,
                55,
                420);
        }

        public static AudioClip CreateAmbienceLoop()
        {
            AudioClip wind =
                CreatePeriodicWindLoop(
                    "Prototype Ambience",
                    6.0f,
                    0.12f,
                    20,
                    12,
                    150);

            return wind;
        }

        public static AudioClip CreateMusicLoop()
        {
            const float duration = 8f;
            int samples =
                Mathf.CeilToInt(
                    duration *
                    SampleRate);

            float[] data =
                new float[samples];

            // Frequencies chosen to complete whole cycles over 8 seconds,
            // keeping the placeholder drone loop reasonably seamless.
            float[] frequencies =
            {
                110f,
                165f,
                220f,
                330f
            };

            float[] amplitudes =
            {
                0.15f,
                0.08f,
                0.055f,
                0.025f
            };

            for (int i = 0; i < samples; i++)
            {
                float t =
                    i /
                    (float)SampleRate;

                float slowBreath =
                    0.72f +
                    0.28f *
                    Mathf.Sin(
                        Mathf.PI *
                        2f *
                        t /
                        duration);

                float sample = 0f;

                for (int n = 0;
                     n < frequencies.Length;
                     n++)
                {
                    sample +=
                        Mathf.Sin(
                            Mathf.PI *
                            2f *
                            frequencies[n] *
                            t) *
                        amplitudes[n];
                }

                data[i] =
                    Mathf.Clamp(
                        sample *
                        slowBreath,
                        -1f,
                        1f);
            }

            return CreateClip(
                "Prototype Music Drone",
                data);
        }

        private static AudioClip CreateRewardWhump()
        {
            const float duration = 0.42f;

            int samples =
                Mathf.CeilToInt(
                    duration *
                    SampleRate);

            float[] data =
                new float[samples];

            System.Random random =
                new System.Random(3341);

            float phase = 0f;
            float filteredNoise = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t =
                    i /
                    (float)SampleRate;

                float normalized =
                    t /
                    duration;

                float bassEnvelope =
                    Mathf.Exp(
                        -normalized *
                        6.2f);

                float airEnvelope =
                    Mathf.Sin(
                        Mathf.PI *
                        Mathf.Clamp01(normalized));

                float frequency =
                    Mathf.Lerp(
                        92f,
                        42f,
                        normalized);

                phase +=
                    Mathf.PI *
                    2f *
                    frequency /
                    SampleRate;

                float rawNoise =
                    (float)(
                        random.NextDouble() *
                        2.0 -
                        1.0);

                filteredNoise =
                    Mathf.Lerp(
                        filteredNoise,
                        rawNoise,
                        0.075f);

                float sample =
                    Mathf.Sin(phase) *
                    bassEnvelope *
                    0.72f +
                    filteredNoise *
                    airEnvelope *
                    0.30f;

                data[i] =
                    Mathf.Clamp(
                        sample,
                        -1f,
                        1f);
            }

            return CreateClip(
                "Prototype Reward Whump",
                data);
        }

        private static AudioClip CreateThump(
            string name,
            float duration,
            float startFrequency,
            float endFrequency,
            float noiseAmount)
        {
            int samples =
                Mathf.CeilToInt(
                    duration *
                    SampleRate);

            float[] data =
                new float[samples];

            System.Random random =
                new System.Random(
                    name.GetHashCode());

            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t =
                    i /
                    (float)SampleRate;

                float normalized =
                    t /
                    duration;

                float envelope =
                    Mathf.Exp(
                        -normalized *
                        7.5f);

                float frequency =
                    Mathf.Lerp(
                        startFrequency,
                        endFrequency,
                        normalized);

                phase +=
                    Mathf.PI *
                    2f *
                    frequency /
                    SampleRate;

                float noise =
                    (float)(
                        random.NextDouble() *
                        2.0 -
                        1.0);

                float sample =
                    Mathf.Sin(phase) *
                    envelope *
                    0.72f +
                    noise *
                    envelope *
                    noiseAmount;

                data[i] =
                    Mathf.Clamp(
                        sample,
                        -1f,
                        1f);
            }

            return CreateClip(
                name,
                data);
        }

        private static AudioClip CreateWhoosh(
            string name,
            float duration,
            float noiseGain,
            float lowToneGain)
        {
            int samples =
                Mathf.CeilToInt(
                    duration *
                    SampleRate);

            float[] data =
                new float[samples];

            System.Random random =
                new System.Random(
                    name.GetHashCode());

            float filteredNoise = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t =
                    i /
                    (float)SampleRate;

                float normalized =
                    t /
                    duration;

                float envelope =
                    Mathf.Pow(
                        Mathf.Sin(
                            Mathf.PI *
                            Mathf.Clamp01(normalized)),
                        1.25f);

                float rawNoise =
                    (float)(
                        random.NextDouble() *
                        2.0 -
                        1.0);

                filteredNoise =
                    Mathf.Lerp(
                        filteredNoise,
                        rawNoise,
                        0.12f);

                float tonal =
                    Mathf.Sin(
                        Mathf.PI *
                        2f *
                        Mathf.Lerp(
                            90f,
                            155f,
                            normalized) *
                        t);

                float sample =
                    filteredNoise *
                    envelope *
                    noiseGain +
                    tonal *
                    envelope *
                    lowToneGain;

                data[i] =
                    Mathf.Clamp(
                        sample *
                        0.72f,
                        -1f,
                        1f);
            }

            return CreateClip(
                name,
                data);
        }

        private static AudioClip CreateToneSweep(
            string name,
            float duration,
            float startFrequency,
            float endFrequency,
            float volume)
        {
            int samples =
                Mathf.CeilToInt(
                    duration *
                    SampleRate);

            float[] data =
                new float[samples];

            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t =
                    i /
                    (float)SampleRate;

                float normalized =
                    t /
                    duration;

                float envelope =
                    Mathf.Sin(
                        Mathf.PI *
                        Mathf.Clamp01(normalized));

                float frequency =
                    Mathf.Lerp(
                        startFrequency,
                        endFrequency,
                        normalized);

                phase +=
                    Mathf.PI *
                    2f *
                    frequency /
                    SampleRate;

                data[i] =
                    Mathf.Sin(phase) *
                    envelope *
                    volume;
            }

            return CreateClip(
                name,
                data);
        }

        private static AudioClip CreatePeriodicWindLoop(
            string name,
            float duration,
            float gain,
            int harmonicCount,
            int minimumHarmonic,
            int maximumHarmonic)
        {
            int samples =
                Mathf.CeilToInt(
                    duration *
                    SampleRate);

            float[] data =
                new float[samples];

            System.Random random =
                new System.Random(
                    name.GetHashCode());

            int[] harmonics =
                new int[harmonicCount];

            float[] phases =
                new float[harmonicCount];

            float[] amplitudes =
                new float[harmonicCount];

            for (int h = 0;
                 h < harmonicCount;
                 h++)
            {
                harmonics[h] =
                    random.Next(
                        minimumHarmonic,
                        maximumHarmonic + 1);

                phases[h] =
                    (float)random.NextDouble() *
                    Mathf.PI *
                    2f;

                amplitudes[h] =
                    Mathf.Lerp(
                        0.35f,
                        1f,
                        (float)random.NextDouble()) /
                    Mathf.Sqrt(
                        harmonics[h]);
            }

            float fundamental =
                1f /
                duration;

            for (int i = 0;
                 i < samples;
                 i++)
            {
                float t =
                    i /
                    (float)SampleRate;

                float sample = 0f;

                for (int h = 0;
                     h < harmonicCount;
                     h++)
                {
                    float frequency =
                        harmonics[h] *
                        fundamental;

                    sample +=
                        Mathf.Sin(
                            Mathf.PI *
                            2f *
                            frequency *
                            t +
                            phases[h]) *
                        amplitudes[h];
                }

                float movement =
                    0.72f +
                    0.28f *
                    Mathf.Sin(
                        Mathf.PI *
                        2f *
                        t /
                        duration);

                data[i] =
                    Mathf.Clamp(
                        sample *
                        gain *
                        movement,
                        -1f,
                        1f);
            }

            return CreateClip(
                name,
                data);
        }

        private static AudioClip CreateClip(
            string name,
            float[] samples)
        {
            AudioClip clip =
                AudioClip.Create(
                    name,
                    samples.Length,
                    1,
                    SampleRate,
                    false);

            clip.SetData(
                samples,
                0);

            return clip;
        }
    }
}
