using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Windrunner.Production.Input
{
    /// <summary>Plain C# owner of pause, cursor and interactive keybind UI.</summary>
    public sealed class WindrunnerPauseMenuController : IDisposable
    {
        private readonly WindrunnerInputService input;
        private readonly WindrunnerPauseMenuView view;
        private readonly InputSystemUIInputModule uiModule;
        private readonly List<Button> bindingRows = new List<Button>();
        private CursorLockMode previousLock;
        private bool previousVisibility;
        private float previousTimeScale = 1f;
        private bool paused;

        public bool IsPaused => paused;

        public WindrunnerPauseMenuController(WindrunnerInputService inputService,
            WindrunnerPauseMenuView menuView, InputSystemUIInputModule module)
        {
            input = inputService ?? throw new ArgumentNullException(nameof(inputService));
            view = menuView;
            uiModule = module;
            if (view != null && view.Panel != null)
                view.Panel.SetActive(false);
            if (uiModule != null)
                uiModule.enabled = false;

            if (view != null)
            {
                if (view.ResumeButton != null) view.ResumeButton.onClick.AddListener(Resume);
                if (view.ResetBindingsButton != null)
                    view.ResetBindingsButton.onClick.AddListener(ResetBindings);
                if (view.MouseSensitivitySlider != null)
                {
                    view.MouseSensitivitySlider.SetValueWithoutNotify(input.MouseSensitivity);
                    view.MouseSensitivitySlider.onValueChanged.AddListener(input.SetMouseSensitivity);
                }
                if (view.StickSensitivitySlider != null)
                {
                    view.StickSensitivitySlider.SetValueWithoutNotify(input.StickSensitivity);
                    view.StickSensitivitySlider.onValueChanged.AddListener(input.SetStickSensitivity);
                }
            }

            input.BindingsChanged += RefreshBindingLabels;
        }

        public void Tick()
        {
            if (input.PausePressedThisFrame)
            {
                if (paused) Resume();
                else Pause();
            }

            if (paused)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void Pause()
        {
            if (paused || input.IsRebinding) return;
            paused = true;
            previousLock = Cursor.lockState;
            previousVisibility = Cursor.visible;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            input.SetMenuOpen(true);
            if (view != null && view.Panel != null)
            {
                view.Panel.SetActive(true);
                if (bindingRows.Count == 0) RebuildBindingRows();
                else RefreshBindingLabels();
                SetStatus("Choose an input to rebind. Backspace cancels.");
            }
            if (uiModule != null) uiModule.enabled = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Resume()
        {
            if (!paused) return;
            if (input.IsRebinding) input.CancelRebind();
            paused = false;
            if (uiModule != null) uiModule.enabled = false;
            if (view != null && view.Panel != null) view.Panel.SetActive(false);
            input.SetMenuOpen(false);
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisibility;
        }

        private void ResetBindings()
        {
            input.ResetBindings();
            SetStatus("Bindings reset to defaults.");
        }

        private void RebuildBindingRows()
        {
            if (view == null || view.BindingsContainer == null || view.BindingRowTemplate == null)
                return;

            foreach (Button row in bindingRows)
                if (row != null) UnityEngine.Object.Destroy(row.gameObject);
            bindingRows.Clear();

            foreach (string mapName in new[] { "Gameplay", "System", "UI" })
            {
                InputActionMap map = input.Actions.FindActionMap(mapName, false);
                if (map == null) continue;
                foreach (InputAction action in map.actions)
                {
                    if (!CanRebindInMenu(mapName, action.name)) continue;
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        if (action.bindings[i].isComposite) continue;
                        int index = i;
                        string capturedMap = mapName;
                        string capturedAction = action.name;
                        Button row = UnityEngine.Object.Instantiate(view.BindingRowTemplate, view.BindingsContainer);
                        row.name = "Binding_" + mapName + "_" + action.name + "_" + i;
                        row.gameObject.SetActive(true);
                        row.onClick.RemoveAllListeners();
                        row.onClick.AddListener(() => BeginBinding(capturedMap, capturedAction, index));
                        bindingRows.Add(row);
                    }
                }
            }

            RefreshBindingLabels();
        }

        private void RefreshBindingLabels()
        {
            if (input.Actions == null) return;
            int rowIndex = 0;
            foreach (string mapName in new[] { "Gameplay", "System", "UI" })
            {
                InputActionMap map = input.Actions.FindActionMap(mapName, false);
                if (map == null) continue;
                foreach (InputAction action in map.actions)
                {
                    if (!CanRebindInMenu(mapName, action.name)) continue;
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        InputBinding binding = action.bindings[i];
                        if (binding.isComposite) continue;
                        if (rowIndex >= bindingRows.Count) return;
                        Button row = bindingRows[rowIndex++];
                        if (row == null) continue;
                        Text label = row.GetComponentInChildren<Text>(true);
                        if (label == null) continue;
                        string part = binding.isPartOfComposite ? "." + binding.name : "";
                        label.text = mapName + "/" + action.name + part + " — " +
                                     action.GetBindingDisplayString(i);
                    }
                }
            }
        }

        private void BeginBinding(string map, string name, int index)
        {
            if (input.IsRebinding) return;
            SetStatus("Press a key/button for " + name + " (Backspace cancels).");
            bool started = input.BeginRebind(map, name, index, success =>
            {
                SetStatus(success ? "Saved " + name + " binding." : "Binding canceled.");
                RefreshBindingLabels();
            });
            if (!started) SetStatus("This binding cannot be rebound.");
        }

        private static bool CanRebindInMenu(string mapName, string actionName)
        {
            // A mouse pointer/click must remain available to interact with the
            // rebinding menu itself. Navigation and confirmation can be remapped.
            return mapName != "UI" ||
                   actionName == "Navigate" ||
                   actionName == "Submit" || actionName == "Cancel";
        }

        private void SetStatus(string message)
        {
            if (view != null && view.StatusText != null)
                view.StatusText.text = message;
        }

        public void Dispose()
        {
            if (paused) Resume();
            input.BindingsChanged -= RefreshBindingLabels;
            if (view != null)
            {
                if (view.ResumeButton != null) view.ResumeButton.onClick.RemoveListener(Resume);
                if (view.ResetBindingsButton != null)
                    view.ResetBindingsButton.onClick.RemoveListener(ResetBindings);
                if (view.MouseSensitivitySlider != null)
                    view.MouseSensitivitySlider.onValueChanged.RemoveListener(input.SetMouseSensitivity);
                if (view.StickSensitivitySlider != null)
                    view.StickSensitivitySlider.onValueChanged.RemoveListener(input.SetStickSensitivity);
            }
        }
    }
}
