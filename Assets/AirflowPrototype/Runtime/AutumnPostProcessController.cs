using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AirflowPrototype
{
    /// <summary>
    /// Owns the scene's autumn grade and the speed-reactive URP lens effects.
    /// Static look + dynamic speed response live in one controller so two
    /// components never fight over the same Volume values.
    /// </summary>
    [ExecuteAlways]
    public sealed class AutumnPostProcessController : MonoBehaviour
    {
        [SerializeField] private Volume volume;
        [SerializeField] private AutumnLookSettings lookSettings;
        [SerializeField] private SpeedPostProcessSettings speedSettings;
        [SerializeField] private PlayerMotor player;

        private float _speedWeight;

        // Compatibility with the original Batch 14 installer.
        public void Configure(
            Volume newVolume,
            AutumnLookSettings newLookSettings)
        {
            volume = newVolume;
            lookSettings = newLookSettings;
            Apply(true);
        }

        public void Configure(
            Volume newVolume,
            AutumnLookSettings newLookSettings,
            SpeedPostProcessSettings newSpeedSettings,
            PlayerMotor newPlayer)
        {
            volume = newVolume;
            lookSettings = newLookSettings;
            speedSettings = newSpeedSettings;
            player = newPlayer;

            _speedWeight = 0f;
            Apply(true);
        }

        private void OnEnable()
        {
            ResolvePlayer();
            Apply(true);
        }

        private void OnValidate()
        {
            Apply(true);
        }

        private void Update()
        {
            ResolvePlayer();
            Apply(false);
        }

        private void ResolvePlayer()
        {
            if (player == null &&
                Application.isPlaying)
            {
                player =
                    FindFirstObjectByType<PlayerMotor>();
            }
        }

        private void Apply(
            bool immediate)
        {
            if (volume == null ||
                lookSettings == null ||
                volume.sharedProfile == null)
            {
                return;
            }

            float targetSpeedWeight =
                GetTargetSpeedWeight();

            if (immediate ||
                !Application.isPlaying)
            {
                _speedWeight =
                    targetSpeedWeight;
            }
            else
            {
                float response =
                    speedSettings != null
                        ? Mathf.Max(
                            0.1f,
                            speedSettings.response)
                        : 6.5f;

                _speedWeight =
                    Mathf.Lerp(
                        _speedWeight,
                        targetSpeedWeight,
                        1f -
                        Mathf.Exp(
                            -response *
                            Time.unscaledDeltaTime));
            }

            VolumeProfile profile =
                volume.sharedProfile;

            ApplyColorGrade(profile);
            ApplyBloom(profile);
            ApplyVignette(profile);
            ApplyFilmGrain(profile);
            ApplyLens(profile);
            ApplyMotionBlur(profile);
            ApplyTonemapping(profile);
        }

        private float GetTargetSpeedWeight()
        {
            if (!Application.isPlaying ||
                speedSettings == null ||
                !speedSettings.enabled)
            {
                return 0f;
            }

            if (speedSettings.previewAtFullStrength)
                return 1f;

            if (player == null)
                return 0f;

            float fullSpeed =
                Mathf.Max(
                    speedSettings.startSpeed + 0.01f,
                    speedSettings.fullSpeed);

            return Mathf.InverseLerp(
                speedSettings.startSpeed,
                fullSpeed,
                player.HorizontalSpeed);
        }

        private void ApplyColorGrade(
            VolumeProfile profile)
        {
            if (!profile.TryGet<ColorAdjustments>(
                    out ColorAdjustments color))
            {
                return;
            }

            color.active = true;

            color.postExposure.overrideState = true;
            color.postExposure.value =
                lookSettings.postExposure;

            color.contrast.overrideState = true;
            color.contrast.value =
                lookSettings.contrast;

            color.hueShift.overrideState = true;
            color.hueShift.value =
                lookSettings.hueShift;

            color.saturation.overrideState = true;
            color.saturation.value =
                lookSettings.saturation;

            color.colorFilter.overrideState = true;
            color.colorFilter.value =
                lookSettings.colorFilter;

            if (profile.TryGet<WhiteBalance>(
                    out WhiteBalance whiteBalance))
            {
                whiteBalance.active = true;

                whiteBalance.temperature.overrideState = true;
                whiteBalance.temperature.value =
                    lookSettings.temperature;

                whiteBalance.tint.overrideState = true;
                whiteBalance.tint.value =
                    lookSettings.tint;
            }
        }

        private void ApplyBloom(
            VolumeProfile profile)
        {
            if (!profile.TryGet<Bloom>(
                    out Bloom bloom))
            {
                return;
            }

            bloom.active =
                lookSettings.bloomEnabled;

            bloom.intensity.overrideState = true;

            float speedBloom =
                speedSettings != null &&
                speedSettings.enabled &&
                speedSettings.speedBloomEnabled
                    ? speedSettings.additionalBloom *
                      _speedWeight
                    : 0f;

            bloom.intensity.value =
                lookSettings.bloomIntensity +
                speedBloom;

            bloom.threshold.overrideState = true;
            bloom.threshold.value =
                lookSettings.bloomThreshold;

            bloom.scatter.overrideState = true;
            bloom.scatter.value =
                lookSettings.bloomScatter;

            bloom.tint.overrideState = true;
            bloom.tint.value =
                lookSettings.bloomTint;
        }

        private void ApplyVignette(
            VolumeProfile profile)
        {
            if (!profile.TryGet<Vignette>(
                    out Vignette vignette))
            {
                return;
            }

            bool speedActive =
                speedSettings != null &&
                speedSettings.enabled &&
                speedSettings.speedVignetteEnabled;

            vignette.active =
                lookSettings.vignetteEnabled ||
                speedActive;

            vignette.intensity.overrideState = true;

            vignette.intensity.value =
                Mathf.Clamp01(
                    lookSettings.vignetteIntensity +
                    (speedActive
                        ? speedSettings.additionalVignette *
                          _speedWeight
                        : 0f));

            vignette.smoothness.overrideState = true;

            vignette.smoothness.value =
                speedActive
                    ? Mathf.Lerp(
                        lookSettings.vignetteSmoothness,
                        speedSettings.speedVignetteSmoothness,
                        _speedWeight)
                    : lookSettings.vignetteSmoothness;

            vignette.color.overrideState = true;
            vignette.color.value =
                lookSettings.vignetteColor;
        }

        private void ApplyFilmGrain(
            VolumeProfile profile)
        {
            if (!profile.TryGet<FilmGrain>(
                    out FilmGrain grain))
            {
                return;
            }

            grain.active =
                lookSettings.filmGrainEnabled;

            grain.intensity.overrideState = true;
            grain.intensity.value =
                lookSettings.filmGrainIntensity;

            grain.response.overrideState = true;
            grain.response.value =
                lookSettings.filmGrainResponse;
        }

        private void ApplyLens(
            VolumeProfile profile)
        {
            if (profile.TryGet<ChromaticAberration>(
                    out ChromaticAberration chromatic))
            {
                bool speedChromatic =
                    speedSettings != null &&
                    speedSettings.enabled &&
                    speedSettings.speedChromaticAberrationEnabled;

                chromatic.active =
                    lookSettings.chromaticAberrationEnabled ||
                    speedChromatic;

                chromatic.intensity.overrideState = true;

                chromatic.intensity.value =
                    Mathf.Clamp01(
                        lookSettings.chromaticAberration +
                        (speedChromatic
                            ? speedSettings.additionalChromaticAberration *
                              _speedWeight
                            : 0f));
            }

            if (profile.TryGet<LensDistortion>(
                    out LensDistortion lens))
            {
                bool speedLens =
                    speedSettings != null &&
                    speedSettings.enabled &&
                    speedSettings.speedLensDistortionEnabled;

                lens.active =
                    lookSettings.lensDistortionEnabled ||
                    speedLens;

                lens.intensity.overrideState = true;

                lens.intensity.value =
                    Mathf.Clamp(
                        lookSettings.lensDistortion +
                        (speedLens
                            ? speedSettings.additionalLensDistortion *
                              _speedWeight
                            : 0f),
                        -1f,
                        1f);

                lens.xMultiplier.overrideState = true;
                lens.xMultiplier.value =
                    lookSettings.lensDistortionX;

                lens.yMultiplier.overrideState = true;
                lens.yMultiplier.value =
                    lookSettings.lensDistortionY;

                lens.scale.overrideState = true;
                lens.scale.value =
                    Mathf.Clamp(
                        lookSettings.lensScale +
                        (speedLens
                            ? speedSettings.additionalLensScale *
                              _speedWeight
                            : 0f),
                        0.5f,
                        1.5f);
            }
        }

        private void ApplyMotionBlur(
            VolumeProfile profile)
        {
            if (!profile.TryGet<MotionBlur>(
                    out MotionBlur motionBlur))
            {
                return;
            }

            bool enabled =
                speedSettings != null &&
                speedSettings.enabled &&
                speedSettings.motionBlurEnabled;

            motionBlur.active =
                enabled;

            motionBlur.intensity.overrideState = true;
            motionBlur.intensity.value =
                enabled
                    ? speedSettings.motionBlurIntensity *
                      _speedWeight
                    : 0f;

            motionBlur.clamp.overrideState = true;
            motionBlur.clamp.value =
                speedSettings != null
                    ? speedSettings.motionBlurClamp
                    : 0.05f;
        }

        private void ApplyTonemapping(
            VolumeProfile profile)
        {
            if (!profile.TryGet<Tonemapping>(
                    out Tonemapping tonemapping))
            {
                return;
            }

            tonemapping.active = true;
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value =
                lookSettings.useACES
                    ? TonemappingMode.ACES
                    : TonemappingMode.Neutral;
        }
    }
}
