using UnityEngine;
using UnityEngine.InputSystem;

namespace AirflowPrototype
{
    /// <summary>
    /// Node impact starts the curved return stream.
    /// Reward arrival triggers FOV, camera recoil and haptics.
    /// </summary>
    public sealed class AirHitFeedback : MonoBehaviour
    {
        [SerializeField] private AirFlowHitSystem hitSystem;
        [SerializeField] private PlayerSpeedFeedback speedFeedback;
        [SerializeField] private PlayerOrbitCamera orbitCamera;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private AirFlowHitSettings settings;

        [Header("Presentation")]
        [SerializeField] private AirPresentationSettings presentationSettings;

        private float _rumbleRemaining;
        private Gamepad _rumblingGamepad;

        public void Configure(
            AirFlowHitSystem newHitSystem,
            PlayerSpeedFeedback newSpeedFeedback,
            Transform newPlayerTransform,
            AirFlowHitSettings newSettings)
        {
            Unsubscribe();

            hitSystem = newHitSystem;
            speedFeedback = newSpeedFeedback;
            playerTransform = newPlayerTransform;
            settings = newSettings;

            ResolveCameraFeedback();

            if (isActiveAndEnabled)
                Subscribe();
        }

        public void ConfigurePresentation(
            AirPresentationSettings newPresentationSettings)
        {
            presentationSettings =
                newPresentationSettings;
        }

        private void Awake()
        {
            if (hitSystem == null)
                hitSystem =
                    GetComponent<AirFlowHitSystem>();

            if (playerTransform == null)
                playerTransform = transform;

            ResolveCameraFeedback();
        }

        private void ResolveCameraFeedback()
        {
            Camera camera =
                Camera.main;

            if (camera == null)
                return;

            if (speedFeedback == null)
            {
                speedFeedback =
                    camera.GetComponent<PlayerSpeedFeedback>();
            }

            if (orbitCamera == null)
            {
                orbitCamera =
                    camera.GetComponent<PlayerOrbitCamera>();
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopRumble();
        }

        private void Update()
        {
            if (_rumbleRemaining <= 0f)
                return;

            _rumbleRemaining -=
                Time.deltaTime;

            if (_rumbleRemaining <= 0f)
                StopRumble();
        }

        private void Subscribe()
        {
            if (hitSystem == null)
                return;

            hitSystem.ReturnStarted -=
                OnReturnStarted;

            hitSystem.ReturnStarted +=
                OnReturnStarted;

            hitSystem.RewardDelivered -=
                OnRewardDelivered;

            hitSystem.RewardDelivered +=
                OnRewardDelivered;
        }

        private void Unsubscribe()
        {
            if (hitSystem == null)
                return;

            hitSystem.ReturnStarted -=
                OnReturnStarted;

            hitSystem.RewardDelivered -=
                OnRewardDelivered;
        }

        private void OnReturnStarted(
            AirNode node,
            float intensity)
        {
            if (node == null ||
                settings == null)
            {
                return;
            }

            AirReturnStreamVisual.Spawn(
                node.TargetPosition,
                playerTransform,
                settings,
                presentationSettings);
        }

        private void OnRewardDelivered(
            AirNode node,
            float intensity)
        {
            if (settings == null)
                return;

            if (speedFeedback != null)
            {
                speedFeedback.AddFovPunch(
                    settings.fovPunchDegrees *
                    intensity,
                    settings.fovPunchDecayPerSecond);
            }

            if (orbitCamera != null &&
                node != null)
            {
                orbitCamera.AddRewardImpulse(
                    node.TargetPosition,
                    intensity,
                    settings.rewardCameraKickBack,
                    settings.rewardCameraKickUp,
                    settings.rewardCameraRollKick,
                    settings.rewardCameraKickRecovery);
            }

            StartRumble(
                intensity);
        }

        private void StartRumble(
            float intensity)
        {
            Gamepad gamepad =
                Gamepad.current;

            if (gamepad == null ||
                settings == null ||
                settings.rumbleDuration <= 0f)
            {
                return;
            }

            _rumblingGamepad =
                gamepad;

            _rumbleRemaining =
                settings.rumbleDuration;

            gamepad.SetMotorSpeeds(
                settings.lowFrequencyRumble *
                intensity,
                settings.highFrequencyRumble *
                intensity);
        }

        private void StopRumble()
        {
            if (_rumblingGamepad != null)
                _rumblingGamepad.SetMotorSpeeds(0f, 0f);

            _rumblingGamepad = null;
            _rumbleRemaining = 0f;
        }
    }
}
