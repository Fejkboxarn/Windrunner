using System;
using System.Collections.Generic;
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
            RootFolder + "/Generated";

        public const string ProfilePath =
            GeneratedFolder + "/Batch19_PlayerAnimationProfile.asset";

        public const string ControllerPath =
            GeneratedFolder + "/Batch19_PlayerHumanoid.controller";

        public static PlayerHumanoidAnimationProfile GetOrCreateProfile()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            PlayerHumanoidAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<PlayerHumanoidAnimationProfile>(
                    ProfilePath);

            if (profile != null)
            {
                profile.EnsureCurrentDefaults();
                return profile;
            }

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

            profile.EnsureCurrentDefaults();

            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
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

            BuildParameters(controller);
            BuildBaseLayer(controller, profile);
            BuildSlingLayer(controller, profile);

            EditorUtility.SetDirty(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                ControllerPath);

            if (targetAnimator == null)
            {
                PlayerMotor motor =
                    UnityEngine.Object.FindAnyObjectByType<PlayerMotor>();

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
            }

            PlayerHumanoidAnimatorDriver driver =
                UnityEngine.Object.FindAnyObjectByType<PlayerHumanoidAnimatorDriver>();

            if (driver != null)
            {
                driver.ApplyGeneratedProfileSettings(
                    profile);

                EditorUtility.SetDirty(
                    driver);

                if (driver.gameObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement
                        .EditorSceneManager
                        .MarkSceneDirty(
                            driver.gameObject.scene);
                }
            }

            return controller;
        }

        public static bool AutoFillKnownCombatRunClips(
            PlayerHumanoidAnimationProfile profile)
        {
            if (profile == null)
                return false;

            Dictionary<string, AnimationClip> clips =
                FindAnimationClipsByName();

            bool changed = false;

            changed |= AssignIfEmpty(
                ref profile.runForward,
                clips,
                "Run_Combat_Fast_Loop");

            changed |= AssignIfEmpty(
                ref profile.runForwardLeft45,
                clips,
                "Run_Combat_Fast_Loop_L_45");

            changed |= AssignIfEmpty(
                ref profile.runForwardRight45,
                clips,
                "Run_Combat_Fast_Loop_R_45");

            changed |= AssignIfEmpty(
                ref profile.runLeft90,
                clips,
                "Run_Combat_Fast_Loop_L_90");

            changed |= AssignIfEmpty(
                ref profile.runRight90,
                clips,
                "Run_Combat_Fast_Loop_R_90");

            changed |= AssignIfEmpty(
                ref profile.runLeanLeft,
                clips,
                "Run_Combat_Fast_Lean_L_Loop");

            changed |= AssignIfEmpty(
                ref profile.runLeanRight,
                clips,
                "Run_Combat_Fast_Lean_R_Loop");

            changed |= AssignIfEmpty(
                ref profile.runStartForward,
                clips,
                "Run_Combat_Fast_Start");

            changed |= AssignIfEmpty(
                ref profile.runStartBackLeft,
                clips,
                "Run_Combat_Fast_Start_B_L");

            changed |= AssignIfEmpty(
                ref profile.runStartBackRight,
                clips,
                "Run_Combat_Fast_Start_B_R");

            changed |= AssignIfEmpty(
                ref profile.runStartLeft,
                clips,
                "Run_Combat_Fast_Start_L");

            changed |= AssignIfEmpty(
                ref profile.runStartRight,
                clips,
                "Run_Combat_Fast_Start_R");

            changed |= AssignIfEmpty(
                ref profile.runStop,
                clips,
                "Run_Combat_Fast_Stop");

            changed |= AssignIfEmpty(
                ref profile.runTurnLeft,
                clips,
                "Run_Combat_Fast_Turn_L");

            changed |= AssignIfEmpty(
                ref profile.runTurnRight,
                clips,
                "Run_Combat_Fast_Turn_R");

            if (changed)
            {
                EditorUtility.SetDirty(
                    profile);

                AssetDatabase.SaveAssets();
            }

            return changed;
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
            AddParameter(
                controller,
                "Speed",
                AnimatorControllerParameterType.Float);

            AddParameter(
                controller,
                "Grounded",
                AnimatorControllerParameterType.Bool);

            AddParameter(
                controller,
                "VerticalSpeed",
                AnimatorControllerParameterType.Float);

            AddParameter(
                controller,
                "LocomotionDirection",
                AnimatorControllerParameterType.Float);

            AddParameter(
                controller,
                "HasMoveInput",
                AnimatorControllerParameterType.Bool);

            AddParameter(
                controller,
                "RunStart",
                AnimatorControllerParameterType.Trigger);

            AddParameter(
                controller,
                "RunStartDirection",
                AnimatorControllerParameterType.Int);

            AddParameter(
                controller,
                "RunStop",
                AnimatorControllerParameterType.Trigger);

            AddParameter(
                controller,
                "HardTurn",
                AnimatorControllerParameterType.Trigger);

            AddParameter(
                controller,
                "HardTurnDirection",
                AnimatorControllerParameterType.Int);

            AddParameter(
                controller,
                "Jump",
                AnimatorControllerParameterType.Trigger);

            AddParameter(
                controller,
                "DoubleJump",
                AnimatorControllerParameterType.Trigger);

            AddParameter(
                controller,
                "DoubleJumpVariant",
                AnimatorControllerParameterType.Int);

            AddParameter(
                controller,
                "DoubleJumpMirror",
                AnimatorControllerParameterType.Bool);

            AddParameter(
                controller,
                "Land",
                AnimatorControllerParameterType.Trigger);

            AddParameter(
                controller,
                "SlingStart",
                AnimatorControllerParameterType.Trigger);

            AddParameter(
                controller,
                "SlingAir",
                AnimatorControllerParameterType.Bool);

            AddParameter(
                controller,
                "SlingImpact",
                AnimatorControllerParameterType.Trigger);
        }

        private static void BuildBaseLayer(
            AnimatorController controller,
            PlayerHumanoidAnimationProfile profile)
        {
            AnimatorControllerLayer baseLayer =
                controller.layers[0];

            baseLayer.name = "Base Layer";

            AnimatorStateMachine machine =
                baseLayer.stateMachine;

            machine.name =
                "Combat Locomotion";

            ClearStateMachine(
                machine);

            AnimatorState locomotion =
                machine.AddState(
                    "Locomotion",
                    new Vector3(
                        300f,
                        220f,
                        0f));

            locomotion.motion =
                BuildCombatLocomotionBlendTree(
                    controller,
                    profile);

            machine.defaultState =
                locomotion;

            AnimatorState jump =
                CreateState(
                    controller,
                    machine,
                    "Jump",
                    profile.jump,
                    "Jump Placeholder",
                    new Vector3(
                        620f,
                        20f,
                        0f));

            AnimatorState fall =
                CreateState(
                    controller,
                    machine,
                    "Fall",
                    profile.fall,
                    "Fall Placeholder",
                    new Vector3(
                        880f,
                        20f,
                        0f));

            AnimatorState land =
                CreateState(
                    controller,
                    machine,
                    "Land",
                    profile.land,
                    "Land Placeholder",
                    new Vector3(
                        880f,
                        250f,
                        0f));

            BuildGroundedOneShots(
                controller,
                machine,
                locomotion,
                profile);

            BuildDoubleJumpStates(
                controller,
                machine,
                profile,
                jump,
                fall,
                locomotion,
                false);

            AnimatorStateTransition jumpTrigger =
                machine.AddAnyStateTransition(
                    jump);

            ConfigureImmediateTransition(
                jumpTrigger,
                profile.jumpTransitionDuration);

            jumpTrigger.canTransitionToSelf = false;

            jumpTrigger.AddCondition(
                AnimatorConditionMode.If,
                0f,
                "Jump");

            AnimatorStateTransition landTrigger =
                machine.AddAnyStateTransition(
                    land);

            ConfigureImmediateTransition(
                landTrigger,
                profile.landTransitionDuration);

            landTrigger.canTransitionToSelf = false;

            landTrigger.AddCondition(
                AnimatorConditionMode.If,
                0f,
                "Land");

            AnimatorStateTransition locomotionToJump =
                locomotion.AddTransition(
                    jump);

            ConfigureImmediateTransition(
                locomotionToJump,
                profile.jumpTransitionDuration);

            locomotionToJump.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                "Grounded");

            locomotionToJump.AddCondition(
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

            AnimatorStateTransition fallToJump =
                fall.AddTransition(
                    jump);

            ConfigureImmediateTransition(
                fallToJump,
                profile.jumpTransitionDuration);

            fallToJump.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                "Grounded");

            fallToJump.AddCondition(
                AnimatorConditionMode.Greater,
                0.05f,
                "VerticalSpeed");

            AnimatorStateTransition landToLocomotion =
                land.AddTransition(
                    locomotion);

            landToLocomotion.hasExitTime = true;
            landToLocomotion.exitTime =
                profile.landExitTime;
            landToLocomotion.hasFixedDuration = true;
            landToLocomotion.duration =
                profile.locomotionTransitionDuration;
        }

        private static BlendTree BuildCombatLocomotionBlendTree(
            AnimatorController controller,
            PlayerHumanoidAnimationProfile profile)
        {
            BlendTree tree =
                new BlendTree
                {
                    name =
                        "Combat Directional Locomotion",
                    blendType =
                        BlendTreeType.FreeformCartesian2D,
                    blendParameter =
                        "LocomotionDirection",
                    blendParameterY =
                        "Speed",
                    useAutomaticThresholds =
                        false
                };

            AssetDatabase.AddObjectToAsset(
                tree,
                controller);

            AnimationClip forward =
                MotionOrPlaceholder(
                    controller,
                    profile.runForward,
                    "Run Forward Placeholder");

            AnimationClip l45 =
                profile.runForwardLeft45 != null
                    ? profile.runForwardLeft45
                    : forward;

            AnimationClip r45 =
                profile.runForwardRight45 != null
                    ? profile.runForwardRight45
                    : forward;

            AnimationClip l90 =
                profile.runLeft90 != null
                    ? profile.runLeft90
                    : l45;

            AnimationClip r90 =
                profile.runRight90 != null
                    ? profile.runRight90
                    : r45;

            AnimationClip leanL =
                profile.runLeanLeft != null
                    ? profile.runLeanLeft
                    : l90;

            AnimationClip leanR =
                profile.runLeanRight != null
                    ? profile.runLeanRight
                    : r90;

            AnimationClip idle =
                MotionOrPlaceholder(
                    controller,
                    profile.idle,
                    "Idle Placeholder");

            tree.AddChild(
                idle,
                new Vector2(
                    0f,
                    0f));

            if (profile.walk != null)
            {
                tree.AddChild(
                    profile.walk,
                    new Vector2(
                        0f,
                        profile.walkThreshold));
            }

            float runY =
                profile.runThreshold;

            tree.AddChild(
                forward,
                new Vector2(
                    0f,
                    runY));

            tree.AddChild(
                l45,
                new Vector2(
                    -0.5f,
                    runY));

            tree.AddChild(
                r45,
                new Vector2(
                    0.5f,
                    runY));

            tree.AddChild(
                l90,
                new Vector2(
                    -1f,
                    runY));

            tree.AddChild(
                r90,
                new Vector2(
                    1f,
                    runY));

            tree.AddChild(
                leanL,
                new Vector2(
                    -1f -
                    profile.turnLeanBlendExtension,
                    runY));

            tree.AddChild(
                leanR,
                new Vector2(
                    1f +
                    profile.turnLeanBlendExtension,
                    runY));

            return tree;
        }

        private static void BuildGroundedOneShots(
            AnimatorController controller,
            AnimatorStateMachine machine,
            AnimatorState locomotion,
            PlayerHumanoidAnimationProfile profile)
        {
            AnimationClip startFallback =
                profile.runStartForward;

            AnimatorState[] starts =
            {
                CreateState(
                    controller,
                    machine,
                    "Run Start Forward",
                    profile.runStartForward,
                    "Run Start Forward Placeholder",
                    new Vector3(40f, 10f, 0f)),

                CreateState(
                    controller,
                    machine,
                    "Run Start Left",
                    profile.runStartLeft != null
                        ? profile.runStartLeft
                        : startFallback,
                    "Run Start Left Placeholder",
                    new Vector3(40f, 90f, 0f)),

                CreateState(
                    controller,
                    machine,
                    "Run Start Right",
                    profile.runStartRight != null
                        ? profile.runStartRight
                        : startFallback,
                    "Run Start Right Placeholder",
                    new Vector3(40f, 170f, 0f)),

                CreateState(
                    controller,
                    machine,
                    "Run Start Back Left",
                    profile.runStartBackLeft != null
                        ? profile.runStartBackLeft
                        : profile.runStartLeft,
                    "Run Start Back Left Placeholder",
                    new Vector3(40f, 250f, 0f)),

                CreateState(
                    controller,
                    machine,
                    "Run Start Back Right",
                    profile.runStartBackRight != null
                        ? profile.runStartBackRight
                        : profile.runStartRight,
                    "Run Start Back Right Placeholder",
                    new Vector3(40f, 330f, 0f))
            };

            for (int i = 0; i < starts.Length; i++)
            {
                AnimatorStateTransition enter =
                    machine.AddAnyStateTransition(
                        starts[i]);

                ConfigureImmediateTransition(
                    enter,
                    profile.locomotionTransitionDuration);

                enter.canTransitionToSelf = false;

                enter.AddCondition(
                    AnimatorConditionMode.If,
                    0f,
                    "RunStart");

                enter.AddCondition(
                    AnimatorConditionMode.Equals,
                    i,
                    "RunStartDirection");

                enter.AddCondition(
                    AnimatorConditionMode.If,
                    0f,
                    "Grounded");

                AnimatorStateTransition exit =
                    starts[i].AddTransition(
                        locomotion);

                exit.hasExitTime = true;
                exit.exitTime =
                    profile.runStartExitTime;
                exit.hasFixedDuration = true;
                exit.duration =
                    profile.locomotionTransitionDuration;
            }

            AnimatorState stop =
                CreateState(
                    controller,
                    machine,
                    "Run Stop",
                    profile.runStop,
                    "Run Stop Placeholder",
                    new Vector3(
                        300f,
                        420f,
                        0f));

            AnimatorStateTransition stopEnter =
                machine.AddAnyStateTransition(
                    stop);

            ConfigureImmediateTransition(
                stopEnter,
                profile.locomotionTransitionDuration);

            stopEnter.canTransitionToSelf = false;

            stopEnter.AddCondition(
                AnimatorConditionMode.If,
                0f,
                "RunStop");

            stopEnter.AddCondition(
                AnimatorConditionMode.If,
                0f,
                "Grounded");

            AnimatorStateTransition stopExit =
                stop.AddTransition(
                    locomotion);

            stopExit.hasExitTime = true;
            stopExit.exitTime =
                profile.runStopExitTime;
            stopExit.hasFixedDuration = true;
            stopExit.duration =
                profile.locomotionTransitionDuration;

            AnimatorStateTransition stopInterrupted =
                stop.AddTransition(
                    locomotion);

            ConfigureImmediateTransition(
                stopInterrupted,
                profile.locomotionTransitionDuration);

            stopInterrupted.AddCondition(
                AnimatorConditionMode.If,
                0f,
                "HasMoveInput");

            AnimatorState turnLeft =
                CreateState(
                    controller,
                    machine,
                    "Hard Turn Left",
                    profile.runTurnLeft,
                    "Hard Turn Left Placeholder",
                    new Vector3(
                        520f,
                        420f,
                        0f));

            AnimatorState turnRight =
                CreateState(
                    controller,
                    machine,
                    "Hard Turn Right",
                    profile.runTurnRight,
                    "Hard Turn Right Placeholder",
                    new Vector3(
                        720f,
                        420f,
                        0f));

            AnimatorState[] turns =
            {
                turnLeft,
                turnRight
            };

            for (int i = 0; i < turns.Length; i++)
            {
                AnimatorStateTransition enter =
                    machine.AddAnyStateTransition(
                        turns[i]);

                ConfigureImmediateTransition(
                    enter,
                    profile.locomotionTransitionDuration);

                enter.canTransitionToSelf = false;

                enter.AddCondition(
                    AnimatorConditionMode.If,
                    0f,
                    "HardTurn");

                enter.AddCondition(
                    AnimatorConditionMode.Equals,
                    i,
                    "HardTurnDirection");

                enter.AddCondition(
                    AnimatorConditionMode.If,
                    0f,
                    "Grounded");

                AnimatorStateTransition exit =
                    turns[i].AddTransition(
                        locomotion);

                exit.hasExitTime = true;
                exit.exitTime =
                    profile.runTurnExitTime;
                exit.hasFixedDuration = true;
                exit.duration =
                    profile.locomotionTransitionDuration;
            }
        }

        private static void BuildDoubleJumpStates(
            AnimatorController controller,
            AnimatorStateMachine machine,
            PlayerHumanoidAnimationProfile profile,
            AnimatorState jump,
            AnimatorState fall,
            AnimatorState groundedReturn,
            bool slingLayer)
        {
            int count =
                Mathf.Max(
                    1,
                    profile.DoubleJumpVariantCount);

            for (int i = 0; i < count; i++)
            {
                AnimationClip clip =
                    profile.GetDoubleJumpVariant(
                        i);

                AnimatorState normal =
                    CreateState(
                        controller,
                        machine,
                        slingLayer
                            ? $"Sling Double Jump {i + 1:00}"
                            : $"Double Jump {i + 1:00}",
                        clip,
                        $"Double Jump {i + 1:00} Placeholder",
                        new Vector3(
                            650f + i * 75f,
                            slingLayer
                                ? 300f + i * 40f
                                : -120f - i * 40f,
                            0f));

                AnimatorState mirrored =
                    CreateState(
                        controller,
                        machine,
                        slingLayer
                            ? $"Sling Double Jump {i + 1:00} Mirrored"
                            : $"Double Jump {i + 1:00} Mirrored",
                        clip,
                        $"Double Jump {i + 1:00} Mirrored Placeholder",
                        new Vector3(
                            650f + i * 75f,
                            slingLayer
                                ? 500f + i * 40f
                                : -300f - i * 40f,
                            0f));

                mirrored.mirror = true;

                if (slingLayer)
                {
                    normal.tag = "SlingDoubleJump";
                    mirrored.tag = "SlingDoubleJump";
                }

                AddDoubleJumpEnterTransition(
                    machine,
                    normal,
                    i,
                    false);

                AddDoubleJumpEnterTransition(
                    machine,
                    mirrored,
                    i,
                    true);

                if (slingLayer)
                {
                    AddExitTimeTransition(
                        normal,
                        groundedReturn,
                        profile.doubleJumpExitTime,
                        profile.doubleJumpTransitionDuration);

                    AddExitTimeTransition(
                        mirrored,
                        groundedReturn,
                        profile.doubleJumpExitTime,
                        profile.doubleJumpTransitionDuration);

                    continue;
                }

                AddAirborneDoubleJumpExits(
                    normal,
                    jump,
                    fall,
                    groundedReturn,
                    profile);

                AddAirborneDoubleJumpExits(
                    mirrored,
                    jump,
                    fall,
                    groundedReturn,
                    profile);
            }
        }

        private static void AddDoubleJumpEnterTransition(
            AnimatorStateMachine machine,
            AnimatorState state,
            int variant,
            bool mirrored)
        {
            AnimatorStateTransition enter =
                machine.AddAnyStateTransition(
                    state);

            ConfigureImmediateTransition(
                enter,
                0f);

            enter.canTransitionToSelf = true;

            enter.AddCondition(
                AnimatorConditionMode.If,
                0f,
                "DoubleJump");

            enter.AddCondition(
                AnimatorConditionMode.Equals,
                variant,
                "DoubleJumpVariant");

            enter.AddCondition(
                mirrored
                    ? AnimatorConditionMode.If
                    : AnimatorConditionMode.IfNot,
                0f,
                "DoubleJumpMirror");
        }

        private static void AddAirborneDoubleJumpExits(
            AnimatorState state,
            AnimatorState jump,
            AnimatorState fall,
            AnimatorState locomotion,
            PlayerHumanoidAnimationProfile profile)
        {
            AnimatorStateTransition toJump =
                state.AddTransition(
                    jump);

            toJump.hasExitTime = true;
            toJump.exitTime =
                profile.doubleJumpExitTime;
            toJump.hasFixedDuration = true;
            toJump.duration =
                profile.doubleJumpTransitionDuration;

            toJump.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                "Grounded");

            toJump.AddCondition(
                AnimatorConditionMode.Greater,
                -0.05f,
                "VerticalSpeed");

            AnimatorStateTransition toFall =
                state.AddTransition(
                    fall);

            toFall.hasExitTime = true;
            toFall.exitTime =
                profile.doubleJumpExitTime;
            toFall.hasFixedDuration = true;
            toFall.duration =
                profile.doubleJumpTransitionDuration;

            toFall.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                "Grounded");

            toFall.AddCondition(
                AnimatorConditionMode.Less,
                -0.05f,
                "VerticalSpeed");

            AnimatorStateTransition toGround =
                state.AddTransition(
                    locomotion);

            toGround.hasExitTime = true;
            toGround.exitTime =
                profile.doubleJumpExitTime;
            toGround.hasFixedDuration = true;
            toGround.duration =
                profile.doubleJumpTransitionDuration;

            toGround.AddCondition(
                AnimatorConditionMode.If,
                0f,
                "Grounded");
        }

        private static void BuildSlingLayer(
            AnimatorController controller,
            PlayerHumanoidAnimationProfile profile)
        {
            AnimatorStateMachine machine =
                new AnimatorStateMachine
                {
                    name =
                        "Sling Override"
                };

            AssetDatabase.AddObjectToAsset(
                machine,
                controller);

            AnimatorControllerLayer layer =
                new AnimatorControllerLayer
                {
                    name = "Sling",
                    defaultWeight = 0f,
                    blendingMode =
                        AnimatorLayerBlendingMode.Override,
                    stateMachine =
                        machine
                };

            controller.AddLayer(
                layer);

            AnimatorState empty =
                CreateState(
                    controller,
                    machine,
                    "Sling Empty",
                    null,
                    "Sling Empty Placeholder",
                    new Vector3(100f, 200f, 0f));

            AnimatorState start =
                CreateState(
                    controller,
                    machine,
                    "Sling Start",
                    profile.slingStart,
                    "Sling Start Placeholder",
                    new Vector3(350f, 80f, 0f));

            AnimatorState air =
                CreateState(
                    controller,
                    machine,
                    "Sling Air",
                    profile.slingAir,
                    "Sling Air Placeholder",
                    new Vector3(600f, 80f, 0f));

            AnimatorState impact =
                CreateState(
                    controller,
                    machine,
                    "Sling Impact",
                    profile.slingImpact,
                    "Sling Impact Placeholder",
                    new Vector3(850f, 80f, 0f));

            machine.defaultState =
                empty;

            AnimatorStateTransition startTrigger =
                machine.AddAnyStateTransition(
                    start);

            ConfigureImmediateTransition(
                startTrigger,
                profile.slingTransitionDuration);

            startTrigger.canTransitionToSelf = true;

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
                machine.AddAnyStateTransition(
                    impact);

            ConfigureImmediateTransition(
                impactTrigger,
                profile.slingTransitionDuration);

            impactTrigger.canTransitionToSelf = true;

            impactTrigger.AddCondition(
                AnimatorConditionMode.If,
                0f,
                "SlingImpact");

            AnimatorStateTransition impactToEmpty =
                impact.AddTransition(
                    empty);

            impactToEmpty.hasExitTime = true;
            impactToEmpty.exitTime =
                profile.slingImpactExitTime;
            impactToEmpty.hasFixedDuration = true;
            impactToEmpty.duration =
                profile.slingTransitionDuration;

            BuildDoubleJumpStates(
                controller,
                machine,
                profile,
                null,
                null,
                empty,
                true);
        }

        private static AnimatorState CreateState(
            AnimatorController controller,
            AnimatorStateMachine machine,
            string name,
            AnimationClip clip,
            string placeholderName,
            Vector3 position)
        {
            AnimatorState state =
                machine.AddState(
                    name,
                    position);

            state.motion =
                MotionOrPlaceholder(
                    controller,
                    clip,
                    placeholderName);

            return state;
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

        private static void AddExitTimeTransition(
            AnimatorState from,
            AnimatorState to,
            float exitTime,
            float duration)
        {
            AnimatorStateTransition transition =
                from.AddTransition(
                    to);

            transition.hasExitTime = true;
            transition.exitTime =
                exitTime;
            transition.hasFixedDuration = true;
            transition.duration =
                duration;
        }

        private static void ConfigureImmediateTransition(
            AnimatorStateTransition transition,
            float duration)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration =
                Mathf.Max(
                    0f,
                    duration);
            transition.offset = 0f;
        }

        private static void AddParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            controller.AddParameter(
                name,
                type);
        }

        private static Dictionary<string, AnimationClip>
            FindAnimationClipsByName()
        {
            Dictionary<string, AnimationClip> result =
                new Dictionary<string, AnimationClip>(
                    StringComparer.OrdinalIgnoreCase);

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:AnimationClip");

            for (int i = 0;
                 i < guids.Length;
                 i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                // Ignore source-import folders named exactly "FBX".
                // The user keeps humanoid-converted animation assets elsewhere,
                // and auto-fill should never grab the raw FBX versions.
                if (HasParentFolderNamed(
                        path,
                        "FBX"))
                {
                    continue;
                }

                UnityEngine.Object[] assets =
                    AssetDatabase.LoadAllAssetsAtPath(
                        path);

                for (int j = 0;
                     j < assets.Length;
                     j++)
                {
                    if (assets[j] is AnimationClip clip &&
                        !result.ContainsKey(
                            clip.name))
                    {
                        result.Add(
                            clip.name,
                            clip);
                    }
                }
            }

            return result;
        }

        private static bool HasParentFolderNamed(
            string assetPath,
            string folderName)
        {
            if (string.IsNullOrWhiteSpace(
                    assetPath) ||
                string.IsNullOrWhiteSpace(
                    folderName))
            {
                return false;
            }

            string normalized =
                assetPath.Replace(
                    "\\",
                    "/");

            int lastSlash =
                normalized.LastIndexOf(
                    '/');

            if (lastSlash <= 0)
                return false;

            string parentPath =
                normalized.Substring(
                    0,
                    lastSlash);

            string[] segments =
                parentPath.Split(
                    '/');

            for (int i = 0;
                 i < segments.Length;
                 i++)
            {
                if (string.Equals(
                        segments[i],
                        folderName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AssignIfEmpty(
            ref AnimationClip field,
            Dictionary<string, AnimationClip> clips,
            string clipName)
        {
            if (field != null)
                return false;

            if (!clips.TryGetValue(
                    clipName,
                    out AnimationClip clip))
            {
                return false;
            }

            field = clip;
            return true;
        }

        private static void ClearStateMachine(
            AnimatorStateMachine machine)
        {
            ChildAnimatorState[] states =
                machine.states;

            for (int i =
                     states.Length - 1;
                 i >= 0;
                 i--)
            {
                if (states[i].state != null)
                {
                    machine.RemoveState(
                        states[i].state);
                }
            }

            ChildAnimatorStateMachine[] childMachines =
                machine.stateMachines;

            for (int i =
                     childMachines.Length - 1;
                 i >= 0;
                 i--)
            {
                if (childMachines[i].stateMachine != null)
                {
                    machine.RemoveStateMachine(
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

            if (string.IsNullOrEmpty(parent) ||
                string.IsNullOrEmpty(name))
            {
                return;
            }

            EnsureFolder(parent);

            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(
                    parent,
                    name);
            }
        }
    }
}
