using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class AirChimeBatch13Setup
    {
        private const string RootFolder =
            "Assets/AirflowPrototype";

        private const string GeneratedFolder =
            RootFolder + "/Generated";

        private const string SettingsPath =
            GeneratedFolder +
            "/Batch13_AirChimeTrialSettings.asset";

        private const string DemoRootName =
            "Air Chime Trial Demo";

        [MenuItem("Tools/Airflow Prototype/Batch 13/Create Air Chime Trial Demo")]
        public static void CreateDemo()
        {
            PlayerMotor player =
                Object.FindFirstObjectByType<PlayerMotor>();

            if (player == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 13",
                    "No PlayerMotor was found in the current scene.",
                    "OK");
                return;
            }

            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            AirChimeTrialSettings settings =
                GetOrCreateSettings();

            GameObject oldDemo =
                GameObject.Find(DemoRootName);

            if (oldDemo != null)
            {
                bool replace =
                    EditorUtility.DisplayDialog(
                        "Airflow Batch 13",
                        "An Air Chime Trial Demo already exists. Replace it?",
                        "Replace",
                        "Cancel");

                if (!replace)
                    return;

                Undo.DestroyObjectImmediate(
                    oldDemo);
            }

            GameObject root =
                new GameObject(
                    DemoRootName);

            Undo.RegisterCreatedObjectUndo(
                root,
                "Create Air Chime Trial Demo");

            AirChimeTrial trial =
                root.AddComponent<AirChimeTrial>();

            Vector3 forward =
                GetPlanarForward(
                    player.transform);

            Vector3 right =
                Vector3.Cross(
                    Vector3.up,
                    forward).normalized;

            Vector3 playerPosition =
                player.transform.position;

            GameObject sealObject =
                new GameObject(
                    "Start Seal");

            Undo.RegisterCreatedObjectUndo(
                sealObject,
                "Create Air Chime Start Seal");

            sealObject.transform.SetParent(
                root.transform,
                true);

            sealObject.transform.position =
                playerPosition +
                forward * 3.5f +
                Vector3.up * 0.02f;

            BoxCollider sealCollider =
                sealObject.AddComponent<BoxCollider>();

            sealCollider.isTrigger = true;

            AirChimeStartSeal seal =
                sealObject.AddComponent<AirChimeStartSeal>();

            List<AirChime> chimes =
                new List<AirChime>();

            Vector3[] offsets =
            {
                forward * 8.0f  + Vector3.up * 2.6f,
                forward * 13.0f + right * 2.4f + Vector3.up * 4.2f,
                forward * 18.0f - right * 2.0f + Vector3.up * 5.7f,
                forward * 23.5f + right * 2.8f + Vector3.up * 4.6f,
                forward * 29.0f + Vector3.up * 3.1f
            };

            for (int i = 0;
                 i < offsets.Length;
                 i++)
            {
                GameObject chimeObject =
                    new GameObject(
                        $"Air Chime {i + 1:00}");

                Undo.RegisterCreatedObjectUndo(
                    chimeObject,
                    "Create Air Chime");

                chimeObject.transform.SetParent(
                    root.transform,
                    true);

                chimeObject.transform.position =
                    playerPosition +
                    offsets[i];

                Vector3 routeDirection;

                if (i < offsets.Length - 1)
                {
                    routeDirection =
                        offsets[i + 1] -
                        offsets[i];
                }
                else
                {
                    routeDirection =
                        forward;
                }

                if (routeDirection.sqrMagnitude <
                    0.001f)
                {
                    routeDirection =
                        forward;
                }

                chimeObject.transform.rotation =
                    Quaternion.LookRotation(
                        routeDirection.normalized,
                        Vector3.up);

                SphereCollider trigger =
                    chimeObject.AddComponent<SphereCollider>();

                trigger.isTrigger = true;

                AirChime chime =
                    chimeObject.AddComponent<AirChime>();

                chimes.Add(
                    chime);
            }

            trial.Configure(
                player,
                settings,
                chimes);

            seal.Configure(
                trial,
                settings);

            EditorUtility.SetDirty(
                trial);

            EditorUtility.SetDirty(
                seal);

            foreach (AirChime chime in chimes)
            {
                EditorUtility.SetDirty(
                    chime);
            }

            EditorSceneManager.MarkSceneDirty(
                root.scene);

            AssetDatabase.SaveAssets();

            Selection.activeGameObject =
                root;

            Debug.Log(
                "Batch 13 Air Chime trial created. Enter the Start Seal, then pass through the glowing chimes in order. " +
                "By default, touching ground after the first chime resets the trial.");
        }

        [MenuItem("Tools/Airflow Prototype/Batch 13/Select Air Chime Settings")]
        public static void SelectSettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            Selection.activeObject =
                GetOrCreateSettings();
        }

        private static Vector3 GetPlanarForward(
            Transform player)
        {
            Camera camera =
                Camera.main;

            Vector3 forward =
                camera != null
                    ? camera.transform.forward
                    : player.forward;

            forward.y = 0f;

            if (forward.sqrMagnitude <
                0.001f)
            {
                forward =
                    Vector3.forward;
            }

            return forward.normalized;
        }

        private static AirChimeTrialSettings GetOrCreateSettings()
        {
            AirChimeTrialSettings settings =
                AssetDatabase.LoadAssetAtPath<AirChimeTrialSettings>(
                    SettingsPath);

            if (settings != null)
                return settings;

            settings =
                ScriptableObject.CreateInstance<AirChimeTrialSettings>();

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
