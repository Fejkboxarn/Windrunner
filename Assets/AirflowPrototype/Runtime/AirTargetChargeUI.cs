using UnityEngine;
using UnityEngine.UI;

namespace AirflowPrototype
{
    /// <summary>
    /// Unified target + charge UI driven by AirCaster.TimingProgress01.
    /// This is the effective cast clock: whichever requirement is behind
    /// (air compression or target lock) determines the displayed progress.
    /// </summary>
    public sealed class AirTargetChargeUI : MonoBehaviour
    {
        [SerializeField] private AirCaster caster;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private AirTargetUISettings settings;

        private GameObject _runtimeCanvasObject;
        private Canvas _canvas;
        private RectTransform _canvasRect;

        private RectTransform _graphicRect;
        private CanvasGroup _canvasGroup;
        private AirTargetChargeGraphic _graphic;

        private float _visibility;
        private float _currentSize;
        private float _rotationDegrees;

        private float _perfectFlashRemaining;
        private float _perfectFlash01;
        private AirNode _perfectFlashTarget;

        public void Configure(
            AirCaster newCaster,
            Camera newCamera,
            AirTargetUISettings newSettings)
        {
            Unsubscribe();

            caster = newCaster;
            targetCamera = newCamera;
            settings = newSettings;

            if (Application.isPlaying)
                RebuildRuntimeUI();

            if (isActiveAndEnabled)
                Subscribe();
        }

        private void Awake()
        {
            ResolveReferences();
            RebuildRuntimeUI();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();

            if (Application.isPlaying &&
                _canvas == null)
            {
                RebuildRuntimeUI();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            DestroyRuntimeUI();
        }

        private void ResolveReferences()
        {
            if (caster == null)
                caster = GetComponent<AirCaster>();

            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void Subscribe()
        {
            if (caster == null)
                return;

            caster.PerfectReleased -= OnPerfectReleased;
            caster.PerfectReleased += OnPerfectReleased;
        }

        private void Unsubscribe()
        {
            if (caster != null)
                caster.PerfectReleased -= OnPerfectReleased;
        }

        private void Update()
        {
            if (caster == null ||
                targetCamera == null)
            {
                ResolveReferences();
            }

            if (caster == null ||
                targetCamera == null ||
                settings == null)
            {
                return;
            }

            if (_canvas == null ||
                _graphicRect == null ||
                _canvasGroup == null)
            {
                RebuildRuntimeUI();

                if (_canvas == null)
                    return;
            }

            float dt =
                Time.unscaledDeltaTime;

            AirNode target =
                _perfectFlashRemaining > 0f &&
                _perfectFlashTarget != null
                    ? _perfectFlashTarget
                    : caster.CurrentTarget;

            bool validTarget =
                target != null &&
                target.IsAvailable;

            float targetVisibility =
                validTarget
                    ? 1f
                    : 0f;

            float response =
                targetVisibility > _visibility
                    ? settings.appearResponse
                    : settings.disappearResponse;

            _visibility =
                Mathf.Lerp(
                    _visibility,
                    targetVisibility,
                    1f -
                    Mathf.Exp(
                        -response *
                        dt));

            _canvasGroup.alpha =
                _visibility;

            if (!validTarget)
                return;

            Vector3 screen =
                targetCamera.WorldToScreenPoint(
                    target.TargetPosition);

            if (screen.z <= 0f)
            {
                _canvasGroup.alpha = 0f;
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                screen,
                null,
                out Vector2 localPoint);

            _graphicRect.anchoredPosition =
                localPoint;

            bool charging =
                caster.IsCharging &&
                caster.CurrentTarget == target;

            float baseSize =
                charging
                    ? settings.chargingSize
                    : settings.idleSize;

            float bloom =
                Mathf.Lerp(
                    1f,
                    settings.perfectBloomScale,
                    _perfectFlash01);

            float desiredSize =
                baseSize *
                bloom;

            _currentSize =
                Mathf.Lerp(
                    _currentSize,
                    desiredSize,
                    1f -
                    Mathf.Exp(
                        -settings.sizeResponse *
                        dt));

            _graphicRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                _currentSize);

            _graphicRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                _currentSize);

            float rotationSpeed =
                charging
                    ? settings.chargingRotationSpeed
                    : settings.idleRotationSpeed;

            _rotationDegrees =
                Mathf.Repeat(
                    _rotationDegrees +
                    rotationSpeed *
                    dt,
                    360f);

            UpdatePerfectFlash(dt);

            float perfectCenter = 0.5f;
            float perfectHalfWidth = 0.15f;
            float progress01 = 0f;

            if (caster.Settings != null)
            {
                perfectCenter =
                    caster.Settings.PerfectCenter01;

                perfectHalfWidth =
                    caster.Settings.PerfectHalfWidth01;

                progress01 =
                    caster.TimingProgress01;
            }

            if (_graphic != null)
            {
                _graphic.SetState(
                    progress01,
                    perfectCenter,
                    perfectHalfWidth,
                    charging,
                    _rotationDegrees,
                    _perfectFlash01);
            }
        }

