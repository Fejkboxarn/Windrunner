using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Windrunner.Production.Input
{
    /// <summary>Single owner for runtime action maps, rebinding and persistence.</summary>
    public sealed class WindrunnerInputService : IDisposable
    {
        private const string OverridesKey = "Windrunner.Input.BindingOverrides.v1";
        private const string MouseKey = "Windrunner.Input.MouseSensitivity.v1";
        private const string StickKey = "Windrunner.Input.StickSensitivity.v1";

        private InputActionAsset actions;
        private InputActionMap gameplay;
        private InputActionMap system;
        private InputActionMap ui;
        private InputActionRebindingExtensions.RebindingOperation activeRebind;
        private bool disposed;
        private bool menuOpen;

        public event Action BindingsChanged;
        public InputActionAsset Actions { get { return actions; } }
        public bool IsMenuOpen { get { return menuOpen; } }
        public bool IsRebinding { get { return activeRebind != null; } }
        public float MouseSensitivity { get; private set; }
        public float StickSensitivity { get; private set; }

        public WindrunnerInputService(InputActionAsset template)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            // Clone the serialized authoring asset. Never enable or modify it.
            actions = InputActionAsset.FromJson(template.ToJson());
            gameplay = RequireMap("Gameplay");
            system = RequireMap("System");
            ui = RequireMap("UI");

            string json = PlayerPrefs.GetString(OverridesKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try { actions.LoadBindingOverridesFromJson(json); }
                catch (Exception ex) { Debug.LogWarning("[Windrunner Input] Could not load saved binds: " + ex.Message); }
            }

            MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(MouseKey, 1f), 0.1f, 5f);
            StickSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(StickKey, 1f), 0.1f, 5f);
            system.Enable();
            gameplay.Enable();
            ui.Disable();
        }

        public InputAction GetAction(string map, string name)
        {
            if (disposed) return null;
            InputActionMap actionMap = actions.FindActionMap(map, false);
            return actionMap != null ? actionMap.FindAction(name, false) : null;
        }

        public bool ButtonHeld(string name) => !menuOpen && GetAction("Gameplay", name)?.IsPressed() == true;
        public bool ButtonPressed(string name) => !menuOpen && GetAction("Gameplay", name)?.WasPressedThisFrame() == true;
        public bool ButtonReleased(string name) => !menuOpen && GetAction("Gameplay", name)?.WasReleasedThisFrame() == true;

        public Vector2 Axis(string name)
        {
            InputAction action = !menuOpen ? GetAction("Gameplay", name) : null;
            return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
        }

        public bool LookIsPointer
        {
            get
            {
                InputAction action = GetAction("Gameplay", "Look");
                return !menuOpen && action != null && action.activeControl != null &&
                       action.activeControl.device is Pointer;
            }
        }

        public bool PausePressedThisFrame => !IsRebinding && GetAction("System", "Pause")?.WasPressedThisFrame() == true;

        public void SetMenuOpen(bool value)
        {
            if (disposed || menuOpen == value) return;
            menuOpen = value;
            if (menuOpen)
            {
                gameplay.Disable();
                ui.Enable();
            }
            else
            {
                ui.Disable();
                gameplay.Enable();
            }
        }

        public void SetMouseSensitivity(float value)
        {
            MouseSensitivity = Mathf.Clamp(value, 0.1f, 5f);
            PlayerPrefs.SetFloat(MouseKey, MouseSensitivity);
            PlayerPrefs.Save();
        }

        public void SetStickSensitivity(float value)
        {
            StickSensitivity = Mathf.Clamp(value, 0.1f, 5f);
            PlayerPrefs.SetFloat(StickKey, StickSensitivity);
            PlayerPrefs.Save();
        }

        public bool BeginRebind(string mapName, string actionName, int bindingIndex, Action<bool> completed)
        {
            if (disposed || activeRebind != null) return false;
            InputAction action = GetAction(mapName, actionName);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count ||
                action.bindings[bindingIndex].isComposite)
                return false;

            bool enabledBefore = action.enabled;
            action.Disable();

            activeRebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithCancelingThrough("<Keyboard>/backspace")
                .OnComplete(operation => FinishRebind(operation, action, enabledBefore, true, completed))
                .OnCancel(operation => FinishRebind(operation, action, enabledBefore, false, completed));
            activeRebind.Start();
            return true;
        }

        private void FinishRebind(InputActionRebindingExtensions.RebindingOperation operation,
            InputAction action, bool previouslyEnabled, bool success, Action<bool> completed)
        {
            if (!ReferenceEquals(activeRebind, operation)) return;
            activeRebind = null;
            operation.Dispose();

            if (previouslyEnabled && action.actionMap != null && action.actionMap.enabled)
                action.Enable();

            if (success)
            {
                SaveBindings();
                BindingsChanged?.Invoke();
            }
            completed?.Invoke(success);
        }

        public void CancelRebind()
        {
            if (activeRebind != null) activeRebind.Cancel();
        }

        public void ResetBindings()
        {
            if (disposed) return;
            CancelRebind();
            actions.RemoveAllBindingOverrides();
            SaveBindings();
            BindingsChanged?.Invoke();
        }

        private void SaveBindings()
        {
            PlayerPrefs.SetString(OverridesKey, actions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        private InputActionMap RequireMap(string name)
        {
            InputActionMap map = actions.FindActionMap(name, false);
            if (map == null) throw new InvalidOperationException("Missing Windrunner action map: " + name);
            return map;
        }

        public void Dispose()
        {
            if (disposed) return;
            CancelRebind();
            disposed = true;
            if (actions != null)
            {
                actions.Disable();
                UnityEngine.Object.Destroy(actions);
                actions = null;
            }
        }
    }
}
