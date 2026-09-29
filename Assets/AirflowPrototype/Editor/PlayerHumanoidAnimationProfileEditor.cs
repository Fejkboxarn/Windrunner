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
            PlayerHumanoidAnimationProfile profile =
                target as PlayerHumanoidAnimationProfile;

            EditorGUILayout.HelpBox(
                "This profile is the source of truth for the generated player Animator. " +
                "Assign clips here rather than hand-editing the generated controller.",
                MessageType.Info);

            if (GUILayout.Button(
                    "Auto-Fill Combat Run Clips By Name"))
            {
                bool changed =
                    PlayerHumanoidAnimatorControllerBuilder
                        .AutoFillKnownCombatRunClips(
                            profile);

                if (changed)
                {
                    PlayerHumanoidAnimatorControllerBuilder
                        .Rebuild(
                            profile);
                }

                Debug.Log(
                    changed
                        ? "Filled empty combat-run animation slots from the known Run_Combat_Fast clip names."
                        : "No empty known combat-run slots could be filled. Make sure the clips are imported into this Unity project.",
                    profile);
            }

            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            base.OnInspectorGUI();

            if (EditorGUI.EndChangeCheck())
            {
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
                PlayerHumanoidAnimatorControllerBuilder
                    .Rebuild(
                        profile);
            }

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Combat run mapping: Forward / L45 / R45 / L90 / R90 blend by actual travel direction. " +
                "Lean L/R are used at the extreme ends during sharper curved turning. " +
                "Starts, Stop and Hard Turn L/R are authored one-shot states.\n\n" +
                "Double Jumps: add any number of clips to the list and set Double Jump Mirror Chance Percent. " +
                "The chosen clip is randomly mirrored at that percentage.\n\n" +
                "Recommended import setup: run loops and Sling Air loop; starts, stop, turns, jump, double jumps, " +
                "land, Sling Start and Sling Impact should generally be non-looping. Root Motion stays off.",
                MessageType.None);
        }
    }
}
