using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    [CustomEditor(typeof(PlayerHumanoidAnimationProfile))]
    public sealed class PlayerHumanoidAnimationProfileEditor :
        UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Drag animation clips into the slots below. Expand Double Jumps and set " +
                "the list size to however many random aerial flourish clips you want. " +
                "The generated player Animator Controller rebuilds automatically. " +
                "Do not hand-edit the generated controller; use this profile as the source of truth.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();

            base.OnInspectorGUI();

            if (EditorGUI.EndChangeCheck())
            {
                PlayerHumanoidAnimationProfile profile =
                    target
                        as PlayerHumanoidAnimationProfile;

                if (profile != null)
                {
                    EditorApplication.delayCall +=
                        () =>
                        {
                            if (profile == null)
                                return;

                            PlayerHumanoidAnimatorControllerBuilder
                                .Rebuild(
                                    profile);
                        };
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button(
                    "Rebuild Generated Animator Now"))
            {
                PlayerHumanoidAnimationProfile profile =
                    target
                        as PlayerHumanoidAnimationProfile;

                PlayerHumanoidAnimatorControllerBuilder
                    .Rebuild(
                        profile);
            }

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Recommended import setup: Idle/Walk/Run/Fall/Sling Air should generally loop. " +
                "Jump/Double Jump/Land/Sling Start/Sling Impact should generally be non-looping. " +
                "Root Motion should stay off because PlayerMotor owns movement.",
                MessageType.None);
        }
    }
}
