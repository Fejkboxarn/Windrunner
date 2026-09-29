using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class PlayerAnimationBatch19Setup
    {
        [MenuItem(
            "Tools/Airflow Prototype/Batch 19/Install Drag-Drop Player Animator")]
        public static void Install()
        {
            PlayerMotor motor =
                Object.FindAnyObjectByType<PlayerMotor>();

            if (motor == null)
            {
                EditorUtility.DisplayDialog(
                    "Batch 19 Player Animator",
                    "No PlayerMotor was found in the current scene.",
                    "OK");

                return;
            }

            GameObject player =
                motor.gameObject;

            Animator animator =
                PlayerHumanoidAnimatorControllerBuilder
                    .FindHumanoidAnimator(
                        player);

            if (animator == null)
            {
                EditorUtility.DisplayDialog(
                    "Batch 19 Player Animator",
                    "No Humanoid Animator was found under the player. " +
                    "Parent your Humanoid character under the player first, then run this installer again.",
                    "OK");

                return;
            }

            PlayerHumanoidAnimationProfile profile =
                PlayerHumanoidAnimatorControllerBuilder
                    .GetOrCreateProfile();

            PlayerHumanoidAnimatorControllerBuilder
                .Rebuild(
                    profile,
                    animator);

            PlayerMovementVisuals movementVisuals =
                player.GetComponent<PlayerMovementVisuals>();

            AirSlingController slingController =
                player.GetComponent<AirSlingController>();

            AirCaster caster =
                player.GetComponent<AirCaster>();

            PlayerHumanoidAnimatorDriver animatorDriver =
                player.GetComponent<PlayerHumanoidAnimatorDriver>();

            if (animatorDriver == null)
            {
                animatorDriver =
                    Undo.AddComponent<PlayerHumanoidAnimatorDriver>(
                        player);
            }

            animatorDriver.Configure(
                motor,
                movementVisuals,
                slingController,
                animator);

            Transform leftOrigin =
                EnsureThrowOrigin(
                    animator,
                    HumanBodyBones.LeftHand,
                    "Left Throw Origin");

            Transform rightOrigin =
                EnsureThrowOrigin(
                    animator,
                    HumanBodyBones.RightHand,
                    "Right Throw Origin");

            PlayerDualThrowPose throwPose =
                player.GetComponent<PlayerDualThrowPose>();

            if (throwPose == null)
            {
                throwPose =
                    Undo.AddComponent<PlayerDualThrowPose>(
                        player);
            }

            throwPose.Configure(
                caster,
                animator,
                leftOrigin,
                rightOrigin);

            DisableLegacyCustomAnimationDriver(
                player);

            DisablePrototypePlayerVisual(
                player);

            animator.applyRootMotion =
                false;

            EditorUtility.SetDirty(
                animator);

            EditorUtility.SetDirty(
                animatorDriver);

            EditorUtility.SetDirty(
                throwPose);

            EditorSceneManager.MarkSceneDirty(
                player.scene);

            AssetDatabase.SaveAssets();

            Selection.activeObject =
                profile;

            Debug.Log(
                "Batch 19 v19.1 installed. Drag clips into Batch19_PlayerAnimationProfile; " +
                "the generated Animator Controller rebuilds automatically with locomotion, " +
                "airborne transitions, Sling Start/Air/Impact, and all required parameters.",
                player);
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 19/Select Player Animation Profile")]
        public static void SelectProfile()
        {
            Selection.activeObject =
                PlayerHumanoidAnimatorControllerBuilder
                    .GetOrCreateProfile();
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 19/Rebuild Generated Player Animator")]
        public static void RebuildAnimator()
        {
            PlayerHumanoidAnimationProfile profile =
                PlayerHumanoidAnimatorControllerBuilder
                    .GetOrCreateProfile();

            PlayerHumanoidAnimatorControllerBuilder
                .Rebuild(
                    profile);

            Selection.activeObject =
                profile;
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 19/Select Generated Animator Controller")]
        public static void SelectGeneratedController()
        {
            Object controller =
                AssetDatabase.LoadAssetAtPath<Object>(
                    PlayerHumanoidAnimatorControllerBuilder
                        .ControllerPath);

            if (controller != null)
            {
                Selection.activeObject =
                    controller;
            }
        }

        private static Transform EnsureThrowOrigin(
            Animator animator,
            HumanBodyBones handBone,
            string originName)
        {
            if (animator == null ||
                !animator.isHuman)
            {
                return null;
            }

            Transform hand =
                animator.GetBoneTransform(
                    handBone);

            if (hand == null)
                return null;

            Transform existing =
                hand.Find(
                    originName);

            if (existing != null)
                return existing;

            GameObject origin =
                new GameObject(
                    originName);

            Undo.RegisterCreatedObjectUndo(
                origin,
                "Create Throw Origin");

            origin.transform.SetParent(
                hand,
                false);

            origin.transform.localPosition =
                Vector3.zero;

            origin.transform.localRotation =
                Quaternion.identity;

            origin.transform.localScale =
                Vector3.one;

            return origin.transform;
        }

        private static void DisableLegacyCustomAnimationDriver(
            GameObject player)
        {
            MonoBehaviour[] behaviours =
                player.GetComponents<MonoBehaviour>();

            for (int i = 0;
                 i < behaviours.Length;
                 i++)
            {
                MonoBehaviour behaviour =
                    behaviours[i];

                if (behaviour == null ||
                    behaviour.GetType().Name !=
                    "PlayerCustomAnimationDriver")
                {
                    continue;
                }

                if (!behaviour.enabled)
                    continue;

                Undo.RecordObject(
                    behaviour,
                    "Disable Legacy Player Animation Driver");

                behaviour.enabled =
                    false;

                EditorUtility.SetDirty(
                    behaviour);
            }
        }

        private static void DisablePrototypePlayerVisual(
            GameObject player)
        {
            Transform prototype =
                FindTransformRecursive(
                    player.transform,
                    "Capsule Body Visual");

            if (prototype == null ||
                !prototype.gameObject.activeSelf)
            {
                return;
            }

            Undo.RecordObject(
                prototype.gameObject,
                "Disable Prototype Player Visual");

            prototype.gameObject.SetActive(
                false);

            EditorUtility.SetDirty(
                prototype.gameObject);
        }

        private static Transform FindTransformRecursive(
            Transform root,
            string targetName)
        {
            if (root == null)
                return null;

            if (root.name ==
                targetName)
            {
                return root;
            }

            for (int i = 0;
                 i < root.childCount;
                 i++)
            {
                Transform found =
                    FindTransformRecursive(
                        root.GetChild(i),
                        targetName);

                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
