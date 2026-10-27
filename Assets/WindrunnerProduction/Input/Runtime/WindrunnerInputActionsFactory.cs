using UnityEngine;
using UnityEngine.InputSystem;

namespace Windrunner.Production.Input
{
    /// <summary>
    /// One authoring source for the project InputActionAsset. The Editor saves
    /// it as .inputactions so action/binding IDs persist across game sessions.
    /// </summary>
    public static class WindrunnerInputActionsFactory
    {
        public static InputActionAsset CreateDefaults()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "WindrunnerGameplay";

            var gameplay = asset.AddActionMap("Gameplay");
            var move = gameplay.AddAction("Move", InputActionType.Value);
            move.expectedControlType = "Vector2";
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            var look = gameplay.AddAction("Look", InputActionType.Value);
            look.expectedControlType = "Vector2";
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick");

            AddButton(gameplay, "Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            AddButton(gameplay, "Dash", "<Keyboard>/leftShift", "<Gamepad>/rightTrigger");
            AddButton(gameplay, "Cast", "<Mouse>/leftButton", "<Gamepad>/leftTrigger");
            AddButton(gameplay, "Sling", "<Mouse>/rightButton", "<Gamepad>/rightShoulder");
            AddButton(gameplay, "SwimUp", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            AddButton(gameplay, "SwimDown", "<Keyboard>/leftCtrl", "<Keyboard>/rightCtrl", "<Gamepad>/buttonEast");
            AddButton(gameplay, "RelockCursor", "<Mouse>/leftButton");

            var system = asset.AddActionMap("System");
            AddButton(system, "Pause", "<Keyboard>/escape", "<Gamepad>/start");

            var ui = asset.AddActionMap("UI");
            var point = ui.AddAction("Point", InputActionType.PassThrough);
            point.expectedControlType = "Vector2";
            point.AddBinding("<Mouse>/position");

            var click = ui.AddAction("Click", InputActionType.PassThrough);
            click.expectedControlType = "Button";
            click.AddBinding("<Mouse>/leftButton");

            var scroll = ui.AddAction("ScrollWheel", InputActionType.PassThrough);
            scroll.expectedControlType = "Vector2";
            scroll.AddBinding("<Mouse>/scroll");

            var navigate = ui.AddAction("Navigate", InputActionType.PassThrough);
            navigate.expectedControlType = "Vector2";
            navigate.AddBinding("<Gamepad>/leftStick");
            navigate.AddBinding("<Gamepad>/dpad");
            navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

            AddButton(ui, "Submit", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
            AddButton(ui, "Cancel", "<Keyboard>/backspace", "<Gamepad>/buttonEast");
            return asset;
        }

        private static void AddButton(InputActionMap map, string name, params string[] paths)
        {
            var action = map.AddAction(name, InputActionType.Button);
            action.expectedControlType = "Button";
            foreach (string path in paths)
                action.AddBinding(path);
        }
    }
}
