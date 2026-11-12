using PortableAnimation;
using UnityEditor;
using UnityEngine;
using Windrunner.Production.Animation;

namespace AirflowPrototype.Editor
{
    [CustomEditor(typeof(GroundMotionProfile))]
    public sealed class WindrunnerGroundMotionProfileInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Character-local locomotion configuration. Named pose slots below use existing compatible Humanoid clips. Optional missing one-shots fall back to motion/idle; mandatory locomotion validation prevents unsafe graph takeover. Existing tuning tools continue to edit this same asset.", MessageType.Info);
            var clips = serializedObject.FindProperty("clips");
            for (int i = 0; i < clips.arraySize && i < (int)GroundPose.Count; i++)
                EditorGUILayout.PropertyField(clips.GetArrayElementAtIndex(i), new GUIContent(((GroundPose)i).ToString()));
            DrawPropertiesExcluding(serializedObject, "m_Script", "clips");
            serializedObject.ApplyModifiedProperties();
            try { ((GroundMotionProfile)target).AssertStandaloneValid(); }
            catch (System.Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
        }
    }

    [CustomEditor(typeof(WindrunnerAnimationProfile))]
    public sealed class WindrunnerAnimationProfileInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Assign this character profile on AirflowPortableAnimationBridge. Duplicate the profile and its clip profiles for a different character. Graph takeover remains opt-in until visual acceptance.", MessageType.Info);
            DrawDefaultInspector();
            var profile = (WindrunnerAnimationProfile)target;
            if (GUILayout.Button("Inspect locomotion clips and transitions")) Selection.activeObject = profile.standaloneGroundProfile;
            if (GUILayout.Button("Inspect action clips and phases")) Selection.activeObject = profile.standaloneActionProfile;
            if (!profile.ValidateForLiveGraph(out string error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            else EditorGUILayout.HelpBox("Graph configuration is valid. Missing action clips fall back to locomotion; gameplay is unaffected.", MessageType.Info);
            var actions = profile.standaloneActionProfile;
            if (actions != null)
                for (int i = 0; i < actions.Count; i++)
                    if (actions.Action(i) != null && !actions.Action(i).IsPlayable)
                        EditorGUILayout.HelpBox("Unassigned/incompatible action: " + actions.Action(i).id + " (slot " + i + ").", MessageType.Warning);
        }
    }

    [CustomEditor(typeof(PortableCharacterActionProfile))]
    public sealed class PortableCharacterActionProfileInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Stable slot order is used by the character's event mappings. Replace clips in place. Speed scales clip time; fades use seconds. Looped phases need a cancel/end mapping. Higher priority interrupts lower priority only when Interruptible is enabled. Unassigned or non-Humanoid clips safely skip playback.", MessageType.Info);
            DrawDefaultInspector();
            var profile = (PortableCharacterActionProfile)target;
            try { profile.AssertValid(); }
            catch (System.Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
            for (int i = 0; i < profile.Count; i++)
            {
                var action = profile.Action(i);
                if (action != null && !action.IsPlayable)
                    EditorGUILayout.HelpBox("Slot " + i + ": " + action.id + " has no compatible Humanoid clip. This slot will not play.", MessageType.Warning);
            }
        }
    }
}
