using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class PlayerInputStartupRepairSetup
    {
        [MenuItem(
            "Tools/Airflow Prototype/Input/Install Startup Input Repair")]
        public static void Install()
        {
            PlayerInputReader reader =
                Object.FindAnyObjectByType<PlayerInputReader>();

            if (reader == null)
            {
                EditorUtility.DisplayDialog(
                    "Input Startup Repair",
                    "No PlayerInputReader was found in the current scene.",
                    "OK");

                return;
            }

            PlayerInputStartupRepair repair =
                reader.GetComponent<PlayerInputStartupRepair>();

            if (repair == null)
            {
                repair =
                    Undo.AddComponent<PlayerInputStartupRepair>(
                        reader.gameObject);
            }

            repair.Configure(
                reader);

            EditorUtility.SetDirty(
                repair);

            EditorSceneManager.MarkSceneDirty(
                reader.gameObject.scene);

            Selection.activeObject =
                repair;

            Debug.Log(
                "Player input startup repair installed. " +
                "It guarantees runtime-created InputActionMaps are enabled " +
                "on the first Play session after script recompilation.");
        }
    }
}
