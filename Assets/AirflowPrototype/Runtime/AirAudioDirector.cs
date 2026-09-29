using UnityEngine;

namespace AirflowPrototype
{
    public sealed class AirAudioDirector : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerMovementVisuals movementVisuals;
        [SerializeField] private AirCaster caster;
        [SerializeField] private AirFlowHitSystem hitSystem;
        [SerializeField] private AirPower airPower;
        [SerializeField] private AirAudioSettings settings;

        private AudioSource _footstepSource;
        private AudioSource _jumpSource;
        private AudioSource _landingSource;
        private AudioSource _flipSource;
        private AudioSource _dashStartSource;

        private AudioSource _targetReadySource;
        private AudioSource _castReleaseSource;
        private AudioSource _nodeHitSource;
        private AudioSource _returnLaunchSource;
        private AudioSource _rewardArrivalSource;
        private AudioSource _airPowerDepletedSource;

        private AudioSource _dashLoopSource;
        private AudioSource _chargeLoopSource;
        private AudioSource _ambienceSource;
        private AudioSource _musicSource;

        private bool _wasDashActive;
        private bool _wasTargetReady;
        private bool _wasAudioEnabled;

        // Compatibility overload used by the existing Batch 11 setup.
        public void Configure(
            PlayerMotor newMotor,
            PlayerMovementVisuals newMovementVisuals,
            AirCaster newCaster,
            AirFlowHitSystem newHitSystem,
            AirDash unusedDash,
            AirPower newAirPower,
            AirAudioSettings newSettings)
        {
            Configure(
                newMotor,
                newMovementVisuals,
                newCaster,
                newHitSystem,
                newAirPower,
                newSettings);
        }

        public void Configure(
            PlayerMotor newMotor,
            PlayerMovementVisuals newMovementVisuals,
            AirCaster newCaster,
            AirFlowHitSystem newHitSystem,
            AirAudioSettings newSettings)
        {
            Configure(
                newMotor,
                newMovementVisuals,
                newCaster,
                newHitSystem,
                GetComponent<AirPower>(),
                newSettings);
        }

        public void Configure(
            PlayerMotor newMotor,
            PlayerMovementVisuals newMovementVisuals,
            AirCaster newCaster,
            AirFlowHitSystem newHitSystem,
            AirPower newAirPower,
            AirAudioSettings newSettings)
        {
            Unsubscribe();

            motor = newMotor;
            movementVisuals = newMovementVisuals;
            caster = newCaster;
            hitSystem = newHitSystem;
            airPower = newAirPower;
            settings = newSettings;

            BuildSourcesIfNeeded();
            ApplyLoopClips();

            if (isActiveAndEnabled)
                Subscribe();
        }

        private void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();

            if (movementVisuals == null)
                movementVisuals = GetComponent<PlayerMovementVisuals>();

            if (caster == null)
                caster = GetComponent<AirCaster>();

            if (hitSystem == null)
                hitSystem = GetComponent<AirFlowHitSystem>();

            if (airPower == null)
                airPower = GetComponent<AirPower>();

