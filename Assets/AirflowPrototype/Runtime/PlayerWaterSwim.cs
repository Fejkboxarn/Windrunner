using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AirflowPrototype
{
    /// <summary>
    /// Additive swimming layer that runs after PlayerMotor.
    /// It does not replace or edit PlayerMotor: normal horizontal locomotion
    /// still happens first, then water drag/buoyancy modifies the resulting
    /// CharacterController displacement.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerWaterSwim : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private CharacterController controller;
        [SerializeField] private WaterSwimSettings settings;

        private Vector3 _previousPosition;
        private bool _hasPreviousPosition;

        private bool _isSwimming;
        private float _surfaceY;
        private float _timeWithoutWater;
        private bool _entryLiftPending;

        public bool IsSwimming => _isSwimming;
        public float CurrentWaterSurfaceY => _surfaceY;

        public event Action<bool> SwimmingChanged;

        public void Configure(
            PlayerMotor newMotor,
            WaterSwimSettings newSettings)
        {
            motor = newMotor;
            controller =
                newMotor != null
                    ? newMotor.GetComponent<CharacterController>()
                    : GetComponent<CharacterController>();

            settings = newSettings;

            _previousPosition =
                transform.position;

            _hasPreviousPosition = true;
        }

        private void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();

            if (controller == null)
                controller = GetComponent<CharacterController>();

            _previousPosition =
                transform.position;

            _hasPreviousPosition = true;
        }

        private void OnEnable()
        {
            _previousPosition =
                transform.position;

            _hasPreviousPosition = true;
        }

        private void OnDisable()
        {
            SetSwimming(false);

            _hasPreviousPosition = false;
        }

        private void Update()
        {
            if (controller == null ||
                settings == null)
            {
                _previousPosition =
                    transform.position;

                _hasPreviousPosition = true;
                return;
            }

            Vector3 currentPosition =
                transform.position;

            Vector3 motorFrameDelta =
                _hasPreviousPosition
                    ? currentPosition -
                      _previousPosition
                    : Vector3.zero;

            bool foundWater =
                TryFindWaterSurface(
                    out float foundSurfaceY);

            if (foundWater)
            {
                _surfaceY =
                    foundSurfaceY;

                _timeWithoutWater = 0f;

                if (!_isSwimming)
                {
                    SetSwimming(true);
                    _entryLiftPending = true;
                }
            }
            else if (_isSwimming)
            {
                _timeWithoutWater +=
                    Time.deltaTime;

                if (_timeWithoutWater >=
                    settings.exitGraceTime)
                {
                    SetSwimming(false);
                }
            }

            if (_isSwimming)
            {
                ApplySwimming(
                    motorFrameDelta);
            }

            _previousPosition =
                transform.position;

            _hasPreviousPosition = true;
        }

        private bool TryFindWaterSurface(
            out float surfaceY)
        {
            surfaceY =
                float.NegativeInfinity;

            IReadOnlyList<AirWaterSurface> surfaces =
                AirWaterSurface.All;

            if (surfaces == null ||
                surfaces.Count == 0)
            {
                return false;
            }

            float controllerBottom =
                controller.bounds.min.y;

            float controllerTop =
                controller.bounds.max.y;

            bool found = false;

            for (int i = 0;
                 i < surfaces.Count;
                 i++)
            {
                AirWaterSurface surface =
                    surfaces[i];

                if (surface == null ||
                    !surface.isActiveAndEnabled)
                {
                    continue;
                }

                if (!surface.TryGetSurfaceHeight(
                        transform.position,
                        0.04f,
                        out float candidateY))
                {
                    continue;
                }

                bool feetReachedWater =
                    controllerBottom <=
                    candidateY +
                    settings.enterSurfaceAllowance;

                bool notFarBelowWater =
                    controllerTop >=
                    candidateY -
                    settings.maximumDetectionDepth;

                if (!feetReachedWater ||
                    !notFarBelowWater)
                {
                    continue;
                }

                if (!found ||
                    candidateY > surfaceY)
                {
                    found = true;
                    surfaceY = candidateY;
                }
            }

            return found;
        }

        private void ApplySwimming(
            Vector3 motorFrameDelta)
        {
            Vector3 correction =
                Vector3.zero;

            if (settings.applyHorizontalDrag)
            {
                float retained =
                    Mathf.Clamp01(
                        settings.horizontalMovementMultiplier);

                Vector3 horizontalDelta =
                    new Vector3(
                        motorFrameDelta.x,
                        0f,
                        motorFrameDelta.z);

                correction -=
                    horizontalDelta *
                    (1f - retained);
            }

            // PlayerMotor has already applied its normal vertical motion this
            // frame. Cancel some/all downward movement before buoyancy.
            if (motorFrameDelta.y < 0f)
            {
                correction.y +=
                    -motorFrameDelta.y *
                    settings.fallingMotionCancellation;
            }

            float rootToBottom =
                controller.bounds.min.y -
                transform.position.y;

            float desiredRootY =
                _surfaceY -
                settings.floatDepth -
                rootToBottom;

            float correctedRootY =
                transform.position.y +
                correction.y;

            float buoyancyError =
                desiredRootY -
                correctedRootY;

            if (buoyancyError >
                settings.surfaceDeadZone)
            {
                float buoyancyStep =
                    buoyancyError *
                    settings.buoyancyStrength *
                    Time.deltaTime;

                buoyancyStep =
                    Mathf.Min(
                        buoyancyStep,
                        settings.maximumBuoyancySpeed *
                        Time.deltaTime);

                correction.y +=
                    Mathf.Max(
                        0f,
                        buoyancyStep);
            }

            if (_entryLiftPending)
            {
                correction.y +=
                    settings.entryLift *
                    Time.deltaTime;

                _entryLiftPending = false;
            }

            if (settings.allowSwimUpInput &&
                SwimUpHeld())
            {
                correction.y +=
                    settings.swimUpSpeed *
                    Time.deltaTime;
            }

            if (settings.allowSwimDownInput &&
                SwimDownHeld())
            {
                correction.y -=
                    settings.swimDownSpeed *
                    Time.deltaTime;
            }

            if (correction.sqrMagnitude >
                0.0000001f)
            {
                controller.Move(
                    correction);
            }
        }

        private void SetSwimming(
            bool value)
        {
            if (_isSwimming == value)
                return;

            _isSwimming = value;

            if (!value)
            {
                _timeWithoutWater = 0f;
                _entryLiftPending = false;
            }

            SwimmingChanged?.Invoke(
                _isSwimming);
        }

        private static bool SwimUpHeld()
        {
            bool keyboard =
                Keyboard.current != null &&
                Keyboard.current.spaceKey.isPressed;

            bool gamepad =
                Gamepad.current != null &&
                Gamepad.current.buttonSouth.isPressed;

            return keyboard ||
                   gamepad;
        }

        private static bool SwimDownHeld()
        {
            bool keyboard =
                Keyboard.current != null &&
                (Keyboard.current.leftCtrlKey.isPressed ||
                 Keyboard.current.rightCtrlKey.isPressed);

            bool gamepad =
                Gamepad.current != null &&
                Gamepad.current.buttonEast.isPressed;

            return keyboard ||
                   gamepad;
        }
    }
}
