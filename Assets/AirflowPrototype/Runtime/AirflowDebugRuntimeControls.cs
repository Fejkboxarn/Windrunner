using UnityEngine;
using UnityEngine.InputSystem;

namespace AirflowPrototype
{
    [DisallowMultipleComponent]
    public sealed class AirflowDebugRuntimeControls : MonoBehaviour
    {
        [Header("Debug Windows")]
        [SerializeField] private MonoBehaviour airflowInfoWindow;
        [SerializeField] private MonoBehaviour airTargetingWindow;

        [Header("Startup Visibility")]
        [SerializeField] private bool airflowInfoVisibleAtStart = false;
        [SerializeField] private bool airTargetingVisibleAtStart = false;

        [Header("Toggle Keys")]
        [SerializeField] private Key airflowInfoToggleKey = Key.F1;
        [SerializeField] private Key airTargetingToggleKey = Key.F2;

        [Header("Frame Rate")]
        [Min(30)]
        [SerializeField] private int targetFrameRate = 166;

        [SerializeField] private bool disableVSyncForFrameCap = true;

        public bool AirflowInfoVisible =>
            airflowInfoWindow != null && airflowInfoWindow.enabled;

        public bool AirTargetingVisible =>
            airTargetingWindow != null && airTargetingWindow.enabled;

        public int TargetFrameRate => targetFrameRate;

        public void Configure(
            MonoBehaviour newAirflowInfoWindow,
            MonoBehaviour newAirTargetingWindow)
        {
            airflowInfoWindow = newAirflowInfoWindow;
            airTargetingWindow = newAirTargetingWindow;
        }

        public void SetStartupVisibility(
            bool airflowInfoVisible,
            bool airTargetingVisible)
        {
            airflowInfoVisibleAtStart = airflowInfoVisible;
            airTargetingVisibleAtStart = airTargetingVisible;
        }

        private void Awake()
        {
            ApplyFrameRateCap();
        }

        private void Start()
        {
            SetAirflowInfoVisible(airflowInfoVisibleAtStart);
            SetAirTargetingVisible(airTargetingVisibleAtStart);
        }

        private void OnEnable()
        {
            ApplyFrameRateCap();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard[airflowInfoToggleKey].wasPressedThisFrame)
                ToggleAirflowInfo();

            if (keyboard[airTargetingToggleKey].wasPressedThisFrame)
                ToggleAirTargeting();
        }

        [ContextMenu("Apply 166 FPS Cap")]
        public void ApplyFrameRateCap()
        {
            targetFrameRate = Mathf.Max(30, targetFrameRate);

            if (disableVSyncForFrameCap)
                QualitySettings.vSyncCount = 0;

            Application.targetFrameRate = targetFrameRate;
        }

        public void ToggleAirflowInfo()
        {
            if (airflowInfoWindow != null)
                SetAirflowInfoVisible(!airflowInfoWindow.enabled);
        }

        public void ToggleAirTargeting()
        {
            if (airTargetingWindow != null)
                SetAirTargetingVisible(!airTargetingWindow.enabled);
        }

        public void SetAirflowInfoVisible(bool visible)
        {
            if (airflowInfoWindow != null)
                airflowInfoWindow.enabled = visible;
        }

        public void SetAirTargetingVisible(bool visible)
        {
            if (airTargetingWindow != null)
                airTargetingWindow.enabled = visible;
        }

        private void OnValidate()
        {
            targetFrameRate = Mathf.Max(30, targetFrameRate);
        }
    }
}
