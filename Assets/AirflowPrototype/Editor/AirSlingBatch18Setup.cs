using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class AirSlingBatch18Setup
    {
        private const string RootFolder =
            "Assets/AirflowPrototype";

        private const string GeneratedFolder =
            RootFolder + "/Generated";

        private const string SettingsPath =
            GeneratedFolder +
            "/Batch18_AirSlingSettings.asset";

        private const string TargetUISettingsPath =
            GeneratedFolder +
            "/Batch18_AirSlingTargetUISettings.asset";

        private const string ImpactAudioLibraryPath =
            GeneratedFolder +
            "/Batch18_AirSlingImpactAudioLibrary.asset";

        private const string NodeMaterialPath =
            GeneratedFolder +
            "/Batch18_AirSlingNodePrototype.mat";

        private const string ImpactParticleMaterialPath =
            GeneratedFolder +
            "/Batch18_SlingImpactParticle.mat";

        [MenuItem(
            "Tools/Airflow Prototype/Batch 18/Install Curved Sling + Camera Feel")]
        public static void Install()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            PlayerMotor motor =
                Object.FindAnyObjectByType<PlayerMotor>();

            if (motor == null)
            {
                EditorUtility.DisplayDialog(
                    "Batch 18 Sling Impact",
                    "No PlayerMotor was found in the current scene.",
                    "OK");

                return;
            }

            GameObject player =
                motor.gameObject;

            CharacterController controller =
                player.GetComponent<CharacterController>();

            AirPower airPower =
                player.GetComponent<AirPower>();

            AirPerfectTimingFeedback perfectFeedback =
                player.GetComponent<AirPerfectTimingFeedback>();

            AirFlowHitSystem regularHitSystem =
                player.GetComponent<AirFlowHitSystem>();

            AirFlowHitSettings regularNodeRewardSettings =
                GetRegularNodeRewardSettings(
                    regularHitSystem);

            PlayerMovementVisuals movementVisuals =
                player.GetComponent<PlayerMovementVisuals>();

            PlayerSpeedFeedback speedFeedback =
                player.GetComponent<PlayerSpeedFeedback>();

            PlayerOrbitCamera orbitCamera =
                Object.FindAnyObjectByType<PlayerOrbitCamera>();

            Camera targetCamera =
                Camera.main;

            if (controller == null)
            {
                EditorUtility.DisplayDialog(
                    "Batch 18 Sling Impact",
                    "The player needs a CharacterController.",
                    "OK");

                return;
            }

            AirSlingSettings settings =
                AssetDatabase.LoadAssetAtPath<AirSlingSettings>(
                    SettingsPath);

            if (settings == null)
            {
                settings =
                    ScriptableObject.CreateInstance<AirSlingSettings>();

                AssetDatabase.CreateAsset(
                    settings,
                    SettingsPath);
            }

            settings.EnsureCurrentDefaults();
            EditorUtility.SetDirty(settings);

            AirSlingTargetUISettings targetUISettings =
                GetOrCreateTargetUISettings();

            AirSlingImpactAudioLibrary impactAudioLibrary =
                GetOrCreateImpactAudioLibrary();

            AirSlingTargeter slingTargeter =
                player.GetComponent<AirSlingTargeter>();

            if (slingTargeter == null)
            {
                slingTargeter =
                    Undo.AddComponent<AirSlingTargeter>(
                        player);
            }

            slingTargeter.Configure(
                targetCamera,
                motor,
                settings);

            AirSlingController slingController =
                player.GetComponent<AirSlingController>();

            if (slingController == null)
            {
                slingController =
                    Undo.AddComponent<AirSlingController>(
                        player);
            }

            slingController.Configure(
                motor,
                controller,
                slingTargeter,
                airPower,
                targetCamera,
                perfectFeedback,
                settings,
                regularNodeRewardSettings,
                movementVisuals,
                orbitCamera,
                impactAudioLibrary,
                speedFeedback);

            AirSlingTargetChargeUI slingTargetUI =
                player.GetComponent<AirSlingTargetChargeUI>();

            if (slingTargetUI == null)
            {
                slingTargetUI =
                    Undo.AddComponent<AirSlingTargetChargeUI>(
                        player);
            }

            slingTargetUI.Configure(
                slingController,
                targetCamera,
                targetUISettings);

            EditorUtility.SetDirty(
                slingTargetUI);

            EditorUtility.SetDirty(
                slingTargeter);

            EditorUtility.SetDirty(
                slingController);

            EditorUtility.SetDirty(
                settings);

            EditorUtility.SetDirty(
                targetUISettings);

            EditorUtility.SetDirty(
                impactAudioLibrary);

            AirSlingNode[] slingNodes =
                Object.FindObjectsByType<AirSlingNode>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i = 0;
                 i < slingNodes.Length;
                 i++)
            {
                EnsureImpactSplash(
                    slingNodes[i],
                    settings);

                EnsureEnemyComponents(
                    slingNodes[i],
                    settings);
            }

            EditorSceneManager.MarkSceneDirty(
                player.scene);

            AssetDatabase.SaveAssets();

            Selection.activeObject =
                targetUISettings;

            if (perfectFeedback == null)
            {
                Debug.LogWarning(
                    "Batch 18.11 installed, but no AirPerfectTimingFeedback component " +
                    "was found on the player. Sling movement will still work, " +
                    "but its release/impact time-stop options cannot play.",
                    player);
            }

            if (speedFeedback == null)
            {
                Debug.LogWarning(
                    "Batch 18.11 installed, but no PlayerSpeedFeedback was found on the player. " +
                    "The curved Sling path will work, but the dedicated Sling FOV profile will not.",
                    player);
            }

            Debug.Log(
                "Batch 18.11 installed. Sling travel now uses a momentum-shaped cubic Bezier " +
                "path, editable speed curve, player banking, optional bear-facing, and an " +
                "explicit release / dash / pre-impact / launch FOV profile.");
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 18/Create Prototype Sling Node")]
        public static void CreatePrototypeNode()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            GameObject root =
                new GameObject(
                    "Air Sling Node");

            Undo.RegisterCreatedObjectUndo(
                root,
                "Create Air Sling Node");

            AirSlingNode node =
                root.AddComponent<AirSlingNode>();

            GameObject visual =
                GameObject.CreatePrimitive(
                    PrimitiveType.Sphere);

            visual.name =
                "Prototype Visual";

            Undo.RegisterCreatedObjectUndo(
                visual,
                "Create Air Sling Visual");

            visual.transform.SetParent(
                root.transform,
                false);

            visual.transform.localScale =
                Vector3.one *
                1.15f;

            Collider collider =
                visual.GetComponent<Collider>();

            if (collider != null)
            {
                Object.DestroyImmediate(
                    collider);
            }

            int ignoreRaycast =
                LayerMask.NameToLayer(
                    "Ignore Raycast");

            if (ignoreRaycast >= 0)
                visual.layer = ignoreRaycast;

            MeshRenderer renderer =
                visual.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                renderer.sharedMaterial =
                    GetOrCreatePrototypeMaterial();
            }

            GameObject landingObject =
                new GameObject(
                    "Landing Point");

            Undo.RegisterCreatedObjectUndo(
                landingObject,
                "Create Sling Landing Point");

            landingObject.transform.SetParent(
                root.transform,
                false);

            // Sphere radius is 0.5 and prototype scale is 1.15.
            // Slightly above the visible top avoids capsule/mesh overlap.
            landingObject.transform.localPosition =
                Vector3.up *
                0.62f;

            node.SetLandingPoint(
                landingObject.transform);

            AirSlingSettings settings =
                GetOrCreateSettings();

            EnsureImpactSplash(
                node,
                settings);

            EnsureEnemyComponents(
                node,
                settings);

            Vector3 position =
                GetSuggestedNodePosition();

            root.transform.position =
                position;

            EditorUtility.SetDirty(
                node);

            Selection.activeGameObject =
                root;

            EditorSceneManager.MarkSceneDirty(
                root.scene);
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 18/Select Air Sling Settings")]
        public static void SelectSettings()
        {
            AirSlingSettings settings =
                GetOrCreateSettings();

            Selection.activeObject =
                settings;
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 18/Select Sling Target UI Settings")]
        public static void SelectTargetUISettings()
        {
            Selection.activeObject =
                GetOrCreateTargetUISettings();
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 18/Select Sling Impact Audio Library")]
        public static void SelectImpactAudioLibrary()
        {
            Selection.activeObject =
                GetOrCreateImpactAudioLibrary();
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 18/Apply Default Visual To Selected Sling Node")]
        public static void ApplyDefaultVisualToSelectedNode()
        {
            GameObject selected =
                Selection.activeGameObject;

            AirSlingNode node =
                selected != null
                    ? selected.GetComponentInParent<AirSlingNode>()
                    : null;

            if (node == null)
            {
                EditorUtility.DisplayDialog(
                    "Sling Node Visual",
                    "Select an Air Sling Node (or one of its children) first.",
                    "OK");

                return;
            }

            AirSlingSettings settings =
                GetOrCreateSettings();

            if (settings.defaultNodeVisualPrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Sling Node Visual",
                    "Assign your bear prefab to Default Node Visual Prefab in AirSlingSettings first.",
                    "OK");

                Selection.activeObject =
                    settings;

                return;
            }

            EnsureDefaultNodeVisual(
                node,
                settings,
                true);

            EnsureEnemyComponents(
                node,
                settings);

            Selection.activeGameObject =
                node.gameObject;
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 18/Configure Health + Bear Visuals On All Nodes")]
        public static void ConfigureHealthAndBearVisuals()
        {
            AirSlingSettings settings =
                GetOrCreateSettings();

            AirSlingNode[] nodes =
                Object.FindObjectsByType<AirSlingNode>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i = 0;
                 i < nodes.Length;
                 i++)
            {
                EnsureEnemyComponents(
                    nodes[i],
                    settings);
            }

            Debug.Log(
                $"Configured health / bear animation support on {nodes.Length} Sling Node(s).");
        }

        private static AirFlowHitSettings GetRegularNodeRewardSettings(
            AirFlowHitSystem hitSystem)
        {
            if (hitSystem == null)
                return null;

            SerializedObject serializedHitSystem =
                new SerializedObject(
                    hitSystem);

            SerializedProperty settingsProperty =
                serializedHitSystem.FindProperty(
                    "settings");

            return
                settingsProperty != null
                    ? settingsProperty.objectReferenceValue
                        as AirFlowHitSettings
                    : null;
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 18/Add Missing Impact Splash VFX")]
        public static void AddMissingImpactSplashVFX()
        {
            AirSlingSettings settings =
                GetOrCreateSettings();

            AirSlingNode[] nodes =
                Object.FindObjectsByType<AirSlingNode>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i = 0;
                 i < nodes.Length;
                 i++)
            {
                EnsureImpactSplash(
                    nodes[i],
                    settings);
            }

            AssetDatabase.SaveAssets();

            Debug.Log(
                $"Ensured impact splash VFX on {nodes.Length} Sling Node(s).");
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 18/Replace All Impact VFX With Settings Prefab")]
        public static void ReplaceAllImpactVFXWithSettingsPrefab()
        {
            AirSlingSettings settings =
                GetOrCreateSettings();

            if (settings.impactSplashPrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Sling Impact VFX",
                    "Assign your prefab to Impact Splash Prefab on the Batch18_AirSlingSettings asset first.",
                    "OK");

                Selection.activeObject =
                    settings;

                return;
            }

            AirSlingImpactVFX prefabVFX =
                settings.impactSplashPrefab
                    .GetComponentInChildren<AirSlingImpactVFX>(
                        true);

            if (prefabVFX == null)
            {
                EditorUtility.DisplayDialog(
                    "Sling Impact VFX",
                    "The assigned prefab must contain an AirSlingImpactVFX component.",
                    "OK");

                Selection.activeObject =
                    settings.impactSplashPrefab;

                return;
            }

            AirSlingNode[] nodes =
                Object.FindObjectsByType<AirSlingNode>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i = 0;
                 i < nodes.Length;
                 i++)
            {
                ReplaceImpactSplash(
                    nodes[i],
                    settings);
            }

            AssetDatabase.SaveAssets();

            Debug.Log(
                $"Replaced impact VFX on {nodes.Length} Sling Node(s) using " +
                $"{settings.impactSplashPrefab.name}.");
        }

        private static void EnsureEnemyComponents(
            AirSlingNode node,
            AirSlingSettings settings)
        {
            if (node == null)
                return;

            EnsureDefaultNodeVisual(
                node,
                settings,
                false);

            AirSlingNodeVisualAnimator visualAnimator =
                node.GetComponent<AirSlingNodeVisualAnimator>();

            if (visualAnimator == null)
            {
                visualAnimator =
                    Undo.AddComponent<AirSlingNodeVisualAnimator>(
                        node.gameObject);
            }

            Animator childAnimator =
                node.GetComponentInChildren<Animator>(
                    true);

            GameObject suggestedVisualRoot =
                FindTopLevelVisualRoot(
                    node,
                    childAnimator);

            if (childAnimator != null)
            {
                DisablePrototypeVisual(
                    node);
            }

            visualAnimator.Configure(
                childAnimator,
                suggestedVisualRoot);

            AirSlingNodeHealth health =
                node.GetComponent<AirSlingNodeHealth>();

            if (health == null)
            {
                health =
                    Undo.AddComponent<AirSlingNodeHealth>(
                        node.gameObject);
            }

            health.Configure(
                node,
                visualAnimator);

            AirSlingNodeWander wander =
                node.GetComponent<AirSlingNodeWander>();

            if (wander == null)
            {
                wander =
                    Undo.AddComponent<AirSlingNodeWander>(
                        node.gameObject);
            }

            wander.Configure(
                node,
                health,
                visualAnimator);

            AirSlingDeathReward deathReward =
                node.GetComponent<AirSlingDeathReward>();

            if (deathReward == null)
            {
                deathReward =
                    Undo.AddComponent<AirSlingDeathReward>(
                        node.gameObject);
            }

            AirPower playerAirPower =
                Object.FindAnyObjectByType<AirPower>();

            Transform playerTransform =
                playerAirPower != null
                    ? playerAirPower.transform
                    : null;

            AirFlowHitSystem regularHitSystem =
                Object.FindAnyObjectByType<AirFlowHitSystem>();

            AirFlowHitSettings regularRewardSettings =
                GetRegularNodeRewardSettings(
                    regularHitSystem);

            AirPresentationController presentationController =
                Object.FindAnyObjectByType<AirPresentationController>();

            AirPresentationSettings presentationSettings =
                GetAirPresentationSettings(
                    presentationController);

            deathReward.Configure(
                node,
                health,
                playerAirPower,
                playerTransform,
                regularRewardSettings,
                presentationSettings);

            node.SetHealth(
                health);

            node.SetVisualAnimator(
                visualAnimator);

            EditorUtility.SetDirty(
                visualAnimator);

            EditorUtility.SetDirty(
                health);

            EditorUtility.SetDirty(
                wander);

            EditorUtility.SetDirty(
                deathReward);

            EditorUtility.SetDirty(
                node);

            EditorSceneManager.MarkSceneDirty(
                node.gameObject.scene);
        }

        private static void EnsureDefaultNodeVisual(
            AirSlingNode node,
            AirSlingSettings settings,
            bool explicitApply)
        {
            if (node == null ||
                settings == null ||
                settings.defaultNodeVisualPrefab == null)
            {
                return;
            }

            Animator existingAnimator =
                node.GetComponentInChildren<Animator>(
                    true);

            Transform existingGenerated =
                node.transform.Find(
                    "Default Node Visual");

            if (existingAnimator != null)
            {
                GameObject existingVisualRoot =
                    FindTopLevelVisualRoot(
                        node,
                        existingAnimator);

                bool isGeneratedDefault =
                    existingVisualRoot != null &&
                    existingVisualRoot.name ==
                        "Default Node Visual";

                if (!isGeneratedDefault)
                {
                    // A manually parented bear/enemy visual already exists.
                    // Keep it and simply wire it through EnsureEnemyComponents.
                    DisablePrototypeVisual(
                        node);

                    return;
                }

                if (!explicitApply)
                {
                    DisablePrototypeVisual(
                        node);

                    return;
                }
            }

            if (existingGenerated != null)
            {
                Undo.DestroyObjectImmediate(
                    existingGenerated.gameObject);
            }

            if (explicitApply &&
                existingAnimator != null)
            {
                GameObject existingRoot =
                    FindTopLevelVisualRoot(
                        node,
                        existingAnimator);

                if (existingRoot != null &&
                    existingRoot.transform !=
                        node.transform &&
                    existingRoot.name ==
                        "Default Node Visual")
                {
                    Undo.DestroyObjectImmediate(
                        existingRoot);
                }
            }

            GameObject instance =
                PrefabUtility.InstantiatePrefab(
                    settings.defaultNodeVisualPrefab,
                    node.transform)
                as GameObject;

            if (instance == null)
            {
                Debug.LogError(
                    "Could not instantiate Default Node Visual Prefab.",
                    node);

                return;
            }

            Undo.RegisterCreatedObjectUndo(
                instance,
                "Create Sling Node Visual");

            instance.name =
                "Default Node Visual";

            Transform visualTransform =
                instance.transform;

            visualTransform.localPosition =
                settings.defaultVisualLocalPosition;

            visualTransform.localRotation =
                Quaternion.Euler(
                    settings
                        .defaultVisualLocalEulerAngles);

            visualTransform.localScale =
                settings.defaultVisualLocalScale;

            Animator animator =
                instance.GetComponentInChildren<Animator>(
                    true);

            if (animator == null)
            {
                Debug.LogWarning(
                    "The configured Sling Node visual prefab has no child Animator. " +
                    "The visual was still added, but Idle/Walk/Hurt/Die cannot play yet.",
                    instance);
            }

            DisablePrototypeVisual(
                node);

            EditorUtility.SetDirty(
                node);

            EditorSceneManager.MarkSceneDirty(
                node.gameObject.scene);
        }

        private static GameObject FindTopLevelVisualRoot(
            AirSlingNode node,
            Animator animator)
        {
            if (node == null ||
                animator == null)
            {
                return null;
            }

            Transform current =
                animator.transform;

            while (current.parent != null &&
                   current.parent != node.transform)
            {
                current =
                    current.parent;
            }

            return current.gameObject;
        }

        private static void DisablePrototypeVisual(
            AirSlingNode node)
        {
            if (node == null)
                return;

            Transform prototype =
                node.transform.Find(
                    "Prototype Visual");

            if (prototype != null &&
                prototype.gameObject.activeSelf)
            {
                Undo.RecordObject(
                    prototype.gameObject,
                    "Disable Prototype Sling Visual");

                prototype.gameObject.SetActive(
                    false);

                EditorUtility.SetDirty(
                    prototype.gameObject);
            }
        }

        private static void EnsureImpactSplash(
            AirSlingNode node,
            AirSlingSettings settings)
        {
            if (node == null)
                return;

            AirSlingImpactVFX existing =
                node.GetComponentInChildren<AirSlingImpactVFX>(
                    true);

            if (existing != null)
            {
                node.SetImpactVFX(
                    existing);

                EditorUtility.SetDirty(
                    node);

                return;
            }

            if (settings != null &&
                settings.impactSplashPrefab != null)
            {
                CreateImpactSplashFromPrefab(
                    node,
                    settings.impactSplashPrefab);

                return;
            }

            CreateFallbackImpactSplash(
                node);
        }

        private static void ReplaceImpactSplash(
            AirSlingNode node,
            AirSlingSettings settings)
        {
            if (node == null ||
                settings == null ||
                settings.impactSplashPrefab == null)
            {
                return;
            }

            AirSlingImpactVFX[] existing =
                node.GetComponentsInChildren<AirSlingImpactVFX>(
                    true);

            for (int i = 0;
                 i < existing.Length;
                 i++)
            {
                if (existing[i] == null)
                    continue;

                Undo.DestroyObjectImmediate(
                    existing[i].gameObject);
            }

            CreateImpactSplashFromPrefab(
                node,
                settings.impactSplashPrefab);
        }

        private static void CreateImpactSplashFromPrefab(
            AirSlingNode node,
            GameObject prefab)
        {
            if (node == null ||
                prefab == null)
            {
                return;
            }

            GameObject instance =
                PrefabUtility.InstantiatePrefab(
                    prefab,
                    node.transform)
                as GameObject;

            if (instance == null)
            {
                Debug.LogError(
                    "Could not instantiate the Sling impact VFX prefab.",
                    node);

                return;
            }

            Undo.RegisterCreatedObjectUndo(
                instance,
                "Create Sling Impact VFX");

            instance.name =
                prefab.name;

            instance.transform.position =
                node.LandingPosition;

            AirSlingImpactVFX impactVFX =
                instance.GetComponentInChildren<AirSlingImpactVFX>(
                    true);

            if (impactVFX == null)
            {
                Debug.LogError(
                    "The assigned impact VFX prefab does not contain AirSlingImpactVFX.",
                    instance);

                Undo.DestroyObjectImmediate(
                    instance);

                return;
            }

            node.SetImpactVFX(
                impactVFX);

            EditorUtility.SetDirty(
                node);

            EditorSceneManager.MarkSceneDirty(
                node.gameObject.scene);
        }

        private static void CreateFallbackImpactSplash(
            AirSlingNode node)
        {
            GameObject vfxObject =
                new GameObject(
                    "Impact Splash VFX");

            Undo.RegisterCreatedObjectUndo(
                vfxObject,
                "Create Sling Impact Splash");

            vfxObject.transform.SetParent(
                node.transform,
                false);

            vfxObject.transform.position =
                node.LandingPosition;

            vfxObject.transform.rotation =
                Quaternion.Euler(
                    -90f,
                    0f,
                    0f);

            AirSlingImpactVFX impactVFX =
                vfxObject.AddComponent<AirSlingImpactVFX>();

            ParticleSystem particles =
                vfxObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main =
                particles.main;

            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.35f;
            main.simulationSpace =
                ParticleSystemSimulationSpace.World;

            main.startLifetime =
                new ParticleSystem.MinMaxCurve(
                    0.32f,
                    0.72f);

            main.startSpeed =
                new ParticleSystem.MinMaxCurve(
                    4.5f,
                    9.0f);

            main.startSize =
                new ParticleSystem.MinMaxCurve(
                    0.045f,
                    0.13f);

            main.startColor =
                new ParticleSystem.MinMaxGradient(
                    new Color(
                        0.46f,
                        0.012f,
                        0.008f,
                        1f),
                    new Color(
                        0.95f,
                        0.055f,
                        0.025f,
                        1f));

            main.gravityModifier =
                new ParticleSystem.MinMaxCurve(
                    1.65f);

            main.maxParticles = 64;

            ParticleSystem.EmissionModule emission =
                particles.emission;

            emission.rateOverTime = 0f;

            ParticleSystem.Burst burst =
                new ParticleSystem.Burst(
                    0f,
                    16,
                    24);

            emission.SetBursts(
                new[]
                {
                    burst
                });

            ParticleSystem.ShapeModule shape =
                particles.shape;

            shape.enabled = true;
            shape.shapeType =
                ParticleSystemShapeType.Cone;

            shape.angle = 68f;
            shape.radius = 0.10f;
            shape.radiusThickness = 1f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particles.colorOverLifetime;

            colorOverLifetime.enabled = true;

            Gradient gradient =
                new Gradient();

            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(
                        Color.white,
                        0f),
                    new GradientColorKey(
                        new Color(
                            0.55f,
                            0.05f,
                            0.025f),
                        1f)
                },
                new[]
                {
                    new GradientAlphaKey(
                        1f,
                        0f),
                    new GradientAlphaKey(
                        1f,
                        0.58f),
                    new GradientAlphaKey(
                        0f,
                        1f)
                });

            colorOverLifetime.color =
                new ParticleSystem.MinMaxGradient(
                    gradient);

            ParticleSystemRenderer particleRenderer =
                particles.GetComponent<ParticleSystemRenderer>();

            particleRenderer.renderMode =
                ParticleSystemRenderMode.Billboard;

            Material material =
                GetOrCreateImpactParticleMaterial();

            if (material != null)
            {
                particleRenderer.sharedMaterial =
                    material;
            }

            impactVFX.Configure(
                new[]
                {
                    particles
                });

            node.SetImpactVFX(
                impactVFX);

            EditorUtility.SetDirty(
                node);

            EditorUtility.SetDirty(
                impactVFX);

            EditorSceneManager.MarkSceneDirty(
                node.gameObject.scene);
        }

        private static AirPresentationSettings GetAirPresentationSettings(
            AirPresentationController presentationController)
        {
            if (presentationController == null)
                return null;

            SerializedObject serializedPresentation =
                new SerializedObject(
                    presentationController);

            SerializedProperty settingsProperty =
                serializedPresentation.FindProperty(
                    "settings");

            return
                settingsProperty != null
                    ? settingsProperty.objectReferenceValue
                        as AirPresentationSettings
                    : null;
        }

        private static AirSlingTargetUISettings GetOrCreateTargetUISettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            AirSlingTargetUISettings settings =
                AssetDatabase.LoadAssetAtPath<AirSlingTargetUISettings>(
                    TargetUISettingsPath);

            if (settings == null)
            {
                settings =
                    ScriptableObject.CreateInstance<AirSlingTargetUISettings>();

                AssetDatabase.CreateAsset(
                    settings,
                    TargetUISettingsPath);

                AssetDatabase.SaveAssets();
            }

            return settings;
        }

        private static AirSlingImpactAudioLibrary GetOrCreateImpactAudioLibrary()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            AirSlingImpactAudioLibrary library =
                AssetDatabase.LoadAssetAtPath<AirSlingImpactAudioLibrary>(
                    ImpactAudioLibraryPath);

            if (library == null)
            {
                library =
                    ScriptableObject.CreateInstance<AirSlingImpactAudioLibrary>();

                AssetDatabase.CreateAsset(
                    library,
                    ImpactAudioLibraryPath);

                AssetDatabase.SaveAssets();
            }

            return library;
        }

        private static AirSlingSettings GetOrCreateSettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            AirSlingSettings settings =
                AssetDatabase.LoadAssetAtPath<AirSlingSettings>(
                    SettingsPath);

            if (settings == null)
            {
                settings =
                    ScriptableObject.CreateInstance<AirSlingSettings>();

                settings.EnsureCurrentDefaults();

                AssetDatabase.CreateAsset(
                    settings,
                    SettingsPath);
            }
            else
            {
                settings.EnsureCurrentDefaults();
            }

            EditorUtility.SetDirty(
                settings);

            AssetDatabase.SaveAssets();

            return settings;
        }

        private static Material GetOrCreateImpactParticleMaterial()
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    ImpactParticleMaterialPath);

            if (material != null)
                return material;

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Particles/Unlit");

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Particles/Standard Unlit");
            }

            if (shader == null)
                return null;

            material =
                new Material(
                    shader);

            material.name =
                "Batch18 Sling Impact Particle";

            if (material.HasProperty(
                    "_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    Color.white);
            }

            if (material.HasProperty(
                    "_Color"))
            {
                material.SetColor(
                    "_Color",
                    Color.white);
            }

            AssetDatabase.CreateAsset(
                material,
                ImpactParticleMaterialPath);

            return material;
        }

        private static Vector3 GetSuggestedNodePosition()
        {
            if (SceneView.lastActiveSceneView != null &&
                SceneView.lastActiveSceneView.camera != null)
            {
                Transform cameraTransform =
                    SceneView.lastActiveSceneView.camera.transform;

                return
                    cameraTransform.position +
                    cameraTransform.forward *
                    18f;
            }

            PlayerMotor motor =
                Object.FindAnyObjectByType<PlayerMotor>();

            if (motor != null)
            {
                Camera camera =
                    Camera.main;

                Vector3 forward =
                    camera != null
                        ? camera.transform.forward
                        : motor.transform.forward;

                forward.y =
                    Mathf.Max(
                        0.12f,
                        forward.y);

                return
                    motor.transform.position +
                    forward.normalized *
                    18f +
                    Vector3.up *
                    3f;
            }

            return
                Vector3.up *
                4f;
        }

        private static Material GetOrCreatePrototypeMaterial()
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    NodeMaterialPath);

            if (material != null)
                return material;

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit");

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Universal Render Pipeline/Simple Lit");
            }

            if (shader == null)
                shader = Shader.Find("Standard");

            material =
                new Material(
                    shader);

            material.name =
                "Batch18 Sling Node Prototype";

            Color color =
                new Color(
                    0.92f,
                    0.075f,
                    0.035f,
                    1f);

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor(
                    "_Color",
                    color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat(
                    "_Smoothness",
                    0.68f);
            }

            AssetDatabase.CreateAsset(
                material,
                NodeMaterialPath);

            AssetDatabase.SaveAssets();

            return material;
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
