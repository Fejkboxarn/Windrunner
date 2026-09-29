using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class AirPowerProgressBarSetup
    {
        [MenuItem(
            "Tools/Airflow Prototype/UI/Install Air Power Bar On Selected")]
        public static void InstallOnSelected()
        {
            GameObject selected =
                Selection.activeGameObject;

            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "Air Power Bar",
                    "Select the GameObject that contains your ProceduralProgressBar component first.",
                    "OK");

                return;
            }

            MonoBehaviour progressBar =
                FindProceduralProgressBar(
                    selected);

            if (progressBar == null)
            {
                EditorUtility.DisplayDialog(
                    "Air Power Bar",
                    "No ProceduralProgressBar component was found on the selected object or its children.\n\n" +
                    "Create/choose your bar using the Procedural Progress Bars asset, then select it and run this menu again.",
                    "OK");

                return;
            }

            AirPower airPower =
                Object.FindAnyObjectByType<AirPower>();

            if (airPower == null)
            {
                EditorUtility.DisplayDialog(
                    "Air Power Bar",
                    "No AirPower component was found in the current scene.",
                    "OK");

                return;
            }

            GameObject owner =
                progressBar.gameObject;

            AirPowerProgressBarUI bridge =
                owner.GetComponent<AirPowerProgressBarUI>();

            if (bridge == null)
            {
                bridge =
                    Undo.AddComponent<AirPowerProgressBarUI>(
                        owner);
            }

            bridge.Configure(
                airPower,
                progressBar);

            EditorUtility.SetDirty(
                bridge);

            EditorSceneManager.MarkSceneDirty(
                owner.scene);

            Selection.activeObject =
                bridge;

            Debug.Log(
                "Air Power progress bar connected. The bridge initializes with UpdateBarFillAmount(), " +
                "then uses the plugin's BarFill()/BarLoss() animations as Air Power changes.");
        }

        private static MonoBehaviour FindProceduralProgressBar(
            GameObject root)
        {
            MonoBehaviour[] behaviours =
                root.GetComponentsInChildren<MonoBehaviour>(
                    true);

            for (int i = 0;
                 i < behaviours.Length;
                 i++)
            {
                MonoBehaviour behaviour =
                    behaviours[i];

                if (behaviour == null)
                    continue;

                if (behaviour.GetType().Name ==
                    "ProceduralProgressBar")
                {
                    return behaviour;
                }

                if (behaviour.GetType().GetMethod(
                        "UpdateBarFillAmount",
                        new[] { typeof(float) }) != null &&
                    behaviour.GetType().GetMethod(
                        "BarFill",
                        new[] { typeof(float) }) != null &&
                    behaviour.GetType().GetMethod(
                        "BarLoss",
                        new[] { typeof(float) }) != null)
                {
                    return behaviour;
                }
            }

            return null;
        }
    }
}
