using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class PlayerHumanoidAnimatorControllerBuilder
    {
        public const string RootFolder =
            "Assets/AirflowPrototype";

        public const string GeneratedFolder =
            RootFolder +
            "/Generated";

        public const string ProfilePath =
            GeneratedFolder +
            "/Batch19_PlayerAnimationProfile.asset";

        public const string ControllerPath =
            GeneratedFolder +
            "/Batch19_PlayerHumanoid.controller";

        public static PlayerHumanoidAnimationProfile GetOrCreateProfile()
        {
            EnsureFolder(
                RootFolder);

            EnsureFolder(
                GeneratedFolder);

            PlayerHumanoidAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<PlayerHumanoidAnimationProfile>(
                    ProfilePath);

            if (profile != null)
                return profile;

            profile =
                ScriptableObject.CreateInstance<PlayerHumanoidAnimationProfile>();

            AssetDatabase.CreateAsset(
                profile,
                ProfilePath);

            AssetDatabase.SaveAssets();

            return profile;
        }

        public static AnimatorController Rebuild(
            PlayerHumanoidAnimationProfile profile,
            Animator targetAnimator = null)
        {
            if (profile == null)
                return null;

            EnsureFolder(
                RootFolder);

            EnsureFolder(
                GeneratedFolder);

            if (AssetDatabase.LoadAssetAtPath<Object>(
                    ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(
                    ControllerPath);
            }

            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(
                    ControllerPath);

            if (controller == null)
                return null;

            BuildParameters(
                controller);

            BuildBaseLayer(
                controller,
                profile);

            BuildSlingLayer(
                controller,
                profile);

            EditorUtility.SetDirty(
                controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                ControllerPath);

            if (targetAnimator == null)
            {
                PlayerMotor motor =
                    Object.FindAnyObjectByType<PlayerMotor>();

                if (motor != null)
                {
                    targetAnimator =
                        FindHumanoidAnimator(
                            motor.gameObject);
                }
            }

            if (targetAnimator != null)
            {
                Undo.RecordObject(
                    targetAnimator,
                    "Assign Generated Player Animator");

                targetAnimator.runtimeAnimatorController =
                    controller;

                targetAnimator.applyRootMotion =
                    false;

                EditorUtility.SetDirty(
                    targetAnimator);

                if (targetAnimator.gameObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement
                        .EditorSceneManager
                        .MarkSceneDirty(
                            targetAnimator.gameObject.scene);
                }
            }

            return controller;
        }

        public static Animator FindHumanoidAnimator(
            GameObject player)
        {
            if (player == null)
                return null;

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

        private static void BuildParameters(
            AnimatorController controller)
        {
            controller.AddParameter(
                "Speed",
                AnimatorControllerParameterType.Float);

            controller.AddParameter(
                "Grounded",
                AnimatorControllerParameterType.Bool);

            controller.AddParameter(
                "VerticalSpeed",
                AnimatorControllerParameterType.Float);

            controller.AddParameter(
                "Jump",
                AnimatorControllerParameterType.Trigger);

            controller.AddParameter(
                "Land",
                AnimatorControllerParameterType.Trigger);

            controller.AddParameter(
                "SlingStart",
                AnimatorControllerParameterType.Trigger);

            controller.AddParameter(
                "SlingAir",
                AnimatorControllerParameterType.Bool);

            controller.AddParameter(
                "SlingImpact",
                AnimatorControllerParameterType.Trigger);
        }

        private static void BuildBaseLayer(
            AnimatorController controller,
            PlayerHumanoidAnimationProfile profile)
        {
            AnimatorControllerLayer[] layers =
                controller.layers;

            AnimatorControllerLayer baseLayer =
                layers[0];

            baseLayer.name =
                "Base Layer";

            AnimatorStateMachine stateMachine =
                baseLayer.stateMachine;

            stateMachine.name =
                "Base Locomotion";

            ClearStateMachine(
                stateMachine);

            AnimationClip idle =
                MotionOrPlaceholder(
                    controller,
                    profile.idle,
                    "Idle Placeholder");

            AnimationClip walk =
                MotionOrPlaceholder(
                    controller,
                    profile.walk,
                    "Walk Placeholder");

            AnimationClip run =
                MotionOrPlaceholder(
                    controller,
                    profile.run,
                    "Run Placeholder");

            BlendTree locomotionTree =
                new BlendTree
                {
                    name = "Locomotion Blend Tree",
                    blendType =
                        BlendTreeType.Simple1D,
                    blendParameter =
                        "Speed",
                    useAutomaticThresholds =
                        false
                };

            AssetDatabase.AddObjectToAsset(
                locomotionTree,
                controller);

            locomotionTree.AddChild(
                idle,
                0f);

            locomotionTree.AddChild(
                walk,
                profile.walkThreshold);

            locomotionTree.AddChild(
                run,
                profile.runThreshold);

            AnimatorState locomotion =
                stateMachine.AddState(
                    "Locomotion",
                    new Vector3(
                        260f,
                        220f,
                        0f));

            locomotion.motion =
                locomotionTree;

            AnimatorState jump =
                stateMachine.AddState(
                    "Jump",
                    new Vector3(
                        560f,
                        90f,
                        0f));

            jump.motion =
                MotionOrPlaceholder(
                    controller,
                    profile.jump,
                    "Jump Placeholder");

            AnimatorState fall =
                stateMachine.AddState(
                    "Fall",
                    new Vector3(
                        820f,
                        90f,
                        0f));

            fall.motion =
                MotionOrPlaceholder(
                    controller,
                    profile.fall,
                    "Fall Placeholder");

            AnimatorState land =
                stateMachine.AddState(
                    "Land",
                    new Vector3(
                        820f,
                        300f,
                        0f));

            land.motion =
                MotionOrPlaceholder(
                    controller,
                    profile.land,
                    "Land Placeholder");

            stateMachine.defaultState =
                locomotion;

            AnimatorStateTransition jumpTrigger =
                stateMachine.AddAnyStateTransition(
                    jump);

            ConfigureImmediateTransition(
                jumpTrigger,
                profile.jumpTransitionDuration);

            jumpTrigger.canTransitionToSelf =
                false;

            jumpTrigger.AddCondition(
                AnimatorConditionMode.If,
                0f,
                "Jump");

            AnimatorStateTransition landTrigger =
                stateMachine.AddAnyStateTransition(
                    land);

            ConfigureImmediateTransition(
                landTrigger,
                profile.landTransitionDuration);

            landTrigger.canTransitionToSelf =
                false;

            landTrigger.AddCondition(
                AnimatorConditionMode.If,
                0f,
                "Land");

            AnimatorStateTransition locomotionToRising =
                locomotion.AddTransition(
                    jump);

            ConfigureImmediateTransition(
                locomotionToRising,
                profile.jumpTransitionDuration);

            locomotionToRising.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                "Grounded");

            locomotionToRising.AddCondition(
                AnimatorConditionMode.Greater,
                0.05f,
                "VerticalSpeed");

            AnimatorStateTransition locomotionToFall =
                locomotion.AddTransition(
                    fall);

            ConfigureImmediateTransition(
                locomotionToFall,
                profile.fallTransitionDuration);

            locomotionToFall.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                "Grounded");

            locomotionToFall.AddCondition(
                AnimatorConditionMode.Less,
                0.05f,
                "VerticalSpeed");

            AnimatorStateTransition jumpToFall =
                jump.AddTransition(
                    fall);

            ConfigureImmediateTransition(
                jumpToFall,
                profile.fallTransitionDuration);

            jumpToFall.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                "Grounded");

            jumpToFall.AddCondition(
                AnimatorConditionMode.Less,
                -0.05f,
                "VerticalSpeed");

            AnimatorStateTransition fallToRising =
                fall.AddTransition(
                    jump);

            ConfigureImmediateTransition(
                fallToRising,
                profile.jumpTransitionDuration);

            fallToRising.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                "Grounded");

            fallToRising.AddCondition(
                AnimatorConditionMode.Greater,
                0.05f,
                "VerticalSpeed");

            AnimatorStateTransition landToLocomotion =
                land.AddTransition(
                    locomotion);

            landToLocomotion.hasExitTime =
                true;

            landToLocomotion.exitTime =
                profile.landExitTime;

            landToLocomotion.hasFixedDuration =
                true;

            landToLocomotion.duration =
                profile.locomotionTransitionDuration;
        }

        private static void BuildSlingLayer(
            AnimatorController controller,
            PlayerHumanoidAnimationProfile profile)
        {
            AnimatorStateMachine stateMachine =
                new AnimatorStateMachine
                {
                    name =
                        "Sling Override"
                };

            AssetDatabase.AddObjectToAsset(
                stateMachine,
                controller);

            AnimatorControllerLayer layer =
                new AnimatorControllerLayer
                {
                    name = "Sling",
                    defaultWeight = 0f,
                    blendingMode =
                        AnimatorLayerBlendingMode.Override,
                    stateMachine =
                        stateMachine
                };

            controller.AddLayer(
                layer);

            AnimatorState empty =
                stateMachine.AddState(
                    "Sling Empty",
                    new Vector3(
                        120f,
                        260f,
                        0f));

            empty.motion =
                MotionOrPlaceholder(
                    controller,
                    null,
                    "Sling Empty Placeholder");

            AnimatorState start =
                stateMachine.AddState(
                    "Sling Start",
                    new Vector3(
                        360f,
                        100f,
                        0f));

            start.motion =
                MotionOrPlaceholder(
                    controller,
                    profile.slingStart,
                    "Sling Start Placeholder");

            AnimatorState air =
                stateMachine.AddState(
                    "Sling Air",
                    new Vector3(
                        620f,
                        100f,
                        0f));

            air.motion =
                MotionOrPlaceholder(
                    controller,
                    profile.slingAir,
                    "Sling Air Placeholder");

            AnimatorState impact =
                stateMachine.AddState(
                    "Sling Impact",
                    new Vector3(
                        880f,
                        100f,
                        0f));

            impact.motion =
                MotionOrPlaceholder(
                    controller,
                    profile.slingImpact,
                    "Sling Impact Placeholder");

            stateMachine.defaultState =
                empty;

            AnimatorStateTransition startTrigger =
                stateMachine.AddAnyStateTransition(
                    start);

            ConfigureImmediateTransition(
                startTrigger,
                profile.slingTransitionDuration);

            startTrigger.canTransitionToSelf =
                true;

            startTrigger.AddCondition(
                AnimatorConditionMode.If,
                0f,
                "SlingStart");

            AnimatorStateTransition startToAir =
                start.AddTransition(
                    air);

            ConfigureImmediateTransition(
                startToAir,
                profile.slingTransitionDuration);

            startToAir.AddCondition(
                AnimatorConditionMode.If,
                0f,
                "SlingAir");

            AnimatorStateTransition impactTrigger =
                stateMachine.AddAnyStateTransition(
                    impact);

            ConfigureImmediateTransition(
                impactTrigger,
                profile.slingTransitionDuration);

            impactTrigger.canTransitionToSelf =
                true;

            impactTrigger.AddCondition(
                AnimatorConditionMode.If,
                0f,
                "SlingImpact");

            AnimatorStateTransition impactToEmpty =
                impact.AddTransition(
                    empty);

            impactToEmpty.hasExitTime =
                true;

            impactToEmpty.exitTime =
                profile.slingImpactExitTime;

            impactToEmpty.hasFixedDuration =
                true;

            impactToEmpty.duration =
                profile.slingTransitionDuration;
        }

        private static void ConfigureImmediateTransition(
            AnimatorStateTransition transition,
            float duration)
        {
            transition.hasExitTime =
                false;

            transition.hasFixedDuration =
                true;

            transition.duration =
                Mathf.Max(
                    0f,
                    duration);

            transition.offset = 0f;
        }

        private static AnimationClip MotionOrPlaceholder(
            AnimatorController controller,
            AnimationClip source,
            string placeholderName)
        {
            if (source != null)
                return source;

            AnimationClip placeholder =
                new AnimationClip
                {
                    name =
                        placeholderName
                };

            AssetDatabase.AddObjectToAsset(
                placeholder,
                controller);

            return placeholder;
        }

        private static void ClearStateMachine(
            AnimatorStateMachine stateMachine)
        {
            ChildAnimatorState[] states =
                stateMachine.states;

            for (int i =
                     states.Length - 1;
                 i >= 0;
                 i--)
            {
                if (states[i].state != null)
                {
                    stateMachine.RemoveState(
                        states[i].state);
                }
            }

            ChildAnimatorStateMachine[] childMachines =
                stateMachine.stateMachines;

            for (int i =
                     childMachines.Length - 1;
                 i >= 0;
                 i--)
            {
                if (childMachines[i].stateMachine != null)
                {
                    stateMachine.RemoveStateMachine(
                        childMachines[i].stateMachine);
                }
            }
        }

        public static void EnsureFolder(
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

            if (string.IsNullOrEmpty(
                    parent) ||
                string.IsNullOrEmpty(
                    name))
            {
                return;
            }

            EnsureFolder(
                parent);

            if (!AssetDatabase.IsValidFolder(
                    path))
            {
                AssetDatabase.CreateFolder(
                    parent,
                    name);
            }
        }
    }
}
