using System.Collections;
using UnityEngine;
using Unity.VisualScripting;
using System.Security.Cryptography;



#if UNITY_EDITOR
using UnityEditor;
#endif


namespace INab.UI
{
    [ExecuteAlways]
    public class ProceduralProgressBar : MonoBehaviour
    {
        #region Setup

        [Tooltip("Enable or disable contextual help boxes in the Inspector.")]
        public bool enableHelpBoxes = true;

        [Tooltip("Configuration asset used to load and save settings at runtime.")]
        public ProceduralProgressBarConfig config;

        [Tooltip("Material used by the progress bar. Must use the INab Studio/Procedural Progress Bar shader. " +
            "You can manually set the material to either sharedMaterial (affects all instances) or material (only the instance).")]
        public Material progressBarMaterial;

        [Tooltip("Creates a unique material instance at runtime to avoid modifying shared materials.")]
        public bool instantiateMaterialOnStart = false;

        [Tooltip("Mesh Renderer used to apply and manage the progress bar material.")]
        public MeshRenderer barRenderer;

        [Tooltip("If enabled, sets an initial fill amount on Start().")]
        public bool UseInitialFillAmount = false;

        [Tooltip("Value used to initialize the bar fill at Start(), if enabled.")]
        [Range(0, 1)] public float InitialFillAmount = 0;


        #endregion     

        #region Runtime Helpers

        public float TargetFillAmount;
        public bool IsEndLineVisible = true;
        public bool IsCurrentlyDelayed = false;

        #endregion

        #region Shader Property Names

        private const string ShaderNameUnlit = "INab Studio/Procedural Progress Bar Unlit";
        private const string ShaderNameCanvas = "INab Studio/Procedural Progress Bar Canvas";

        private const string FillAmountName = "_Fill_Amount";
        private const string EndLineVisibilityName = "_End_Line_Visibility";
        private const string SecondBarBlendToBarName = "_Second_Bar_Main_Blend";
        private const string SecondBarBlendToInvisibleName = "_Second_Bar_Invisible_Blend";
        private const string MainBarFillAmountName = "_Main_Bar_Fill_Amount";
        private const string SecondBarColorName = "_Fill_Second_Bar_Color";

        private const string EndLineEnabledName = "_END_LINE_ENABLE";
        private const string SecondBarEnabled = "_Fill_Second_Bar_Enable";
        private const string CUSTOM_OUTLINE_ENABLE = "_CUSTOM_OUTLINE_ENABLE";
        private const string FILL_OVERLAY_ENABLE = "_FILL_OVERLAY_ENABLE";
        private const string FILL_OVERLAY_USE_GUIDES = "_FILL_OVERLAY_USE_GUIDES";
        private const string Fill_Shadow_Enable = "_Fill_Shadow_Enable";
        private const string Fill_Enable_Smooth_Edge = "_Fill_Enable_Smooth_Edge";

        private const string CustomOutlinePowerName = "_Custom_Outline_Power";
        private const string CustomOutlineStrengthName = "_Custom_Outline_Strength";
        private const string CustomOutlineColorName = "_Custom_Outline_Color";
        private const string FillOverlayStrengthName = "_Fill_Overlay_Strength";
        private const string FillOverlayColorName = "_Fill_Overlay_Fill_Color";
        private const string FillOverlayColorBackgroundName = "_Fill_Overlay_Background_Color";
        private const string FillShadowPowerHardnessName = "_Fill_Shadow_Power_Hardness";

        #endregion

        #region Coroutines

        private Coroutine FillAmountCoroutine;
        private Coroutine EndLineCoroutine;
        private Coroutine FillImpulseCoroutine;
        private Coroutine LossImpulseCoroutine;

        #endregion

        #region Runtime Helpers - Sliders

        [Tooltip("Displays internal shader values for debugging or testing at runtime.")]
        public bool ShowShaderCoreControls = false;

        [Tooltip("Current visible fill amount of the bar.")]
        [Range(0, 1)] public float FillAmount = 0;

        [Tooltip("Main bar fill amount used internally for second bar calculations.")]
        [Range(0, 1)] public float MainBarFillAmount = 1;

        [Tooltip("Blend value between second bar and main bar visuals.")]
        [Range(0, 1)] public float SecondBarBlendToBar = 0;

        [Tooltip("Blend value to fade the second bar into invisibility.")]
        [Range(0, 1)] public float SecondBarBlendToInvisible = 0;


        #endregion

        #region Editor Helpers

        [Tooltip("Fill amount to apply when testing bar fill in the Editor.")]
        public float BarFillAmountEditor = 0.25f;

        [Tooltip("Loss amount to apply when testing bar loss in the Editor.")]
        public float BarLossAmountEditor = 0.25f;

        #endregion

        #region Fill Settings

        [Tooltip("Time in seconds it takes to fill the bar.")]
        public float DefaultFillTime = 0.25f;

