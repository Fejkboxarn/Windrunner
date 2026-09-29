using UnityEngine;
using UnityEngine.InputSystem;

namespace AirflowPrototype
{
    /// <summary>
    /// Prototype-local input adapter. It creates actions in code so the package
    /// remains drop-in. This can later be swapped for a project InputActionAsset.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private InputActionMap _gameplay;
        private InputAction _move;
        private InputAction _look;
        private InputAction _jump;
        private InputAction _dash;
        private InputAction _cast;

        public Vector2 Move => _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => _look != null ? _look.ReadValue<Vector2>() : Vector2.zero;
        public bool JumpPressedThisFrame => _jump != null && _jump.WasPressedThisFrame();
        public bool DashHeld => _dash != null && _dash.IsPressed();

        public bool CastHeld => _cast != null && _cast.IsPressed();
        public bool CastPressedThisFrame => _cast != null && _cast.WasPressedThisFrame();
        public bool CastReleasedThisFrame => _cast != null && _cast.WasReleasedThisFrame();

        public bool LookIsPointer
        {
            get
            {
                var control = _look?.activeControl;
                return control != null && control.device is Pointer;
            }
        }

        private void Awake()
        {
            BuildActions();
        }

        private void OnEnable()
        {
            _gameplay?.Enable();
        }

        private void OnDisable()
        {
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
