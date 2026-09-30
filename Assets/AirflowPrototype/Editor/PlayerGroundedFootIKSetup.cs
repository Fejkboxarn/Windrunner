using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class PlayerGroundedFootIKSetup
    {
        [MenuItem(
            "Tools/Airflow Prototype/Batch 19/Install Grounded Foot IK")]
        public static void Install()
        {
            PlayerMotor motor =
                Object.FindAnyObjectByType<PlayerMotor>();

            if (motor == null)
            {
                EditorUtility.DisplayDialog(
                    "Grounded Foot IK",
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
                    "Grounded Foot IK",
                    "No Humanoid Animator was found under the player.",
                    "OK");

                return;
            }

            PlayerGroundedFootIK footIK =
                animator.GetComponent<PlayerGroundedFootIK>();

            if (footIK == null)
            {
                footIK =
                    Undo.AddComponent<PlayerGroundedFootIK>(
                        animator.gameObject);
            }

            AirSlingController slingController =
                motor.GetComponent<AirSlingController>();

            footIK.Configure(
                animator,
                motor,
                slingController);

            EditorUtility.SetDirty(
                footIK);

            if (animator.runtimeAnimatorController != null)
            {
                PlayerHumanoidAnimationProfile profile =
                    PlayerHumanoidAnimatorControllerBuilder
                        .GetOrCreateProfile();

                PlayerHumanoidAnimatorControllerBuilder
                    .Rebuild(
                        profile,
                        animator);
            }

            EditorSceneManager.MarkSceneDirty(
                motor.gameObject.scene);

            Selection.activeObject =
                footIK;

            Debug.Log(
                "Installed Grounded Foot IK on the Humanoid Animator. " +
                "The generated Base Layer IK Pass is enabled automatically.",
                animator);
        }
    }
}
