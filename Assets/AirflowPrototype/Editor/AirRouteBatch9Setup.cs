using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class AirRouteBatch9Setup
    {
        private const string TargetingSettingsPath =
            "Assets/AirflowPrototype/Generated/Batch3_TargetingSettings.asset";

        [MenuItem("Tools/Airflow Prototype/Batch 9/Apply Aerial Targeting Defaults")]
        public static void ApplyAerialDefaults()
        {
            AirTargetingSettings settings =
                AssetDatabase.LoadAssetAtPath<AirTargetingSettings>(
                    TargetingSettingsPath);

            if (settings == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 9",
                    "Could not find the existing Batch 3 targeting settings asset.",
                    "OK");
                return;
            }

            Undo.RecordObject(
                settings,
                "Apply Batch 9 Aerial Targeting Defaults");

            settings.aerialContinuationWeight = 0.24f;
            settings.aerialLeadDistanceWeight = 0.14f;
            settings.preferredAerialLeadDistance = 14f;
            settings.fallingHeightRecoveryWeight = 0.18f;
            settings.aerialHeightRange = 10f;
            settings.fallSpeedForMaxHeightBias = 5f;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Selection.activeObject = settings;

            Debug.Log(
                "Batch 9 aerial targeting defaults applied.");
        }

        [MenuItem("Tools/Airflow Prototype/Batch 9/Create Aerial Route Demo")]
        public static void CreateAerialRouteDemo()
        {
            PlayerMotor motor =
                Object.FindFirstObjectByType<PlayerMotor>();

            if (motor == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 9",
                    "No PlayerMotor was found in the current scene.",
                    "OK");
                return;
            }

            GameObject root =
                new GameObject("Batch 9 Aerial Route Demo");

            Undo.RegisterCreatedObjectUndo(
                root,
                "Create Batch 9 Aerial Route Demo");

            Vector3 start =
                motor.transform.position;

            Vector3 forward =
                motor.transform.forward;

            forward.y = 0f;

            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;

            forward.Normalize();

            Vector3 right =
                Vector3.Cross(
                    Vector3.up,
                    forward).normalized;

            // A deliberately readable S-curve with a climb, crest and recovery dip.
            Vector3[] localPoints =
            {
                new Vector3(0f, 2.6f, 10f),
                new Vector3(2.8f, 4.0f, 17f),
                new Vector3(4.3f, 5.5f, 24f),
                new Vector3(2.2f, 7.0f, 31f),
                new Vector3(-1.8f, 7.8f, 38f),
                new Vector3(-4.2f, 6.8f, 45f),
                new Vector3(-3.0f, 5.2f, 52f),
                new Vector3(0.6f, 4.2f, 59f),
                new Vector3(4.0f, 5.4f, 66f),
                new Vector3(5.0f, 7.2f, 73f),
                new Vector3(2.4f, 8.6f, 80f),
                new Vector3(-1.0f, 7.0f, 87f)
            };

            for (int i = 0; i < localPoints.Length; i++)
            {
                Vector3 p =
                    start +
                    right * localPoints[i].x +
                    Vector3.up * localPoints[i].y +
                    forward * localPoints[i].z;

                GameObject sphere =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Sphere);

                Undo.RegisterCreatedObjectUndo(
                    sphere,
                    "Create Route Node");

                sphere.name =
                    $"Route Node {i + 1:00}";

                sphere.transform.SetParent(
                    root.transform,
                    true);

                sphere.transform.position = p;
                sphere.transform.localScale =
                    Vector3.one * 1.0f;

                Collider collider =
                    sphere.GetComponent<Collider>();

                if (collider != null)
                    Undo.DestroyObjectImmediate(collider);

                sphere.layer =
                    LayerMask.NameToLayer("Ignore Raycast");

                AirNode node =
                    Undo.AddComponent<AirNode>(sphere);

                if (sphere.GetComponent<AirNodePulseFeedback>() == null)
                {
                    Undo.AddComponent<AirNodePulseFeedback>(
                        sphere);
                }

                // Crest nodes are slightly more valuable because they require
                // committing to the aerial line.
                float multiplier =
                    (i == 4 || i == 9)
                        ? 1.35f
                        : 1f;

                node.SetRewardMultiplier(multiplier);
            }

            EditorSceneManager.MarkSceneDirty(
                motor.gameObject.scene);

            Selection.activeGameObject =
                root;

            Debug.Log(
                "Created Batch 9 Aerial Route Demo. If the old random Batch 3 node field competes with it, temporarily disable that old node parent while testing this route.");
        }
    }
}
