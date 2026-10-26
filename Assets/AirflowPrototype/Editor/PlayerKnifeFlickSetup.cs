using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class PlayerKnifeFlickSetup
    {
        private const string RootFolder =
            "Assets/AirflowPrototype";

        private const string GeneratedFolder =
            "Assets/AirflowPrototype/Generated";

        private const string DefaultPresetPath =
            "Assets/AirflowPrototype/Generated/Batch21_KnifeFlickPreset.asset";

        [MenuItem(
            "Tools/Airflow Prototype/Batch 21/Install Hela-Style Procedural Knife Flick")]
        public static void Install()
        {
            PlayerMotor motor =
                Object.FindAnyObjectByType<PlayerMotor>();

            if (motor == null)
            {
                EditorUtility.DisplayDialog(
                    "Procedural Knife Flick",
                    "No PlayerMotor was found in the current scene.",
                    "OK");

                return;
            }

            AirCaster caster =
                motor.GetComponent<AirCaster>();

            if (caster == null)
            {
                EditorUtility.DisplayDialog(
                    "Procedural Knife Flick",
                    "The player does not have an AirCaster.",
                    "OK");

                return;
            }

            Animator animator =
                FindHumanoidAnimator(
                    motor.gameObject);

            if (animator == null)
            {
                EditorUtility.DisplayDialog(
                    "Procedural Knife Flick",
                    "No Humanoid Animator was found under the player.",
                    "OK");

                return;
            }

            EnsureFolder(
                RootFolder);

            EnsureFolder(
                GeneratedFolder);

            PlayerKnifeFlickPreset preset =
                AssetDatabase.LoadAssetAtPath<PlayerKnifeFlickPreset>(
                    DefaultPresetPath);

            if (preset == null)
            {
                preset =
                    ScriptableObject.CreateInstance<PlayerKnifeFlickPreset>();

                AssetDatabase.CreateAsset(
                    preset,
                    DefaultPresetPath);

                AssetDatabase.SaveAssets();
            }

            Transform leftSocket =
                EnsureSocket(
                    animator,
                    HumanBodyBones.LeftHand,
                    "Left Knife Flick Socket");

            Transform rightSocket =
                EnsureSocket(
                    animator,
                    HumanBodyBones.RightHand,
                    "Right Knife Flick Socket");

            PlayerProceduralKnifeFlick flick =
                motor.GetComponent<PlayerProceduralKnifeFlick>();

            if (flick == null)
            {
                flick =
                    Undo.AddComponent<PlayerProceduralKnifeFlick>(
                        motor.gameObject);
            }

            flick.Configure(
                caster,
                animator,
                preset,
                leftSocket,
                rightSocket);

            DisableLegacyThrowPresentation(
                motor.gameObject);

            EditorUtility.SetDirty(
                flick);

            EditorUtility.SetDirty(
                preset);

            EditorSceneManager.MarkSceneDirty(
                motor.gameObject.scene);

            Selection.activeObject =
                flick;

            Debug.Log(
                "Installed Batch 21 procedural knife flick. " +
                "The old procedural dual-throw presentation is disabled. " +
                "Use Debug Preview Pose in Play Mode to tune Ready / Prep / Strike / Follow Through.",
                flick);
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
                Animator animator =
                    animators[i];

                if (animator != null &&
                    animator.isHuman)
                {
                    return animator;
                }
            }

            return null;
        }

        private static Transform EnsureSocket(
            Animator animator,
            HumanBodyBones handBone,
            string socketName)
        {
            Transform hand =
                animator.GetBoneTransform(
                    handBone);

            if (hand == null)
                return null;

            Transform existing =
                hand.Find(
                    socketName);

            if (existing != null)
                return existing;

            GameObject socket =
                new GameObject(
                    socketName);

            Undo.RegisterCreatedObjectUndo(
                socket,
                "Create Knife Flick Socket");

            socket.transform.SetParent(
                hand,
                false);

            socket.transform.localPosition =
                Vector3.zero;

            socket.transform.localRotation =
                Quaternion.identity;

            socket.transform.localScale =
                Vector3.one;

            return socket.transform;
        }

        private static void DisableLegacyThrowPresentation(
            GameObject player)
        {
            MonoBehaviour[] behaviours =
                player.GetComponentsInChildren<MonoBehaviour>(
                    true);

            for (int i = 0;
                 i < behaviours.Length;
                 i++)
            {
                MonoBehaviour behaviour =
                    behaviours[i];

                if (behaviour == null ||
                    behaviour is PlayerProceduralKnifeFlick)
                {
                    continue;
                }

                string typeName =
                    behaviour.GetType().Name;

                if (typeName !=
                        "PlayerDualThrowPose" &&
                    typeName !=
                        "PlayerKnifeThrowController")
                {
                    continue;
                }

                Undo.RecordObject(
                    behaviour,
                    "Disable Legacy Throw Presentation");

                behaviour.enabled = false;

                EditorUtility.SetDirty(
                    behaviour);

                Debug.Log(
                    $"Disabled legacy throw presentation component: {typeName}",
                    behaviour);
            }
        }

        private static void EnsureFolder(
            string path)
        {
            if (AssetDatabase.IsValidFolder(
                    path))
            {
                return;
            }

            string parent =
                System.IO.Path.GetDirectoryName(
                    path)
                ?.Replace(
                    "\\",
                    "/");

            string name =
                System.IO.Path.GetFileName(
                    path);

            if (!string.IsNullOrWhiteSpace(
                    parent) &&
                !AssetDatabase.IsValidFolder(
                    parent))
            {
                EnsureFolder(
                    parent);
            }

            if (!string.IsNullOrWhiteSpace(
                    parent) &&
                !string.IsNullOrWhiteSpace(
                    name))
            {
                AssetDatabase.CreateFolder(
                    parent,
                    name);
            }
        }
    }
}
