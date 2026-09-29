using UnityEngine;
using UnityEngine.UI;

namespace AirflowPrototype
{
    /// <summary>
    /// Keeps a screen-space HUD Canvas configured for resolution-independent UI.
    /// RectTransform anchors still determine where individual HUD elements sit.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class ResponsiveHUDCanvas : MonoBehaviour
    {
        [Header("Reference Resolution")]
        [SerializeField]
        private Vector2 referenceResolution =
            new Vector2(1920f, 1080f);

        [Header("Aspect Ratio Scaling")]
        [Tooltip(
            "0 = match screen width, 1 = match screen height. " +
            "0.5 is a balanced default for landscape HUDs.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float matchWidthOrHeight = 0.5f;

        [Header("UI Density")]
        [Min(1f)]
        [SerializeField]
        private float referencePixelsPerUnit = 100f;

        [Header("Runtime")]
        [Tooltip(
            "If enabled, the component reapplies these CanvasScaler settings " +
            "when enabled so accidental inspector changes do not break builds.")]
        [SerializeField]
        private bool enforceAtRuntime = true;

        private CanvasScaler _scaler;

        public Vector2 ReferenceResolution =>
            referenceResolution;

        public float MatchWidthOrHeight =>
            matchWidthOrHeight;

        public void Configure(
            Vector2 newReferenceResolution,
            float newMatchWidthOrHeight)
        {
            referenceResolution =
                new Vector2(
                    Mathf.Max(
                        1f,
                        newReferenceResolution.x),
                    Mathf.Max(
                        1f,
                        newReferenceResolution.y));

            matchWidthOrHeight =
                Mathf.Clamp01(
                    newMatchWidthOrHeight);

            Apply();
        }

        private void Reset()
        {
            referenceResolution =
                new Vector2(
                    1920f,
                    1080f);

            matchWidthOrHeight =
                0.5f;

            referencePixelsPerUnit =
                100f;

            enforceAtRuntime = true;

            Apply();
        }

        private void Awake()
        {
            if (Application.isPlaying &&
                enforceAtRuntime)
            {
                Apply();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying ||
                enforceAtRuntime)
            {
                Apply();
            }
        }

        private void OnValidate()
        {
            referenceResolution.x =
                Mathf.Max(
                    1f,
                    referenceResolution.x);

            referenceResolution.y =
                Mathf.Max(
                    1f,
                    referenceResolution.y);

            matchWidthOrHeight =
                Mathf.Clamp01(
                    matchWidthOrHeight);

            referencePixelsPerUnit =
                Mathf.Max(
                    1f,
                    referencePixelsPerUnit);

            Apply();
        }

        [ContextMenu("Apply Responsive Canvas Settings")]
        public void Apply()
        {
            if (_scaler == null)
                _scaler = GetComponent<CanvasScaler>();

            if (_scaler == null)
                return;

            _scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            _scaler.referenceResolution =
                referenceResolution;

            _scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            _scaler.matchWidthOrHeight =
                matchWidthOrHeight;

            _scaler.referencePixelsPerUnit =
                referencePixelsPerUnit;
        }
    }
}
