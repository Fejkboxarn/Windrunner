using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class AirflowBatch2Setup
    {
        private const string SettingsPath =
            "Assets/AirflowPrototype/Generated/Batch1_MovementSettings.asset";

        [MenuItem("Tools/Airflow Prototype/Batch 2/Upgrade Current Playground")]
        public static void UpgradeCurrentPlayground()
        {
            PlayerMotor motor = Object.FindFirstObjectByType<PlayerMotor>();
            if (motor == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 2",
                    "No PlayerMotor was found in the open scene.\n\n" +
                    "Open your Batch 1 playground first, or use Create Fresh Batch 2 Playground.",
                    "OK");
                return;
            }

            AirflowMovementSettings settings = motor.Settings;
            if (settings == null)
                settings = AssetDatabase.LoadAssetAtPath<AirflowMovementSettings>(SettingsPath);

            if (settings == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 2",
                    "Could not find the movement settings asset.",
                    "OK");
                return;
            }

            GameObject player = motor.gameObject;
            PlayerInputReader input = player.GetComponent<PlayerInputReader>();

            AirPower airPower = player.GetComponent<AirPower>();
            if (airPower == null)
                airPower = Undo.AddComponent<AirPower>(player);
            airPower.Configure(settings, true);

            AirDash airDash = player.GetComponent<AirDash>();
            if (airDash == null)
                airDash = Undo.AddComponent<AirDash>(player);
            airDash.Configure(motor, input, airPower, settings);

            PlayerDebugHUD hud = player.GetComponent<PlayerDebugHUD>();
            if (hud == null)
                hud = Undo.AddComponent<PlayerDebugHUD>(player);
            hud.Configure(motor, input, airPower, airDash);

            Transform visual = player.transform.Find("Visual");
            if (visual != null)
            {
                Transform presentation = visual.Find("Presentation");
                if (presentation == null)
                {
                    GameObject presentationObject = new GameObject("Presentation");
                    Undo.RegisterCreatedObjectUndo(presentationObject, "Create Airflow Presentation Root");
                    presentation = presentationObject.transform;
                    presentation.SetParent(visual, false);

                    // Move existing visible children under Presentation.
                    for (int i = visual.childCount - 1; i >= 0; i--)
                    {
                        Transform child = visual.GetChild(i);
                        if (child != presentation)
                            child.SetParent(presentation, true);
                    }

                    // The primitive renderer is on Visual itself in Batch 1, so the
                    // presentation root can only affect its child marker. We therefore
                    // move the lean component to Visual for existing scenes.
                    presentation = visual;
                }

                PlayerMovementVisuals visuals = player.GetComponent<PlayerMovementVisuals>();
                if (visuals == null)
                    visuals = Undo.AddComponent<PlayerMovementVisuals>(player);

                visuals.Configure(motor, presentation, settings);
            }

            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(settings);
            EditorSceneManager.MarkSceneDirty(player.scene);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "Airflow Batch 2 upgrade complete. Hold Left Shift / Right Trigger while moving. " +
                "Air Power currently regenerates passively for testing; Air Nodes will replace that loop later.");
        }

        [MenuItem("Tools/Airflow Prototype/Batch 2/Create Fresh Batch 2 Playground")]
        public static void CreateFreshBatch2Playground()
        {
            AirflowBatch1Setup.CreatePlaygroundScene();
            UpgradeCurrentPlayground();
        }

        [MenuItem("Tools/Airflow Prototype/Batch 2/Select Movement Settings")]
        public static void SelectMovementSettings()
        {
            AirflowMovementSettings settings =
                AssetDatabase.LoadAssetAtPath<AirflowMovementSettings>(SettingsPath);

            if (settings != null)
                Selection.activeObject = settings;
            else
                Debug.LogWarning("Airflow movement settings asset was not found.");
        }

        [MenuItem("Tools/Airflow Prototype/Batch 2/Refill Air Power (Play Mode)")]
        public static void RefillAirPower()
        {
            AirPower power = Object.FindFirstObjectByType<AirPower>();
            if (power != null)
                power.Refill();
        }
    }
}
