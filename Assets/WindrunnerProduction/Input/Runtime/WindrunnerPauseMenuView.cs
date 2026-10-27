using UnityEngine;
using UnityEngine.UI;

namespace Windrunner.Production.Input
{
    /// <summary>Only serialized UI references. Presentation logic is in PauseMenuController.</summary>
    [DisallowMultipleComponent]
    public sealed class WindrunnerPauseMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button resetBindingsButton;
        [SerializeField] private Button bindingRowTemplate;
        [SerializeField] private RectTransform bindingsContainer;
        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private Slider stickSensitivitySlider;
        [SerializeField] private Text statusText;

        public GameObject Panel => panel;
        public Button ResumeButton => resumeButton;
        public Button ResetBindingsButton => resetBindingsButton;
        public Button BindingRowTemplate => bindingRowTemplate;
        public RectTransform BindingsContainer => bindingsContainer;
        public Slider MouseSensitivitySlider => mouseSensitivitySlider;
        public Slider StickSensitivitySlider => stickSensitivitySlider;
        public Text StatusText => statusText;

        public void Configure(GameObject newPanel, Button newResume, Button newReset,
            Button newTemplate, RectTransform newContainer, Slider mouseSlider,
            Slider stickSlider, Text newStatus)
        {
            panel = newPanel;
            resumeButton = newResume;
            resetBindingsButton = newReset;
            bindingRowTemplate = newTemplate;
            bindingsContainer = newContainer;
            mouseSensitivitySlider = mouseSlider;
            stickSensitivitySlider = stickSlider;
            statusText = newStatus;
        }
    }
}