        [Tooltip("Curve used to animate the fill over time.")]
        public AnimationCurve FillCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Tooltip("Time in seconds it takes for the bar to lose value.")]
        public float DefaultLossTime = 0.25f;

        [Tooltip("Curve used to animate the loss over time.")]
        public AnimationCurve LossCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        #endregion

        #region Auto End Line

        [Tooltip("Automatically controls end line visibility based on fill state.")]
        public bool UseAutoEndLine = false;

        [Tooltip("Visibility of the end line effect.")]
        [Range(0, 1)] public float EndLineVisibility = 1;

        [Tooltip("Time to fade out the end line when overfilled.")]
        public float TurnOffTime = 1f;

        [Tooltip("Time to fade in the end line when under threshold.")]
        public float TurnOnTime = 1f;

        [Tooltip("Threshold after which the end line is hidden.")]
        [Range(0, 1)] public float FillAmountThreshold = 0.95f;

        #endregion

        #region Second Bar Settings

        [Tooltip("Enable second bar fill behavior.")]
        public bool UseSecondBarFill = false;

        [Tooltip("Enable second bar loss behavior.")]
        public bool UseSecondBarLoss = false;

        [Tooltip("Use custom color for second bar fill.")]
        public bool OverrideFillSecondBarColor = false;

        [Tooltip("Color used for second bar fill stage.")]
        [ColorUsage(true, true)] public Color FillSecondBarColor = Color.green;

        [Tooltip("Delays second bar fill.")]
        public bool FillUseTimeDelay = false;

        [Tooltip("Delay time before second bar starts to fill.")]
        public float FillDelayTime = 0.2f;

        [Tooltip("Time it takes for the second bar to catch up to main bar fill.")]
        public float FillCatchUpTime = 0.5f;

        [Tooltip("Blends second bar into the main bar during fill.")]
        public bool BlendToMainBar = false;

