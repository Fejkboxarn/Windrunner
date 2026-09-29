using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class AirAnimationAudioBatch11Setup
    {
        private const string RootFolder =
            "Assets/AirflowPrototype";

        private const string GeneratedFolder =
            RootFolder + "/Generated";

        private const string AnimationSettingsPath =
            GeneratedFolder +
            "/Batch11_CapsuleAnimationSettings.asset";

        private const string AudioSettingsPath =
            GeneratedFolder +
            "/Batch11_AudioSettings.asset";

        private const string AnimationProfilePath =
            GeneratedFolder +
            "/Batch11_PlayerAnimationProfile.asset";

        private const string DustSettingsPath =
            GeneratedFolder +
            "/Batch11_DustSettings.asset";

        private const string TemplateFolder =
            GeneratedFolder +
            "/Batch11_AnimationTemplate";

        private const string TemplateControllerPath =
            TemplateFolder +
            "/Batch11_AnimationTemplate.controller";

        private const string AnimationRootName =
            "Capsule Animation Root";

        private const string CustomAnimationRootName =
            "Custom Animation Root";

        [MenuItem("Tools/Airflow Prototype/Batch 11/Install Animation + Audio")]
        public static void Install()
        {
            PlayerMotor motor =
                Object.FindFirstObjectByType<PlayerMotor>();

            if (!ValidatePlayer(
                    motor,
                    out PlayerMovementVisuals visuals,
                    out AirCaster caster,
                    out AirFlowHitSystem hitSystem,
                    out AirDash dash,
                    out AirPower airPower))
            {
                return;
            }

            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);
            EnsureFolder(TemplateFolder);

            CapsuleAnimationSettings animationSettings =
                GetOrCreateAnimationSettings();

            AirAudioSettings audioSettings =
                GetOrCreateAudioSettings();

            PlayerAnimationProfile animationProfile =
                GetOrCreateAnimationProfile();

            PlayerDustSettings dustSettings =
                GetOrCreateDustSettings();

            AnimatorController templateController =
                GetOrCreateAnimationTemplate();

            animationProfile.SetTemplateController(
                templateController);

            EditorUtility.SetDirty(
                animationProfile);

            Transform animationRoot =
                BuildOrRepairAnimationRoot(
                    motor);

            Transform customAnimationRoot =
                BuildOrRepairCustomAnimationRoot(
                    animationRoot);

            Animator animator =
                customAnimationRoot.GetComponent<Animator>();

            if (animator == null)
            {
                animator =
                    Undo.AddComponent<Animator>(
                        customAnimationRoot.gameObject);
            }

            animator.runtimeAnimatorController =
                templateController;

            animator.applyRootMotion =
                false;

            EditorUtility.SetDirty(
                animator);

            visuals.Configure(
                motor,
                animationRoot,
                motor.Settings);

            visuals.ConfigureAnimation(
                animationSettings,
                hitSystem);

            PlayerCustomAnimationDriver driver =
                motor.GetComponent<PlayerCustomAnimationDriver>();

            if (driver == null)
            {
                driver =
                    Undo.AddComponent<PlayerCustomAnimationDriver>(
                        motor.gameObject);
            }

            driver.Configure(
                motor,
                visuals,
                animator,
                animationProfile);

            visuals.ConfigureCustomAnimationDriver(
                driver);

            EditorUtility.SetDirty(
                driver);

            EditorUtility.SetDirty(
                visuals);

            AirAudioDirector audio =
                motor.GetComponent<AirAudioDirector>();

            if (audio == null)
            {
                audio =
                    Undo.AddComponent<AirAudioDirector>(
                        motor.gameObject);
            }

            audio.Configure(
                motor,
                visuals,
                caster,
                hitSystem,
                dash,
                airPower,
                audioSettings);

            EditorUtility.SetDirty(
                audio);

            PlayerDustFeedback dust =
                motor.GetComponent<PlayerDustFeedback>();

            if (dust == null)
            {
                dust =
                    Undo.AddComponent<PlayerDustFeedback>(
                        motor.gameObject);
            }

            dust.Configure(
                motor,
                visuals,
                dustSettings);

            EditorUtility.SetDirty(
                dust);

            EditorSceneManager.MarkSceneDirty(
                motor.gameObject.scene);

            AssetDatabase.SaveAssets();

            Selection.activeObject =
                animationProfile;

            Debug.Log(
                "Airflow Batch 11 custom-animation bridge + dust installed. " +
                "Drop clips into Batch11_PlayerAnimationProfile. Any empty slot " +
                "continues using its procedural fallback.");
        }

        [MenuItem("Tools/Airflow Prototype/Batch 11/Select Custom Animation Profile")]
        public static void SelectCustomAnimationProfile()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);
            EnsureFolder(TemplateFolder);

            PlayerAnimationProfile profile =
                GetOrCreateAnimationProfile();

            profile.SetTemplateController(
                GetOrCreateAnimationTemplate());

            EditorUtility.SetDirty(
                profile);

            AssetDatabase.SaveAssets();

            Selection.activeObject =
                profile;
        }

        [MenuItem("Tools/Airflow Prototype/Batch 11/Select Dust Settings")]
        public static void SelectDustSettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            Selection.activeObject =
                GetOrCreateDustSettings();
        }

        [MenuItem("Tools/Airflow Prototype/Batch 11/Select Animation Settings")]
        public static void SelectAnimationSettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            Selection.activeObject =
                GetOrCreateAnimationSettings();
        }

        [MenuItem("Tools/Airflow Prototype/Batch 11/Select Audio Settings")]
        public static void SelectAudioSettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            Selection.activeObject =
                GetOrCreateAudioSettings();
        }

        private static bool ValidatePlayer(
            PlayerMotor motor,
            out PlayerMovementVisuals visuals,
            out AirCaster caster,
            out AirFlowHitSystem hitSystem,
            out AirDash dash,
            out AirPower airPower)
        {
            visuals = null;
            caster = null;
            hitSystem = null;
            dash = null;
            airPower = null;

            if (motor == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 11",
                    "No PlayerMotor was found in the current scene.",
                    "OK");

                return false;
            }

            GameObject player =
                motor.gameObject;

            visuals =
                player.GetComponent<PlayerMovementVisuals>();

            caster =
                player.GetComponent<AirCaster>();

            hitSystem =
                player.GetComponent<AirFlowHitSystem>();

            dash =
                player.GetComponent<AirDash>();

            airPower =
                player.GetComponent<AirPower>();

            if (visuals == null ||
                caster == null ||
                hitSystem == null ||
                dash == null ||
                airPower == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 11",
                    "Batch 11 needs the existing PlayerMovementVisuals, AirCaster, " +
                    "AirFlowHitSystem, AirDash and AirPower.",
                    "OK");

                return false;
            }

            return true;
        }

        private static Transform BuildOrRepairAnimationRoot(
            PlayerMotor motor)
        {
            Transform player =
                motor.transform;

            Transform animationRoot =
                player.Find(
                    AnimationRootName);

            if (animationRoot == null)
            {
                GameObject rootObject =
                    new GameObject(
                        AnimationRootName);

                Undo.RegisterCreatedObjectUndo(
                    rootObject,
                    "Create Capsule Animation Root");

                animationRoot =
                    rootObject.transform;

                animationRoot.SetParent(
                    player,
                    false);
            }

            SerializedObject serializedMotor =
                new SerializedObject(
                    motor);

            SerializedProperty visualRoot =
                serializedMotor.FindProperty(
                    "visualRoot");

            if (visualRoot != null)
            {
                visualRoot.objectReferenceValue =
                    animationRoot;

                serializedMotor.ApplyModifiedProperties();

                EditorUtility.SetDirty(
                    motor);
            }

            return animationRoot;
        }

        private static Transform BuildOrRepairCustomAnimationRoot(
            Transform animationRoot)
        {
            Transform customRoot =
                animationRoot.Find(
                    CustomAnimationRootName);

            if (customRoot == null)
            {
                GameObject rootObject =
                    new GameObject(
                        CustomAnimationRootName);

                Undo.RegisterCreatedObjectUndo(
                    rootObject,
                    "Create Custom Animation Root");

                customRoot =
                    rootObject.transform;

                customRoot.SetParent(
                    animationRoot,
                    false);
            }

            Transform[] children =
                new Transform[
                    animationRoot.childCount];

            for (int i = 0;
                 i < animationRoot.childCount;
                 i++)
            {
                children[i] =
                    animationRoot.GetChild(i);
            }

            for (int i = 0;
                 i < children.Length;
                 i++)
            {
                Transform child =
                    children[i];

                if (child == null ||
                    child == customRoot)
                {
                    continue;
                }

                string lower =
                    child.name.ToLowerInvariant();

                if (lower.Contains("audio") ||
                    lower.Contains("dust") ||
                    lower.Contains("air cast") ||
                    lower.Contains("wind") ||
                    lower.Contains("charge") ||
                    lower.Contains("pressure"))
                {
                    continue;
                }

                bool containsVisual =
                    child.GetComponentInChildren<Renderer>(true) != null;

                if (containsVisual)
                {
                    Undo.SetTransformParent(
                        child,
                        customRoot,
                        "Move Visual Under Custom Animation Root");
                }
            }

            return customRoot;
        }

        private static AnimatorController GetOrCreateAnimationTemplate()
        {
            AnimatorController existing =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    TemplateControllerPath);

            if (existing != null)
                return existing;

            AnimationClip empty =
                GetOrCreatePlaceholder(
                    PlayerAnimationProfile.EmptyPlaceholderName);

            AnimationClip idle =
                GetOrCreatePlaceholder(
                    PlayerAnimationProfile.IdlePlaceholderName);

            AnimationClip locomotion =
                GetOrCreatePlaceholder(
                    PlayerAnimationProfile.LocomotionPlaceholderName);

            AnimationClip jump =
                GetOrCreatePlaceholder(
                    PlayerAnimationProfile.JumpPlaceholderName);

            AnimationClip fall =
                GetOrCreatePlaceholder(
                    PlayerAnimationProfile.FallPlaceholderName);

            AnimationClip land =
                GetOrCreatePlaceholder(
                    PlayerAnimationProfile.LandPlaceholderName);

            AnimationClip flipA =
                GetOrCreatePlaceholder(
                    PlayerAnimationProfile.FlipAPlaceholderName);

            AnimationClip flipB =
                GetOrCreatePlaceholder(
                    PlayerAnimationProfile.FlipBPlaceholderName);

            AnimationClip flipC =
                GetOrCreatePlaceholder(
                    PlayerAnimationProfile.FlipCPlaceholderName);

            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(
                    TemplateControllerPath);

            controller.AddParameter(
                "LocomotionRate",
                AnimatorControllerParameterType.Float);

            AnimatorStateMachine machine =
                controller.layers[0].stateMachine;

            AnimatorState fallback =
                machine.AddState(
                    "Fallback");

            fallback.motion = empty;

            AnimatorState idleState =
                machine.AddState(
                    "Idle");

            idleState.motion = idle;

            AnimatorState locomotionState =
                machine.AddState(
                    "Locomotion");

            locomotionState.motion =
                locomotion;

            locomotionState.speed =
                1f;

            locomotionState.speedParameter =
                "LocomotionRate";

            locomotionState.speedParameterActive =
                true;

            AnimatorState jumpState =
                machine.AddState(
                    "Jump");

            jumpState.motion = jump;

            AnimatorState fallState =
                machine.AddState(
                    "Fall");

            fallState.motion = fall;

            AnimatorState landState =
                machine.AddState(
                    "Land");

            landState.motion = land;

            AnimatorState flipAState =
                machine.AddState(
                    "Flip A");

            flipAState.motion = flipA;

            AnimatorState flipBState =
                machine.AddState(
                    "Flip B");

            flipBState.motion = flipB;

            AnimatorState flipCState =
                machine.AddState(
                    "Flip C");

            flipCState.motion = flipC;

            machine.defaultState =
                fallback;

            EditorUtility.SetDirty(
                controller);

            AssetDatabase.SaveAssets();

            return controller;
        }

        private static AnimationClip GetOrCreatePlaceholder(
            string name)
        {
            string path =
                TemplateFolder +
                "/" +
                name +
                ".anim";

            AnimationClip clip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    path);

            if (clip != null)
                return clip;

            clip =
                new AnimationClip();

            clip.name =
                name;

            AssetDatabase.CreateAsset(
                clip,
                path);

            return clip;
        }

        private static PlayerAnimationProfile GetOrCreateAnimationProfile()
        {
            PlayerAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<PlayerAnimationProfile>(
                    AnimationProfilePath);

            if (profile != null)
                return profile;

            profile =
                ScriptableObject.CreateInstance<PlayerAnimationProfile>();

            AssetDatabase.CreateAsset(
                profile,
                AnimationProfilePath);

            AssetDatabase.SaveAssets();

            return profile;
        }

        private static PlayerDustSettings GetOrCreateDustSettings()
        {
            PlayerDustSettings settings =
                AssetDatabase.LoadAssetAtPath<PlayerDustSettings>(
                    DustSettingsPath);

            if (settings != null)
                return settings;

            settings =
                ScriptableObject.CreateInstance<PlayerDustSettings>();

            AssetDatabase.CreateAsset(
                settings,
                DustSettingsPath);

            AssetDatabase.SaveAssets();

            return settings;
        }

        private static CapsuleAnimationSettings GetOrCreateAnimationSettings()
        {
            CapsuleAnimationSettings settings =
                AssetDatabase.LoadAssetAtPath<CapsuleAnimationSettings>(
                    AnimationSettingsPath);

            if (settings != null)
                return settings;

            settings =
                ScriptableObject.CreateInstance<CapsuleAnimationSettings>();

            AssetDatabase.CreateAsset(
                settings,
                AnimationSettingsPath);

            AssetDatabase.SaveAssets();

            return settings;
        }

        private static AirAudioSettings GetOrCreateAudioSettings()
        {
            AirAudioSettings settings =
                AssetDatabase.LoadAssetAtPath<AirAudioSettings>(
                    AudioSettingsPath);

            if (settings != null)
                return settings;

            settings =
                ScriptableObject.CreateInstance<AirAudioSettings>();

            AssetDatabase.CreateAsset(
                settings,
                AudioSettingsPath);

            AssetDatabase.SaveAssets();

            return settings;
        }

        private static void EnsureFolder(
            string assetPath)
        {
            if (AssetDatabase.IsValidFolder(
                    assetPath))
            {
                return;
            }

            string parent =
                Path.GetDirectoryName(
                    assetPath)?
                    .Replace("\\", "/");

            string name =
                Path.GetFileName(
                    assetPath);

            if (!string.IsNullOrEmpty(parent) &&
                !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(
                parent,
                name);
        }
    }
}
