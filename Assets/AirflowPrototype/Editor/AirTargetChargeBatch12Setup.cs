using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class AirTargetChargeBatch12Setup
    {
        private const string RootFolder =
            "Assets/AirflowPrototype";

        private const string GeneratedFolder =
            RootFolder + "/Generated";

        private const string SettingsPath =
            GeneratedFolder +
            "/Batch12_TargetUISettings.asset";

        [MenuItem("Tools/Airflow Prototype/Batch 12/Install Integrated Target + Charge UI")]
        public static void Install()
        {
            AirCaster caster =
                Object.FindFirstObjectByType<AirCaster>();

            if (caster == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 12",
                    "No AirCaster was found in the current scene.",
                    "OK");
                return;
            }

            Camera camera =
                Camera.main;

            if (camera == null)
                camera =
                    Object.FindFirstObjectByType<Camera>();

            if (camera == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 12",
                    "No camera was found.",
                    "OK");
                return;
            }

            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            AirTargetUISettings settings =
                GetOrCreateSettings();

            GameObject player =
                caster.gameObject;

            RemoveOldGeneratedSceneUI(
                player.transform);

            AirTargetChargeUI ui =
                player.GetComponent<AirTargetChargeUI>();

            if (ui == null)
            {
                ui =
                    Undo.AddComponent<AirTargetChargeUI>(
                        player);
            }

            ui.Configure(
                caster,
                camera,
                settings);

            EditorUtility.SetDirty(ui);

            AirPerfectTimingFeedback perfectFeedback =
                player.GetComponent<AirPerfectTimingFeedback>();

            if (perfectFeedback == null)
            {
                perfectFeedback =
                    Undo.AddComponent<AirPerfectTimingFeedback>(
                        player);
            }

            perfectFeedback.Configure(
                caster,
                settings);

            EditorUtility.SetDirty(
                perfectFeedback);

            AirTargetIndicator oldTarget =
                player.GetComponent<AirTargetIndicator>();

            if (oldTarget != null &&
                oldTarget.enabled)
            {
                Undo.RecordObject(
                    oldTarget,
                    "Disable old target indicator");

                oldTarget.enabled = false;

                EditorUtility.SetDirty(
                    oldTarget);
            }

            AirCastFeedback oldCastFeedback =
                player.GetComponent<AirCastFeedback>();

            if (oldCastFeedback != null &&
                oldCastFeedback.enabled)
            {
                Undo.RecordObject(
                    oldCastFeedback,
                    "Disable old cast feedback");

                oldCastFeedback.enabled = false;

                EditorUtility.SetDirty(
                    oldCastFeedback);
            }

            EditorSceneManager.MarkSceneDirty(
                player.scene);

            AssetDatabase.SaveAssets();

            Selection.activeGameObject =
                player;

            Debug.Log(
                "Batch 12 UI repaired/installed. The target reticle now creates a root-level Screen Space Overlay canvas at runtime.");
        }

        [MenuItem("Tools/Airflow Prototype/Batch 12/Select Target UI Settings")]
        public static void SelectSettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            Selection.activeObject =
                GetOrCreateSettings();
        }

        private static void RemoveOldGeneratedSceneUI(
            Transform player)
        {
            Transform old =
                player.Find(
                    "Air Target Charge UI");

            if (old != null)
            {
                Undo.DestroyObjectImmediate(
                    old.gameObject);
            }
        }

        private static AirTargetUISettings GetOrCreateSettings()
        {
            AirTargetUISettings settings =
                AssetDatabase.LoadAssetAtPath<AirTargetUISettings>(
                    SettingsPath);

            if (settings != null)
                return settings;

            settings =
                ScriptableObject.CreateInstance<AirTargetUISettings>();

            AssetDatabase.CreateAsset(
                settings,
                SettingsPath);

            AssetDatabase.SaveAssets();

            return settings;
        }

        private static void EnsureFolder(
            string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            string parent =
                Path.GetDirectoryName(assetPath)?
                    .Replace("\\", "/");

            string name =
                Path.GetFileName(assetPath);

            if (!string.IsNullOrEmpty(parent) &&
                !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(
                parent,
                name);
        }
    }
}
