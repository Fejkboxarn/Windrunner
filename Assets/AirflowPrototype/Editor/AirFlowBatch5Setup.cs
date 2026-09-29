using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class AirFlowBatch5Setup
    {
        private const string RootFolder =
            "Assets/AirflowPrototype";
        private const string GeneratedFolder =
            RootFolder + "/Generated";
        private const string SettingsPath =
            GeneratedFolder + "/Batch5_FlowHitSettings.asset";

        [MenuItem("Tools/Airflow Prototype/Batch 5/Install Flow Hit Reward")]
        public static void Install()
        {
            PlayerMotor motor =
                Object.FindFirstObjectByType<PlayerMotor>();

            AirCaster caster =
                Object.FindFirstObjectByType<AirCaster>();

            AirPower airPower =
                Object.FindFirstObjectByType<AirPower>();

            if (motor == null || caster == null || airPower == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 5",
                    "Batch 5 needs the existing PlayerMotor, AirCaster and AirPower. " +
                    "Open your current prototype scene first.",
                    "OK");
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
                camera = Object.FindFirstObjectByType<Camera>();

            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            AirFlowHitSettings settings = GetOrCreateSettings();

            GameObject player = motor.gameObject;

            AirFlowHitSystem hitSystem =
                player.GetComponent<AirFlowHitSystem>();

            if (hitSystem == null)
                hitSystem =
                    Undo.AddComponent<AirFlowHitSystem>(player);

            hitSystem.Configure(
                caster,
                motor,
                airPower,
                camera,
                settings);

            PlayerSpeedFeedback speedFeedback =
                camera != null
                    ? camera.GetComponent<PlayerSpeedFeedback>()
                    : Object.FindFirstObjectByType<PlayerSpeedFeedback>();

            AirHitFeedback feedback =
                player.GetComponent<AirHitFeedback>();

            if (feedback == null)
                feedback =
                    Undo.AddComponent<AirHitFeedback>(player);

            feedback.Configure(
                hitSystem,
                speedFeedback,
                player.transform,
                settings);

            EditorUtility.SetDirty(player);

            if (camera != null)
                EditorUtility.SetDirty(camera.gameObject);

            EditorSceneManager.MarkSceneDirty(player.scene);
            AssetDatabase.SaveAssets();

            Selection.activeObject = settings;

            Debug.Log(
                "Airflow Batch 5 installed. Successful node hits now restore Air Power, " +
                "add momentum, give a small airborne lift, punch FOV/haptics, send an " +
                "energy streak back to the player, and briefly remove the struck node " +
                "from targeting so the chain advances.");
        }

        [MenuItem("Tools/Airflow Prototype/Batch 5/Select Flow Hit Settings")]
        public static void SelectSettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);
            Selection.activeObject = GetOrCreateSettings();
        }

        private static AirFlowHitSettings GetOrCreateSettings()
        {
            AirFlowHitSettings settings =
                AssetDatabase.LoadAssetAtPath<AirFlowHitSettings>(
                    SettingsPath);

            if (settings != null)
                return settings;

            settings =
                ScriptableObject.CreateInstance<AirFlowHitSettings>();

            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void EnsureFolder(string assetPath)
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

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