        private void UpdatePerfectFlash(
            float dt)
        {
            if (_perfectFlashRemaining > 0f)
            {
                _perfectFlashRemaining =
                    Mathf.Max(
                        0f,
                        _perfectFlashRemaining -
                        dt);

                float duration =
                    Mathf.Max(
                        0.03f,
                        settings.perfectFlashDuration);

                float normalized =
                    _perfectFlashRemaining /
                    duration;

                _perfectFlash01 =
                    Mathf.Max(
                        _perfectFlash01,
                        normalized);

                if (_perfectFlashRemaining <= 0f)
                    _perfectFlashTarget = null;
            }

            _perfectFlash01 =
                Mathf.Lerp(
                    _perfectFlash01,
                    0f,
                    1f -
                    Mathf.Exp(
                        -settings.perfectFlashResponse *
                        dt));
        }

        private void OnPerfectReleased(
            AirNode node,
            float quality)
        {
            _perfectFlashTarget = node;

            _perfectFlashRemaining =
                settings != null
                    ? settings.perfectFlashDuration
                    : 0.24f;

            _perfectFlash01 =
                Mathf.Max(
                    _perfectFlash01,
                    Mathf.Lerp(
                        0.72f,
                        1f,
                        Mathf.Clamp01(quality)));
        }

        private void RebuildRuntimeUI()
        {
            if (!Application.isPlaying)
                return;

            DestroyRuntimeUI();

            _runtimeCanvasObject =
                new GameObject(
                    "Air Target Charge UI Runtime",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));

            _runtimeCanvasObject.transform.SetParent(
                null,
                false);

            _canvas =
                _runtimeCanvasObject.GetComponent<Canvas>();

            _canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;

            _canvas.sortingOrder =
                500;

            _canvas.overrideSorting =
                true;

            CanvasScaler scaler =
                _runtimeCanvasObject.GetComponent<CanvasScaler>();

            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution =
                new Vector2(
                    1920f,
                    1080f);

            scaler.matchWidthOrHeight =
                0.5f;

            _canvasRect =
                _runtimeCanvasObject.GetComponent<RectTransform>();

            GameObject graphicObject =
                new GameObject(
                    "Air Target Reticle",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(CanvasGroup),
                    typeof(AirTargetChargeGraphic));

            graphicObject.transform.SetParent(
                _runtimeCanvasObject.transform,
                false);

            _graphicRect =
                graphicObject.GetComponent<RectTransform>();

            _graphicRect.anchorMin =
                new Vector2(
                    0.5f,
                    0.5f);

            _graphicRect.anchorMax =
                new Vector2(
                    0.5f,
                    0.5f);

            _graphicRect.pivot =
                new Vector2(
                    0.5f,
                    0.5f);

            _graphic =
                graphicObject.GetComponent<AirTargetChargeGraphic>();

            _graphic.raycastTarget =
                false;

            _graphic.color =
                Color.white;

            _canvasGroup =
                graphicObject.GetComponent<CanvasGroup>();

            _canvasGroup.blocksRaycasts =
                false;

            _canvasGroup.interactable =
                false;

            _canvasGroup.alpha =
                0f;

            _visibility = 0f;

            _currentSize =
                settings != null
                    ? settings.idleSize
                    : 108f;

            _graphicRect.sizeDelta =
                Vector2.one *
                _currentSize;

            if (settings != null)
                _graphic.Configure(settings);
        }

        private void DestroyRuntimeUI()
        {
            if (_runtimeCanvasObject != null)
                Destroy(_runtimeCanvasObject);

            _runtimeCanvasObject = null;
            _canvas = null;
            _canvasRect = null;
            _graphicRect = null;
            _canvasGroup = null;
            _graphic = null;
        }
    }
}
