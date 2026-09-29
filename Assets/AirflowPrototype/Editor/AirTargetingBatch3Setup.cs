using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class AirTargetingBatch3Setup
    {
        private const string RootFolder = "Assets/AirflowPrototype";
        private const string GeneratedFolder = RootFolder + "/Generated";
        private const string SettingsPath =
            GeneratedFolder + "/Batch3_TargetingSettings.asset";
        private const string NodeMaterialPath =
            GeneratedFolder + "/Batch3_AirNode.mat";

        [MenuItem("Tools/Airflow Prototype/Batch 3/Install Targeting + Create Test Nodes")]
        public static void Install()
        {
            PlayerMotor motor = Object.FindFirstObjectByType<PlayerMotor>();
            if (motor == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 3",
                    "No PlayerMotor was found. Open your current Airflow playground first.",
                    "OK");
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
                camera = Object.FindFirstObjectByType<Camera>();

            if (camera == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 3",
                    "No camera was found in the current scene.",
                    "OK");
                return;
            }

            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            AirTargetingSettings settings = GetOrCreateSettings();
            GameObject player = motor.gameObject;

            AirTargeter targeter = player.GetComponent<AirTargeter>();
            if (targeter == null)
                targeter = Undo.AddComponent<AirTargeter>(player);

            targeter.Configure(camera, motor, settings);

            AirTargetIndicator indicator = player.GetComponent<AirTargetIndicator>();
            if (indicator == null)
                indicator = Undo.AddComponent<AirTargetIndicator>(player);

            indicator.Configure(targeter, camera);

            AirTargetDebugHUD hud = player.GetComponent<AirTargetDebugHUD>();
            if (hud == null)
                hud = Undo.AddComponent<AirTargetDebugHUD>(player);

            hud.Configure(targeter);

            CreateTestNodes();

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(player.scene);
            AssetDatabase.SaveAssets();

            Selection.activeObject = settings;

            Debug.Log(
                "Airflow Batch 3 installed. Move and rotate the camera through the node field. " +
                "The reticle should favor the node that best continues your current path and should " +
                "not rapidly flicker between similar candidates.");
        }

        [MenuItem("Tools/Airflow Prototype/Batch 3/Create Test Nodes Only")]
        public static void CreateTestNodes()
        {
            GameObject existing = GameObject.Find("Air Nodes — Batch 3");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog(
                    "Airflow Batch 3",
                    "A Batch 3 node field already exists. Replace it?",
                    "Replace",
                    "Cancel"))
                {
                    return;
                }

                Undo.DestroyObjectImmediate(existing);
            }

            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            GameObject root = new GameObject("Air Nodes — Batch 3");
            Undo.RegisterCreatedObjectUndo(root, "Create Air Node Field");

            Material material = GetOrCreateNodeMaterial();

            Vector3[] positions =
            {
                new Vector3( 0f, 2.2f,  8f),
                new Vector3( 5f, 2.7f, 12f),
                new Vector3(-5f, 3.0f, 13f),
                new Vector3(10f, 3.5f, 16f),
                new Vector3(-10f, 2.4f, 17f),
                new Vector3( 2f, 4.5f, 20f),
                new Vector3(-3f, 5.2f, 23f),

                new Vector3(10f, 2.1f,  4f),
                new Vector3(15f, 3.2f,  8f),
                new Vector3(20f, 4.0f,  3f),
                new Vector3(22f, 2.5f, -5f),
                new Vector3(17f, 4.8f,-12f),

                new Vector3(-9f, 2.3f,  5f),
                new Vector3(-15f,3.8f,  9f),
                new Vector3(-21f,2.6f,  3f),
                new Vector3(-22f,4.5f, -6f),
                new Vector3(-16f,3.0f,-13f),

                new Vector3( 0f, 2.0f, -9f),
                new Vector3( 6f, 3.7f,-15f),
                new Vector3(-6f, 4.2f,-16f),
                new Vector3(11f, 5.0f,-21f),
                new Vector3(-10f,3.1f,-22f),

                // Close competing pair for hysteresis testing.
                new Vector3( 2.8f, 2.5f, 10.5f),
                new Vector3( 3.8f, 2.5f, 10.8f)
            };

            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Undo.RegisterCreatedObjectUndo(node, "Create Air Node");

                node.name = $"Air Node {i + 1:00}";
                node.transform.SetParent(root.transform, false);
                node.transform.position = positions[i];
                node.transform.localScale = Vector3.one * 0.7f;

                if (ignoreRaycast >= 0)
                    node.layer = ignoreRaycast;

                Collider collider = node.GetComponent<Collider>();
                if (collider != null)
                    Object.DestroyImmediate(collider);

                Renderer renderer = node.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = material;

                node.AddComponent<AirNode>();
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
        }

        [MenuItem("Tools/Airflow Prototype/Batch 3/Select Targeting Settings")]
        public static void SelectSettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);
            Selection.activeObject = GetOrCreateSettings();
        }

        private static AirTargetingSettings GetOrCreateSettings()
        {
            AirTargetingSettings settings =
                AssetDatabase.LoadAssetAtPath<AirTargetingSettings>(SettingsPath);

            if (settings != null)
                return settings;

            settings = ScriptableObject.CreateInstance<AirTargetingSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static Material GetOrCreateNodeMaterial()
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(NodeMaterialPath);

            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader);
            material.color = new Color(0.55f, 0.86f, 1f, 1f);
            AssetDatabase.CreateAsset(material, NodeMaterialPath);
            return material;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            string parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            string name = Path.GetFileName(assetPath);

            if (!string.IsNullOrEmpty(parent) &&
                !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
