using UnityEngine;
using UnityEngine.InputSystem;

namespace AirflowPrototype
{
    public sealed class PlayerOrbitCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private AirflowMovementSettings settings;
        [SerializeField] private LayerMask collisionMask = 1;

        [Header("Feel")]
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private AirFeelSettings feelSettings;

        private float _yaw;
        private float _pitch = 16f;
        private Vector3 _positionVelocity;

        private Vector3 _lastTargetPosition;
        private bool _hasTargetPosition;

        private Vector3 _smoothedLookAhead;
        private float _smoothedPullback;
        private Vector3 _lastTravelDirection;
        private float _turnRoll;

        private float _impactBack;
        private float _impactUp;
        private float _impactRoll;
        private float _impactRecovery = 16f;

        public void Configure(
            Transform newTarget,
            PlayerInputReader newInput,
            AirflowMovementSettings newSettings,
            LayerMask newCollisionMask)
        {
            target = newTarget;
            input = newInput;
            settings = newSettings;
            collisionMask = newCollisionMask;
        }

        public void ConfigureFeedback(
            PlayerMotor newMotor,
            AirFeelSettings newFeelSettings)
        {
            motor = newMotor;
            feelSettings = newFeelSettings;
        }

        public void AddRewardImpulse(
            Vector3 worldSource,
            float intensity,
            float kickBack,
            float kickUp,
            float rollDegrees,
            float recovery)
        {
            intensity = Mathf.Clamp01(intensity);

            _impactBack =
                Mathf.Max(
                    _impactBack,
                    Mathf.Max(0f, kickBack) * intensity);

            _impactUp =
                Mathf.Max(
                    _impactUp,
                    Mathf.Max(0f, kickUp) * intensity);

            float side =
                Vector3.Dot(
                    transform.right,
                    worldSource - transform.position);

            float rollSign =
                Mathf.Abs(side) < 0.01f
                    ? 1f
                    : -Mathf.Sign(side);

            float requestedRoll =
                rollDegrees *
                intensity *
                rollSign;

            if (Mathf.Abs(requestedRoll) >
                Mathf.Abs(_impactRoll))
            {
                _impactRoll = requestedRoll;
            }

            _impactRecovery =
                Mathf.Max(
                    0.01f,
                    recovery);
        }

        private void Start()
        {
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
                _lastTargetPosition = target.position;
                _hasTargetPosition = true;
            }