            BuildSourcesIfNeeded();
            ApplyLoopClips();
        }

        private void OnEnable()
        {
            Subscribe();

            _wasDashActive =
                motor != null &&
                motor.IsDashActive;

            _wasTargetReady =
                caster != null &&
                caster.IsReady;

            _wasAudioEnabled =
                settings != null &&
                settings.audioEnabled;

            if (_wasAudioEnabled)
                StartWorldLoops();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopAllLoops();
        }

        private void Update()
        {
            if (settings == null)
                return;

            if (!settings.audioEnabled)
            {
                if (_wasAudioEnabled)
                    StopAllLoops();

                _wasAudioEnabled = false;
                return;
            }

            if (!_wasAudioEnabled)
            {
                _wasAudioEnabled = true;
                ApplyLoopClips();
                StartWorldLoops();
            }

            UpdateWorldLoopStates();
            UpdateDashAudio();
            UpdateChargeAudio();
            UpdateTargetReadyCue();
        }

        private void Subscribe()
        {
            if (movementVisuals != null)
            {
                movementVisuals.Footstep -= OnFootstep;
                movementVisuals.Footstep += OnFootstep;

                movementVisuals.JumpStarted -= OnJumpStarted;
                movementVisuals.JumpStarted += OnJumpStarted;

                movementVisuals.Landed -= OnLanded;
                movementVisuals.Landed += OnLanded;

                movementVisuals.FlipStarted -= OnFlipStarted;
                movementVisuals.FlipStarted += OnFlipStarted;
            }

            if (caster != null)
            {
                caster.PulseReleased -= OnPulseReleased;
                caster.PulseReleased += OnPulseReleased;

                caster.PulseHit -= OnPulseHit;
                caster.PulseHit += OnPulseHit;
            }

            if (hitSystem != null)
            {
                hitSystem.ReturnStarted -= OnReturnStarted;
                hitSystem.ReturnStarted += OnReturnStarted;

                hitSystem.RewardDelivered -= OnRewardDelivered;
                hitSystem.RewardDelivered += OnRewardDelivered;
            }

            if (airPower != null)
            {
                airPower.Depleted -= OnAirPowerDepleted;
                airPower.Depleted += OnAirPowerDepleted;
            }
        }

        private void Unsubscribe()
        {
            if (movementVisuals != null)
            {
                movementVisuals.Footstep -= OnFootstep;
                movementVisuals.JumpStarted -= OnJumpStarted;
                movementVisuals.Landed -= OnLanded;
                movementVisuals.FlipStarted -= OnFlipStarted;
            }

            if (caster != null)
            {
                caster.PulseReleased -= OnPulseReleased;
                caster.PulseHit -= OnPulseHit;
            }

            if (hitSystem != null)
            {
                hitSystem.ReturnStarted -= OnReturnStarted;
                hitSystem.RewardDelivered -= OnRewardDelivered;
            }

            if (airPower != null)
                airPower.Depleted -= OnAirPowerDepleted;
        }

        private void OnFootstep(float intensity)
        {
            PlayOneShot(
                settings != null && settings.footstepEnabled,
                _footstepSource,
                settings != null ? settings.footstep : null,
                settings != null ? settings.footstepVolume : 1f,
                settings != null ? settings.footstepPitchMin : 1f,
                settings != null ? settings.footstepPitchMax : 1f,
                settings != null ? settings.movementVolume : 1f,
                Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(intensity)));
        }

        private void OnJumpStarted()
        {
            PlayOneShot(
                settings != null && settings.jumpEnabled,
                _jumpSource,
                settings != null ? settings.jump : null,
                settings != null ? settings.jumpVolume : 1f,
                settings != null ? settings.jumpPitchMin : 1f,
                settings != null ? settings.jumpPitchMax : 1f,
                settings != null ? settings.movementVolume : 1f,
                1f);
        }

        private void OnLanded(float intensity)
        {
            PlayOneShot(
                settings != null && settings.landingEnabled,
                _landingSource,
                settings != null ? settings.landing : null,
                settings != null ? settings.landingVolume : 1f,
                settings != null ? settings.landingPitchMin : 1f,
                settings != null ? settings.landingPitchMax : 1f,
                settings != null ? settings.movementVolume : 1f,
                Mathf.Lerp(0.55f, 1.15f, Mathf.Clamp01(intensity)));
        }

        private void OnFlipStarted(int variant, float intensity)
        {
            PlayOneShot(
                settings != null && settings.flipWhooshEnabled,
                _flipSource,
                settings != null ? settings.flipWhoosh : null,
                settings != null ? settings.flipWhooshVolume : 1f,
                settings != null ? settings.flipWhooshPitchMin : 1f,
                settings != null ? settings.flipWhooshPitchMax : 1f,
                settings != null ? settings.movementVolume : 1f,
                Mathf.Lerp(0.75f, 1.1f, Mathf.Clamp01(intensity)));
        }

        private void UpdateDashAudio()
        {
            if (motor == null ||
                settings == null)
            {
                return;
            }

            bool dashActive =
                motor.IsDashActive;

            if (dashActive &&
                !_wasDashActive)
            {
                PlayOneShot(
                    settings.dashStartEnabled,
                    _dashStartSource,
                    settings.dashStart,
                    settings.dashStartVolume,
                    settings.dashStartPitchMin,
                    settings.dashStartPitchMax,
                    settings.movementVolume,
                    1f);
            }

            _wasDashActive =
                dashActive;

            if (_dashLoopSource == null)
                return;

            if (!settings.dashWindLoopEnabled ||
                settings.dashWindLoop == null)
            {
                StopSource(_dashLoopSource);
                return;
            }

            float speed01 =
                Mathf.InverseLerp(
                    settings.windStartSpeed,
                    settings.windFullSpeed,
                    motor.HorizontalSpeed);

            float targetVolume =
                dashActive
                    ? speed01 *
                      settings.windMaxVolume *
                      settings.movementVolume *
                      settings.masterVolume
                    : 0f;

            _dashLoopSource.volume =
                Mathf.MoveTowards(
                    _dashLoopSource.volume,
                    targetVolume,
                    Time.unscaledDeltaTime * 2.5f);

            _dashLoopSource.pitch =
                Mathf.Lerp(
                    settings.windMinPitch,
                    settings.windMaxPitch,
                    speed01);

            if (targetVolume > 0.001f)
            {
                if (!_dashLoopSource.isPlaying)
                    _dashLoopSource.Play();
            }
            else if (_dashLoopSource.isPlaying &&
                     _dashLoopSource.volume <= 0.001f)
            {
                _dashLoopSource.Stop();
            }
        }

        private void UpdateChargeAudio()
        {
            if (caster == null ||
                settings == null ||
                _chargeLoopSource == null)
            {
                return;
            }

            if (!settings.chargeLoopEnabled ||
                settings.chargeLoop == null)
            {
                StopSource(_chargeLoopSource);
                return;
            }

            bool charging =
                caster.IsCharging;

            float targetVolume =
                charging
                    ? settings.chargeMaxVolume *
                      settings.abilityVolume *
                      settings.masterVolume
                    : 0f;

            _chargeLoopSource.volume =
                Mathf.MoveTowards(
                    _chargeLoopSource.volume,
                    targetVolume,
                    Time.unscaledDeltaTime * 3.5f);

            _chargeLoopSource.pitch =
                Mathf.Lerp(
                    settings.chargeStartPitch,
                    settings.chargeReadyPitch,
                    caster.TimingProgress01);

            if (charging)
            {
                if (!_chargeLoopSource.isPlaying)
                    _chargeLoopSource.Play();
            }
            else if (_chargeLoopSource.isPlaying &&
                     _chargeLoopSource.volume <= 0.001f)
            {
                _chargeLoopSource.Stop();
            }
        }

        private void UpdateTargetReadyCue()
        {
            if (caster == null ||
                settings == null)
            {
                return;
            }

            bool ready =
                caster.IsReady;

            if (ready &&
                !_wasTargetReady)
            {
                PlayOneShot(
                    settings.targetReadyEnabled,
                    _targetReadySource,
                    settings.targetReady,
                    settings.targetReadyVolume,
                    settings.targetReadyPitchMin,
                    settings.targetReadyPitchMax,
                    settings.abilityVolume,
                    0.75f);
            }

            _wasTargetReady =
                ready;
        }

        private void OnPulseReleased(float charge01, bool ready)
        {
            PlayOneShot(
                settings != null && settings.castReleaseEnabled,
                _castReleaseSource,
                settings != null ? settings.castRelease : null,
                settings != null ? settings.castReleaseVolume : 1f,
                settings != null ? settings.castReleasePitchMin : 1f,
                settings != null ? settings.castReleasePitchMax : 1f,
                settings != null ? settings.abilityVolume : 1f,
                Mathf.Lerp(0.75f, 1f, charge01));
        }

        private void OnPulseHit(AirNode node, float charge01)
        {
            PlayOneShot(
                settings != null && settings.nodeHitEnabled,
                _nodeHitSource,
                settings != null ? settings.nodeHit : null,
                settings != null ? settings.nodeHitVolume : 1f,
                settings != null ? settings.nodeHitPitchMin : 1f,
                settings != null ? settings.nodeHitPitchMax : 1f,
                settings != null ? settings.abilityVolume : 1f,
                Mathf.Lerp(0.7f, 1f, charge01));
        }

        private void OnReturnStarted(AirNode node, float intensity)
        {
            PlayOneShot(
                settings != null && settings.returnLaunchEnabled,
                _returnLaunchSource,
                settings != null ? settings.returnLaunch : null,
                settings != null ? settings.returnLaunchVolume : 1f,
                settings != null ? settings.returnLaunchPitchMin : 1f,
                settings != null ? settings.returnLaunchPitchMax : 1f,
                settings != null ? settings.abilityVolume : 1f,
                Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(intensity)));
        }

        private void OnRewardDelivered(AirNode node, float intensity)
        {
            PlayOneShot(
                settings != null && settings.rewardArrivalEnabled,
                _rewardArrivalSource,
                settings != null ? settings.rewardArrival : null,
                settings != null ? settings.rewardArrivalVolume : 1f,
                settings != null ? settings.rewardArrivalPitchMin : 1f,
                settings != null ? settings.rewardArrivalPitchMax : 1f,
                settings != null ? settings.abilityVolume : 1f,
                Mathf.Lerp(0.75f, 1.15f, Mathf.Clamp01(intensity)));
        }

        private void OnAirPowerDepleted()
        {
            PlayOneShot(
                settings != null && settings.airPowerDepletedEnabled,
                _airPowerDepletedSource,
                settings != null ? settings.airPowerDepleted : null,
                settings != null ? settings.airPowerDepletedVolume : 1f,
                settings != null ? settings.airPowerDepletedPitchMin : 1f,
                settings != null ? settings.airPowerDepletedPitchMax : 1f,
                settings != null ? settings.abilityVolume : 1f,
                1f);
        }

        private void PlayOneShot(
            bool enabled,
            AudioSource source,
            AudioClip clip,
            float soundVolume,
            float pitchMin,
            float pitchMax,
            float busVolume,
            float intensity)
        {
            if (!enabled ||
                settings == null ||
                !settings.audioEnabled ||
                source == null ||
                clip == null)
            {
                return;
            }

            float min =
                Mathf.Min(
                    pitchMin,
                    pitchMax);

            float max =
                Mathf.Max(
                    pitchMin,
                    pitchMax);

            source.pitch =
                Mathf.Approximately(min, max)
                    ? min
                    : Random.Range(min, max);

            source.PlayOneShot(
                clip,
                Mathf.Max(0f, intensity) *
                soundVolume *
                busVolume *
                settings.masterVolume);
        }

        private void BuildSourcesIfNeeded()
        {
            if (_footstepSource == null)
                _footstepSource = CreateSource("Audio - Footstep", false);

            if (_jumpSource == null)
                _jumpSource = CreateSource("Audio - Jump", false);

            if (_landingSource == null)
                _landingSource = CreateSource("Audio - Landing", false);

            if (_flipSource == null)
                _flipSource = CreateSource("Audio - Flip", false);

            if (_dashStartSource == null)
                _dashStartSource = CreateSource("Audio - Dash Start", false);

            if (_targetReadySource == null)
                _targetReadySource = CreateSource("Audio - Target Ready", false);

            if (_castReleaseSource == null)
                _castReleaseSource = CreateSource("Audio - Cast Release", false);

            if (_nodeHitSource == null)
                _nodeHitSource = CreateSource("Audio - Node Hit", false);

            if (_returnLaunchSource == null)
                _returnLaunchSource = CreateSource("Audio - Return Launch", false);

            if (_rewardArrivalSource == null)
                _rewardArrivalSource = CreateSource("Audio - Reward Arrival", false);

            if (_airPowerDepletedSource == null)
                _airPowerDepletedSource = CreateSource("Audio - Air Power Depleted", false);

            if (_dashLoopSource == null)
                _dashLoopSource = CreateSource("Audio - Dash Loop", true);

            if (_chargeLoopSource == null)
                _chargeLoopSource = CreateSource("Audio - Charge Loop", true);

            if (_ambienceSource == null)
                _ambienceSource = CreateSource("Audio - Ambience", true);

            if (_musicSource == null)
                _musicSource = CreateSource("Audio - Music", true);
        }

        private AudioSource CreateSource(
            string objectName,
            bool loop)
        {
            GameObject go =
                new GameObject(objectName);

            go.transform.SetParent(
                transform,
                false);

            AudioSource source =
                go.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;

            return source;
        }

        private void ApplyLoopClips()
        {
            if (settings == null)
                return;

            if (_dashLoopSource != null)
                _dashLoopSource.clip = settings.dashWindLoop;

            if (_chargeLoopSource != null)
                _chargeLoopSource.clip = settings.chargeLoop;

            if (_ambienceSource != null)
            {
                _ambienceSource.clip = settings.ambienceLoop;
                _ambienceSource.volume =
                    settings.ambienceVolume *
                    settings.masterVolume;
                _ambienceSource.pitch =
                    settings.ambiencePitch;
            }

            if (_musicSource != null)
            {
                _musicSource.clip = settings.musicLoop;
                _musicSource.volume =
                    settings.musicVolume *
                    settings.masterVolume;
                _musicSource.pitch =
                    settings.musicPitch;
            }
        }

        private void UpdateWorldLoopStates()
        {
            if (settings == null)
                return;

            UpdateWorldLoop(
                _ambienceSource,
                settings.ambienceEnabled,
                settings.ambienceLoop,
                settings.ambienceVolume,
                settings.ambiencePitch);

            UpdateWorldLoop(
                _musicSource,
                settings.musicEnabled,
                settings.musicLoop,
                settings.musicVolume,
                settings.musicPitch);
        }

        private void UpdateWorldLoop(
            AudioSource source,
            bool enabled,
            AudioClip clip,
            float volume,
            float pitch)
        {
            if (source == null)
                return;

            if (!enabled ||
                clip == null)
            {
                StopSource(source);
                return;
            }

            if (source.clip != clip)
                source.clip = clip;

            source.volume =
                volume *
                settings.masterVolume;

            source.pitch =
                pitch;

            if (!source.isPlaying)
                source.Play();
        }

        private void StartWorldLoops()
        {
            ApplyLoopClips();
            UpdateWorldLoopStates();
        }

        private void StopAllLoops()
        {
            StopSource(_dashLoopSource);
            StopSource(_chargeLoopSource);
            StopSource(_ambienceSource);
            StopSource(_musicSource);
        }

        private static void StopSource(
            AudioSource source)
        {
            if (source != null &&
                source.isPlaying)
            {
                source.Stop();
            }
        }
    }
}
