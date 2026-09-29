using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class WaterSwimBatch16Setup
    {
        private const string RootFolder =
            "Assets/AirflowPrototype";

        private const string GeneratedFolder =
            RootFolder + "/Generated";

        private const string SettingsPath =
            GeneratedFolder +
            "/Batch16_WaterSwimSettings.asset";

        [MenuItem(
            "Tools/Airflow Prototype/Batch 16/Install Water Swimming")]
        public static void Install()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            PlayerMotor player =
                Object.FindFirstObjectByType<PlayerMotor>();

            if (player == null)
            {
                EditorUtility.DisplayDialog(
                    "Batch 16 Water Swimming",
                    "No PlayerMotor was found in the current scene.",
                    "OK");

                return;
            }

            CharacterController controller =
                player.GetComponent<CharacterController>();

            if (controller == null)
            {
                EditorUtility.DisplayDialog(
                    "Batch 16 Water Swimming",
                    "The PlayerMotor object has no CharacterController.",
                    "OK");

                return;
            }

            WaterSwimSettings settings =
                AssetDatabase.LoadAssetAtPath<WaterSwimSettings>(
                    SettingsPath);

            if (settings == null)
            {
                settings =
                    ScriptableObject.CreateInstance<WaterSwimSettings>();

                AssetDatabase.CreateAsset(
                    settings,
                    SettingsPath);
            }

            PlayerWaterSwim swim =
                player.GetComponent<PlayerWaterSwim>();

            if (swim == null)
            {
                swim =
                    Undo.AddComponent<PlayerWaterSwim>(
                        player.gameObject);
            }

            swim.Configure(
                player,
                settings);

            EditorUtility.SetDirty(swim);
            EditorUtility.SetDirty(settings);

            EditorSceneManager.MarkSceneDirty(
                player.gameObject.scene);

            AssetDatabase.SaveAssets();

            Selection.activeObject =
                settings;

            Debug.Log(
                "Batch 16 water swimming installed. Rebuild the Batch 15 v15.4 terrain/water once so generated water meshes receive AirWaterSurface components.");
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 16/Select Water Swim Settings")]
        public static void SelectSettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            WaterSwimSettings settings =
                AssetDatabase.LoadAssetAtPath<WaterSwimSettings>(
                    SettingsPath);

            if (settings == null)
            {
                settings =
                    ScriptableObject.CreateInstance<WaterSwimSettings>();

                AssetDatabase.CreateAsset(
                    settings,
                    SettingsPath);

                AssetDatabase.SaveAssets();
            }

            Selection.activeObject =
                settings;
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
