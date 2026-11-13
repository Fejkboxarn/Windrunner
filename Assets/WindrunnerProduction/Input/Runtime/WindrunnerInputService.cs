using System;
using System.Collections.Generic;
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
        private int gameplayBlocks;
        private InputActionMap progression;
        private readonly HashSet<InputDevice> gameplayDevices = new HashSet<InputDevice>();
        public int InterruptionVersion { get; private set; }

        public event Action BindingsChanged;
        public InputActionAsset Actions { get { return actions; } }
        public bool IsMenuOpen { get { return menuOpen; } }
        public bool IsGameplayBlocked => menuOpen || gameplayBlocks > 0;
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
            // Runtime-only extension with stable IDs; existing authoring assets remain untouched.
            progression = actions.FindActionMap("Progression", false);
            if (progression == null)
            {
                progression = InputActionMap.FromJson(ProgressionActionsJson)[0];
                actions.AddActionMap(progression);
            }

            string json = PlayerPrefs.GetString(OverridesKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try { actions.LoadBindingOverridesFromJson(json); }
                catch (Exception ex) { Debug.LogWarning("[Windrunner Input] Could not load saved binds: " + ex.Message); }
            }

            MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(MouseKey, 1f), 0.1f, 5f);
            StickSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(StickKey, 1f), 0.1f, 5f);
            system.Enable();
            progression.Enable();
            gameplay.Enable();
            ui.Disable();
            gameplay.actionTriggered += TrackGameplayDevice;
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private void TrackGameplayDevice(InputAction.CallbackContext context)
        {
            if (context.started || context.performed)
                gameplayDevices.Add(context.control.device);
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change != InputDeviceChange.Removed && change != InputDeviceChange.Disconnected &&
                change != InputDeviceChange.Disabled) return;
            if (gameplayDevices.Remove(device)) InterruptionVersion++;
        }

        public InputAction GetAction(string map, string name)
        {
            if (disposed) return null;
            InputActionMap actionMap = actions.FindActionMap(map, false);
            return actionMap != null ? actionMap.FindAction(name, false) : null;
        }

        public bool ButtonHeld(string name) => !IsGameplayBlocked && GetAction("Gameplay", name)?.IsPressed() == true;
        public bool ButtonPressed(string name) => !IsGameplayBlocked && GetAction("Gameplay", name)?.WasPressedThisFrame() == true;
        public bool ButtonReleased(string name) => !IsGameplayBlocked && GetAction("Gameplay", name)?.WasReleasedThisFrame() == true;
        public bool ProgressionPressed(string name) => !IsRebinding && GetAction("Progression", name)?.WasPressedThisFrame() == true;
        public string ProgressionBindingLabel(string name) => GetAction("Progression", name)?.GetBindingDisplayString() ?? string.Empty;
        public IDisposable BlockGameplay()
        {
            if (disposed) throw new ObjectDisposedException(nameof(WindrunnerInputService));
            gameplayBlocks++; InterruptionVersion++; ApplyGameplayMap();
            return new GameplayBlock(this);
        }
        private sealed class GameplayBlock : IDisposable
        {
            private WindrunnerInputService owner;
            public GameplayBlock(WindrunnerInputService owner) { this.owner = owner; }
            public void Dispose() { if (owner == null) return; var service = owner; owner = null; service.gameplayBlocks--; service.ApplyGameplayMap(); }
        }
        private void ApplyGameplayMap()
        {
            if (disposed) return;
            if (IsGameplayBlocked) gameplay.Disable(); else gameplay.Enable();
        }

        public Vector2 Axis(string name)
        {
            InputAction action = !IsGameplayBlocked ? GetAction("Gameplay", name) : null;
            return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
        }

        public bool LookIsPointer
        {
            get
            {
                InputAction action = GetAction("Gameplay", "Look");
                return !IsGameplayBlocked && action != null && action.activeControl != null &&
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
                ApplyGameplayMap();
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

            // A map reports disabled when its sole action is suspended for
            // rebinding. Restore from our ownership policy, not that transient
            // aggregate state, and never enable gameplay behind an open menu.
            bool mapShouldBeEnabled = action.actionMap == system || action.actionMap == progression ||
                (action.actionMap == gameplay && !IsGameplayBlocked) ||
                (action.actionMap == ui && menuOpen);
            if (!disposed && previouslyEnabled && mapShouldBeEnabled)
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

        private const string ProgressionActionsJson = @"{""maps"":[{""name"":""Progression"",""id"":""977b9ff5-e34e-43dc-9985-4cf61bd291d0"",
""actions"":[{""name"":""JournalNext"",""type"":""Button"",""id"":""e8ae2f6b-043b-42ed-97e6-81d568970c45""},{""name"":""TrackQuest"",""type"":""Button"",""id"":""4643c37c-85e5-41bd-9210-a611505fbfa8""},{""name"":""ViewCutscene"",""type"":""Button"",""id"":""ba50c1c0-87a4-42ba-8dda-b15a41511644""},{""name"":""Interact"",""type"":""Button"",""id"":""049b391b-de56-4308-97d9-5ed03661bda8""},{""name"":""Journal"",""type"":""Button"",""id"":""e490bef8-7078-4552-b25c-c7cf9b16b815""},{""name"":""Skip"",""type"":""Button"",""id"":""cb8c2bc2-7f26-4ec3-b012-b39de8f4da99""}],
""bindings"":[{""action"":""JournalNext"",""path"":""<Keyboard>/downArrow"",""id"":""e159ef9d-c4c0-46e7-a154-e9fe5556218e""},{""action"":""TrackQuest"",""path"":""<Keyboard>/t"",""id"":""12d302cb-3572-442a-99f3-d7a63f35815a""},{""action"":""ViewCutscene"",""path"":""<Keyboard>/c"",""id"":""f593d6cf-bf8c-4e9e-9f8c-55ed53c71963""},{""action"":""Interact"",""path"":""<Keyboard>/e"",""id"":""b29e12b7-c460-4500-8e05-3feea66b1e19""},{""action"":""Interact"",""path"":""<Gamepad>/buttonWest"",""id"":""7ef004f1-f5a4-4343-a27d-e973657d322e""},{""action"":""Journal"",""path"":""<Keyboard>/j"",""id"":""0e316ca4-4f80-49f5-a497-eb1117c493f4""},{""action"":""Skip"",""path"":""<Keyboard>/enter"",""id"":""b54f8f6b-ee0b-4b5e-b466-bb1b35f668eb""}]}]}";

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            InputSystem.onDeviceChange -= OnDeviceChange;
            gameplay.actionTriggered -= TrackGameplayDevice;
            gameplayDevices.Clear();
            // Close the service before cancellation invokes client callbacks;
            // they must not be able to create a new operation during teardown.
            try { CancelRebind(); }
            finally
            {
                if (actions != null)
                {
                    actions.Disable();
                    UnityEngine.Object.Destroy(actions);
                    actions = null;
                }
            }
        }
    }
}
