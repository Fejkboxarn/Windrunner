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

        private const string ProgressionActionsJson = @"{""maps"":[{""name"":""Progression"",""id"":""977b9ff5-e34e-43dc-9985-4cf61bd291d0"",""actions"":[{""name"":""JournalNext"",""type"":""Button"",""id"":""e8ae2f6b-043b-42ed-97e6-81d568970c45""},{""name"":""TrackQuest"",""type"":""Button"",""id"":""4643c37c-85e5-41bd-9210-a611505fbfa8""},{""name"":""ViewCutscene"",""type"":""Button"",""id"":""ba50c1c0-87a4-42ba-8dda-b15a41511644""},{""name"":""Interact"",""type"":""Button"",""id"":""049b391b-de56-4308-97d9-5ed03661bda8""},{""name"":""Journal"",""type"":""Button"",""id"":""e490bef8-7078-4552-b25c-c7cf9b16b815""},{""name"":""Skip"",""type"":""Button"",""id"":""cb8c2bc2-7f26-4ec3-b012-b39de8f4da99""},{""name"":""JournalPrevious"",""type"":""Button"",""id"":""1eccc7ee-1a61-5c1b-8ca0-f18779910850""},{""name"":""JournalBack"",""type"":""Button"",""id"":""690ce967-812a-53ee-8909-131d1d78fa76""},{""name"":""JournalScrollDown"",""type"":""Button"",""id"":""97ffbce2-f919-538a-9930-cf09dfe4561d""},{""name"":""JournalScrollUp"",""type"":""Button"",""id"":""94836343-d3dd-5072-9df3-b34e25ac8df0""},{""name"":""FixtureGuide"",""type"":""Button"",""id"":""3f45a866-ac45-512f-8c36-76bde264092c""},{""name"":""FixtureTools"",""type"":""Button"",""id"":""7b940987-312b-51ff-9b95-53dcf88d427b""},{""name"":""JournalCategoryNext"",""type"":""Button"",""id"":""cf22ad70-76ef-5646-b754-c738831bccc6""},{""name"":""JournalCategoryPrevious"",""type"":""Button"",""id"":""59720796-cd7a-5959-9cbc-396ad7905dc8""}],""bindings"":[{""action"":""JournalNext"",""path"":""<Keyboard>/downArrow"",""id"":""e159ef9d-c4c0-46e7-a154-e9fe5556218e""},{""action"":""TrackQuest"",""path"":""<Keyboard>/t"",""id"":""12d302cb-3572-442a-99f3-d7a63f35815a""},{""action"":""ViewCutscene"",""path"":""<Keyboard>/c"",""id"":""f593d6cf-bf8c-4e9e-9f8c-55ed53c71963""},{""action"":""Interact"",""path"":""<Keyboard>/e"",""id"":""b29e12b7-c460-4500-8e05-3feea66b1e19""},{""action"":""Interact"",""path"":""<Gamepad>/buttonWest"",""id"":""7ef004f1-f5a4-4343-a27d-e973657d322e""},{""action"":""Journal"",""path"":""<Keyboard>/j"",""id"":""0e316ca4-4f80-49f5-a497-eb1117c493f4""},{""action"":""Skip"",""path"":""<Keyboard>/enter"",""id"":""b54f8f6b-ee0b-4b5e-b466-bb1b35f668eb""},{""action"":""Journal"",""path"":""<Gamepad>/select"",""id"":""9ec73bb1-5c96-5fcd-b39a-3dd6c0e8ac5f""},{""action"":""JournalNext"",""path"":""<Gamepad>/dpad/down"",""id"":""1b71bc35-34a3-512c-84a7-83954e97b066""},{""action"":""JournalPrevious"",""path"":""<Keyboard>/upArrow"",""id"":""f8ee46f9-a771-5ac8-a0e6-dceb20921a7f""},{""action"":""JournalPrevious"",""path"":""<Gamepad>/dpad/up"",""id"":""507f43f5-9327-51a5-8ea1-ed956e6c11f1""},{""action"":""TrackQuest"",""path"":""<Gamepad>/buttonSouth"",""id"":""cdc9f098-7f85-597f-ad40-12190da15e59""},{""action"":""ViewCutscene"",""path"":""<Gamepad>/buttonNorth"",""id"":""a6099b1e-d9ba-5ce1-ab63-07e091c1d318""},{""action"":""Skip"",""path"":""<Gamepad>/buttonSouth"",""id"":""aa61c4bf-76d0-59e6-8978-a003941b7f92""},{""action"":""JournalBack"",""path"":""<Keyboard>/backspace"",""id"":""67cda33e-86a4-50f7-b639-5b5fd74f169c""},{""action"":""JournalBack"",""path"":""<Gamepad>/buttonEast"",""id"":""4b0748e3-682d-5b78-a179-a58e68014720""},{""action"":""JournalScrollDown"",""path"":""<Keyboard>/pageDown"",""id"":""81dc9bfd-ac9e-54f4-a895-9519fed1bac9""},{""action"":""JournalScrollDown"",""path"":""<Gamepad>/rightShoulder"",""id"":""0cf13145-4543-5b25-be7d-5e8844454c52""},{""action"":""JournalScrollUp"",""path"":""<Keyboard>/pageUp"",""id"":""e451fce8-9411-503d-ad31-819cd0b3ac48""},{""action"":""JournalScrollUp"",""path"":""<Gamepad>/leftShoulder"",""id"":""8227177a-9b82-5ed4-b0e8-cb14a6f67270""},{""action"":""FixtureGuide"",""path"":""<Keyboard>/f1"",""id"":""9e2be8fe-537f-5977-8fa4-5ead67977345""},{""action"":""FixtureGuide"",""path"":""<Gamepad>/rightStickPress"",""id"":""096a5713-0a33-5183-b626-a7b5bb9e5664""},{""action"":""FixtureTools"",""path"":""<Keyboard>/f2"",""id"":""fa1c372b-1325-59f8-b1ba-e27b782f547e""},{""action"":""FixtureTools"",""path"":""<Gamepad>/leftStickPress"",""id"":""84eccc6d-34e7-5456-be1b-f97c41171383""},{""action"":""JournalCategoryNext"",""path"":""<Keyboard>/rightArrow"",""id"":""bab64147-7891-5a7f-a803-edac436d5fe2""},{""action"":""JournalCategoryNext"",""path"":""<Gamepad>/rightTrigger"",""id"":""f364c0e9-0600-52bb-9104-fa555e8c593a""},{""action"":""JournalCategoryPrevious"",""path"":""<Keyboard>/leftArrow"",""id"":""d5a09f95-97e0-5875-bf88-5740d6aa7d31""},{""action"":""JournalCategoryPrevious"",""path"":""<Gamepad>/leftTrigger"",""id"":""114dcf3f-a70f-5c54-9576-d3c0b6ee3db8""}]}]}";

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
