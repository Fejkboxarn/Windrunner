using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    [CustomEditor(typeof(AirCastSettings))]
    public sealed class AirCastSettingsEditor : UnityEditor.Editor
    {
        private bool _showProjectile;
        private bool _showLegacyVisuals;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            AirCastSettings settings =
                (AirCastSettings)target;

            EditorGUILayout.LabelField(
                "Cast Timing",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "These three values drive the reticle, ready state, perfect timing and full lock. " +
                "You should normally only tune these for timing feel.",
                MessageType.Info);

            DrawProperty("perfectTime");
            DrawProperty("fullTimingTime");
            DrawProperty("perfectLeeway");

            EditorGUILayout.Space(4f);

            float start =
                Mathf.Max(
                    settings.minimumFireTime,
                    settings.perfectTime -
                    settings.perfectLeeway);

            float end =
                settings.perfectTime +
                settings.perfectLeeway;

            EditorGUILayout.HelpBox(
                $"Perfect: {settings.perfectTime:0.00}s    " +
                $"Window: {start:0.00}s - {end:0.00}s    " +
                $"Full: {settings.fullTimingTime:0.00}s",
                MessageType.None);

            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField(
                "Release & Aim",
                EditorStyles.boldLabel);

            DrawProperty("minimumFireTime");
            DrawProperty("minimumAimAssist");
            DrawProperty("readyAimAssist");

            EditorGUILayout.Space(8f);

            _showProjectile =
                EditorGUILayout.Foldout(
                    _showProjectile,
                    "Projectile / Trail",
                    true);

            if (_showProjectile)
            {
                EditorGUI.indentLevel++;

                DrawProperty("pulseSpeed");
                DrawProperty("pulseLifetime");
                DrawProperty("nodeHitRadius");
                DrawProperty("trailLifetime");
                DrawProperty("trailStartWidth");
                DrawProperty("trailEndWidth");

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4f);

            _showLegacyVisuals =
                EditorGUILayout.Foldout(
                    _showLegacyVisuals,
                    "Legacy Charge Visual Compatibility",
                    true);

            if (_showLegacyVisuals)
            {
                EditorGUILayout.HelpBox(
                    "These only exist for older scripts/components. " +
                    "They do not control the new integrated target/timing UI.",
                    MessageType.None);

                EditorGUI.indentLevel++;

                DrawProperty("unchargedRingRadius");
                DrawProperty("chargedRingRadius");
                DrawProperty("ringRotationSpeed");
                DrawProperty("ringWidth");

                EditorGUI.indentLevel--;
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                settings.SynchronizeCompatibilityFields();

                EditorUtility.SetDirty(
                    settings);
            }
        }

        private void DrawProperty(
            string name)
        {
            SerializedProperty property =
                serializedObject.FindProperty(
                    name);

            if (property != null)
            {
                EditorGUILayout.PropertyField(
                    property);
            }
        }
    }
}
