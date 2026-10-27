using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using Windrunner.Production.Unity;

namespace Windrunner.Production.Input
{
    /// <summary>
    /// Unity composition bridge. A temporary static entry point lets legacy
    /// AirflowPrototype components bind without editing each generated scene.
    /// Future character installers should inject WindrunnerInputService directly.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class WindrunnerInputHost : MonoBehaviour
    {
        [SerializeField] private InputActionAsset authoringActions;
        [SerializeField] private WindrunnerPauseMenuView pauseView;
        [SerializeField] private InputSystemUIInputModule uiModule;

        public static WindrunnerInputHost Active { get; private set; }
        public WindrunnerInputService Input { get; private set; }
        public InputActionAsset AuthoringActions => authoringActions;
        public WindrunnerPauseMenuView PauseView => pauseView;
        public InputSystemUIInputModule UiModule => uiModule;
        public bool IsPaused => menu != null && menu.IsPaused;

        private WindrunnerPauseMenuController menu;
        private readonly List<InputActionReference> uiActionReferences = new List<InputActionReference>();

        public void Configure(InputActionAsset actions, WindrunnerPauseMenuView view,
            InputSystemUIInputModule module)
        {
            authoringActions = actions;
            pauseView = view;
            uiModule = module;
        }

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Debug.LogError("[Windrunner Input] Duplicate input host; duplicate disabled.", this);
                enabled = false;
                return;
            }
            if (authoringActions == null)
            {
                Debug.LogError("[Windrunner Input] No InputActionAsset assigned. Run Batch 02 setup.", this);
                enabled = false;
                return;
            }

            Active = this;
            try
            {
                Input = new WindrunnerInputService(authoringActions);
                if (ProjectRoot.Active != null && ProjectRoot.Active.Runtime != null)
                    ProjectRoot.Active.Runtime.Services.Register(Input);
                ConfigureUiModule();
                menu = new WindrunnerPauseMenuController(Input, pauseView, uiModule);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                if (Input != null) Input.Dispose();
                Input = null;
                Active = null;
                enabled = false;
            }
        }

        private void ConfigureUiModule()
        {
            if (uiModule == null) return;
            uiModule.enabled = false;
            uiModule.UnassignActions();
            uiModule.actionsAsset = Input.Actions;
            SetUiAction("Point", reference => uiModule.point = reference);
            SetUiAction("Click", reference => uiModule.leftClick = reference);
            SetUiAction("Navigate", reference => uiModule.move = reference);
            SetUiAction("Submit", reference => uiModule.submit = reference);
            SetUiAction("Cancel", reference => uiModule.cancel = reference);
            SetUiAction("ScrollWheel", reference => uiModule.scrollWheel = reference);
        }

        private void SetUiAction(string name, System.Action<InputActionReference> apply)
        {
            InputAction action = Input.GetAction("UI", name);
            if (action == null) return;
            InputActionReference reference = InputActionReference.Create(action);
            uiActionReferences.Add(reference);
            apply(reference);
        }

        private void Update()
        {
            menu?.Tick();
        }

        private void OnDestroy()
        {
            if (Active != this) return;
            if (menu != null) menu.Dispose();
            menu = null;
            if (uiModule != null)
            {
                uiModule.enabled = false;
                uiModule.UnassignActions();
            }
            foreach (InputActionReference reference in uiActionReferences)
                if (reference != null) Destroy(reference);
            uiActionReferences.Clear();
            if (Input != null) Input.Dispose();
            Input = null;
            Active = null;
        }
    }
}
