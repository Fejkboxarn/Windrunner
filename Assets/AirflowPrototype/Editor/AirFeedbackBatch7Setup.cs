using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class AirFeedbackBatch7Setup
    {
        private const string RootFolder =
            "Assets/AirflowPrototype";
        private const string GeneratedFolder =
            RootFolder + "/Generated";
        private const string SettingsPath =
            GeneratedFolder + "/Batch7_AirFeelSettings.asset";

        [MenuItem("Tools/Airflow Prototype/Batch 7/Install Feedback Pass")]
        public static void Install()
        {
            PlayerMotor motor =
                Object.FindFirstObjectByType<PlayerMotor>();

            if (motor == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 7",
                    "No PlayerMotor was found in the current scene.",
                    "OK");
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
                camera =
                    Object.FindFirstObjectByType<Camera>();

            if (camera == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 7",
                    "No camera was found in the current scene.",
                    "OK");
                return;
            }

            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            AirFeelSettings feelSettings =
                GetOrCreateSettings();

            PlayerOrbitCamera orbit =
                camera.GetComponent<PlayerOrbitCamera>();

            if (orbit != null)
            {
                orbit.ConfigureFeedback(
                    motor,
                    feelSettings);

                EditorUtility.SetDirty(orbit);
            }

            PlayerMovementVisuals visuals =
                motor.GetComponent<PlayerMovementVisuals>();

            if (visuals != null)
            {
                visuals.ConfigureFeelSettings(
                    feelSettings);

                EditorUtility.SetDirty(visuals);
            }

            PlayerWindFeedback wind =
                camera.GetComponent<PlayerWindFeedback>();

            if (wind == null)
            {
                wind =
                    Undo.AddComponent<PlayerWindFeedback>(
                        camera.gameObject);
            }

            wind.Configure(
                motor,
                feelSettings);

            EditorUtility.SetDirty(wind);

            EditorSceneManager.MarkSceneDirty(
                motor.gameObject.scene);

            AssetDatabase.SaveAssets();

            Selection.activeObject =
                feelSettings;

            Debug.Log(
                "Airflow Batch 7 installed. High speed now adds camera look-ahead, " +
                "subtle pullback, turn banking, character turn lean and procedural wind streaks. " +
                "Reward delivery also adds a short camera recoil on top of the existing FOV punch.");
        }

        [MenuItem("Tools/Airflow Prototype/Batch 7/Select Feel Settings")]
        public static void SelectSettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            Selection.activeObject =
                GetOrCreateSettings();
        }

        private static AirFeelSettings GetOrCreateSettings()
        {
            AirFeelSettings settings =
                AssetDatabase.LoadAssetAtPath<AirFeelSettings>(
                    SettingsPath);

            if (settings != null)
                return settings;

            settings =
                ScriptableObject.CreateInstance<AirFeelSettings>();

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