        [Tooltip("Controls the blend curve between second bar and main bar during fill.")]
        public AnimationCurve BlendToMainBarCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Tooltip("Enable catch-up animation for second bar fill.")]
        public bool FillAmountCatchUp = false;

        [Tooltip("Curve controlling how second bar catches up during fill.")]
        public AnimationCurve FillAmountCatchUpCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Tooltip("Use custom color for second bar loss.")]
        public bool OverrideLossSecondBarColor = false;

        [Tooltip("Color used for second bar loss stage.")]
        [ColorUsage(true, true)] public Color LossSecondBarColor = Color.red;

        [Tooltip("Delays second bar loss.")]
        public bool LossUseTimeDelay = false;

        [Tooltip("Delay time before second bar starts to reduce.")]
        public float LossDelayTime = 0.2f;

        [Tooltip("Time it takes for the second bar to catch up during loss.")]
        public float LossCatchUpTime = 0.5f;

        [Tooltip("Blend second bar to invisible during loss.")]
        public bool BlendToInvisible = false;

        [Tooltip("Curve controlling how second bar blends to invisible during loss.")]
        public AnimationCurve BlendToInvisibleCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Tooltip("Enable catch-up animation for second bar loss.")]
        public bool LossAmountCatchUp = false;

        [Tooltip("Curve controlling how second bar catches up during loss.")]
        public AnimationCurve LossAmountCatchUpCurve = AnimationCurve.Linear(0, 0, 1, 1);

        #endregion

        #region Impulse Settings

        [Tooltip("Enable visual impulse when bar is filled.")]
        public bool UseFillImpulse = false;

        [Tooltip("Curve that defines the intensity of the fill impulse over time.")]
        public AnimationCurve ImpulseCurveFill = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0f));


        [Tooltip("Duration of the fill impulse effect.")]
        public float ImpulseDurationFill = 0.5f;

        [Tooltip("Enable custom outline effects during fill impulse.")]
        public bool UseCustomOutlineFill = false;

        [Tooltip("Minimum and maximum strength for custom outline during fill impulse.")]
        [Range(0, 1)] public float StrengthMinFill = 0, StrengthMaxFill = 1;

        [Tooltip("Minimum and maximum power for custom outline during fill impulse.")]
        [Range(1, 5)] public float PowerMinFill = 3, PowerMaxFill = 2;

        [Tooltip("Override custom outline color during fill impulse.")]
        public bool OverrideFillCustomOutlineColor = false;

        [Tooltip("Outline color used during fill impulse.")]
        [ColorUsage(true, true)] public Color FillCustomOutlineColor = Color.green;

        [Tooltip("Enable shadow effect during fill impulse.")]
        public bool UseFillShadowFill = false;

        [Tooltip("Minimum and maximum shadow intensity during fill impulse.")]
        [Range(0, 5)] public float ShadowPowerMinFill = 0, ShadowPowerMaxFill = 1.5f;

        [Tooltip("Enable overlay effect during fill impulse.")]
        public bool UseOverlayFill = false;

        [Tooltip("Minimum and maximum overlay intensity during fill impulse.")]
        [Range(0, 1)] public float OverlayStrengthMinFill = 0, OverlayStrengthMaxFill = 1;

        [Tooltip("Override overlay color during fill impulse.")]
        public bool OverrideFillOverlayColor = false;

        [Tooltip("Overlay fill and background color during fill impulse.")]
        [ColorUsage(true, true)] public Color FillOverlayColor = Color.green;

        [Tooltip("Overlay fill and background color during fill impulse.")]
        [ColorUsage(true, true)] public Color FillOverlayBackgroundColor = Color.green;

        [Tooltip("Enable visual impulse when bar loses value.")]
        public bool UseLossImpulse = false;

        [Tooltip("Curve that defines the intensity of the loss impulse over time.")]
        public AnimationCurve ImpulseCurveLoss = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0f));

        [Tooltip("Duration of the loss impulse effect.")]
        public float ImpulseDurationLoss = 0.5f;

        [Tooltip("Enable custom outline effects during loss impulse.")]
        public bool UseCustomOutlineLoss = false;

        [Tooltip("Minimum and maximum strength for custom outline during loss impulse.")]
        [Range(0, 1)] public float StrengthMinLoss = 0, StrengthMaxLoss = 1;

        [Tooltip("Minimum and maximum power for custom outline during loss impulse.")]
        [Range(1, 5)] public float PowerMinLoss = 3, PowerMaxLoss = 2;

        [Tooltip("Override custom outline color during loss impulse.")]
        public bool OverrideLossCustomOutlineColor = false;

        [Tooltip("Outline color used during loss impulse.")]
        [ColorUsage(true, true)] public Color LossCustomOutlineColor = Color.red;

        [Tooltip("Enable shadow effect during loss impulse.")]
        public bool UseFillShadowLoss = false;

        [Tooltip("Minimum and maximum shadow intensity during loss impulse.")]
        [Range(0, 5)] public float ShadowPowerMinLoss = 0, ShadowPowerMaxLoss = 1.5f;

        [Tooltip("Enable overlay effect during loss impulse.")]
        public bool UseOverlayLoss = false;

        [Tooltip("Minimum and maximum overlay intensity during loss impulse.")]
        [Range(0, 1)] public float OverlayStrengthMinLoss = 0, OverlayStrengthMaxLoss = 1;

        [Tooltip("Override overlay color during loss impulse.")]
        public bool OverrideLossOverlayColor = false;

        [Tooltip("Overlay fill and background color during loss impulse.")]
        [ColorUsage(true, true)] public Color LossOverlayColor = Color.red;

        [Tooltip("Overlay fill and background color during loss impulse.")]
        [ColorUsage(true, true)] public Color LossOverlayBackgroundColor = Color.red;

        #endregion

        #region Unity Methods

        private void Start()
        {
            if (Application.isPlaying && instantiateMaterialOnStart && barRenderer != null)
            {
                progressBarMaterial = barRenderer.material;
            }

            IsEndLineVisible = EndLineVisibility > 0;

            if (UseInitialFillAmount)
            {
                UpdateBarFillAmount(InitialFillAmount);
            }
        }

        private void Update()
        {
            //if (!UpdateInEditor && !Application.isPlaying) return;
            if (progressBarMaterial == null) return;

            progressBarMaterial.SetFloat(FillAmountName, FillAmount);
            progressBarMaterial.SetFloat(EndLineVisibilityName, EndLineVisibility);
            progressBarMaterial.SetFloat(SecondBarBlendToBarName, SecondBarBlendToBar);
            progressBarMaterial.SetFloat(SecondBarBlendToInvisibleName, SecondBarBlendToInvisible);
            progressBarMaterial.SetFloat(MainBarFillAmountName, MainBarFillAmount); //  + 0.0005f

            if (!Application.isPlaying) return;

            if (UseAutoEndLine)
            {
                if (FillAmount > FillAmountThreshold && IsEndLineVisible)
                {
                    if (EndLineCoroutine != null) StopCoroutine(EndLineCoroutine);
                    EndLineCoroutine = StartCoroutine(EndLineTurnOff(TurnOffTime));
                }
                else if (FillAmount < FillAmountThreshold && !IsEndLineVisible)
                {
                    if (EndLineCoroutine != null) StopCoroutine(EndLineCoroutine);
                    EndLineCoroutine = StartCoroutine(EndLineTurnOn(TurnOnTime));
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Instantly starts a smooth fill animation, increasing the current bar value by the specified amount.
        /// Uses the default fill duration defined in the config.
        /// </summary>
        /// <param name="amount">The amount to fill (e.g., 0.3 for 30%). Must be positive.</param>
        public void BarFill(float amount) => BarFill(amount, DefaultFillTime);

        /// <summary>
        /// Starts a smooth fill animation, increasing the current bar value by the specified amount over a custom duration.
        /// </summary>
        /// <param name="amount">The amount to fill. Must be a positive value.</param>
        /// <param name="time">Duration in seconds for the fill animation. Minimum of 0.001 to avoid glitches.</param>
        public void BarFill(float amount, float time)
        {
            if (!_IsMaterialValid()) return;

            if (UseFillImpulse)
            {
                if (FillImpulseCoroutine != null) StopCoroutine(FillImpulseCoroutine);
                if (LossImpulseCoroutine != null) StopCoroutine(LossImpulseCoroutine);
                FillImpulseCoroutine = StartCoroutine(FillImpulse());
            }

            if (amount < 0)
            {
                Debug.LogWarning("Amount to fill should be positive.");
                amount = 0;
            }

            if (time == 0) time = 0.001f; // Prevents issues with zero fill amount

            if (FillAmountCoroutine != null)
            {
                TargetFillAmount += amount;
                IsCurrentlyDelayed = false;
                StopCoroutine(FillAmountCoroutine);
            }
            else
            {
                TargetFillAmount = FillAmount + amount;
            }

            TargetFillAmount = Mathf.Clamp01(TargetFillAmount);
            FillAmountCoroutine = StartCoroutine(FillAmountChange(time));
        }


        /// <summary>
        /// Instantly starts a smooth loss animation, decreasing the current bar value by the specified amount.
        /// Uses the default loss duration defined in the config.
        /// </summary>
        /// <param name="amount">The amount to reduce the bar by. Must be positive.</param>
        public void BarLoss(float amount) => BarLoss(amount, DefaultLossTime);


        /// <summary>
        /// Starts a smooth loss animation, decreasing the current bar value by the specified amount over a custom duration.
        /// </summary>
        /// <param name="amount">The amount to reduce. Must be a positive value.</param>
        /// <param name="time">Duration in seconds for the loss animation. Minimum of 0.001 to avoid glitches.</param>
        public void BarLoss(float amount, float time)
        {
            if (!_IsMaterialValid()) return;

            if (UseLossImpulse)
            {
                if (LossImpulseCoroutine != null) StopCoroutine(LossImpulseCoroutine);
                if (FillImpulseCoroutine != null) StopCoroutine(FillImpulseCoroutine);
                LossImpulseCoroutine = StartCoroutine(LossImpulse());
            }

            if (amount < 0)
            {
                Debug.LogWarning("Amount to loss should be positive.");
                amount = 0;
            }

            if (time == 0) time = 0.001f; // Prevents issues with zero fill amount

            if (FillAmountCoroutine != null)
            {
                TargetFillAmount -= amount;
                IsCurrentlyDelayed = false;
                StopCoroutine(FillAmountCoroutine);
            }
            else
            {
                TargetFillAmount = FillAmount - amount;
            }

            TargetFillAmount = Mathf.Clamp01(TargetFillAmount);
            FillAmountCoroutine = StartCoroutine(FillAmountChange(time, true));
        }

        /// <summary>
        /// Immediately sets the bar’s fill amount without animation.
        /// Also updates related shader parameters (main bar and target fill).
        /// </summary>
        /// <param name="value">Fill value between 0 and 1. 0 = empty, 1 = full.</param>
        public void UpdateBarFillAmount(float value)
        {
            FillAmount = value;
            progressBarMaterial.SetFloat(FillAmountName, FillAmount);

            MainBarFillAmount = value + 0.001f;
            progressBarMaterial.SetFloat(MainBarFillAmountName, MainBarFillAmount);

            TargetFillAmount = value;
        }

        #endregion

        #region Editor Methods

        public void _GetRenderer() => barRenderer = GetComponent<MeshRenderer>();

        public void _GetMaterial()
        {
            _GetRenderer();
            var image = GetComponent<UnityEngine.UI.Image>();

            if (barRenderer != null)
                progressBarMaterial = barRenderer.sharedMaterial;
            else if (image != null)
                progressBarMaterial = image.material;
        }

        public void _BarFillEditor() => BarFill(BarFillAmountEditor);

        public void _BarLossEditor() => BarLoss(BarLossAmountEditor);

        public bool _IsMaterialValid()
        {
            if (_IsMaterialNull())
            {
                return false;
            }

            return progressBarMaterial.shader.name == ShaderNameUnlit || progressBarMaterial.shader.name == ShaderNameCanvas;
        }

        public bool _IsRendererNull() => barRenderer == null;
        public bool _IsMaterialNull() => progressBarMaterial == null;

        public bool _IsEndLineEnabledInMaterial() =>
            _IsMaterialValid() && progressBarMaterial.IsKeywordEnabled(EndLineEnabledName);

        public bool _IsSecondBarEnabledInMaterial() =>
            _IsMaterialValid() && progressBarMaterial.GetInt(SecondBarEnabled) == 1;

        public bool _IsCustomOutlineEnabledInMaterial() =>
            _IsMaterialValid() && progressBarMaterial.IsKeywordEnabled(CUSTOM_OUTLINE_ENABLE);

        public bool _IsFillShadowEnabledInMaterial() =>
            _IsMaterialValid() && progressBarMaterial.GetInt(Fill_Shadow_Enable) == 1;

        public bool _IsOverlayEnabledInMaterial() =>
            _IsMaterialValid() && progressBarMaterial.IsKeywordEnabled(FILL_OVERLAY_ENABLE);

        public bool _IsOverlayGuidesEnabledInMaterial() =>
            _IsMaterialValid() && progressBarMaterial.IsKeywordEnabled(FILL_OVERLAY_USE_GUIDES);
        
        public bool _IsSmoothEdgeEnabledInMaterial() =>
            progressBarMaterial.GetInt(Fill_Enable_Smooth_Edge) == 1;

        #endregion

        #region Coroutine Helpers

        private IEnumerator FillImpulse()
        {
            float timePassed = 0;
            while (timePassed < ImpulseDurationFill)
            {
                timePassed += Time.deltaTime;
                float value = ImpulseCurveFill.Evaluate(timePassed / ImpulseDurationFill);
                UpdateImpulsePropertiesFill(value);
                yield return null;
            }
        }

        private IEnumerator LossImpulse()
        {
            float timePassed = 0;
            while (timePassed < ImpulseDurationLoss)
            {
                timePassed += Time.deltaTime;
                float value = ImpulseCurveLoss.Evaluate(timePassed / ImpulseDurationLoss);
                UpdateImpulsePropertiesLoss(value);
                yield return null;
            }
        }

        private void UpdateImpulsePropertiesFill(float t)
        {
            if (UseCustomOutlineFill)
            {
                progressBarMaterial.SetFloat(CustomOutlineStrengthName, Mathf.Lerp(StrengthMinFill, StrengthMaxFill, t));
                progressBarMaterial.SetFloat(CustomOutlinePowerName, Mathf.Lerp(PowerMinFill, PowerMaxFill, t));
                if (OverrideFillCustomOutlineColor)
                    progressBarMaterial.SetColor(CustomOutlineColorName, FillCustomOutlineColor);
            }

            if (UseFillShadowFill)
                progressBarMaterial.SetFloat(FillShadowPowerHardnessName, Mathf.Lerp(ShadowPowerMinFill, ShadowPowerMaxFill, t));

            if (UseOverlayFill)
            {
                progressBarMaterial.SetFloat(FillOverlayStrengthName, Mathf.Lerp(OverlayStrengthMinFill, OverlayStrengthMaxFill, t));
                if (OverrideFillOverlayColor)
                {
                    progressBarMaterial.SetColor(FillOverlayColorName, FillOverlayColor);
                    progressBarMaterial.SetColor(FillOverlayColorBackgroundName, FillOverlayBackgroundColor);
                }
            }
        }

        private void UpdateImpulsePropertiesLoss(float t)
        {
            if (UseCustomOutlineLoss)
            {
                progressBarMaterial.SetFloat(CustomOutlineStrengthName, Mathf.Lerp(StrengthMinLoss, StrengthMaxLoss, t));
                progressBarMaterial.SetFloat(CustomOutlinePowerName, Mathf.Lerp(PowerMinLoss, PowerMaxLoss, t));
                if (OverrideLossCustomOutlineColor)
                    progressBarMaterial.SetColor(CustomOutlineColorName, LossCustomOutlineColor);
            }

            if (UseFillShadowLoss)
                progressBarMaterial.SetFloat(FillShadowPowerHardnessName, Mathf.Lerp(ShadowPowerMinLoss, ShadowPowerMaxLoss, t));

            if (UseOverlayLoss)
            {
                progressBarMaterial.SetFloat(FillOverlayStrengthName, Mathf.Lerp(OverlayStrengthMinLoss, OverlayStrengthMaxLoss, t));
                if (OverrideLossOverlayColor)
                {
                    progressBarMaterial.SetColor(FillOverlayColorName, LossOverlayColor);
                    progressBarMaterial.SetColor(FillOverlayColorBackgroundName, LossOverlayBackgroundColor);
                }
            }
        }

        private IEnumerator EndLineTurnOff(float time)
        {
            IsEndLineVisible = false;
            float start = EndLineVisibility, t = 0;

            while (t <= time)
            {
                t += Time.deltaTime;
                EndLineVisibility = Mathf.Clamp01(start * (1 - t / time));
                yield return null;
            }
        }

        private IEnumerator EndLineTurnOn(float time)
        {
            IsEndLineVisible = true;
            float start = EndLineVisibility, diff = 1 - start, t = 0;

            while (t <= time)
            {
                t += Time.deltaTime;
                EndLineVisibility = Mathf.Clamp01(start + diff * (t / time));
                yield return null;
            }
        }

        private IEnumerator FillAmountChange(float time, bool loss = false)
        {
            SecondBarBlendToInvisible = 0;
            SecondBarBlendToBar = 0;
            bool secondBarActive = loss ? UseSecondBarLoss : UseSecondBarFill;

            if (secondBarActive)
            {
                if (loss && OverrideLossSecondBarColor)
                    progressBarMaterial.SetColor(SecondBarColorName, LossSecondBarColor);
                else if (!loss && OverrideFillSecondBarColor)
                    progressBarMaterial.SetColor(SecondBarColorName, FillSecondBarColor);
            }
            else
            {
                SecondBarBlendToInvisible = 0;
                SecondBarBlendToBar = 1;
                MainBarFillAmount = FillAmount;
            }

            if (time > 0f)
            {
                float start = secondBarActive && loss ? MainBarFillAmount : FillAmount;
                float diff = TargetFillAmount - start;
                float t = 0;
                AnimationCurve curve = loss ? LossCurve : FillCurve;

                while (t < time)
                {
                    t += Time.deltaTime;
                    float val = start + curve.Evaluate(t / time) * diff;

                    if (secondBarActive && loss)
                        MainBarFillAmount = Mathf.Clamp01(val);
                    else
                        FillAmount = Mathf.Clamp01(val);

                    yield return null;
                }
            }
            else
            {
                if (secondBarActive && loss)
                    MainBarFillAmount = TargetFillAmount;
                else
                    FillAmount = TargetFillAmount;
            }

            FillAmountCoroutine = null;

            if (secondBarActive)
            {
                _SecondBarCatchUp(loss);
            }
            else
            {
                MainBarFillAmount = FillAmount;

            }
        }

        private IEnumerator SecondBarCoroutine(bool shouldWait, bool loss)
        {

            if (loss)
            {
                if (UseSecondBarLoss)
                {
                    if (LossUseTimeDelay && shouldWait)
                    {
                        IsCurrentlyDelayed = true;
                        yield return new WaitForSeconds(LossDelayTime);
                        IsCurrentlyDelayed = false;
                    }

                    float start = FillAmount, diff = MainBarFillAmount - start;
                    float t = 0;

                    while (t <= LossCatchUpTime)
                    {
                        t += Time.deltaTime;
                        if (LossAmountCatchUp)
                            FillAmount = Mathf.Clamp01(start + LossAmountCatchUpCurve.Evaluate(t / LossCatchUpTime) * diff);

                        if (BlendToInvisible)
                            SecondBarBlendToInvisible = BlendToInvisibleCurve.Evaluate(t / LossCatchUpTime);

                        yield return null;
                    }

                    //if (!LossAmountCatchUp)
                    //    FillAmount = MainBarFillAmount;

                    FillAmount = MainBarFillAmount;

                }
                else
                {
                    MainBarFillAmount = FillAmount;
                    SecondBarBlendToInvisible = 0;
                }
            }
            else
            {
                if (UseSecondBarFill)
                {
                    if (FillUseTimeDelay && shouldWait)
                    {
                        IsCurrentlyDelayed = true;
                        yield return new WaitForSeconds(FillDelayTime);
                        IsCurrentlyDelayed = false;
                    }

                    float start = MainBarFillAmount, diff = FillAmount - start;
                    float t = 0;

                    while (t <= FillCatchUpTime)
                    {
                        t += Time.deltaTime;

                        if (FillAmountCatchUp)
                            MainBarFillAmount = Mathf.Clamp01(start + FillAmountCatchUpCurve.Evaluate(t / FillCatchUpTime) * diff);

                        if (BlendToMainBar)
                            SecondBarBlendToBar = BlendToMainBarCurve.Evaluate(t / FillCatchUpTime);

                        yield return null;
                    }

                    //if (!FillAmountCatchUp)
                    //   MainBarFillAmount = FillAmount;

                    MainBarFillAmount = FillAmount;

                    // Fixes the second bar artifact on the fill edge
                    MainBarFillAmount += 0.001f;
                }
                else
                {
                    MainBarFillAmount = FillAmount;
                    SecondBarBlendToBar = 1;
                }
            }
        }

        public void _SecondBarCatchUp(bool loss)
        {
            FillAmountCoroutine = StartCoroutine(SecondBarCoroutine(!IsCurrentlyDelayed, loss));
        }

        #endregion

        #region Config Methods

        public void CreateNewConfig()
        {
#if UNITY_EDITOR
            if (progressBarMaterial == null)
            {
                Debug.LogError("Progress Bar Material is not assigned.");
                return;
            }

            string materialPath = AssetDatabase.GetAssetPath(progressBarMaterial);
            if (string.IsNullOrEmpty(materialPath))
            {
                Debug.LogError("Could not find the asset path for the Progress Bar Material.");
                return;
            }

            // Get the directory where the material is
            string directory = System.IO.Path.GetDirectoryName(materialPath);

            // Create a new config asset
            ProceduralProgressBarConfig newConfig = ScriptableObject.CreateInstance<ProceduralProgressBarConfig>();

            // Generate a unique path inside that directory
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.Combine(directory, "ProgressBarConfig.asset"));

            // Save the new asset
            AssetDatabase.CreateAsset(newConfig, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Assign the newly created config to the field
            config = newConfig;

            // Mark the parent object dirty if needed
            EditorUtility.SetDirty(this);
#endif
        }


        public void ApplyToConfig()
        {
            if (config == null)
            {
                Debug.LogWarning("Config is not assigned.");
                return;
            }

            // Fill Settings
            config.DefaultFillTime = DefaultFillTime;
            config.FillCurve = FillCurve;
            config.DefaultLossTime = DefaultLossTime;
            config.LossCurve = LossCurve;

            // Auto End Line
            config.UseAutoEndLine = UseAutoEndLine;
            config.EndLineVisibility = EndLineVisibility;
            config.TurnOffTime = TurnOffTime;
            config.TurnOnTime = TurnOnTime;
            config.FillAmountThreshold = FillAmountThreshold;

            // Second Bar Settings
            config.UseSecondBarFill = UseSecondBarFill;
            config.UseSecondBarLoss = UseSecondBarLoss;

            config.OverrideFillSecondBarColor = OverrideFillSecondBarColor;
            config.FillSecondBarColor = FillSecondBarColor;
            config.FillUseTimeDelay = FillUseTimeDelay;
            config.FillDelayTime = FillDelayTime;
            config.FillCatchUpTime = FillCatchUpTime;
            config.BlendToMainBar = BlendToMainBar;
            config.BlendToMainBarCurve = BlendToMainBarCurve;
            config.FillAmountCatchUp = FillAmountCatchUp;
            config.FillAmountCatchUpCurve = FillAmountCatchUpCurve;

            config.OverrideLossSecondBarColor = OverrideLossSecondBarColor;
            config.LossSecondBarColor = LossSecondBarColor;
            config.LossUseTimeDelay = LossUseTimeDelay;
            config.LossDelayTime = LossDelayTime;
            config.LossCatchUpTime = LossCatchUpTime;
            config.BlendToInvisible = BlendToInvisible;
            config.BlendToInvisibleCurve = BlendToInvisibleCurve;
            config.LossAmountCatchUp = LossAmountCatchUp;
            config.LossAmountCatchUpCurve = LossAmountCatchUpCurve;

            // Impulse Settings - Fill
            config.UseFillImpulse = UseFillImpulse;
            config.ImpulseCurveFill = ImpulseCurveFill;
            config.ImpulseDurationFill = ImpulseDurationFill;
            config.UseCustomOutlineFill = UseCustomOutlineFill;
            config.StrengthMinFill = StrengthMinFill;
            config.StrengthMaxFill = StrengthMaxFill;
            config.PowerMinFill = PowerMinFill;
            config.PowerMaxFill = PowerMaxFill;
            config.OverrideFillCustomOutlineColor = OverrideFillCustomOutlineColor;
            config.FillCustomOutlineColor = FillCustomOutlineColor;
            config.UseFillShadowFill = UseFillShadowFill;
            config.ShadowPowerMinFill = ShadowPowerMinFill;
            config.ShadowPowerMaxFill = ShadowPowerMaxFill;
            config.UseOverlayFill = UseOverlayFill;
            config.OverlayStrengthMinFill = OverlayStrengthMinFill;
            config.OverlayStrengthMaxFill = OverlayStrengthMaxFill;
            config.OverrideFillOverlayColor = OverrideFillOverlayColor;
            config.FillOverlayColor = FillOverlayColor;
            config.FillOverlayBackgroundColor = FillOverlayBackgroundColor;

            // Impulse Settings - Loss
            config.UseLossImpulse = UseLossImpulse;
            config.ImpulseCurveLoss = ImpulseCurveLoss;
            config.ImpulseDurationLoss = ImpulseDurationLoss;
            config.UseCustomOutlineLoss = UseCustomOutlineLoss;
            config.StrengthMinLoss = StrengthMinLoss;
            config.StrengthMaxLoss = StrengthMaxLoss;
            config.PowerMinLoss = PowerMinLoss;
            config.PowerMaxLoss = PowerMaxLoss;
            config.OverrideLossCustomOutlineColor = OverrideLossCustomOutlineColor;
            config.LossCustomOutlineColor = LossCustomOutlineColor;
            config.UseFillShadowLoss = UseFillShadowLoss;
            config.ShadowPowerMinLoss = ShadowPowerMinLoss;
            config.ShadowPowerMaxLoss = ShadowPowerMaxLoss;
            config.UseOverlayLoss = UseOverlayLoss;
            config.OverlayStrengthMinLoss = OverlayStrengthMinLoss;
            config.OverlayStrengthMaxLoss = OverlayStrengthMaxLoss;
            config.OverrideLossOverlayColor = OverrideLossOverlayColor;
            config.LossOverlayColor = LossOverlayColor;
            config.LossOverlayBackgroundColor = LossOverlayBackgroundColor;

#if UNITY_EDITOR
    EditorUtility.SetDirty(config);
    AssetDatabase.SaveAssets();
#endif

        }

        public void LoadFromConfig()
        {
            if (config == null)
            {
                Debug.LogWarning("Config is not assigned.");
                return;
            }

            // Fill Settings
            DefaultFillTime = config.DefaultFillTime;
            FillCurve = config.FillCurve;
            DefaultLossTime = config.DefaultLossTime;
            LossCurve = config.LossCurve;

            // Auto End Line
            UseAutoEndLine = config.UseAutoEndLine;
            EndLineVisibility = config.EndLineVisibility;
            TurnOffTime = config.TurnOffTime;
            TurnOnTime = config.TurnOnTime;
            FillAmountThreshold = config.FillAmountThreshold;

            // Second Bar Settings
            UseSecondBarFill = config.UseSecondBarFill;
            UseSecondBarLoss = config.UseSecondBarLoss;

            OverrideFillSecondBarColor = config.OverrideFillSecondBarColor;
            FillSecondBarColor = config.FillSecondBarColor;
            FillUseTimeDelay = config.FillUseTimeDelay;
            FillDelayTime = config.FillDelayTime;
            FillCatchUpTime = config.FillCatchUpTime;
            BlendToMainBar = config.BlendToMainBar;
            BlendToMainBarCurve = config.BlendToMainBarCurve;
            FillAmountCatchUp = config.FillAmountCatchUp;
            FillAmountCatchUpCurve = config.FillAmountCatchUpCurve;

            OverrideLossSecondBarColor = config.OverrideLossSecondBarColor;
            LossSecondBarColor = config.LossSecondBarColor;
            LossUseTimeDelay = config.LossUseTimeDelay;
            LossDelayTime = config.LossDelayTime;
            LossCatchUpTime = config.LossCatchUpTime;
            BlendToInvisible = config.BlendToInvisible;
            BlendToInvisibleCurve = config.BlendToInvisibleCurve;
            LossAmountCatchUp = config.LossAmountCatchUp;
            LossAmountCatchUpCurve = config.LossAmountCatchUpCurve;

            // Impulse Settings - Fill
            UseFillImpulse = config.UseFillImpulse;
            ImpulseCurveFill = config.ImpulseCurveFill;
            ImpulseDurationFill = config.ImpulseDurationFill;
            UseCustomOutlineFill = config.UseCustomOutlineFill;
            StrengthMinFill = config.StrengthMinFill;
            StrengthMaxFill = config.StrengthMaxFill;
            PowerMinFill = config.PowerMinFill;
            PowerMaxFill = config.PowerMaxFill;
            OverrideFillCustomOutlineColor = config.OverrideFillCustomOutlineColor;
            FillCustomOutlineColor = config.FillCustomOutlineColor;
            UseFillShadowFill = config.UseFillShadowFill;
            ShadowPowerMinFill = config.ShadowPowerMinFill;
            ShadowPowerMaxFill = config.ShadowPowerMaxFill;
            UseOverlayFill = config.UseOverlayFill;
            OverlayStrengthMinFill = config.OverlayStrengthMinFill;
            OverlayStrengthMaxFill = config.OverlayStrengthMaxFill;
            OverrideFillOverlayColor = config.OverrideFillOverlayColor;
            FillOverlayColor = config.FillOverlayColor;
            FillOverlayBackgroundColor = config.FillOverlayBackgroundColor;

            // Impulse Settings - Loss
            UseLossImpulse = config.UseLossImpulse;
            ImpulseCurveLoss = config.ImpulseCurveLoss;
            ImpulseDurationLoss = config.ImpulseDurationLoss;
            UseCustomOutlineLoss = config.UseCustomOutlineLoss;
            StrengthMinLoss = config.StrengthMinLoss;
            StrengthMaxLoss = config.StrengthMaxLoss;
            PowerMinLoss = config.PowerMinLoss;
            PowerMaxLoss = config.PowerMaxLoss;
            OverrideLossCustomOutlineColor = config.OverrideLossCustomOutlineColor;
            LossCustomOutlineColor = config.LossCustomOutlineColor;
            UseFillShadowLoss = config.UseFillShadowLoss;
            ShadowPowerMinLoss = config.ShadowPowerMinLoss;
            ShadowPowerMaxLoss = config.ShadowPowerMaxLoss;
            UseOverlayLoss = config.UseOverlayLoss;
            OverlayStrengthMinLoss = config.OverlayStrengthMinLoss;
            OverlayStrengthMaxLoss = config.OverlayStrengthMaxLoss;
            OverrideLossOverlayColor = config.OverrideLossOverlayColor;
            LossOverlayColor = config.LossOverlayColor;
            LossOverlayBackgroundColor = config.LossOverlayBackgroundColor;
        }

        #endregion
    }
}
