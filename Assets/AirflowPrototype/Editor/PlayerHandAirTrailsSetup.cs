using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class PlayerHandAirTrailsSetup
    {
        [MenuItem(
            "Tools/Airflow Prototype/Batch 19/Install Hand Air Trails")]
        public static void Install()
        {
            PlayerMotor motor =
                Object.FindAnyObjectByType<PlayerMotor>();

            if (motor == null)
            {
                EditorUtility.DisplayDialog(
                    "Hand Air Trails",
                    "No PlayerMotor was found in the current scene.",
                    "OK");

                return;
            }

            GameObject player =
                motor.gameObject;

            Animator animator =
                FindHumanoidAnimator(
                    player);

            if (animator == null)
            {
                EditorUtility.DisplayDialog(
                    "Hand Air Trails",
                    "No Humanoid Animator was found under the player.",
                    "OK");

                return;
            }

            AirFlowHitSystem hitSystem =
                player.GetComponent<AirFlowHitSystem>();

            AirSlingController slingController =
                player.GetComponent<AirSlingController>();

            PlayerHandAirTrails trails =
                player.GetComponent<PlayerHandAirTrails>();

            if (trails == null)
            {
                trails =
                    Undo.AddComponent<PlayerHandAirTrails>(
                        player);
            }

            Transform leftOrigin =
                ResolvePreferredHandOrigin(
                    animator,
                    HumanBodyBones.LeftHand,
                    "Left Throw Origin");

            Transform rightOrigin =
                ResolvePreferredHandOrigin(
                    animator,
                    HumanBodyBones.RightHand,
                    "Right Throw Origin");

            trails.Configure(
                motor,
                hitSystem,
                slingController,
                animator,
                leftOrigin,
                rightOrigin);

            EditorUtility.SetDirty(
                trails);

            EditorSceneManager.MarkSceneDirty(
                player.scene);

            Selection.activeObject =
                trails;

            Debug.Log(
                "Installed PlayerHandAirTrails. Airborne normal Air Node rewards " +
                "and Sling impacts now emit configurable fading trails from both hands.",
                player);
        }

        private static Animator FindHumanoidAnimator(
            GameObject player)
        {
            Animator[] animators =
                player.GetComponentsInChildren<Animator>(
                    true);

            for (int i = 0;
                 i < animators.Length;
                 i++)
            {
                Animator candidate =
                    animators[i];

                if (candidate != null &&
                    candidate.isHuman)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Transform ResolvePreferredHandOrigin(
            Animator animator,
            HumanBodyBones handBone,
            string throwOriginName)
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

            Transform throwOrigin =
                hand.Find(
                    throwOriginName);

            return
                throwOrigin != null
                    ? throwOrigin
                    : hand;
        }
    }
}
