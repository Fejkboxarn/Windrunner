using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    [CustomEditor(typeof(PlayerRunningArmPoseController))]
    public sealed class PlayerRunningArmPoseControllerEditor :
        UnityEditor.Editor
    {
        private readonly List<PlayerRunningArmPosePreset> _presets =
            new List<PlayerRunningArmPosePreset>();

        private string[] _presetNames =
            System.Array.Empty<string>();

        private SerializedProperty _activePose;

        private void OnEnable()
        {
            _activePose =
                serializedObject.FindProperty(
                    "activePose");

            RefreshPresetList();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "This layer keeps the movement from your run animation and adds local arm rotation offsets on top. " +
                "Use several preset assets to quickly compare different arm poses in Play Mode. " +
                "Enable Debug Always Show Pose to hold the selected pose on the character while standing still.",
                MessageType.Info);

            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "activePose");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Arm Pose Preset",
                EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                _activePose);

            DrawPresetPopup();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(
                    "New Pose Preset"))
            {
                CreatePreset();
            }

            using (new EditorGUI.DisabledScope(
                       _activePose.objectReferenceValue == null))
            {
                if (GUILayout.Button(
                        "Duplicate Pose"))
                {
                    DuplicatePreset();
                }
            }

            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();

            PlayerRunningArmPosePreset preset =
                _activePose.objectReferenceValue
                    as PlayerRunningArmPosePreset;

            if (preset != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    "Live Pose Offsets",
                    EditorStyles.boldLabel);

                SerializedObject poseObject =
                    new SerializedObject(
                        preset);

                poseObject.Update();
                DrawPropertiesExcluding(
                    poseObject,
                    "m_Script");

                if (poseObject.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(
                        preset);
                }

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(
                        "Select Pose Asset"))
                {
                    Selection.activeObject =
                        preset;
                }

                if (GUILayout.Button(
                        "Reset Pose Offsets"))
                {
                    Undo.RecordObject(
                        preset,
                        "Reset Running Arm Pose");

                    preset.ResetPose();

                    EditorUtility.SetDirty(
                        preset);

                    AssetDatabase.SaveAssets();
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Workflow: enter Play Mode, keep the character running, tweak the six Euler offset fields above, " +
                "then duplicate the preset when you find a version you want to keep. Switching the preset dropdown loads another saved pose instantly.",
                MessageType.None);
        }

        private void DrawPresetPopup()
        {
            RefreshPresetList();

            int currentIndex = -1;

            PlayerRunningArmPosePreset current =
                _activePose.objectReferenceValue
                    as PlayerRunningArmPosePreset;

            for (int i = 0;
                 i < _presets.Count;
                 i++)
            {
                if (_presets[i] == current)
                {
                    currentIndex = i;
                    break;
                }
            }

            int displayIndex =
                Mathf.Max(
                    0,
                    currentIndex + 1);

            string[] options =
                new string[
                    _presetNames.Length + 1];

            options[0] =
                "<None>";

            for (int i = 0;
                 i < _presetNames.Length;
                 i++)
            {
                options[i + 1] =
                    _presetNames[i];
            }

            int selected =
                EditorGUILayout.Popup(
                    "Load Saved Pose",
                    displayIndex,
                    options);

            if (selected == displayIndex)
                return;

            _activePose.objectReferenceValue =
                selected <= 0
                    ? null
                    : _presets[
                        selected - 1];
        }

        private void RefreshPresetList()
        {
            _presets.Clear();

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:PlayerRunningArmPosePreset");

            for (int i = 0;
                 i < guids.Length;
                 i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                PlayerRunningArmPosePreset preset =
                    AssetDatabase.LoadAssetAtPath<PlayerRunningArmPosePreset>(
                        path);

                if (preset != null)
                    _presets.Add(preset);
            }

            _presets.Sort(
                (a, b) =>
                    string.CompareOrdinal(
                        a.name,
                        b.name));

            _presetNames =
                new string[
                    _presets.Count];

            for (int i = 0;
                 i < _presets.Count;
                 i++)
            {
                _presetNames[i] =
                    _presets[i].name;
            }
        }

        private void CreatePreset()
        {
            string path =
                EditorUtility.SaveFilePanelInProject(
                    "Create Running Arm Pose",
                    "RunningArmPose",
                    "asset",
                    "Choose where to save the new arm pose preset.");

            if (string.IsNullOrWhiteSpace(
                    path))
            {
                return;
            }

            PlayerRunningArmPosePreset preset =
                ScriptableObject.CreateInstance<PlayerRunningArmPosePreset>();

            AssetDatabase.CreateAsset(
                preset,
                path);

            AssetDatabase.SaveAssets();

            serializedObject.Update();

            _activePose.objectReferenceValue =
                preset;

            serializedObject.ApplyModifiedProperties();

            RefreshPresetList();

            Selection.activeObject =
                target;
        }

        private void DuplicatePreset()
        {
            PlayerRunningArmPosePreset source =
                _activePose.objectReferenceValue
                    as PlayerRunningArmPosePreset;

            if (source == null)
                return;

            string sourcePath =
                AssetDatabase.GetAssetPath(
                    source);

            string directory =
                System.IO.Path.GetDirectoryName(
                    sourcePath)
                ?.Replace(
                    "\\",
                    "/");

            string path =
                AssetDatabase.GenerateUniqueAssetPath(
                    $"{directory}/{source.name}_Variant.asset");

            PlayerRunningArmPosePreset copy =
                Instantiate(
                    source);

            copy.name =
                System.IO.Path.GetFileNameWithoutExtension(
                    path);

            AssetDatabase.CreateAsset(
                copy,
                path);

            AssetDatabase.SaveAssets();

            serializedObject.Update();

            _activePose.objectReferenceValue =
                copy;

            serializedObject.ApplyModifiedProperties();

            RefreshPresetList();
        }
    }
}
