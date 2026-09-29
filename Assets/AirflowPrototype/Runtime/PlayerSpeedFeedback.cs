using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Speed-driven FOV plus short external FOV impulses.
    /// Gameplay never writes directly to the camera FOV.
    /// </summary>
    public sealed class PlayerSpeedFeedback : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private AirflowMovementSettings settings;

        private float _fovPunch;
        private float _fovPunchDecayPerSecond = 20f;

        private bool _fovOverrideActive;
        private float _fovOverrideTarget;
        private float _fovOverrideResponse = 12f;

        private bool _timedOverride;
        private float _timedOverrideRemaining;
        private float _overrideRecoveryResponse = 8f;
        private bool _recoveringFromOverride;

        public void Configure(
            Camera newCamera,
            PlayerMotor newMotor,
            AirflowMovementSettings newSettings)
        {
            targetCamera = newCamera;
            motor = newMotor;
            settings = newSettings;
        }

        public void AddFovPunch(float degrees, float decayPerSecond)
        {
            _fovPunch = Mathf.Max(_fovPunch, Mathf.Max(0f, degrees));
            _fovPunchDecayPerSecond = Mathf.Max(0.01f, decayPerSecond);
        }

        /// <summary>
        /// Gives an authored traversal move temporary ownership of FOV.
        /// Uses unscaled time so hit-stop does not freeze the camera response.
        /// </summary>
        public void SetFovOverride(
            float targetFov,
            float response)
        {
            _fovOverrideActive = true;
            _timedOverride = false;
            _recoveringFromOverride = false;

            _fovOverrideTarget =
                Mathf.Clamp(
                    targetFov,
                    1f,
                    179f);

            _fovOverrideResponse =
                Mathf.Max(
                    0.01f,
                    response);
        }

        /// <summary>
        /// Authored FOV pulse that holds, then blends back to normal speed FOV.
        /// </summary>
        public void PulseFovOverride(
            float targetFov,
            float enterResponse,
            float holdDuration,
            float recoveryResponse)
        {
            _fovOverrideActive = true;
            _timedOverride = true;
            _recoveringFromOverride = false;

            _fovOverrideTarget =
                Mathf.Clamp(
                    targetFov,
                    1f,
                    179f);

            _fovOverrideResponse =
                Mathf.Max(
                    0.01f,
                    enterResponse);

            _timedOverrideRemaining =
                Mathf.Max(
                    0f,
                    holdDuration);

            _overrideRecoveryResponse =
                Mathf.Max(
                    0.01f,
                    recoveryResponse);
        }

        public void ClearFovOverride(
            float recoveryResponse)
        {
            _fovOverrideActive = false;
            _timedOverride = false;

            _overrideRecoveryResponse =
                Mathf.Max(
                    0.01f,
                    recoveryResponse);

            _recoveringFromOverride = true;
        }

        private void Start()
        {
            if (targetCamera != null && settings != null)
                targetCamera.fieldOfView = settings.baseFov;
        }

        private void LateUpdate()
        {
            if (targetCamera == null ||
                motor == null ||
                settings == null)
            {
                return;
            }

            float scaledDt =
                Time.deltaTime;

            float unscaledDt =
                Time.unscaledDeltaTime;

            float speed01 =
                Mathf.Clamp01(
                    motor.HorizontalSpeed /
                    settings.speedForMaxFov);

            speed01 =
                speed01 *
                speed01 *
                (3f -
                 2f *
                 speed01);

            float speedFov =
                Mathf.Lerp(
                    settings.baseFov,
                    settings.fastFov,
                    speed01);

            _fovPunch =
                Mathf.MoveTowards(
                    _fovPunch,
                    0f,
                    _fovPunchDecayPerSecond *
                    scaledDt);

            float normalTargetFov =
                speedFov +
                _fovPunch;

            if (_fovOverrideActive)
            {
                if (_timedOverride)
                {
                    _timedOverrideRemaining -=
                        unscaledDt;

                    if (_timedOverrideRemaining <= 0f)
                    {
                        _fovOverrideActive = false;
                        _timedOverride = false;
                        _recoveringFromOverride = true;
                    }
                }
            }

            float targetFov;
            float response;
            float dt;

            if (_fovOverrideActive)
            {
                targetFov =
                    _fovOverrideTarget;

                response =
                    _fovOverrideResponse;

                dt =
                    unscaledDt;
            }
            else if (_recoveringFromOverride)
            {
                targetFov =
                    normalTargetFov;

                response =
                    _overrideRecoveryResponse;

                dt =
                    unscaledDt;

                if (Mathf.Abs(
                        targetCamera.fieldOfView -
                        normalTargetFov) <
                    0.08f)
                {
                    _recoveringFromOverride = false;
                }
            }
            else
            {
                targetFov =
                    normalTargetFov;

                response =
                    settings.fovResponse;

                dt =
                    scaledDt;
            }

            float t =
                1f -
                Mathf.Exp(
                    -Mathf.Max(
                        0.01f,
                        response) *
                    dt);

            targetCamera.fieldOfView =
                Mathf.Lerp(
                    targetCamera.fieldOfView,
                    targetFov,
                    t);
        }

        private void OnDisable()
        {
            _fovPunch = 0f;
            _fovOverrideActive = false;
            _timedOverride = false;
            _recoveringFromOverride = false;
        }
    }
}
