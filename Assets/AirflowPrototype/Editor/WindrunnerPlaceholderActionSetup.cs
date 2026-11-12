using System;
using PortableAnimation;
using UnityEditor;
using UnityEngine;
using Windrunner.Production.Animation;
using Windrunner.Production.Feedback;

namespace AirflowPrototype.Editor
{
    public static class WindrunnerPlaceholderActionSetup
    {
        public static void InstallAvailableTransitions()
        {
            var legacy = AssetDatabase.LoadAssetAtPath<PlayerHumanoidAnimationProfile>("Assets/AirflowPrototype/Generated/Batch19_PlayerAnimationProfile.asset");
            var ground = AssetDatabase.LoadAssetAtPath<GroundMotionProfile>("Assets/WindrunnerProduction/Animation/Generated/WindrunnerGroundMotion.asset");
            using (var serialized = new SerializedObject(ground))
            {
                var slots = serialized.FindProperty("clips");
                Fill(slots, GroundPose.StartForward, legacy.runStartForward);
                Fill(slots, GroundPose.StartLeft, legacy.runStartLeft);
                Fill(slots, GroundPose.StartRight, legacy.runStartRight);
                Fill(slots, GroundPose.Stop, legacy.runStop);
                Fill(slots, GroundPose.PivotLeft, legacy.runTurnLeft);
                Fill(slots, GroundPose.PivotRight, legacy.runTurnRight);
                Fill(slots, GroundPose.Fall, legacy.fall);
                Fill(slots, GroundPose.Land, legacy.landIdle);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            ground.AssertStandaloneValid(); EditorUtility.SetDirty(ground); AssetDatabase.SaveAssets();
        }
        private static void Fill(SerializedProperty slots, GroundPose pose, AnimationClip clip)
        {
            if (clip == null || !clip.humanMotion || clip.legacy) throw new InvalidOperationException("Incompatible transition: " + pose);
            var slot = slots.GetArrayElementAtIndex((int)pose);
            if (slot.objectReferenceValue == null) slot.objectReferenceValue = clip;
        }

        public static void Install()
        {
            var legacy = AssetDatabase.LoadAssetAtPath<PlayerHumanoidAnimationProfile>("Assets/AirflowPrototype/Generated/Batch19_PlayerAnimationProfile.asset");
            var character = AssetDatabase.LoadAssetAtPath<WindrunnerAnimationProfile>("Assets/WindrunnerProduction/Animation/Generated/WindrunnerAnimationProfile.asset");
            var actions = character.standaloneActionProfile;
            if (actions == null || actions.Count != 0) throw new InvalidOperationException("Expected the audited empty action profile; refusing to overwrite authoring.");
            var clips = new[] { legacy.doubleJumps[0], legacy.slingAir, legacy.slingImpact, null, null, null, legacy.slingStart };
            string[] names = { "DoubleJumpFlip", "SlingTravel", "SlingImpact", "CastCharge", "CastRelease", "AirDash", "SlingCharge" };
            string[] groups = { "Flip", "Sling", "Sling", "Cast", "Cast", "Dash", "Sling" };
            int[] priorities = { 40, 20, 30, 10, 40, 15, 20 };
            for (int i = 0; i < 3; i++)
                if (clips[i] == null || !clips[i].humanMotion || clips[i].legacy)
                    throw new InvalidOperationException("Audited placeholder is incompatible: " + names[i]);
            using (var serialized = new SerializedObject(actions))
            {
                var list = serialized.FindProperty("actions"); list.arraySize = names.Length;
                for (int i = 0; i < names.Length; i++)
                {
                    var item = list.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("id").stringValue = names[i];
                    item.FindPropertyRelative("group").stringValue = groups[i];
                    item.FindPropertyRelative("clip").objectReferenceValue = clips[i];
                    item.FindPropertyRelative("mask").objectReferenceValue = null;
                    item.FindPropertyRelative("blendInSeconds").floatValue = i == 0 ? .001f : legacy.slingTransitionDuration;
                    item.FindPropertyRelative("blendOutSeconds").floatValue = .14f;
                    item.FindPropertyRelative("playbackSpeed").floatValue = 1f;
                    item.FindPropertyRelative("priority").intValue = priorities[i];
                    item.FindPropertyRelative("allowSameActionRestart").boolValue = i == 0;
                    item.FindPropertyRelative("interruptible").boolValue = true;
                    item.FindPropertyRelative("loopUntilCanceled").boolValue = i == 1 || i == 3 || i == 5 || i == 6;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            character.actionBindings = new[] {
                Play(FeedbackKey.Flip, 0), Play(FeedbackKey.SlingChargeStarted, 6, "Sling"),
                Play(FeedbackKey.SlingDashStarted, 1, "Sling"), Play(FeedbackKey.SlingImpact, 2, "Sling"),
                Cancel(FeedbackKey.SlingLaunched, 2, "Sling"), Cancel(FeedbackKey.SlingCanceled, 1, "Sling"),
                Play(FeedbackKey.CastChargeStarted, 3, "Cast"), Play(FeedbackKey.CastReleased, 4, "Cast"),
                Cancel(FeedbackKey.CastCanceled, 3, "Cast"), Play(FeedbackKey.DashStarted, 5),
                Cancel(FeedbackKey.DashStopped, 5, "Dash") };
            EditorUtility.SetDirty(actions); EditorUtility.SetDirty(character); AssetDatabase.SaveAssets();
            if (!character.ValidateForLiveGraph(out string error)) throw new InvalidOperationException(error);
            Debug.Log("Installed compatible placeholder action mappings. Auto-start unchanged; missing clips remain safe fallbacks.");
        }
        private static WindrunnerAnimationActionBinding Play(FeedbackKey key, int slot, string cancel = "") =>
            new WindrunnerAnimationActionBinding { feedbackEvent = key, montageSlot = slot, cancelGroup = cancel };
        private static WindrunnerAnimationActionBinding Cancel(FeedbackKey key, int slot, string group) =>
            new WindrunnerAnimationActionBinding { feedbackEvent = key, montageSlot = slot, operation = WindrunnerAnimationOperation.Cancel, cancelGroup = group };
    }
}
