using System;
using System.Reflection;
using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Bridges AirPower into INab Studio's Procedural Progress Bars asset.
    ///
    /// The progress-bar component is intentionally stored as MonoBehaviour and
    /// invoked through its documented public API. This avoids a hard compile
    /// dependency on the asset's namespace/assembly while still using:
    ///
    /// UpdateBarFillAmount(float)
    /// BarFill(float)
    /// BarFill(float, float)
    /// BarLoss(float)
    /// BarLoss(float, float)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AirPowerProgressBarUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AirPower airPower;

        [Tooltip(
            "Assign the ProceduralProgressBar component from INab Studio's " +
            "Procedural Progress Bars asset.")]
        [SerializeField] private MonoBehaviour proceduralProgressBar;

        public AirPower Resource => airPower;
        public RectTransform Meter => proceduralProgressBar ? proceduralProgressBar.transform as RectTransform : null;

        [Header("Plugin Animation")]
        [Tooltip(
            "When enabled, Air Power increases use BarFill and decreases use " +
            "BarLoss so the plugin's configured animations/impulses are preserved.")]
        [SerializeField] private bool animateChanges = true;

        [Tooltip(
            "Normally the plugin's Default Fill/Loss Time is used. Enable this " +
            "only if you want this bridge to override those durations.")]
        [SerializeField] private bool overridePluginDurations;

        [Min(0.01f)]
        [SerializeField] private float fillDuration = 0.20f;

        [Min(0.01f)]
        [SerializeField] private float lossDuration = 0.12f;

        [Header("Legacy serialized compatibility")]
        [Tooltip(
            "Retained for existing serialized assets. The typed AirPower API supplies its own maximum.")]
        [Min(0.01f)]
        // Retained only for the existing serialized schema; AirPower owns capacity.
#pragma warning disable CS0414
        [SerializeField, HideInInspector] private float fallbackMaximumAirPower = 100f;
