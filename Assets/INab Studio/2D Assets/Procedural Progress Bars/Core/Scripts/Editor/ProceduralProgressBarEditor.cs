using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace INab.UI
{
    [CustomEditor(typeof(ProceduralProgressBar))]
    [CanEditMultipleObjects]
    public class ProceduralProgressBarEditor : Editor
    {
        #region Properties

        SerializedProperty config;
        SerializedProperty enableHelpBoxes;
        
        SerializedProperty progressBarMaterial;
        SerializedProperty instantiateMaterialOnStart;
        SerializedProperty barRenderer;

        SerializedProperty UseInitialFillAmount;
        SerializedProperty InitialFillAmount;


        SerializedProperty ShowShaderCoreControls;
        SerializedProperty fillAmount;
        SerializedProperty MainBarFillAmount;
        SerializedProperty SecondBarBlendToBar;
        SerializedProperty SecondBarBlendToInvisible;


        SerializedProperty defaultFillTime;
        SerializedProperty fillCurve;
        SerializedProperty defaultLossTime;
        SerializedProperty lossCurve;
        SerializedProperty endLineVisibility;
        SerializedProperty useAutoEndLine;
        SerializedProperty turnOffTime;
        SerializedProperty turnOnTime;
        SerializedProperty fillAmountThreshold;
        SerializedProperty barFillAmountEditor;
        SerializedProperty barLossAmountEditor;
        SerializedProperty UseSecondBarFill;
        SerializedProperty UseSecondBarLoss;
        SerializedProperty FillUseTimeDelay;
        SerializedProperty FillDelayTime;
        SerializedProperty LossUseTimeDelay;
        SerializedProperty LossDelayTime;
        SerializedProperty FillCatchUpTime;
        SerializedProperty BlendToMainBar;
        SerializedProperty BlendToMainBarCurve;
        SerializedProperty FillAmountCatchUp;
        SerializedProperty FillAmountCatchUpCurve;
        SerializedProperty LossCatchUpTime;
        SerializedProperty BlendToInvisible;
        SerializedProperty BlendToInvisibleCurve;
        SerializedProperty LossAmountCatchUp;
        SerializedProperty LossAmountCatchUpCurve;

        // New Impulse & Shader Properties

        // Impulse Settings (common for fill & loss)
        SerializedProperty useFillImpulse;
        SerializedProperty useLossImpulse;
        SerializedProperty impulseCurveFill;
        SerializedProperty impulseDurationFill;
        SerializedProperty impulseCurveLoss;
        SerializedProperty impulseDurationLoss;

        // Fill Stage Shader Properties
        SerializedProperty useCustomOutlineFill;
        SerializedProperty strengthMinFill;
        SerializedProperty strengthMaxFill;
        SerializedProperty powerMinFill;
        SerializedProperty powerMaxFill;
        SerializedProperty useFillShadowFill;
        SerializedProperty shadowPowerMinFill;
        SerializedProperty shadowPowerMaxFill;
        SerializedProperty useOverlayFill;
        SerializedProperty OverlayStrengthMinLoss;
        SerializedProperty OverlayStrengthMaxLoss;
        SerializedProperty OverlayStrengthMinFill;
        SerializedProperty OverlayStrengthMaxFill;

        // Loss Stage Shader Properties
        SerializedProperty useCustomOutlineLoss;
        SerializedProperty strengthMinLoss;
        SerializedProperty strengthMaxLoss;
        SerializedProperty powerMinLoss;
        SerializedProperty powerMaxLoss;
        SerializedProperty useFillShadowLoss;
        SerializedProperty shadowPowerMinLoss;
        SerializedProperty shadowPowerMaxLoss;
        SerializedProperty useOverlayLoss;
        SerializedProperty fillColorImpactMinLoss;
        SerializedProperty fillColorImpactMaxLoss;
        SerializedProperty fillAlphaImpactMinLoss;
        SerializedProperty fillAlphaImpactMaxLoss;
        SerializedProperty backgroundColorImpactMinLoss;
        SerializedProperty backgroundColorImpactMaxLoss;
        SerializedProperty backgroundAlphaImpactMinLoss;
        SerializedProperty backgroundAlphaImpactMaxLoss;

        // Fill stage color overrides
        SerializedProperty overrideFillCustomOutlineColor;
        SerializedProperty fillCustomOutlineColor;
        SerializedProperty overrideFillOverlayColor;
        SerializedProperty fillOverlayColor;
        SerializedProperty fillOverlayBackgroundColor;

        // Loss stage color overrides
        SerializedProperty overrideLossCustomOutlineColor;
        SerializedProperty lossCustomOutlineColor;
        SerializedProperty overrideLossOverlayColor;
        SerializedProperty lossOverlayColor;
        SerializedProperty lossOverlayBackgroundColor;

        SerializedProperty overrideFillSecondBarColor;
        SerializedProperty fillSecondBarColor;
        SerializedProperty overrideLossSecondBarColor;
        SerializedProperty lossSecondBarColor;

        #endregion

        ProceduralProgressBar progressBar;

        private bool DrawPersistedFoldout(string key, string label, bool defaultState = true)
        {
            bool state = EditorPrefs.GetBool(key, defaultState);
            bool newState = EditorGUILayout.BeginFoldoutHeaderGroup(state, label, EditorStyles.foldoutHeader);
            if (newState != state)
            {
                EditorPrefs.SetBool(key, newState);
            }
            return newState;
        }



        void OnEnable()
        {
            overrideFillSecondBarColor = serializedObject.FindProperty("OverrideFillSecondBarColor");
            fillSecondBarColor = serializedObject.FindProperty("FillSecondBarColor");
            overrideLossSecondBarColor = serializedObject.FindProperty("OverrideLossSecondBarColor");
            lossSecondBarColor = serializedObject.FindProperty("LossSecondBarColor");
            config = serializedObject.FindProperty("config");
            enableHelpBoxes = serializedObject.FindProperty("enableHelpBoxes");
            

            overrideFillCustomOutlineColor = serializedObject.FindProperty("OverrideFillCustomOutlineColor");
            fillCustomOutlineColor = serializedObject.FindProperty("FillCustomOutlineColor");
            overrideFillOverlayColor = serializedObject.FindProperty("OverrideFillOverlayColor");
            fillOverlayColor = serializedObject.FindProperty("FillOverlayColor");
            fillOverlayBackgroundColor = serializedObject.FindProperty("FillOverlayBackgroundColor");

            overrideLossCustomOutlineColor = serializedObject.FindProperty("OverrideLossCustomOutlineColor");
            lossCustomOutlineColor = serializedObject.FindProperty("LossCustomOutlineColor");
            overrideLossOverlayColor = serializedObject.FindProperty("OverrideLossOverlayColor");
            lossOverlayColor = serializedObject.FindProperty("LossOverlayColor");
            lossOverlayBackgroundColor = serializedObject.FindProperty("LossOverlayBackgroundColor");

            progressBarMaterial = serializedObject.FindProperty("progressBarMaterial");
            //updateInEditor = serializedObject.FindProperty("UpdateInEditor");
            instantiateMaterialOnStart = serializedObject.FindProperty("instantiateMaterialOnStart");
            barRenderer = serializedObject.FindProperty("barRenderer");

            UseInitialFillAmount = serializedObject.FindProperty("UseInitialFillAmount");
            InitialFillAmount = serializedObject.FindProperty("InitialFillAmount");


            fillAmount = serializedObject.FindProperty("FillAmount");

            ShowShaderCoreControls = serializedObject.FindProperty("ShowShaderCoreControls");
            MainBarFillAmount = serializedObject.FindProperty("MainBarFillAmount");
            SecondBarBlendToBar = serializedObject.FindProperty("SecondBarBlendToBar");
            SecondBarBlendToInvisible = serializedObject.FindProperty("SecondBarBlendToInvisible");

            defaultFillTime = serializedObject.FindProperty("DefaultFillTime");
            fillCurve = serializedObject.FindProperty("FillCurve");
            defaultLossTime = serializedObject.FindProperty("DefaultLossTime");
            lossCurve = serializedObject.FindProperty("LossCurve");
            endLineVisibility = serializedObject.FindProperty("EndLineVisibility");
            useAutoEndLine = serializedObject.FindProperty("UseAutoEndLine");
            turnOffTime = serializedObject.FindProperty("TurnOffTime");
            turnOnTime = serializedObject.FindProperty("TurnOnTime");
            fillAmountThreshold = serializedObject.FindProperty("FillAmountThreshold");
            barFillAmountEditor = serializedObject.FindProperty("BarFillAmountEditor");
            barLossAmountEditor = serializedObject.FindProperty("BarLossAmountEditor");
            UseSecondBarFill = serializedObject.FindProperty("UseSecondBarFill");
            UseSecondBarLoss = serializedObject.FindProperty("UseSecondBarLoss");
            FillUseTimeDelay = serializedObject.FindProperty("FillUseTimeDelay");
            FillDelayTime = serializedObject.FindProperty("FillDelayTime");
            LossUseTimeDelay = serializedObject.FindProperty("LossUseTimeDelay");
            LossDelayTime = serializedObject.FindProperty("LossDelayTime");
            FillCatchUpTime = serializedObject.FindProperty("FillCatchUpTime");
            BlendToMainBar = serializedObject.FindProperty("BlendToMainBar");
            BlendToMainBarCurve = serializedObject.FindProperty("BlendToMainBarCurve");
            FillAmountCatchUp = serializedObject.FindProperty("FillAmountCatchUp");
            FillAmountCatchUpCurve = serializedObject.FindProperty("FillAmountCatchUpCurve");
            LossCatchUpTime = serializedObject.FindProperty("LossCatchUpTime");
            BlendToInvisible = serializedObject.FindProperty("BlendToInvisible");
            BlendToInvisibleCurve = serializedObject.FindProperty("BlendToInvisibleCurve");
            LossAmountCatchUp = serializedObject.FindProperty("LossAmountCatchUp");
            LossAmountCatchUpCurve = serializedObject.FindProperty("LossAmountCatchUpCurve");
            MainBarFillAmount = serializedObject.FindProperty("MainBarFillAmount");
            SecondBarBlendToBar = serializedObject.FindProperty("SecondBarBlendToBar");
            SecondBarBlendToInvisible = serializedObject.FindProperty("SecondBarBlendToInvisible");

            // Impulse Settings
            useFillImpulse = serializedObject.FindProperty("UseFillImpulse");
            useLossImpulse = serializedObject.FindProperty("UseLossImpulse");
            impulseCurveFill = serializedObject.FindProperty("ImpulseCurveFill");
            impulseDurationFill = serializedObject.FindProperty("ImpulseDurationFill");
            impulseCurveLoss = serializedObject.FindProperty("ImpulseCurveLoss");
            impulseDurationLoss = serializedObject.FindProperty("ImpulseDurationLoss");

            // Fill Stage Shader Properties
            useCustomOutlineFill = serializedObject.FindProperty("UseCustomOutlineFill");
            strengthMinFill = serializedObject.FindProperty("StrengthMinFill");
            strengthMaxFill = serializedObject.FindProperty("StrengthMaxFill");
            powerMinFill = serializedObject.FindProperty("PowerMinFill");
            powerMaxFill = serializedObject.FindProperty("PowerMaxFill");
            useFillShadowFill = serializedObject.FindProperty("UseFillShadowFill");
            shadowPowerMinFill = serializedObject.FindProperty("ShadowPowerMinFill");
            shadowPowerMaxFill = serializedObject.FindProperty("ShadowPowerMaxFill");
            useOverlayFill = serializedObject.FindProperty("UseOverlayFill");
            OverlayStrengthMinLoss = serializedObject.FindProperty("OverlayStrengthMinLoss");
            OverlayStrengthMaxLoss = serializedObject.FindProperty("OverlayStrengthMaxLoss");
            OverlayStrengthMinFill = serializedObject.FindProperty("OverlayStrengthMinFill");
            OverlayStrengthMaxFill = serializedObject.FindProperty("OverlayStrengthMaxFill");


            // Loss Stage Shader Properties
            useCustomOutlineLoss = serializedObject.FindProperty("UseCustomOutlineLoss");
            strengthMinLoss = serializedObject.FindProperty("StrengthMinLoss");
            strengthMaxLoss = serializedObject.FindProperty("StrengthMaxLoss");
            powerMinLoss = serializedObject.FindProperty("PowerMinLoss");
            powerMaxLoss = serializedObject.FindProperty("PowerMaxLoss");
            useFillShadowLoss = serializedObject.FindProperty("UseFillShadowLoss");
            shadowPowerMinLoss = serializedObject.FindProperty("ShadowPowerMinLoss");
            shadowPowerMaxLoss = serializedObject.FindProperty("ShadowPowerMaxLoss");
            useOverlayLoss = serializedObject.FindProperty("UseOverlayLoss");
            fillColorImpactMinLoss = serializedObject.FindProperty("FillColorImpactMinLoss");
            fillColorImpactMaxLoss = serializedObject.FindProperty("FillColorImpactMaxLoss");
            fillAlphaImpactMinLoss = serializedObject.FindProperty("FillAlphaImpactMinLoss");
            fillAlphaImpactMaxLoss = serializedObject.FindProperty("FillAlphaImpactMaxLoss");
            backgroundColorImpactMinLoss = serializedObject.FindProperty("BackgroundColorImpactMinLoss");
            backgroundColorImpactMaxLoss = serializedObject.FindProperty("BackgroundColorImpactMaxLoss");
            backgroundAlphaImpactMinLoss = serializedObject.FindProperty("BackgroundAlphaImpactMinLoss");
            backgroundAlphaImpactMaxLoss = serializedObject.FindProperty("BackgroundAlphaImpactMaxLoss");
        }

        void Setup()
        {


            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                CurrentFillAmount();
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

                // --- Existing Inspector GUI ---
                EditorGUILayout.PropertyField(enableHelpBoxes);

                if (progressBar._IsMaterialNull())
                {
                    EditorGUILayout.HelpBox("The material is missing. Assign a material to use the progress bar. You can use the 'Get Material' button below.", MessageType.Warning);
                }
                else if (!progressBar._IsMaterialValid())
                {
                    EditorGUILayout.HelpBox("Assigned material must use the INab Studio/Procedural Progress Bar shader. Please change the material's shader or create and assign a new material using the correct shader to the renderer and script.", MessageType.Error);
                }

                if (progressBar.enableHelpBoxes)
                {
                    EditorGUILayout.HelpBox("Assign a material that uses the INab Studio/Procedural Progress Bar shader. This material will be controlled by the script.", MessageType.Info);
                }
                EditorGUILayout.PropertyField(progressBarMaterial);

                if (progressBar.enableHelpBoxes)
                {
                    EditorGUILayout.HelpBox("Enable this if you're using the same material on multiple objects in the scene. It creates a unique instance to avoid modifying shared materials.", MessageType.Info);
                }
                EditorGUILayout.PropertyField(instantiateMaterialOnStart);
                if (instantiateMaterialOnStart.boolValue)
                {
                    EditorGUILayout.PropertyField(barRenderer);
                    if (progressBar._IsRendererNull())
                    {
                        EditorGUILayout.HelpBox("The renderer is not assigned. Please assign a Renderer component to control the bar's material instance.", MessageType.Warning);
                    }
                }

                if (GUILayout.Button("Get Material"))
                {
                    progressBar._GetMaterial();
                }

                // Warnings
                if(progressBar._IsMaterialValid())
                {
                    if (progressBar._IsSmoothEdgeEnabledInMaterial() && progressBar._IsSecondBarEnabledInMaterial())
                    {
                        EditorGUILayout.HelpBox("Second Bar does not work correctly when 'Smooth Edge' is enabled. Please turn off 'Smooth Edge' in the shader settings.", MessageType.Warning);
                    }
                }

                EditorGUILayout.Space();


                if (enableHelpBoxes.boolValue)
                {
                    EditorGUILayout.HelpBox("Enable this if you want to set an initial fill value at Start().", MessageType.Info);
                }
                EditorGUILayout.PropertyField(UseInitialFillAmount);
                if(UseInitialFillAmount.boolValue) EditorGUILayout.PropertyField(InitialFillAmount);

                //EditorGUILayout.PropertyField(updateInEditor);
            }
        }

        void FillSettings()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (progressBar.enableHelpBoxes)
                {
                    EditorGUILayout.HelpBox("Duration in seconds it takes to fully fill the bar. Controls the speed of the fill animation. Fill time value can be overridden via code.", MessageType.Info);
                }
                EditorGUILayout.PropertyField(defaultFillTime);

                if (progressBar.enableHelpBoxes)
                {
                    EditorGUILayout.HelpBox("Animation curve that defines how the fill progresses over time. Allows easing effects.", MessageType.Info);
                }
                EditorGUILayout.PropertyField(fillCurve);

                EditorGUILayout.Space();

                if (progressBar.enableHelpBoxes)
                {
                    EditorGUILayout.HelpBox("Duration in seconds for the bar to decrease when losing value. Controls how fast it drains. Fill time value can be overridden via code.", MessageType.Info);
                }
                EditorGUILayout.PropertyField(defaultLossTime);

                if (progressBar.enableHelpBoxes)
                {
                    EditorGUILayout.HelpBox("Animation curve that defines how the loss animation behaves over time. Use it for easing or impact effects.", MessageType.Info);
                }
                EditorGUILayout.PropertyField(lossCurve);
            }
        }

        void Testing()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Properties Saves", EditorStyles.boldLabel);

                if (progressBar.enableHelpBoxes)
                {
                    EditorGUILayout.HelpBox("A config asset allows you to tweak and store settings at runtime, making iteration easier.", MessageType.Info);
                }

                EditorGUILayout.PropertyField(config);
                if (progressBar.config == null)
                {
                    if (GUILayout.Button("Create New"))
                    {
                        progressBar.CreateNewConfig();
                    }
                }
                else
                {
                    if (progressBar.enableHelpBoxes)
                    {
                        EditorGUILayout.HelpBox("Use these buttons to load settings from or save current settings to the config asset. Great for keeping changes made during runtime.", MessageType.Info);
                    }

                    if (GUILayout.Button("Save To Config"))
                    {
                        Undo.RecordObject(progressBar.config, "Apply Progress Bar Settings ");
                        progressBar.ApplyToConfig();
                    }

                    if (GUILayout.Button("Load From Config"))
                    {
                        Undo.RecordObject(progressBar.config, "Apply Progress Bar Settings To Script From Config");
                        progressBar.LoadFromConfig();
                    }
                }

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Core Controls", EditorStyles.boldLabel);
                if (progressBar.enableHelpBoxes)
                {
                    EditorGUILayout.HelpBox("Displays and allows editing of the internal shader fill values. Works only in Play mode.", MessageType.Info);
                }
                EditorGUILayout.PropertyField(ShowShaderCoreControls);

                //using (new EditorGUI.DisabledGroupScope(!Application.isPlaying))
                {
                    if (progressBar.ShowShaderCoreControls)
                    {
                        if (progressBar.enableHelpBoxes)
                        {
                            EditorGUILayout.HelpBox("These are low-level shader properties.", MessageType.Info); // Use them only if you need direct control over the fill visuals during runtime.
                        }
                        EditorGUILayout.PropertyField(fillAmount);
                        if(progressBar._IsSecondBarEnabledInMaterial())
                        {
                            EditorGUILayout.PropertyField(MainBarFillAmount);
                            EditorGUILayout.PropertyField(SecondBarBlendToBar);
                            EditorGUILayout.PropertyField(SecondBarBlendToInvisible);
                        }
                        EditorGUILayout.Space();
                    }
                }

                EditorGUILayout.LabelField("Testing Buttons", EditorStyles.boldLabel);
                if (progressBar.enableHelpBoxes)
                {
                    EditorGUILayout.HelpBox("Use these buttons to simulate fill or loss changes in the progress bar for testing purposes. Works only in Play mode.", MessageType.Info);
                    EditorGUILayout.HelpBox("Bar Fill/Loss amount is a value to apply to the fill function during testing.", MessageType.Info);
                }

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.PropertyField(barFillAmountEditor);

                    using (new EditorGUI.DisabledGroupScope(!Application.isPlaying))
                    {
                        if (GUILayout.Button("Bar Fill"))
                        {
                            progressBar._BarFillEditor();
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.PropertyField(barLossAmountEditor);
                    using (new EditorGUI.DisabledGroupScope(!Application.isPlaying))
                    {
                        if (GUILayout.Button("Bar Loss"))
                        {
                            progressBar._BarLossEditor();
                        }
                    }
                }
            }
        }

        void AutoEndLine()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                //EditorGUILayout.LabelField("Auto End Line Settings", EditorStyles.boldLabel);

                if (progressBar.enableHelpBoxes)
                {
                    EditorGUILayout.HelpBox("Automatically manages the end line visibility based on fill state. Useful to hide the line when the bar is overfilled.", MessageType.Info);
                }

                EditorGUILayout.PropertyField(useAutoEndLine);
                if (useAutoEndLine.boolValue)
                {
                    if (!progressBar._IsEndLineEnabledInMaterial())
                    {
                        EditorGUILayout.HelpBox("'End Line' is disabled in the material. Enable it to use this feature.", MessageType.Error);
                    }

                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.PropertyField(endLineVisibility);
                    EditorGUI.EndDisabledGroup();

                    EditorGUILayout.PropertyField(turnOffTime);
                    EditorGUILayout.PropertyField(turnOnTime);
                    EditorGUILayout.PropertyField(fillAmountThreshold);
                }
            }
        }

        void SecondBar()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                //EditorGUILayout.LabelField("Second Bar Settings", EditorStyles.boldLabel);
                if (progressBar.enableHelpBoxes)
                {
                    EditorGUILayout.HelpBox("Enables an additional visual layer for the progress bar with separate fill/loss behavior. Useful for showing delayed or ghosted values.", MessageType.Info);
                }

                //EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Fill Stage", EditorStyles.miniBoldLabel);

                if (progressBar.enableHelpBoxes)
                {
                    EditorGUILayout.HelpBox("Enables the second bar layer for the fill stage. Useful for creating delayed or ghost-style fill visuals.", MessageType.Info);
                }
               
                EditorGUILayout.PropertyField(UseSecondBarFill);
                if (UseSecondBarFill.boolValue)
                {
                    if (!progressBar._IsSecondBarEnabledInMaterial())
                    {
                        EditorGUILayout.HelpBox("'Second Bar' is disabled in the material. Enable it to use this feature.", MessageType.Error);
                    }
                    EditorGUILayout.PropertyField(FillUseTimeDelay);
                    if (FillUseTimeDelay.boolValue)
                    {
                        EditorGUILayout.PropertyField(FillDelayTime);
                        EditorGUILayout.Space();
                    }
                    EditorGUILayout.PropertyField(FillCatchUpTime);
                    EditorGUILayout.Space();
                    EditorGUILayout.PropertyField(BlendToMainBar);
                    if (BlendToMainBar.boolValue)
                    {
                        EditorGUILayout.PropertyField(BlendToMainBarCurve);
                        EditorGUILayout.Space();
                    }
                    EditorGUILayout.PropertyField(FillAmountCatchUp);
                    if (FillAmountCatchUp.boolValue)
                    {
                        EditorGUILayout.PropertyField(FillAmountCatchUpCurve);
                        EditorGUILayout.Space();
                    }

                    EditorGUILayout.PropertyField(overrideFillSecondBarColor);
                    if (overrideFillSecondBarColor.boolValue)
                    {
                        EditorGUILayout.PropertyField(fillSecondBarColor);
                    }

                }

                EditorGUILayout.LabelField("Loss Stage", EditorStyles.miniBoldLabel);

                if (progressBar.enableHelpBoxes)
                {
                    EditorGUILayout.HelpBox("Enables the second bar layer for the loss stage. Useful for trailing visuals or delayed loss effects.", MessageType.Info);
                }

                EditorGUILayout.PropertyField(UseSecondBarLoss);
                if (UseSecondBarLoss.boolValue)
                {
                    if (!progressBar._IsSecondBarEnabledInMaterial())
                    {
                        EditorGUILayout.HelpBox("'Second Bar' is disabled in the material. Enable it to use this feature.", MessageType.Error);
                    }
                    EditorGUILayout.PropertyField(LossUseTimeDelay);
                    if (LossUseTimeDelay.boolValue)
                    {
                        EditorGUILayout.PropertyField(LossDelayTime);
                        EditorGUILayout.Space();
                    }
                    EditorGUILayout.PropertyField(LossCatchUpTime);
                    EditorGUILayout.Space();
                    EditorGUILayout.PropertyField(BlendToInvisible);
                    if (BlendToInvisible.boolValue)
                    {
                        EditorGUILayout.PropertyField(BlendToInvisibleCurve);
                        EditorGUILayout.Space();
                    }
                    EditorGUILayout.PropertyField(LossAmountCatchUp);
                    if (LossAmountCatchUp.boolValue)
                        EditorGUILayout.PropertyField(LossAmountCatchUpCurve);

                    EditorGUILayout.PropertyField(overrideLossSecondBarColor);
                    if (overrideLossSecondBarColor.boolValue)
                    {
                        EditorGUILayout.PropertyField(lossSecondBarColor);
                    }

                }

                //EditorGUI.indentLevel--;
            }
        }

        void Impulse()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (progressBar.enableHelpBoxes)
                {
                    EditorGUILayout.HelpBox("Enables visual 'impulse' effects when the bar fills or loses value. Useful for feedback animations like pulsing, glowing, or color shifts. You can configure separate effects for fill and loss stages.", MessageType.Info);
                }

                EditorGUILayout.LabelField("Fill Stage Impulse", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(useFillImpulse);

                if (useFillImpulse.boolValue)
                {
                    if (progressBar.enableHelpBoxes)
                    {
                        EditorGUILayout.HelpBox("Controls how the fill impulse effect animates over time. Use the curve to define the shape of the effect.", MessageType.Info);
                    }

                    EditorGUILayout.PropertyField(impulseCurveFill);
                    EditorGUILayout.PropertyField(impulseDurationFill);
                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(useCustomOutlineFill);
                    if (useCustomOutlineFill.boolValue)
                    {
                        EditorGUILayout.PropertyField(overrideFillCustomOutlineColor);
                        if (overrideFillCustomOutlineColor.boolValue)
                        {
                            EditorGUILayout.PropertyField(fillCustomOutlineColor);
                        }

                        if (!progressBar._IsCustomOutlineEnabledInMaterial())
                        {
                            EditorGUILayout.HelpBox("The 'Custom Outline' feature is disabled in the material. Enable it to use outline impulses.", MessageType.Error);
                        }

                        if (progressBar.enableHelpBoxes)
                        {
                            EditorGUILayout.HelpBox("Controls the outline strength and power during the impulse effect.", MessageType.Info);
                        }

                        EditorGUILayout.PropertyField(strengthMinFill);
                        EditorGUILayout.PropertyField(strengthMaxFill);
                        EditorGUILayout.PropertyField(powerMinFill);
                        EditorGUILayout.PropertyField(powerMaxFill);
                    }

                    EditorGUILayout.PropertyField(useFillShadowFill);
                    if (useFillShadowFill.boolValue)
                    {
                        if (!progressBar._IsFillShadowEnabledInMaterial())
                        {
                            EditorGUILayout.HelpBox("The 'Fill Shadow' feature is disabled in the material. Enable it to use shadow impulses.", MessageType.Error);
                        }

                        if (progressBar.enableHelpBoxes)
                        {
                            EditorGUILayout.HelpBox("Adjusts shadow intensity over time during the impulse.", MessageType.Info);
                        }

                        EditorGUILayout.PropertyField(shadowPowerMinFill);
                        EditorGUILayout.PropertyField(shadowPowerMaxFill);
                    }

                    EditorGUILayout.PropertyField(useOverlayFill);
                    if (useOverlayFill.boolValue)
                    {
                        EditorGUILayout.PropertyField(overrideFillOverlayColor);
                        if (overrideFillOverlayColor.boolValue)
                        {
                            EditorGUILayout.PropertyField(fillOverlayColor);
                            if(progressBar._IsOverlayGuidesEnabledInMaterial()) EditorGUILayout.PropertyField(fillOverlayBackgroundColor);
                        }

                        if (!progressBar._IsOverlayEnabledInMaterial())
                        {
                            EditorGUILayout.HelpBox("The 'Overlay' feature is disabled in the material. Enable it to use overlay impulses.", MessageType.Error);
                        }

                        if (progressBar.enableHelpBoxes)
                        {
                            EditorGUILayout.HelpBox("Controls the minimum and maximum overlay intensity during fill impulse.", MessageType.Info);
                        }

                        EditorGUILayout.PropertyField(OverlayStrengthMinFill);
                        EditorGUILayout.PropertyField(OverlayStrengthMaxFill);
                    }
                }

                EditorGUILayout.LabelField("Loss Stage Impulse", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(useLossImpulse);

                if (useLossImpulse.boolValue)
                {
                    if (progressBar.enableHelpBoxes)
                    {
                        EditorGUILayout.HelpBox("Controls how the loss impulse effect animates over time. Use the curve to define its progression.", MessageType.Info);
                    }

                    EditorGUILayout.PropertyField(impulseCurveLoss);
                    EditorGUILayout.PropertyField(impulseDurationLoss);
                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(useCustomOutlineLoss);
                    if (useCustomOutlineLoss.boolValue)
                    {
                        EditorGUILayout.PropertyField(overrideLossCustomOutlineColor);
                        if (overrideLossCustomOutlineColor.boolValue)
                        {
                            EditorGUILayout.PropertyField(lossCustomOutlineColor);
                        }

                        if (!progressBar._IsCustomOutlineEnabledInMaterial())
                        {
                            EditorGUILayout.HelpBox("The 'Custom Outline' feature is disabled in the material. Enable it to use outline impulses.", MessageType.Error);
                        }

                        if (progressBar.enableHelpBoxes)
                        {
                            EditorGUILayout.HelpBox("Controls the outline strength and power during the loss impulse effect.", MessageType.Info);
                        }

                        EditorGUILayout.PropertyField(strengthMinLoss);
                        EditorGUILayout.PropertyField(strengthMaxLoss);
                        EditorGUILayout.PropertyField(powerMinLoss);
                        EditorGUILayout.PropertyField(powerMaxLoss);
                    }

                    EditorGUILayout.PropertyField(useFillShadowLoss);
                    if (useFillShadowLoss.boolValue)
                    {
                        if (!progressBar._IsFillShadowEnabledInMaterial())
                        {
                            EditorGUILayout.HelpBox("The 'Fill Shadow' feature is disabled in the material. Enable it to use shadow impulses.", MessageType.Error);
                        }

                        if (progressBar.enableHelpBoxes)
                        {
                            EditorGUILayout.HelpBox("Adjusts shadow intensity over time during the loss impulse.", MessageType.Info);
                        }

                        EditorGUILayout.PropertyField(shadowPowerMinLoss);
                        EditorGUILayout.PropertyField(shadowPowerMaxLoss);
                    }

                    EditorGUILayout.PropertyField(useOverlayLoss);
                    if (useOverlayLoss.boolValue)
                    {
                        EditorGUILayout.PropertyField(overrideLossOverlayColor);
                        if (overrideLossOverlayColor.boolValue)
                        {
                            EditorGUILayout.PropertyField(lossOverlayColor);
                            if (progressBar._IsOverlayGuidesEnabledInMaterial()) EditorGUILayout.PropertyField(lossOverlayBackgroundColor);
                        }

                        if (!progressBar._IsOverlayEnabledInMaterial())
                        {
                            EditorGUILayout.HelpBox("The 'Overlay' feature is disabled in the material. Enable it to use overlay impulses.", MessageType.Error);
                        }

                        if (progressBar.enableHelpBoxes)
                        {
                            EditorGUILayout.HelpBox("Controls the minimum and maximum overlay intensity during loss impulse.", MessageType.Info);
                        }

                        EditorGUILayout.PropertyField(OverlayStrengthMinLoss);
                        EditorGUILayout.PropertyField(OverlayStrengthMaxLoss);
                    }
                }
            }
        }

        private void CurrentFillAmount()
        {

            if (progressBar.enableHelpBoxes)
            {
                EditorGUILayout.HelpBox("Controls the fill amount of the bar. Disabled if using second bar.", MessageType.Info);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                bool disabled = false;
                if (progressBar._IsSecondBarEnabledInMaterial()) disabled = true;

                EditorGUILayout.LabelField("Current Fill Amount", EditorStyles.boldLabel);
                EditorGUI.BeginDisabledGroup(disabled);
                fillAmount.floatValue = EditorGUILayout.Slider(fillAmount.floatValue, 0f, 1f);
                EditorGUI.EndDisabledGroup();
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            progressBar = (ProceduralProgressBar)target;


            if (DrawPersistedFoldout("ProceduralProgressBarEditor_SetupFoldout", "Setup"))
            {
                Setup();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (DrawPersistedFoldout("ProceduralProgressBarEditor_TestingFoldout", "Setting Up (only in runtime)"))
            {
                Testing();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (DrawPersistedFoldout("ProceduralProgressBarEditor_FillFoldout", "Fill Settings"))
            {
                FillSettings();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (DrawPersistedFoldout("ProceduralProgressBarEditor_AutoEndLineFoldout", "Auto End Line Settings"))
            {
                AutoEndLine();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (DrawPersistedFoldout("ProceduralProgressBarEditor_SecondBarFoldout", "Second Bar Settings"))
            {
                SecondBar();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (DrawPersistedFoldout("ProceduralProgressBarEditor_ImpulseFoldout", "Impulse Settings"))
            {
                Impulse();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

           


            serializedObject.ApplyModifiedProperties();
        }
    }
}
