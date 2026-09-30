using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class PlayerRunningArmPoseSetup
    {
        [MenuItem(
            "Tools/Airflow Prototype/Batch 19/Install Running Arm Pose Layer")]
        public static void Install()
        {
            PlayerMotor motor =
                Object.FindAnyObjectByType<PlayerMotor>();

            if (motor == null)
            {
                EditorUtility.DisplayDialog(
                    "Running Arm Pose",
                    "No PlayerMotor was found in the current scene.",
                    "OK");

                return;
            }

            Animator animator =
                PlayerHumanoidAnimatorControllerBuilder
                    .FindHumanoidAnimator(
                        motor.gameObject);

            if (animator == null)
            {
                EditorUtility.DisplayDialog(
                    "Running Arm Pose",
                    "No Humanoid Animator was found under the player.",
                    "OK");

                return;
            }

            PlayerRunningArmPoseController controller =
                motor.GetComponent<PlayerRunningArmPoseController>();

            if (controller == null)
            {
                controller =
                    Undo.AddComponent<PlayerRunningArmPoseController>(
                        motor.gameObject);
            }

            controller.Configure(
                motor,
                motor.GetComponent<AirSlingController>(),
                animator);

            EditorUtility.SetDirty(
                controller);

            EditorSceneManager.MarkSceneDirty(
                motor.gameObject.scene);

            Selection.activeObject =
                controller;
        }
    }
}
