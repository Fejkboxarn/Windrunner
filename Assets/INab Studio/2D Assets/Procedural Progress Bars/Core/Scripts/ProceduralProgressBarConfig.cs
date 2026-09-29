using UnityEngine;

namespace INab.UI
{
    [CreateAssetMenu(fileName = "ProgressBarConfig", menuName = "INab/Progress Bar Config")]
    public class ProceduralProgressBarConfig : ScriptableObject
    {
        [Header("Fill Settings")]
        public float DefaultFillTime = 0.25f;
        public AnimationCurve FillCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public float DefaultLossTime = 0.25f;
        public AnimationCurve LossCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Auto End Line")]
        public bool UseAutoEndLine = false;
        [Range(0, 1)] public float EndLineVisibility = 1;
        public float TurnOffTime = 1f;
        public float TurnOnTime = 1f;
        [Range(0, 1)] public float FillAmountThreshold = 0.95f;

        [Header("Second Bar Settings")]
        public bool UseSecondBarFill = false;
        public bool UseSecondBarLoss = false;

        public bool OverrideFillSecondBarColor = false;
        [ColorUsage(true, true)] public Color FillSecondBarColor = Color.green;
        public bool FillUseTimeDelay = false;
        public float FillDelayTime = 0.2f;
        public float FillCatchUpTime = 0.5f;
        public bool BlendToMainBar = false;
        public AnimationCurve BlendToMainBarCurve = AnimationCurve.Linear(0, 0, 1, 1);
        public bool FillAmountCatchUp = false;
        public AnimationCurve FillAmountCatchUpCurve = AnimationCurve.Linear(0, 0, 1, 1);

        public bool OverrideLossSecondBarColor = false;
        [ColorUsage(true, true)] public Color LossSecondBarColor = Color.red;
        public bool LossUseTimeDelay = false;
        public float LossDelayTime = 0.2f;
        public float LossCatchUpTime = 0.5f;
        public bool BlendToInvisible = false;
        public AnimationCurve BlendToInvisibleCurve = AnimationCurve.Linear(0, 0, 1, 1);
        public bool LossAmountCatchUp = false;
        public AnimationCurve LossAmountCatchUpCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Impulse Settings - Fill")]
        public bool UseFillImpulse = false;
        public AnimationCurve ImpulseCurveFill = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0f));
        public float ImpulseDurationFill = 0.5f;
        public bool UseCustomOutlineFill = false;
        [Range(0, 1)] public float StrengthMinFill = 0, StrengthMaxFill = 1;
        [Range(1, 5)] public float PowerMinFill = 3, PowerMaxFill = 2;
        public bool OverrideFillCustomOutlineColor = false;
        [ColorUsage(true, true)] public Color FillCustomOutlineColor = Color.green;
        public bool UseFillShadowFill = false;
        [Range(0, 5)] public float ShadowPowerMinFill = 0, ShadowPowerMaxFill = 1.5f;
        public bool UseOverlayFill = false;
        [Range(0, 1)] public float OverlayStrengthMinFill = 0, OverlayStrengthMaxFill = 1;
        public bool OverrideFillOverlayColor = false;
        [ColorUsage(true, true)] public Color FillOverlayColor = Color.green;
        [ColorUsage(true, true)] public Color FillOverlayBackgroundColor = Color.green;

        [Header("Impulse Settings - Loss")]
        public bool UseLossImpulse = false;
        public AnimationCurve ImpulseCurveLoss = AnimationCurve.Linear(0, 0, 1, 1);
        public float ImpulseDurationLoss = 0.5f;
        public bool UseCustomOutlineLoss = false;
        [Range(0, 1)] public float StrengthMinLoss = 0, StrengthMaxLoss = 1;
        [Range(1, 5)] public float PowerMinLoss = 3, PowerMaxLoss = 2;
        public bool OverrideLossCustomOutlineColor = false;
        [ColorUsage(true, true)] public Color LossCustomOutlineColor = Color.red;
        public bool UseFillShadowLoss = false;
        [Range(0, 5)] public float ShadowPowerMinLoss = 0, ShadowPowerMaxLoss = 1.5f;
        public bool UseOverlayLoss = false;
        [Range(0, 1)] public float OverlayStrengthMinLoss = 0, OverlayStrengthMaxLoss = 1;
        public bool OverrideLossOverlayColor = false;
        [ColorUsage(true, true)] public Color LossOverlayColor = Color.red;
        [ColorUsage(true, true)] public Color LossOverlayBackgroundColor = Color.red;
    }

}
