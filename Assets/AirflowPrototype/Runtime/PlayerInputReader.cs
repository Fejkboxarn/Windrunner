using UnityEngine;
using UnityEngine.InputSystem;
using Windrunner.Production.Input;

namespace AirflowPrototype
{
    /// <summary>
    /// Legacy-facing data adapter. ProductionInputHost owns the shared actions;
    /// the original local bindings remain a safe fallback for old scenes.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private InputActionMap _gameplay;
        private InputAction _move;
        private InputAction _look;
        private InputAction _jump;
        private InputAction _dash;
        private InputAction _cast;

        private WindrunnerInputService Production => WindrunnerInputHost.Active != null
            ? WindrunnerInputHost.Active.Input : null;
        public bool UsesProductionInput => Production != null;
        public bool IsGameplaySuspended => Production != null && Production.IsMenuOpen;

        public Vector2 Move => Production != null ? Production.Axis("Move") :
            (_move != null ? _move.ReadValue<Vector2>() : Vector2.zero);
        public Vector2 Look => Production != null ? Production.Axis("Look") :
            (_look != null ? _look.ReadValue<Vector2>() : Vector2.zero);
        public bool JumpPressedThisFrame => Production != null ? Production.ButtonPressed("Jump") :
            (_jump != null && _jump.WasPressedThisFrame());
        public bool DashHeld => Production != null ? Production.ButtonHeld("Dash") :
            (_dash != null && _dash.IsPressed());

        public bool CastHeld => Production != null ? Production.ButtonHeld("Cast") :
            (_cast != null && _cast.IsPressed());
        public bool CastPressedThisFrame => Production != null ? Production.ButtonPressed("Cast") :
            (_cast != null && _cast.WasPressedThisFrame());
        public bool CastReleasedThisFrame => Production != null ? Production.ButtonReleased("Cast") :
            (_cast != null && _cast.WasReleasedThisFrame());
        public bool SlingHeld => Production != null ? Production.ButtonHeld("Sling") :
            (Mouse.current != null && Mouse.current.rightButton.isPressed);
        public bool SlingPressedThisFrame => Production != null ? Production.ButtonPressed("Sling") :
            (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
        public bool SlingReleasedThisFrame => Production != null ? Production.ButtonReleased("Sling") :
            (Mouse.current != null && Mouse.current.rightButton.wasReleasedThisFrame);
        public bool SwimUpHeld => Production != null ? Production.ButtonHeld("SwimUp") :
            ((Keyboard.current != null && Keyboard.current.spaceKey.isPressed) ||
             (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed));
        public bool SwimDownHeld => Production != null ? Production.ButtonHeld("SwimDown") :
            ((Keyboard.current != null &&
              (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed)) ||
             (Gamepad.current != null && Gamepad.current.buttonEast.isPressed));
        public bool RelockCursorPressedThisFrame => Production != null ?
            Production.ButtonPressed("RelockCursor") :
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        public bool LookIsPointer
        {
            get
            {
                if (Production != null)
                    return Production.LookIsPointer;
                var control = _look?.activeControl;
                return control != null && control.device is Pointer;
            }
        }

        private void Awake()
        {
            if (!UsesProductionInput)
                BuildActions();
        }

        private void OnEnable()
        {
            if (!UsesProductionInput)
                _gameplay?.Enable();
        }

        private void OnDisable()
        {
            if (!UsesProductionInput)
                _gameplay?.Disable();
        }

        private void OnDestroy()
        {
            _gameplay?.Dispose();
        }

        private void BuildActions()
        {
            if (_gameplay != null)
                return;

            _gameplay = new InputActionMap("Airflow Gameplay");

            _move = _gameplay.AddAction("Move", InputActionType.Value);
            _move.expectedControlType = "Vector2";
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");

            _look = _gameplay.AddAction("Look", InputActionType.Value);
            _look.expectedControlType = "Vector2";
            _look.AddBinding("<Mouse>/delta");
            _look.AddBinding("<Gamepad>/rightStick");

            _jump = _gameplay.AddAction("Jump", InputActionType.Button);
            _jump.AddBinding("<Keyboard>/space");
            _jump.AddBinding("<Gamepad>/buttonSouth");

            _dash = _gameplay.AddAction("Dash", InputActionType.Button);
            _dash.AddBinding("<Keyboard>/leftShift");
            _dash.AddBinding("<Gamepad>/rightTrigger");

            _cast = _gameplay.AddAction("Air Cast", InputActionType.Button);
            _cast.AddBinding("<Mouse>/leftButton");
            _cast.AddBinding("<Gamepad>/leftTrigger");
        }
    }
}
