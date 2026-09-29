using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    [CustomEditor(typeof(AirAudioSettings))]
    public sealed class AirAudioSettingsEditor : UnityEditor.Editor
    {
        private bool _showEnable = true;
        private bool _showClips = true;
        private bool _showMix = true;
        private bool _showMovement = true;
        private bool _showAbilities = true;
        private bool _showLoops = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty audioEnabled =
                serializedObject.FindProperty("audioEnabled");

            EditorGUILayout.HelpBox(
                "Every one-shot now has independent Volume + Random Pitch Min/Max. " +
                "Loops have direct volume and pitch controls. Set Pitch Min = Pitch Max = 1.00 for no variation.",
                MessageType.Info);

            EditorGUILayout.PropertyField(
                audioEnabled,
                new GUIContent("Master Audio Enabled"));

            EditorGUILayout.Space(6f);

            _showEnable =
                EditorGUILayout.Foldout(
                    _showEnable,
                    "Individual Source Toggles",
                    true);

            if (_showEnable)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField(
                    "Movement",
                    EditorStyles.boldLabel);

                Draw("footstepEnabled");
                Draw("jumpEnabled");
                Draw("landingEnabled");
                Draw("flipWhooshEnabled");
                Draw("dashStartEnabled");
                Draw("dashWindLoopEnabled");

                EditorGUILayout.Space(3f);

                EditorGUILayout.LabelField(
                    "Air Cast",
                    EditorStyles.boldLabel);

                Draw("chargeLoopEnabled");
                Draw("targetReadyEnabled");
                Draw("castReleaseEnabled");
                Draw("nodeHitEnabled");
                Draw("returnLaunchEnabled");
                Draw("rewardArrivalEnabled");
                Draw("airPowerDepletedEnabled");

                EditorGUILayout.Space(3f);

                EditorGUILayout.LabelField(
                    "World",
                    EditorStyles.boldLabel);

                Draw("ambienceEnabled");
                Draw("musicEnabled");

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6f);

            using (new EditorGUI.DisabledScope(
                audioEnabled != null &&
                !audioEnabled.boolValue))
            {
                DrawRest();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRest()
        {
            _showClips =
                EditorGUILayout.Foldout(
                    _showClips,
                    "Audio Clips",
                    true);

            if (_showClips)
            {
                EditorGUI.indentLevel++;

                Draw("footstep");
                Draw("jump");
                Draw("landing");
                Draw("flipWhoosh");
                Draw("dashStart");
                Draw("dashWindLoop");

                EditorGUILayout.Space(3f);

                Draw("chargeLoop");
                Draw("targetReady");
                Draw("castRelease");
                Draw("nodeHit");
                Draw("returnLaunch");
                Draw("rewardArrival");
                Draw("airPowerDepleted");

                EditorGUILayout.Space(3f);

                Draw("ambienceLoop");
                Draw("musicLoop");

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6f);

            _showMix =
                EditorGUILayout.Foldout(
                    _showMix,
                    "Global Mix",
                    true);

            if (_showMix)
            {
                EditorGUI.indentLevel++;

                Draw("useProceduralFallbacks");
                Draw("masterVolume");
                Draw("movementVolume");
                Draw("abilityVolume");
                Draw("ambienceVolume");
                Draw("musicVolume");

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6f);

            _showMovement =
                EditorGUILayout.Foldout(
                    _showMovement,
                    "Movement One-Shots",
                    true);

            if (_showMovement)
            {
                DrawOneShotBlock(
                    "Footstep",
                    "footstepEnabled",
                    "footstepVolume",
                    "footstepPitchMin",
                    "footstepPitchMax");

                DrawOneShotBlock(
                    "Jump",
                    "jumpEnabled",
                    "jumpVolume",
                    "jumpPitchMin",
                    "jumpPitchMax");

                DrawOneShotBlock(
                    "Landing",
                    "landingEnabled",
                    "landingVolume",
                    "landingPitchMin",
                    "landingPitchMax");

                DrawOneShotBlock(
                    "Flip Whoosh",
                    "flipWhooshEnabled",
                    "flipWhooshVolume",
                    "flipWhooshPitchMin",
                    "flipWhooshPitchMax");

                DrawOneShotBlock(
                    "Dash Start",
                    "dashStartEnabled",
                    "dashStartVolume",
                    "dashStartPitchMin",
                    "dashStartPitchMax");
            }

            EditorGUILayout.Space(6f);

            _showAbilities =
                EditorGUILayout.Foldout(
                    _showAbilities,
                    "Ability One-Shots",
                    true);

            if (_showAbilities)
            {
                DrawOneShotBlock(
                    "Target Ready",
                    "targetReadyEnabled",
                    "targetReadyVolume",
                    "targetReadyPitchMin",
                    "targetReadyPitchMax");

                DrawOneShotBlock(
                    "Cast Release",
                    "castReleaseEnabled",
                    "castReleaseVolume",
                    "castReleasePitchMin",
                    "castReleasePitchMax");

                DrawOneShotBlock(
                    "Node Hit",
                    "nodeHitEnabled",
                    "nodeHitVolume",
                    "nodeHitPitchMin",
                    "nodeHitPitchMax");

                DrawOneShotBlock(
                    "Return Launch",
                    "returnLaunchEnabled",
                    "returnLaunchVolume",
                    "returnLaunchPitchMin",
                    "returnLaunchPitchMax");

                DrawOneShotBlock(
                    "Reward Arrival",
                    "rewardArrivalEnabled",
                    "rewardArrivalVolume",
                    "rewardArrivalPitchMin",
                    "rewardArrivalPitchMax");

                DrawOneShotBlock(
                    "Air Power Depleted",
                    "airPowerDepletedEnabled",
                    "airPowerDepletedVolume",
                    "airPowerDepletedPitchMin",
                    "airPowerDepletedPitchMax");
            }

            EditorGUILayout.Space(6f);

            _showLoops =
                EditorGUILayout.Foldout(
                    _showLoops,
                    "Loops / Continuous Audio",
                    true);

            if (_showLoops)
            {
                DrawLoopBox(
                    "Dash Wind",
                    "dashWindLoopEnabled",
                    new[]
                    {
                        "windStartSpeed",
                        "windFullSpeed",
                        "windMaxVolume",
                        "windMinPitch",
                        "windMaxPitch"
                    });

                DrawLoopBox(
                    "Charge",
                    "chargeLoopEnabled",
                    new[]
                    {
                        "chargeMaxVolume",
                        "chargeStartPitch",
                        "chargeReadyPitch"
                    });

                DrawLoopBox(
                    "Ambience",
                    "ambienceEnabled",
                    new[]
                    {
                        "ambienceVolume",
                        "ambiencePitch"
                    });

                DrawLoopBox(
                    "Music",
                    "musicEnabled",
                    new[]
                    {
                        "musicVolume",
                        "musicPitch"
                    });
            }
        }

        private void DrawOneShotBlock(
            string label,
            string enabledName,
            string volumeName,
            string pitchMinName,
            string pitchMaxName)
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox);

            SerializedProperty enabled =
                serializedObject.FindProperty(
                    enabledName);

            if (enabled != null)
            {
                enabled.boolValue =
                    EditorGUILayout.ToggleLeft(
                        label,
                        enabled.boolValue,
                        EditorStyles.boldLabel);
            }
            else
            {
                EditorGUILayout.LabelField(
                    label,
                    EditorStyles.boldLabel);
            }

            using (new EditorGUI.DisabledScope(
                enabled != null &&
                !enabled.boolValue))
            {
                Draw(volumeName);

                SerializedProperty min =
                    serializedObject.FindProperty(
                        pitchMinName);

                SerializedProperty max =
                    serializedObject.FindProperty(
                        pitchMaxName);

                if (min != null &&
                    max != null)
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.PrefixLabel(
                        "Random Pitch");

                    min.floatValue =
                        EditorGUILayout.FloatField(
                            min.floatValue,
                            GUILayout.MinWidth(45f));

                    GUILayout.Label(
                        "to",
                        GUILayout.Width(18f));

                    max.floatValue =
                        EditorGUILayout.FloatField(
                            max.floatValue,
                            GUILayout.MinWidth(45f));

                    EditorGUILayout.EndHorizontal();

                    min.floatValue =
                        Mathf.Clamp(
                            min.floatValue,
                            0.5f,
                            1.5f);

                    max.floatValue =
                        Mathf.Clamp(
                            max.floatValue,
                            0.5f,
                            1.5f);

                    if (max.floatValue <
                        min.floatValue)
                    {
                        max.floatValue =
                            min.floatValue;
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLoopBox(
            string label,
            string enabledName,
            string[] propertyNames)
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox);

            SerializedProperty enabled =
                serializedObject.FindProperty(
                    enabledName);

            if (enabled != null)
            {
                enabled.boolValue =
                    EditorGUILayout.ToggleLeft(
                        label,
                        enabled.boolValue,
                        EditorStyles.boldLabel);
            }
            else
            {
                EditorGUILayout.LabelField(
                    label,
                    EditorStyles.boldLabel);
            }

            using (new EditorGUI.DisabledScope(
                enabled != null &&
                !enabled.boolValue))
            {
                foreach (string propertyName in propertyNames)
                    Draw(propertyName);
            }

            EditorGUILayout.EndVertical();
        }

        private void Draw(string propertyName)
        {
            SerializedProperty property =
                serializedObject.FindProperty(
                    propertyName);

            if (property != null)
                EditorGUILayout.PropertyField(property, true);
        }
    }
}
