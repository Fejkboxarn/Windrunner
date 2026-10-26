using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    [CustomEditor(typeof(PlayerProceduralKnifeFlick))]
    public sealed class PlayerProceduralKnifeFlickEditor :
        UnityEditor.Editor
    {
        private readonly List<PlayerKnifeFlickPreset> _presets =
            new List<PlayerKnifeFlickPreset>();

        private string[] _presetNames =
            System.Array.Empty<string>();

        private SerializedProperty _presetProperty;

        private void OnEnable()
        {
            _presetProperty =
                serializedObject.FindProperty(
                    "preset");

            RefreshPresetList();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Fast procedural upper-body knife flick. Locomotion keeps playing underneath. " +
                "The visible knife releases early; most of the large arm motion is follow-through.",
                MessageType.Info);

            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "preset");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Flick Preset",
                EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                _presetProperty);

            DrawPresetPopup();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(
                    "New Flick Preset"))
            {
                CreatePreset();
            }

            using (new EditorGUI.DisabledScope(
                       _presetProperty.objectReferenceValue == null))
            {
                if (GUILayout.Button(
                        "Duplicate Preset"))
                {
                    DuplicatePreset();
                }
            }

            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();

            PlayerKnifeFlickPreset preset =
                _presetProperty.objectReferenceValue
                    as PlayerKnifeFlickPreset;

            if (preset != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    "Live Preset Tuning",
                    EditorStyles.boldLabel);

                SerializedObject presetObject =
                    new SerializedObject(
                        preset);

                presetObject.Update();

                DrawPropertiesExcluding(
                    presetObject,
                    "m_Script");

                if (presetObject.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(
                        preset);
                }

                EditorGUILayout.HelpBox(
                    "Best tuning workflow: enter Play Mode, enable Debug Preview Pose on the component, " +
                    "then inspect Ready, Prep, Strike and Follow Through one at a time while editing the preset above. " +
                    "The same right-hand reference values are mirrored automatically for left-hand throws.",
                    MessageType.None);
            }
        }

        private void DrawPresetPopup()
        {
            RefreshPresetList();

            PlayerKnifeFlickPreset current =
                _presetProperty.objectReferenceValue
                    as PlayerKnifeFlickPreset;

            int currentIndex = -1;

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

            string[] options =
                new string[
                    _presetNames.Length + 1];

            options[0] = "<None>";

            for (int i = 0;
                 i < _presetNames.Length;
                 i++)
            {
                options[i + 1] =
                    _presetNames[i];
            }

            int displayIndex =
                Mathf.Max(
                    0,
                    currentIndex + 1);

            int selected =
                EditorGUILayout.Popup(
                    "Load Saved Preset",
                    displayIndex,
                    options);

            if (selected == displayIndex)
                return;

            _presetProperty.objectReferenceValue =
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
                    "t:PlayerKnifeFlickPreset");

            for (int i = 0;
                 i < guids.Length;
                 i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                PlayerKnifeFlickPreset preset =
                    AssetDatabase.LoadAssetAtPath<PlayerKnifeFlickPreset>(
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
                    "Create Knife Flick Preset",
                    "KnifeFlickPreset",
                    "asset",
                    "Choose where to save the knife flick preset.");

            if (string.IsNullOrWhiteSpace(
                    path))
            {
                return;
            }

            PlayerKnifeFlickPreset preset =
                ScriptableObject.CreateInstance<PlayerKnifeFlickPreset>();

            AssetDatabase.CreateAsset(
                preset,
                path);

            AssetDatabase.SaveAssets();

            serializedObject.Update();

            _presetProperty.objectReferenceValue =
                preset;

            serializedObject.ApplyModifiedProperties();

            RefreshPresetList();
        }

        private void DuplicatePreset()
        {
            PlayerKnifeFlickPreset source =
                _presetProperty.objectReferenceValue
                    as PlayerKnifeFlickPreset;

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

            PlayerKnifeFlickPreset copy =
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

            _presetProperty.objectReferenceValue =
                copy;

            serializedObject.ApplyModifiedProperties();

            RefreshPresetList();
        }
    }
}
