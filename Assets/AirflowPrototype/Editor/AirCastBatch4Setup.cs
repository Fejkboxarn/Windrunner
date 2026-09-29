using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class AirCastBatch4Setup
    {
        private const string RootFolder =
            "Assets/AirflowPrototype";
        private const string GeneratedFolder =
            RootFolder + "/Generated";
        private const string SettingsPath =
            GeneratedFolder + "/Batch4_AirCastSettings.asset";

        [MenuItem("Tools/Airflow Prototype/Batch 4/Install Air Casting")]
        public static void Install()
        {
            PlayerMotor motor =
                Object.FindFirstObjectByType<PlayerMotor>();

            AirTargeter targeter =
                Object.FindFirstObjectByType<AirTargeter>();

            if (motor == null || targeter == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 4",
                    "Batch 4 needs the existing PlayerMotor and AirTargeter. " +
                    "Open your current prototype scene first.",
                    "OK");
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
                camera = Object.FindFirstObjectByType<Camera>();

            if (camera == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 4",
                    "No camera was found in the current scene.",
                    "OK");
                return;
            }

            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            AirCastSettings settings = GetOrCreateSettings();

            GameObject player = motor.gameObject;
            PlayerInputReader input =
                player.GetComponent<PlayerInputReader>();

            if (input == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 4",
                    "The player has no PlayerInputReader.",
                    "OK");
                return;
            }

            Transform castOrigin =
                player.transform.Find("Air Cast Origin");

            if (castOrigin == null)
            {
                GameObject originObject =
                    new GameObject("Air Cast Origin");

                Undo.RegisterCreatedObjectUndo(
                    originObject,
                    "Create Air Cast Origin");

                castOrigin = originObject.transform;
                castOrigin.SetParent(player.transform, false);
                castOrigin.localPosition =
                    new Vector3(0f, 1.25f, 0.35f);
            }

            AirCaster caster =
                player.GetComponent<AirCaster>();

            if (caster == null)
                caster = Undo.AddComponent<AirCaster>(player);

            caster.Configure(
                input,
                targeter,
                camera,
                castOrigin,
                settings);

            AirCastFeedback feedback =
                player.GetComponent<AirCastFeedback>();

            if (feedback == null)
                feedback =
                    Undo.AddComponent<AirCastFeedback>(player);

            feedback.Configure(caster, camera);

            AirChargeVisual chargeVisual =
                player.GetComponent<AirChargeVisual>();

            if (chargeVisual == null)
                chargeVisual =
                    Undo.AddComponent<AirChargeVisual>(player);

            chargeVisual.Configure(
                caster,
                castOrigin,
                settings);

            AirNode[] nodes =
                Object.FindObjectsByType<AirNode>(
                    FindObjectsSortMode.None);

            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].GetComponent<AirNodePulseFeedback>() == null)
                {
                    Undo.AddComponent<AirNodePulseFeedback>(
                        nodes[i].gameObject);
                }
            }

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(player.scene);
            AssetDatabase.SaveAssets();

            Selection.activeObject = settings;

            Debug.Log(
                "Airflow Batch 4 installed. Hold Left Mouse / Left Trigger, " +
                "wait for the compressed-air rings and target bar to reach ready, " +
                "then release. Early releases receive only partial aim correction.");
        }

        [MenuItem("Tools/Airflow Prototype/Batch 4/Select Air Cast Settings")]
        public static void SelectSettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);
            Selection.activeObject = GetOrCreateSettings();
        }

        private static AirCastSettings GetOrCreateSettings()
        {
            AirCastSettings settings =
                AssetDatabase.LoadAssetAtPath<AirCastSettings>(
                    SettingsPath);

            if (settings != null)
                return settings;

            settings =
                ScriptableObject.CreateInstance<AirCastSettings>();

            AssetDatabase.CreateAsset(
                settings,
                SettingsPath);

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
