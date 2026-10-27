using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Windrunner.Production.Input;
using Windrunner.Production.Unity;

namespace Windrunner.Production.Input.Editor
{
    public static class WindrunnerInputSetup
    {
        private const string AssetPath =
            "Assets/WindrunnerProduction/Input/Generated/WindrunnerGameplay.inputactions";

        [MenuItem("Tools/Windrunner/Production/Batch 02 - Install Input and Pause")]
        public static void Install()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Windrunner Input] Run setup in Edit Mode.");
                return;
            }

            ProjectRoot[] roots = Object.FindObjectsByType<ProjectRoot>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (roots.Length != 1)
            {
                Debug.LogError("[Windrunner Input] Install exactly one Batch 01 ProjectRoot first.");
                return;
            }

            EventSystem[] existingEventSystems = Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existingEventSystems.Length > 1)
            {
                Debug.LogError("[Windrunner Input] Multiple EventSystems found. " +
                    "Resolve those first so pause UI receives events exactly once.");
                return;
            }

            InputActionAsset actions = EnsureAuthoringActions();
            if (actions == null) return;
            GameObject root = roots[0].gameObject;
            InputSystemUIInputModule module = EnsureUiModule(root);
            WindrunnerPauseMenuView view = root.GetComponentInChildren<WindrunnerPauseMenuView>(true);
            if (view == null)
                view = CreatePauseUi(root.transform);

            WindrunnerInputHost host = root.GetComponent<WindrunnerInputHost>();
            if (host == null)
                host = Undo.AddComponent<WindrunnerInputHost>(root);
            host.Configure(actions, view, module);

            EditorUtility.SetDirty(host);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;

            Debug.Log("[Windrunner Input] Installed centralized actions, pause menu, rebinding UI " +
                      "and camera sensitivity. Save the scene and enter Play Mode.");
        }

        [MenuItem("Tools/Windrunner/Production/Batch 02 - Audit Input Scene")]
        public static void AuditInputScene()
        {
            int failures = 0;
            ProjectRoot[] roots = Object.FindObjectsByType<ProjectRoot>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            WindrunnerInputHost[] hosts = Object.FindObjectsByType<WindrunnerInputHost>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            EventSystem[] events = Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (roots.Length != 1)
            {
                Debug.LogError("[Windrunner Input Audit] Expected one ProjectRoot, found " + roots.Length);
                failures++;
            }
            if (hosts.Length != 1)
            {
                Debug.LogError("[Windrunner Input Audit] Expected one InputHost, found " + hosts.Length);
                failures++;
            }
            if (events.Length != 1)
            {
                Debug.LogError("[Windrunner Input Audit] Expected one EventSystem, found " + events.Length);
                failures++;
            }

            if (hosts.Length == 1)
            {
                WindrunnerInputHost host = hosts[0];
                if (host.AuthoringActions == null ||
                    host.AuthoringActions.FindActionMap("Gameplay", false) == null ||
                    host.AuthoringActions.FindActionMap("System", false) == null ||
                    host.AuthoringActions.FindActionMap("UI", false) == null)
                {
                    Debug.LogError("[Windrunner Input Audit] Missing authoring asset or required maps.");
                    failures++;
                }
                if (host.PauseView == null || host.PauseView.Panel == null ||
                    host.PauseView.ResumeButton == null ||
                    host.PauseView.BindingRowTemplate == null)
                {
                    Debug.LogError("[Windrunner Input Audit] Pause UI view has missing references.");
                    failures++;
                }
                if (host.UiModule == null ||
                    host.UiModule.GetComponent<EventSystem>() == null)
                {
                    Debug.LogError("[Windrunner Input Audit] UI InputModule is not linked correctly.");
                    failures++;
                }
            }

            if (failures == 0)
                Debug.Log("[Windrunner Input Audit] Scene wiring PASSED. " +
                          "Still run the Play Mode and build regression checklist.");
            else
                Debug.LogError("[Windrunner Input Audit] Found " + failures +
                               " scene wiring issue(s). Fix these before entering Play Mode.");
        }

        private static InputActionAsset EnsureAuthoringActions()
        {
            InputActionAsset existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            if (existing != null) return existing;

            string directory = Path.GetDirectoryName(AssetPath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            InputActionAsset defaults = WindrunnerInputActionsFactory.CreateDefaults();
            try
            {
                File.WriteAllText(AssetPath, defaults.ToJson());
            }
            finally
            {
                Object.DestroyImmediate(defaults);
            }

            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            if (asset == null)
                Debug.LogError("[Windrunner Input] Could not import " + AssetPath);
            return asset;
        }

        private static InputSystemUIInputModule EnsureUiModule(GameObject owner)
        {
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystem == null)
            {
                GameObject go = new GameObject("WindrunnerEventSystem", typeof(EventSystem));
                Undo.RegisterCreatedObjectUndo(go, "Create Windrunner EventSystem");
                go.transform.SetParent(owner.transform, false);
                eventSystem = go.GetComponent<EventSystem>();
            }

            InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null)
                module = Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);

            module.enabled = false;
            module.UnassignActions();

            BaseInputModule[] existingModules = eventSystem.GetComponents<BaseInputModule>();
            foreach (BaseInputModule other in existingModules)
            {
                if (other != module && other.enabled)
                {
                    other.enabled = false;
                    Debug.LogWarning("[Windrunner Input] Disabled old UI input module " +
                                     other.GetType().Name + " to prevent duplicate UI events.");
                }
            }
            EditorUtility.SetDirty(eventSystem);
            return module;
        }

        private static WindrunnerPauseMenuView CreatePauseUi(Transform parent)
        {
            var resources = new DefaultControls.Resources();
            GameObject canvasObject = new GameObject("WindrunnerPauseCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(WindrunnerPauseMenuView));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Windrunner Pause UI");
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panel = DefaultControls.CreatePanel(resources);
            panel.name = "PausePanel";
            panel.transform.SetParent(canvasObject.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(620f, 860f);
            panel.GetComponent<Image>().color = new Color(0.075f, 0.10f, 0.16f, 0.96f);

            CreateText(panel.transform, resources, "PAUSED", 27, 22, 560, 56, 28);
            Button resume = CreateButton(panel.transform, resources, "Resume", 85, 510, 46);
            CreateText(panel.transform, resources, "INPUT BINDINGS", 143, 18, 520, 32, 20);

            GameObject scrollObject = DefaultControls.CreateScrollView(resources);
            scrollObject.name = "BindingScrollView";
            scrollObject.transform.SetParent(panel.transform, false);
            Position(scrollObject.GetComponent<RectTransform>(), 181, 540, 390);
            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            if (scroll.horizontalScrollbar != null)
                Object.DestroyImmediate(scroll.horizontalScrollbar.gameObject);
            scroll.horizontalScrollbar = null;

            RectTransform content = scroll.content;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 3f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject templateObject = DefaultControls.CreateButton(resources);
            templateObject.name = "BindingRowTemplate";
            templateObject.transform.SetParent(content, false);
            templateObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            Text rowLabel = templateObject.GetComponentInChildren<Text>(true);
            if (rowLabel != null) rowLabel.fontSize = 13;
            templateObject.SetActive(false);
            Button template = templateObject.GetComponent<Button>();

            Button reset = CreateButton(panel.transform, resources, "Reset bindings", 590, 510, 40);
            CreateText(panel.transform, resources, "Mouse sensitivity", 642, 15, 250, 30, 18);
            Slider mouse = CreateSlider(panel.transform, resources, 675);
            CreateText(panel.transform, resources, "Gamepad sensitivity", 719, 15, 250, 30, 18);
            Slider stick = CreateSlider(panel.transform, resources, 752);
            Text status = CreateText(panel.transform, resources,
                "Choose an input to rebind. Backspace cancels.", 804, 12, 555, 35, 15);

            WindrunnerPauseMenuView view = canvasObject.GetComponent<WindrunnerPauseMenuView>();
            view.Configure(panel, resume, reset, template, content, mouse, stick, status);
            panel.SetActive(false);
            return view;
        }

        private static Button CreateButton(Transform parent, DefaultControls.Resources resources,
            string label, float top, float width, float height)
        {
            GameObject go = DefaultControls.CreateButton(resources);
            go.name = label.Replace(' ', '_');
            go.transform.SetParent(parent, false);
            Position(go.GetComponent<RectTransform>(), top, width, height);
            Text text = go.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
                text.fontSize = 18;
            }
            return go.GetComponent<Button>();
        }

        private static Text CreateText(Transform parent, DefaultControls.Resources resources,
            string value, float top, float unusedX, float width, float height, int size)
        {
            GameObject go = DefaultControls.CreateText(resources);
            go.name = "Label";
            go.transform.SetParent(parent, false);
            Position(go.GetComponent<RectTransform>(), top, width, height);
            Text text = go.GetComponent<Text>();
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }

        private static Slider CreateSlider(Transform parent, DefaultControls.Resources resources, float top)
        {
            GameObject go = DefaultControls.CreateSlider(resources);
            go.transform.SetParent(parent, false);
            Position(go.GetComponent<RectTransform>(), top, 490, 29);
            Slider slider = go.GetComponent<Slider>();
            slider.minValue = 0.1f;
            slider.maxValue = 5f;
            slider.value = 1f;
            return slider;
        }

        private static void Position(RectTransform rect, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