            LockCursor();
            SnapToDesiredPosition();
        }

        private void LateUpdate()
        {
            if (target == null ||
                input == null ||
                settings == null)
            {
                return;
            }

            HandleCursorState();
            ApplyTargetTranslation();
            UpdateRotationInput();
            UpdateFeelState();
            UpdatePosition();

            _lastTargetPosition = target.position;
            _hasTargetPosition = true;
        }

        private void ApplyTargetTranslation()
        {
            if (!_hasTargetPosition)
            {
                _lastTargetPosition = target.position;
                _hasTargetPosition = true;
                return;
            }

            Vector3 targetDelta =
                target.position -
                _lastTargetPosition;

            transform.position += targetDelta;
        }

        private void HandleCursorState()
        {
            if (Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState =
                    CursorLockMode.None;

                Cursor.visible = true;
            }

            if (Cursor.lockState !=
                    CursorLockMode.Locked &&
                Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                LockCursor();
            }
        }

        private void LockCursor()
        {
            Cursor.lockState =
                CursorLockMode.Locked;

            Cursor.visible = false;
        }

        private void UpdateRotationInput()
        {
            if (Cursor.lockState !=
                CursorLockMode.Locked)
            {
                return;
            }

            Vector2 look = input.Look;

            if (input.LookIsPointer)
            {
                _yaw +=
                    look.x *
                    settings.mouseSensitivity;

                _pitch -=
                    look.y *
                    settings.mouseSensitivity;
            }
            else
            {
                float scale =
                    settings.gamepadLookDegreesPerSecond *
                    Time.deltaTime;

                _yaw += look.x * scale;
                _pitch -= look.y * scale;
            }

            _pitch =
                Mathf.Clamp(
                    _pitch,
                    settings.minCameraPitch,
                    settings.maxCameraPitch);
        }

        private void UpdateFeelState()
        {
            if (motor == null ||
                feelSettings == null)
            {
                RelaxFeelState();
                DecayImpact();
                return;
            }

            float speed =
                motor.HorizontalSpeed;

            Vector3 planarVelocity =
                motor.PlanarVelocity;

            planarVelocity.y = 0f;

            Vector3 travelDirection =
                planarVelocity.sqrMagnitude > 0.04f
                    ? planarVelocity.normalized
                    : Vector3.zero;

            UpdateLookAhead(
                travelDirection,
                speed);

            UpdatePullback(speed);

            UpdateTurnRoll(
                travelDirection,
                speed);

            DecayImpact();
        }

        private void UpdateLookAhead(
            Vector3 travelDirection,
            float speed)
        {
            float speed01 =
                Mathf.InverseLerp(
                    feelSettings.lookAheadStartSpeed,
                    feelSettings.maxFeedbackSpeed,
                    speed);

            Vector3 desiredLookAhead =
                travelDirection *
                feelSettings.lookAheadDistanceAtMaxSpeed *
                speed01;

            float t =
                1f -
                Mathf.Exp(
                    -feelSettings.lookAheadResponse *
                    Time.deltaTime);

            _smoothedLookAhead =
                Vector3.Lerp(
                    _smoothedLookAhead,
                    desiredLookAhead,
                    t);
        }

        private void UpdatePullback(
            float speed)
        {
            float pullback01 =
                Mathf.InverseLerp(
                    feelSettings.cameraPullbackStartSpeed,
                    feelSettings.maxFeedbackSpeed,
                    speed);

            float desiredPullback =
                feelSettings.cameraPullbackAtMaxSpeed *
                pullback01;

            float t =
                1f -
                Mathf.Exp(
                    -feelSettings.cameraPullbackResponse *
                    Time.deltaTime);

            _smoothedPullback =
                Mathf.Lerp(
                    _smoothedPullback,
                    desiredPullback,
                    t);
        }

        private void UpdateTurnRoll(
            Vector3 travelDirection,
            float speed)
        {
            float desiredRoll = 0f;

            float speedInfluence =
                Mathf.InverseLerp(
                    feelSettings.cameraTurnRollStartSpeed,
                    feelSettings.cameraTurnRollFullSpeed,
                    speed);

            if (speedInfluence > 0f &&
                travelDirection.sqrMagnitude > 0.01f &&
                _lastTravelDirection.sqrMagnitude > 0.01f &&
                Time.deltaTime > 0f)
            {
                float signedAngle =
                    Vector3.SignedAngle(
                        _lastTravelDirection,
                        travelDirection,
                        Vector3.up);

                float degreesPerSecond =
                    signedAngle /
                    Time.deltaTime;

                float turn01 =
                    Mathf.Clamp(
                        degreesPerSecond /
                        feelSettings.turnRateForMaxCameraRoll,
                        -1f,
                        1f);

                desiredRoll =
                    -turn01 *
                    feelSettings.maxCameraTurnRoll *
                    speedInfluence;
            }

            if (travelDirection.sqrMagnitude > 0.01f)
                _lastTravelDirection =
                    travelDirection;

            float t =
                1f -
                Mathf.Exp(
                    -feelSettings.cameraTurnRollResponse *
                    Time.deltaTime);

            _turnRoll =
                Mathf.Lerp(
                    _turnRoll,
                    desiredRoll,
                    t);
        }

        private void RelaxFeelState()
        {
            float dt = Time.deltaTime;

            _smoothedLookAhead =
                Vector3.Lerp(
                    _smoothedLookAhead,
                    Vector3.zero,
                    1f - Mathf.Exp(-6f * dt));

            _smoothedPullback =
                Mathf.Lerp(
                    _smoothedPullback,
                    0f,
                    1f - Mathf.Exp(-6f * dt));

            _turnRoll =
                Mathf.Lerp(
                    _turnRoll,
                    0f,
                    1f - Mathf.Exp(-8f * dt));
        }

        private void DecayImpact()
        {
            float t =
                1f -
                Mathf.Exp(
                    -_impactRecovery *
                    Time.deltaTime);

            _impactBack =
                Mathf.Lerp(
                    _impactBack,
                    0f,
                    t);

            _impactUp =
                Mathf.Lerp(
                    _impactUp,
                    0f,
                    t);

            _impactRoll =
                Mathf.Lerp(
                    _impactRoll,
                    0f,
                    t);
        }

        private void UpdatePosition()
        {
            Vector3 pivot =
                target.position +
                Vector3.up *
                settings.cameraPivotHeight +
                _smoothedLookAhead;

            Quaternion baseRotation =
                Quaternion.Euler(
                    _pitch,
                    _yaw,
                    0f);

            float distance =
                settings.cameraDistance +
                _smoothedPullback;

            Vector3 desired =
                pivot -
                baseRotation *
                Vector3.forward *
                distance;

            Vector3 corrected =
                ResolveCameraCollision(
                    pivot,
                    desired);

            float smoothTime =
                Mathf.Max(
                    0.0001f,
                    settings.cameraPositionSmoothTime);

            Vector3 smoothed =
                Vector3.SmoothDamp(
                    transform.position,
                    corrected,
                    ref _positionVelocity,
                    smoothTime);

            Vector3 localImpact =
                new Vector3(
                    0f,
                    _impactUp,
                    -_impactBack);

            transform.position =
                smoothed +
                baseRotation *
                localImpact;

            transform.rotation =
                Quaternion.Euler(
                    _pitch,
                    _yaw,
                    _turnRoll +
                    _impactRoll);
        }

        private Vector3 ResolveCameraCollision(
            Vector3 pivot,
            Vector3 desired)
        {
            Vector3 delta =
                desired - pivot;

            float distance =
                delta.magnitude;

            if (distance <= 0.0001f)
                return desired;

            Vector3 direction =
                delta / distance;

            if (Physics.SphereCast(
                    pivot,
                    settings.cameraCollisionRadius,
                    direction,
                    out RaycastHit hit,
                    distance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                float safeDistance =
                    Mathf.Max(
                        0.1f,
                        hit.distance -
                        settings.cameraCollisionPadding);

                return
                    pivot +
                    direction *
                    safeDistance;
            }

            return desired;
        }

        private void SnapToDesiredPosition()
        {
            if (target == null ||
                settings == null)
            {
                return;
            }

            Vector3 pivot =
                target.position +
                Vector3.up *
                settings.cameraPivotHeight;

            Quaternion rotation =
                Quaternion.Euler(
                    _pitch,
                    _yaw,
                    0f);

            transform.position =
                pivot -
                rotation *
                Vector3.forward *
                settings.cameraDistance;

            transform.rotation =
                rotation;

            _positionVelocity =
                Vector3.zero;
        }

        private void OnEnable()
        {
            if (target != null)
            {
                _lastTargetPosition =
                    target.position;

                _hasTargetPosition =
                    true;
            }
        }

        private void OnDisable()
        {
            _hasTargetPosition = false;
            _positionVelocity = Vector3.zero;
            _smoothedLookAhead = Vector3.zero;
            _smoothedPullback = 0f;
            _turnRoll = 0f;
            _impactBack = 0f;
            _impactUp = 0f;
            _impactRoll = 0f;
        }
    }
}
