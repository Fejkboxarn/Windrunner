using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class AirPresentationBatch10Setup
    {
        private const string RootFolder =
            "Assets/AirflowPrototype";

        private const string GeneratedFolder =
            RootFolder + "/Generated";

        private const string SettingsPath =
            GeneratedFolder +
            "/Batch10_AirPresentationSettings.asset";

        [MenuItem("Tools/Airflow Prototype/Batch 10/Install Air Presentation")]
        public static void Install()
        {
            PlayerMotor motor =
                Object.FindFirstObjectByType<PlayerMotor>();

            AirCaster caster =
                Object.FindFirstObjectByType<AirCaster>();

            AirFlowHitSystem hitSystem =
                Object.FindFirstObjectByType<AirFlowHitSystem>();

            if (motor == null ||
                caster == null ||
                hitSystem == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 10",
                    "Batch 10 needs the existing PlayerMotor, AirCaster and AirFlowHitSystem.",
                    "OK");

                return;
            }

            Camera camera = Camera.main;

            if (camera == null)
            {
                camera =
                    Object.FindFirstObjectByType<Camera>();
            }

            if (camera == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 10",
                    "No camera was found.",
                    "OK");

                return;
            }

            Transform castOrigin =
                motor.transform.Find(
                    "Air Cast Origin");

            if (castOrigin == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 10",
                    "Could not find the existing 'Air Cast Origin' under the player.",
                    "OK");

                return;
            }

            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            AirPresentationSettings settings =
                GetOrCreateSettings();

            // Re-bind the actual gameplay caster as well as presentation.
            // This makes the origin explicit instead of relying on an old
            // serialized reference.
            Undo.RecordObject(
                caster,
                "Rebind Air Cast Origin");

            caster.SetCastOrigin(
                castOrigin);

            EditorUtility.SetDirty(
                caster);

            AirPresentationController controller =
                motor.GetComponent<AirPresentationController>();

            if (controller == null)
            {
                controller =
                    Undo.AddComponent<AirPresentationController>(
                        motor.gameObject);
            }

            controller.Configure(
                caster,
                hitSystem,
                castOrigin,
                camera,
                settings);

            AirHitFeedback hitFeedback =
                motor.GetComponent<AirHitFeedback>();

            if (hitFeedback != null)
            {
                hitFeedback.ConfigurePresentation(
                    settings);

                EditorUtility.SetDirty(
                    hitFeedback);
            }

            AirChargeVisual oldCharge =
                motor.GetComponent<AirChargeVisual>();

            if (oldCharge != null &&
                oldCharge.enabled)
            {
                Undo.RecordObject(
                    oldCharge,
                    "Disable old charge visual");

                oldCharge.enabled = false;

                EditorUtility.SetDirty(
                    oldCharge);
            }

            EditorUtility.SetDirty(
                controller);

            EditorSceneManager.MarkSceneDirty(
                motor.gameObject.scene);

            AssetDatabase.SaveAssets();

            Selection.activeObject =
                settings;

            Debug.Log(
                "Airflow Batch 10 presentation installed/repaired. " +
                "The gameplay projectile and world-space charge visuals now both use 'Air Cast Origin'.");
        }

        [MenuItem("Tools/Airflow Prototype/Batch 10/Select Presentation Settings")]
        public static void SelectSettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            Selection.activeObject =
                GetOrCreateSettings();
        }

        private static AirPresentationSettings GetOrCreateSettings()
        {
            AirPresentationSettings settings =
                AssetDatabase.LoadAssetAtPath<AirPresentationSettings>(
                    SettingsPath);

            if (settings != null)
                return settings;

            settings =
                ScriptableObject.CreateInstance<AirPresentationSettings>();

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
