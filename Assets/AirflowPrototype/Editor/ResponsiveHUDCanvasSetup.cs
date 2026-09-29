using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AirflowPrototype.Editor
{
    public static class ResponsiveHUDCanvasSetup
    {
        [MenuItem(
            "Tools/Airflow Prototype/UI/Configure Selected Canvas Responsive")]
        public static void ConfigureSelectedCanvas()
        {
            Canvas canvas =
                ResolveSelectedCanvas();

            if (canvas == null)
            {
                EditorUtility.DisplayDialog(
                    "Responsive HUD Canvas",
                    "Select your HUD Canvas or one of its child UI objects first.",
                    "OK");

                return;
            }

            CanvasScaler scaler =
                canvas.GetComponent<CanvasScaler>();

            if (scaler == null)
            {
                scaler =
                    Undo.AddComponent<CanvasScaler>(
                        canvas.gameObject);
            }

            ResponsiveHUDCanvas responsive =
                canvas.GetComponent<ResponsiveHUDCanvas>();

            if (responsive == null)
            {
                responsive =
                    Undo.AddComponent<ResponsiveHUDCanvas>(
                        canvas.gameObject);
            }

            responsive.Configure(
                new Vector2(
                    1920f,
                    1080f),
                0.5f);

            EditorUtility.SetDirty(
                scaler);

            EditorUtility.SetDirty(
                responsive);

            EditorSceneManager.MarkSceneDirty(
                canvas.gameObject.scene);

            Selection.activeObject =
                responsive;

            Debug.Log(
                "HUD Canvas configured for Scale With Screen Size at 1920x1080 " +
                "with a 0.5 Width/Height match.");
        }

        [MenuItem(
            "Tools/Airflow Prototype/UI/Configure Selected Canvas Responsive",
            true)]
        private static bool ValidateConfigureSelectedCanvas()
        {
            return
                Selection.activeGameObject != null;
        }

        private static Canvas ResolveSelectedCanvas()
        {
            GameObject selected =
                Selection.activeGameObject;

            if (selected == null)
                return null;

            Canvas canvas =
                selected.GetComponent<Canvas>();

            if (canvas != null)
                return canvas;

            return
                selected.GetComponentInParent<Canvas>();
        }
    }
}