#pragma warning restore CS0414

        [Header("Refresh")]
        [Tooltip(
            "Minimum normalized change before calling the progress bar API. " +
            "Prevents unnecessary updates from tiny floating-point differences.")]
        [Range(0.00001f, 0.02f)]
        [SerializeField] private float changeEpsilon = 0.0005f;

        private MethodInfo _setAbsoluteMethod;
        private MethodInfo _fillMethod;
        private MethodInfo _fillTimedMethod;
        private MethodInfo _lossMethod;
        private MethodInfo _lossTimedMethod;

        private Func<float> _readNormalized;

        private float _lastNormalized;
        private bool _initialized;
        private bool _reportedBarApiError;
        private ProgressBarMaterialLease _materialLease;
        private MonoBehaviour _leasedBar;

        public void Configure(
            AirPower newAirPower,
            MonoBehaviour newProgressBar)
        {
            airPower = newAirPower;
            proceduralProgressBar = newProgressBar;

            EnsureRuntimeMaterial();
            RebuildBindings();
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureRuntimeMaterial();
            RebuildBindings();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureRuntimeMaterial();
            RebuildBindings();

            _initialized = false;
        }

        private void Start()
        {
            RefreshImmediately();
        }

        private void EnsureRuntimeMaterial()
        {
            if (!Application.isPlaying || _leasedBar == proceduralProgressBar) return;
            _materialLease?.Dispose();
            _leasedBar = proceduralProgressBar;
            _materialLease = proceduralProgressBar != null
                ? new ProgressBarMaterialLease(proceduralProgressBar) : null;
        }

        private void OnDestroy()
        {
            _materialLease?.Dispose();
            _materialLease = null;
            _leasedBar = null;
        }

        private void LateUpdate()
        {
            if (airPower == null ||
                proceduralProgressBar == null)
            {
                ResolveReferences();

                if (airPower == null ||
                    proceduralProgressBar == null)
                {
                    return;
                }

                RebuildBindings();
            }

            EnsureRuntimeMaterial();
            if (_readNormalized == null)
            {
                BuildAirPowerReader();

                if (_readNormalized == null)
                    return;
            }

            if (_setAbsoluteMethod == null)
            {
                BuildProgressBarBindings();

                if (_setAbsoluteMethod == null)
                    return;
            }

            float normalized =
                ReadNormalizedSafely();

            if (!_initialized)
            {
                SetAbsolute(normalized);

                _lastNormalized = normalized;
                _initialized = true;
                return;
            }

            float delta =
                normalized -
                _lastNormalized;

            if (Mathf.Abs(delta) <
                changeEpsilon)
            {
                return;
            }

            if (animateChanges)
            {
                if (delta > 0f)
                    AnimateFill(delta);
                else
                    AnimateLoss(-delta);
            }
            else
            {
                SetAbsolute(normalized);
            }

            _lastNormalized = normalized;
        }

        [ContextMenu("Refresh Bar Immediately")]
        public void RefreshImmediately()
        {
            ResolveReferences();

            if (airPower == null ||
                proceduralProgressBar == null)
            {
                return;
            }

            RebuildBindings();

            if (_readNormalized == null ||
                _setAbsoluteMethod == null)
            {
                return;
            }

            float normalized =
                ReadNormalizedSafely();

            SetAbsolute(normalized);

            _lastNormalized = normalized;
            _initialized = true;
        }

        private void ResolveReferences()
        {
            if (airPower == null)
                airPower = FindAnyObjectByType<AirPower>();

            if (proceduralProgressBar == null)
                proceduralProgressBar = FindProgressBarOnThisObject();
        }

        private MonoBehaviour FindProgressBarOnThisObject()
        {
            MonoBehaviour[] behaviours =
                GetComponents<MonoBehaviour>();

            for (int i = 0;
                 i < behaviours.Length;
                 i++)
            {
                MonoBehaviour behaviour =
                    behaviours[i];

                if (behaviour == null ||
                    behaviour == this)
                {
                    continue;
                }

                Type type =
                    behaviour.GetType();

                if (type.Name ==
                    "ProceduralProgressBar")
                {
                    return behaviour;
                }

                MethodInfo updateMethod =
                    type.GetMethod(
                        "UpdateBarFillAmount",
                        BindingFlags.Instance |
                        BindingFlags.Public,
                        null,
                        new[] { typeof(float) },
                        null);

                if (updateMethod != null)
                    return behaviour;
            }

            return null;
        }

        private void RebuildBindings()
        {
            _readNormalized = null;

            _setAbsoluteMethod = null;
            _fillMethod = null;
            _fillTimedMethod = null;
            _lossMethod = null;
            _lossTimedMethod = null;

            _reportedBarApiError = false;

            BuildAirPowerReader();
            BuildProgressBarBindings();
        }

        private void BuildProgressBarBindings()
        {
            if (proceduralProgressBar == null)
                return;

            Type type =
                proceduralProgressBar.GetType();

            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public;

            _setAbsoluteMethod =
                type.GetMethod(
                    "UpdateBarFillAmount",
                    flags,
                    null,
                    new[] { typeof(float) },
                    null);

            _fillMethod =
                type.GetMethod(
                    "BarFill",
                    flags,
                    null,
                    new[] { typeof(float) },
                    null);

            _fillTimedMethod =
                type.GetMethod(
                    "BarFill",
                    flags,
                    null,
                    new[]
                    {
                        typeof(float),
                        typeof(float)
                    },
                    null);

            _lossMethod =
                type.GetMethod(
                    "BarLoss",
                    flags,
                    null,
                    new[] { typeof(float) },
                    null);

            _lossTimedMethod =
                type.GetMethod(
                    "BarLoss",
                    flags,
                    null,
                    new[]
                    {
                        typeof(float),
                        typeof(float)
                    },
                    null);

            if (_setAbsoluteMethod == null &&
                !_reportedBarApiError)
            {
                _reportedBarApiError = true;

                Debug.LogError(
                    "AirPowerProgressBarUI: The assigned component does not expose " +
                    "UpdateBarFillAmount(float). Assign the ProceduralProgressBar " +
                    "component from the Procedural Progress Bars asset.",
                    this);
            }
        }

        private void BuildAirPowerReader()
        {
            // AirPower is a sealed, explicitly serialized dependency. Reflection
            // over guessed member names adds overhead without compatibility value.
            if (airPower != null) _readNormalized = ReadAirPowerNormalized;
            else _readNormalized = null;
        }

        private float ReadAirPowerNormalized() => Mathf.Clamp01(airPower.Normalized);

        private float ReadNormalizedSafely()
        {
            if (_readNormalized == null)
                return 0f;

            try
            {
                return Mathf.Clamp01(
                    _readNormalized());
            }
            catch (Exception exception)
            {
                Debug.LogException(
                    exception,
                    this);

                _readNormalized = null;

                return _lastNormalized;
            }
        }

        private void SetAbsolute(
            float normalized)
        {
            if (_setAbsoluteMethod == null ||
                proceduralProgressBar == null)
            {
                return;
            }

            InvokeBarMethod(
                _setAbsoluteMethod,
                normalized);
        }

        private void AnimateFill(
            float normalizedAmount)
        {
            if (proceduralProgressBar == null)
                return;

            if (overridePluginDurations &&
                _fillTimedMethod != null)
            {
                InvokeBarMethod(
                    _fillTimedMethod,
                    normalizedAmount,
                    fillDuration);

                return;
            }

            if (_fillMethod != null)
            {
                InvokeBarMethod(
                    _fillMethod,
                    normalizedAmount);

                return;
            }

            SetAbsolute(
                _lastNormalized +
                normalizedAmount);
        }

        private void AnimateLoss(
            float normalizedAmount)
        {
            if (proceduralProgressBar == null)
                return;

            if (overridePluginDurations &&
                _lossTimedMethod != null)
            {
                InvokeBarMethod(
                    _lossTimedMethod,
                    normalizedAmount,
                    lossDuration);

                return;
            }

            if (_lossMethod != null)
            {
                InvokeBarMethod(
                    _lossMethod,
                    normalizedAmount);

                return;
            }

            SetAbsolute(
                _lastNormalized -
                normalizedAmount);
        }

        private void InvokeBarMethod(
            MethodInfo method,
            params object[] arguments)
        {
            try
            {
                method.Invoke(
                    proceduralProgressBar,
                    arguments);
            }
            catch (Exception exception)
            {
                Debug.LogException(
                    exception,
                    this);
            }
        }

    }
}
