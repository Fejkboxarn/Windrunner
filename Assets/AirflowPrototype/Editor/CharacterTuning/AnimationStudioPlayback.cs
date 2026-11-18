using System;
using PortableAnimation;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public sealed partial class DaggerMotionPreview
    {
        [SerializeField] int timingComparison;
        int speedPreset;
        string speedClipboard;
        VisualPlaybackSample timingSample;
        static readonly string[] TimingModes = { "Unsaved draft", "Saved production", "Original 1× (all)" };
        static readonly string[] SpeedPresets = { "Original Speed", "Half Speed", "Slow Start", "Slow Finish",
            "Slow / Fast / Slow", "Fast Attack / Slow Recovery", "Hold and Release" };

        void SpeedControls(SerializedObject so, string prefix)
        {
            EditorGUILayout.Space();
            GUILayout.Label("Timing & Speed", EditorStyles.boldLabel);
            SerializedProperty settings = null;
            if (action >= 0) settings = so.FindProperty(prefix + ".playback");
            else
            {
                var list = so.FindProperty("slotPlayback");
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("slot").stringValue == slots[slot].path)
                        settings = list.GetArrayElementAtIndex(i).FindPropertyRelative("playback");
                if (settings == null)
                {
                    EditorGUILayout.HelpBox("Uses the existing shared locomotion timing. Enable an assignment override to set independent authored speed.", MessageType.Info);
                    if (GUILayout.Button("Customize this slot's speed"))
                    {
                        so.ApplyModifiedProperties();
                        Undo.RecordObject(draftGround, "Customize animation playback");
                        draftGround.EditPlayback(slots[slot].path).mode = VisualPlaybackMode.AuthoredSpeed;
                        so.Update(); rebuild = true;
                    }
                    TimingReadout();
                    return;
                }
            }
            var mode = settings.FindPropertyRelative("mode");
            EditorGUILayout.PropertyField(mode, new GUIContent("Timing policy",
                "Existing preserves the current fit/cadence or inherits the base action. Authored uses source seconds. Fixed duration explicitly fits the visual segment."));
            if (mode.enumValueIndex == 0)
                EditorGUILayout.HelpBox(action < 0 ? "Shared cadence or state-duration fitting applies." :
                    "Existing phase fitting or inherited base timing applies. Reset to original speed removes inherited acceleration for this assignment.", MessageType.Info);
            if (GUILayout.Button("Reset to original 1.0×"))
            {
                SetSpeedPreset(settings, 0);
            }
            if (mode.enumValueIndex != 0)
            {
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("baseSpeed"), new GUIContent("Base speed ×", "0 holds. 1 is authored speed. Safe numeric range 0–100."));
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("reverse"), new GUIContent("Reverse", "Start at the segment end. The curve is read right to left."));
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("useCurve"), new GUIContent("Use speed curve"));
                if (settings.FindPropertyRelative("useCurve").boolValue)
                {
                    GUILayout.Label("Source progress 0–100%  ·  speed multiplier ×", EditorStyles.miniLabel);
                    var curve = settings.FindPropertyRelative("speedCurve");
                    var value=curve.animationCurveValue ?? AnimationCurve.Linear(0,1,1,1);
                    float maximum=2;
                    foreach(var key in value.keys)maximum=Mathf.Max(maximum,key.value);
                    EditorGUI.BeginChangeCheck();
                    var edited=EditorGUILayout.CurveField(GUIContent.none,value,Color.green,new Rect(0,0,1,maximum),GUILayout.Height(65));
                    if(EditorGUI.EndChangeCheck())curve.animationCurveValue=edited;
                    var rect = GUILayoutUtility.GetLastRect();
                    float x = rect.x + Mathf.Clamp01(timingSample.Progress) * rect.width;
                    EditorGUI.DrawRect(new Rect(x, rect.y, 1, rect.height), Color.yellow);
                    GUILayout.Label("Click curve for keys, tangents and zoom. Yellow = source playhead.", EditorStyles.wordWrappedMiniLabel);
                }
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("entryHoldSeconds"), new GUIContent("Entry hold (s)", "Finite visual hold before progression; independent of gameplay."));
                if (mode.enumValueIndex == (int)VisualPlaybackMode.FixedVisualDuration)
                {
                    EditorGUILayout.PropertyField(settings.FindPropertyRelative("fixedSeconds"), new GUIContent("Visual travel duration (s)"));
                    EditorGUILayout.HelpBox("Duration fitting overrides requested speed. A zero-speed barrier cannot be fitted.", MessageType.Warning);
                }
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("maximumActionSeconds"), new GUIContent("One-shot safety limit (s)",
                    "Bounds held or very slow one-shots. Gameplay-owned looping phases end through their cancellation signals."));
                using (new EditorGUILayout.HorizontalScope())
                {
                    speedPreset = EditorGUILayout.Popup(speedPreset, SpeedPresets);
                    if (GUILayout.Button("Use", GUILayout.Width(40))) SetSpeedPreset(settings, speedPreset);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy speed"))
                {
                    so.ApplyModifiedProperties();
                    var value = action < 0 ? draftGround.Playback(slots[slot].path) :
                        activeContext == 1 ? Actions.Action(action).playback : Variant.playback;
                    speedClipboard = JsonUtility.ToJson(value);
                }
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(speedClipboard)))
                    if (GUILayout.Button("Paste speed"))
                    {
                        so.ApplyModifiedProperties();
                        Undo.RecordObject(so.targetObject, "Paste animation speed");
                        var value = action < 0 ? draftGround.EditPlayback(slots[slot].path) :
                            activeContext == 1 ? Actions.Action(action).playback : Variant.playback;
                        JsonUtility.FromJsonOverwrite(speedClipboard, value);
                        so.Update(); rebuild = true;
                    }
            }
            TimingReadout();
        }

        static void SetSpeedPreset(SerializedProperty settings, int preset)
        {
            settings.FindPropertyRelative("mode").enumValueIndex = (int)VisualPlaybackMode.AuthoredSpeed;
            settings.FindPropertyRelative("baseSpeed").floatValue = preset == 1 ? .5f : 1;
            settings.FindPropertyRelative("reverse").boolValue = false;
            settings.FindPropertyRelative("entryHoldSeconds").floatValue = preset == 6 ? .25f : 0;
            settings.FindPropertyRelative("useCurve").boolValue = preset >= 2 && preset <= 5;
            AnimationCurve curve = AnimationCurve.Linear(0, 1, 1, 1);
            switch (preset)
            {
                case 2: curve = AnimationCurve.EaseInOut(0, .3f, 1, 1); break;
                case 3: curve = AnimationCurve.EaseInOut(0, 1, 1, .3f); break;
                case 4: curve = new AnimationCurve(new Keyframe(0, .3f), new Keyframe(.5f, 2), new Keyframe(1, .3f)); break;
                case 5: curve = AnimationCurve.EaseInOut(0, 2, 1, .4f); break;
            }
            settings.FindPropertyRelative("speedCurve").animationCurveValue = curve;
        }

        void TimingReadout()
        {
            GUILayout.Label($"Source {timingSample.SourceLength:F3}s   ·   {timingSample.SourceSeconds:F3}s ({timingSample.Progress:P0})",
                EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label($"Base {timingSample.BaseSpeed:F2}×   Curve {timingSample.CurveMultiplier:F2}×   Effective {timingSample.EffectiveSpeed:F2}×",
                EditorStyles.wordWrappedMiniLabel);
            string duration = double.IsInfinity(timingSample.EstimatedSeconds) ? "Held / no finite completion" : $"{timingSample.EstimatedSeconds:F3}s";
            GUILayout.Label("Estimated traversal " + duration, EditorStyles.wordWrappedMiniLabel);
        }

    }
}
