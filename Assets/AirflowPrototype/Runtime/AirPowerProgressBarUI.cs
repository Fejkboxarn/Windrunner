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

        [Header("Air Power Value Fallback")]
        [Tooltip(
            "Used only if the bridge can read the current Air Power value but " +
            "cannot discover its maximum value. Your current prototype uses 100.")]
        [Min(0.01f)]
        [SerializeField] private float fallbackMaximumAirPower = 100f;

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
        private bool _reportedAirPowerError;

        private static readonly string[] NormalizedMemberNames =
        {
            "Normalized",
            "NormalizedValue",
            "NormalizedPower",
            "NormalizedAmount",
            "Power01",
            "Fill01"
        };

        private static readonly string[] CurrentMemberNames =
        {
            "Current",
            "CurrentValue",
            "CurrentPower",
            "CurrentAirPower",
            "Value",
            "Amount",
            "_current",
            "_currentValue",
            "_currentPower",
            "_currentAirPower",
            "current",
            "currentValue",
            "currentPower",
            "currentAirPower"
        };

        private static readonly string[] MaximumMemberNames =
        {
            "Max",
            "Maximum",
            "MaxValue",
            "MaximumValue",
            "MaxPower",
            "MaximumPower",
            "MaxAirPower",
            "Capacity",
            "_max",
            "_maximum",
            "_maxValue",
            "_maxPower",
            "_maxAirPower",
            "max",
            "maximum",
            "maxValue",
            "maxPower",
            "maxAirPower"
        };

        public void Configure(
            AirPower newAirPower,
            MonoBehaviour newProgressBar)
        {
            airPower = newAirPower;
            proceduralProgressBar = newProgressBar;

            RebuildBindings();
        }

        private void Awake()
        {
            ResolveReferences();
            RebuildBindings();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RebuildBindings();

            _initialized = false;
        }

        private void Start()
        {
            RefreshImmediately();
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
            _reportedAirPowerError = false;

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
            if (airPower == null)
                return;

            Type type =
                airPower.GetType();

            MemberInfo normalizedMember =
                FindNumericMember(
                    type,
                    NormalizedMemberNames);

            if (normalizedMember != null)
            {
                _readNormalized =
                    () =>
                    {
                        float value =
                            ReadNumericMember(
                                airPower,
                                normalizedMember);

                        return Mathf.Clamp01(value);
                    };

                return;
            }

            MemberInfo currentMember =
                FindNumericMember(
                    type,
                    CurrentMemberNames);

            MemberInfo maximumMember =
                FindNumericMember(
                    type,
                    MaximumMemberNames);

            if (currentMember != null)
            {
                _readNormalized =
                    () =>
                    {
                        float current =
                            ReadNumericMember(
                                airPower,
                                currentMember);

                        float maximum =
                            maximumMember != null
                                ? ReadNumericMember(
                                    airPower,
                                    maximumMember)
                                : fallbackMaximumAirPower;

                        if (maximum <= 0.0001f)
                            maximum = fallbackMaximumAirPower;

                        return Mathf.Clamp01(
                            current /
                            Mathf.Max(
                                0.0001f,
                                maximum));
                    };

                return;
            }

            if (!_reportedAirPowerError)
            {
                _reportedAirPowerError = true;

                Debug.LogError(
                    "AirPowerProgressBarUI: Could not discover a readable Air Power " +
                    "value on AirPower. The bridge looks for common normalized/current " +
                    "property or field names. If your AirPower API uses another name, " +
                    "add it to the member-name list in this script.",
                    this);
            }
        }

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

        private static MemberInfo FindNumericMember(
            Type type,
            string[] names)
        {
            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            for (int i = 0;
                 i < names.Length;
                 i++)
            {
                PropertyInfo property =
                    type.GetProperty(
                        names[i],
                        flags);

                if (property != null &&
                    property.CanRead &&
                    IsNumericType(
                        property.PropertyType))
                {
                    return property;
                }

                FieldInfo field =
                    type.GetField(
                        names[i],
                        flags);

                if (field != null &&
                    IsNumericType(
                        field.FieldType))
                {
                    return field;
                }
            }

            return null;
        }

        private static float ReadNumericMember(
            object instance,
            MemberInfo member)
        {
            object value;

            if (member is PropertyInfo property)
                value = property.GetValue(instance);
            else if (member is FieldInfo field)
                value = field.GetValue(instance);
            else
                return 0f;

            return Convert.ToSingle(value);
        }

        private static bool IsNumericType(
            Type type)
        {
            return
                type == typeof(float) ||
                type == typeof(double) ||
                type == typeof(int) ||
                type == typeof(uint) ||
                type == typeof(short) ||
                type == typeof(ushort) ||
                type == typeof(byte) ||
                type == typeof(sbyte) ||
                type == typeof(long) ||
                type == typeof(ulong);
        }
    }
}
