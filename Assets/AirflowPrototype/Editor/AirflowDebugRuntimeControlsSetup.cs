using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class AirflowDebugRuntimeControlsSetup
    {
        private static readonly string[] AirflowInfoTypeNames =
        {
            "PlayerDebugHUD",
            "AirflowInfoHUD",
            "AirflowDebugHUD"
        };

        private static readonly string[] AirTargetingTypeNames =
        {
            "AirTargetingDebugHUD",
            "AirTargetDebugHUD",
            "AirTargetingHUD",
            "AirTargeterDebugHUD",
            "AirTargetingDebugWindow"
        };

        [MenuItem("Tools/Airflow Prototype/Debug/Install HUD Toggles + 166 FPS Cap")]
        public static void Install()
        {
            GameObject owner = ResolveOwner();

            if (owner == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Debug Controls",
                    "Could not find a PlayerInputReader or PlayerMotor in the current scene.",
                    "OK");
                return;
            }

            AirflowDebugRuntimeControls controls =
                owner.GetComponent<AirflowDebugRuntimeControls>();

            if (controls == null)
                controls = Undo.AddComponent<AirflowDebugRuntimeControls>(owner);

            MonoBehaviour airflowInfo =
                FindWindow(AirflowInfoTypeNames, true);

            MonoBehaviour airTargeting =
                FindWindow(AirTargetingTypeNames, false);

            controls.Configure(
                airflowInfo,
                airTargeting);

            controls.SetStartupVisibility(
                false,
                false);

            EditorUtility.SetDirty(controls);
            EditorSceneManager.MarkSceneDirty(owner.scene);
            Selection.activeObject = controls;

            Debug.Log(
                "Airflow debug controls installed. F1 = Airflow Info, " +
                "F2 = Air Targeting, both start OFF, FPS cap = 166.");
        }

        private static GameObject ResolveOwner()
        {
            PlayerInputReader reader =
                UnityEngine.Object.FindAnyObjectByType<PlayerInputReader>();

            if (reader != null)
                return reader.gameObject;

            PlayerMotor motor =
                UnityEngine.Object.FindAnyObjectByType<PlayerMotor>();

            return motor != null ? motor.gameObject : null;
        }

        private static MonoBehaviour FindWindow(
            string[] preferredTypeNames,
            bool allowDebugHudFallback)
        {
            MonoBehaviour[] behaviours =
                UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int n = 0; n < preferredTypeNames.Length; n++)
            {
                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour behaviour = behaviours[i];

                    if (behaviour == null)
                        continue;

                    if (string.Equals(
                        behaviour.GetType().Name,
                        preferredTypeNames[n],
                        StringComparison.Ordinal))
                    {
                        return behaviour;
                    }
                }
            }

            if (!allowDebugHudFallback)
                return null;

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];

                if (behaviour == null)
                    continue;

                string typeName = behaviour.GetType().Name;

                if (typeName.Contains(
                        "DebugHUD",
                        StringComparison.OrdinalIgnoreCase) &&
                    !typeName.Contains(
                        "Target",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return behaviour;
                }
            }

            return null;
        }
    }
}
