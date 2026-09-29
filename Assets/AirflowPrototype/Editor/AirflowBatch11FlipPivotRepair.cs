using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class AirflowBatch11FlipPivotRepair
    {
        private const string AnimationRootName = "Capsule Animation Root";
        private const string FlipPivotName = "Capsule Flip Pivot";
        private const string CustomAnimationRootName = "Custom Animation Root";

        [MenuItem("Tools/Airflow Prototype/Batch 11/Install Improved Procedural Animation")]
        public static void InstallImprovedProceduralAnimation()
        {
            PlayerMotor motor =
                Object.FindFirstObjectByType<PlayerMotor>();

            if (motor == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 11",
                    "No PlayerMotor was found.",
                    "OK");
                return;
            }

            PlayerMovementVisuals visuals =
                motor.GetComponent<PlayerMovementVisuals>();

            if (visuals == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 11",
                    "PlayerMovementVisuals was not found on the player.",
                    "OK");
                return;
            }

            Transform animationRoot =
                motor.transform.Find(AnimationRootName);

            if (animationRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 11",
                    "Could not find 'Capsule Animation Root'. Run the previous Batch 11 install/repair first.",
                    "OK");
                return;
            }

            CharacterController controller =
                motor.GetComponent<CharacterController>();

            float centerY =
                controller != null
                    ? controller.center.y
                    : 1f;

            Transform flipPivot =
                animationRoot.Find(FlipPivotName);

            if (flipPivot == null)
            {
                GameObject pivotObject =
                    new GameObject(FlipPivotName);

                Undo.RegisterCreatedObjectUndo(
                    pivotObject,
                    "Create Capsule Flip Pivot");

                flipPivot =
                    pivotObject.transform;

                flipPivot.SetParent(
                    animationRoot,
                    false);
            }

            flipPivot.localPosition =
                new Vector3(0f, centerY, 0f);

            flipPivot.localRotation =
                Quaternion.identity;

            flipPivot.localScale =
                Vector3.one;

            Transform customRoot =
                animationRoot.Find(CustomAnimationRootName);

            if (customRoot != null &&
                customRoot.parent != flipPivot)
            {
                Undo.SetTransformParent(
                    customRoot,
                    flipPivot,
                    "Move Custom Animation Root Under Flip Pivot");
            }
            else if (customRoot == null)
            {
                Transform[] children =
                    new Transform[animationRoot.childCount];

                for (int i = 0;
                     i < animationRoot.childCount;
                     i++)
                {
                    children[i] =
                        animationRoot.GetChild(i);
                }

                for (int i = 0;
                     i < children.Length;
                     i++)
                {
                    Transform child =
                        children[i];

                    if (child == null ||
                        child == flipPivot)
                    {
                        continue;
                    }

                    bool hasRenderer =
                        child.GetComponentInChildren<Renderer>(true) != null;

                    if (hasRenderer)
                    {
                        Undo.SetTransformParent(
                            child,
                            flipPivot,
                            "Move Capsule Visual Under Flip Pivot");
                    }
                }
            }

            visuals.ConfigureFlipPivot(flipPivot);

            EditorUtility.SetDirty(visuals);

            EditorSceneManager.MarkSceneDirty(
                motor.gameObject.scene);

            Selection.activeGameObject =
                flipPivot.gameObject;

            Debug.Log(
                "Improved procedural animation installed. The air-reward stunt is now one centered 360 front flip around 'Capsule Flip Pivot'.");
        }
    }
}
