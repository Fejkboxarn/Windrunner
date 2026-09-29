using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirflowPrototype.Editor
{
    public static class AirflowBatch1Setup
    {
        private const string RootFolder = "Assets/AirflowPrototype";
        private const string GeneratedFolder = RootFolder + "/Generated";
        private const string SettingsPath = GeneratedFolder + "/Batch1_MovementSettings.asset";
        private const string ScenePath = GeneratedFolder + "/Batch1_Playground.unity";

        [MenuItem("Tools/Airflow Prototype/Batch 1/Create Playground Scene")]
        public static void CreatePlaygroundScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            AirflowMovementSettings settings = GetOrCreateSettings();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Batch1_Playground";

            GameObject environmentRoot = new GameObject("Environment");
            CreateGround(environmentRoot.transform);
            CreateTraversalShapes(environmentRoot.transform);

            GameObject player = CreatePlayer(settings, out PlayerMotor motor, out PlayerInputReader input);
            GameObject cameraObject = CreateCamera(player.transform, input, motor, settings);

            motor.Configure(
                player.GetComponent<CharacterController>(),
                input,
                cameraObject.transform,
                player.transform.Find("Visual"),
                settings);

            CreateSun();

            PlayerDebugHUD hud = player.AddComponent<PlayerDebugHUD>();
            hud.Configure(motor, input);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = player;

            Debug.Log(
                "Airflow Batch 1 playground created. Press Play, then test acceleration, abrupt turns, " +
                "jump timing, coyote time, buffered landing jumps, and camera feel.");
        }

        [MenuItem("Tools/Airflow Prototype/Batch 1/Select Movement Settings")]
        public static void SelectMovementSettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);
            Selection.activeObject = GetOrCreateSettings();
        }

        private static AirflowMovementSettings GetOrCreateSettings()
        {
            AirflowMovementSettings settings =
                AssetDatabase.LoadAssetAtPath<AirflowMovementSettings>(SettingsPath);

            if (settings != null)
                return settings;

            settings = ScriptableObject.CreateInstance<AirflowMovementSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static GameObject CreatePlayer(
            AirflowMovementSettings settings,
            out PlayerMotor motor,
            out PlayerInputReader input)
        {
            GameObject player = new GameObject("Airflow Player");
            player.transform.position = new Vector3(0f, 0.05f, 0f);

            // Ignore Raycast prevents the camera collision probe from hitting the player capsule.
            player.layer = LayerMask.NameToLayer("Ignore Raycast");

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 1f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.32f;
            controller.skinWidth = 0.04f;
            controller.minMoveDistance = 0f;

            input = player.AddComponent<PlayerInputReader>();
            motor = player.AddComponent<PlayerMotor>();

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.layer = player.layer;
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = Vector3.up;
            visual.transform.localScale = new Vector3(0.78f, 1f, 0.78f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            Material bodyMaterial = GetOrCreateMaterial(
                GeneratedFolder + "/Player.mat",
                new Color(0.82f, 0.88f, 0.92f));
            visual.GetComponent<Renderer>().sharedMaterial = bodyMaterial;

            // Small nose marker makes facing direction obvious while tuning turns.
            GameObject facing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            facing.name = "Facing Marker";
            facing.layer = player.layer;
            facing.transform.SetParent(visual.transform, false);
            facing.transform.localPosition = new Vector3(0f, 0.15f, 0.48f);
            facing.transform.localScale = new Vector3(0.22f, 0.18f, 0.32f);
            Object.DestroyImmediate(facing.GetComponent<Collider>());
            facing.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(
                GeneratedFolder + "/FacingMarker.mat",
                new Color(0.22f, 0.65f, 0.85f));

            return player;
        }

        private static GameObject CreateCamera(
            Transform target,
            PlayerInputReader input,
            PlayerMotor motor,
            AirflowMovementSettings settings)
        {
            GameObject cameraObject = new GameObject("Airflow Camera");
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = settings.baseFov;

            cameraObject.AddComponent<AudioListener>();

            PlayerOrbitCamera orbit = cameraObject.AddComponent<PlayerOrbitCamera>();
            orbit.Configure(target, input, settings, 1 << LayerMask.NameToLayer("Default"));

            PlayerSpeedFeedback feedback = cameraObject.AddComponent<PlayerSpeedFeedback>();
            feedback.Configure(camera, motor, settings);

            return cameraObject;
        }

        private static void CreateGround(Transform parent)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(0f, -0.25f, 0f);
            ground.transform.localScale = new Vector3(60f, 0.5f, 60f);
            ground.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(
                GeneratedFolder + "/Ground.mat",
                new Color(0.22f, 0.25f, 0.27f));
        }

        private static void CreateTraversalShapes(Transform parent)
        {
            Material obstacleMat = GetOrCreateMaterial(
                GeneratedFolder + "/Obstacle.mat",
                new Color(0.38f, 0.42f, 0.45f));

            CreateBox(parent, "Low Step", new Vector3(5f, 0.25f, 6f), new Vector3(3f, 0.5f, 3f), Vector3.zero, obstacleMat);
            CreateBox(parent, "Platform", new Vector3(10f, 0.75f, 1f), new Vector3(5f, 1.5f, 5f), Vector3.zero, obstacleMat);
            CreateBox(parent, "Gentle Ramp", new Vector3(-8f, 0.75f, 6f), new Vector3(7f, 0.5f, 4f), new Vector3(0f, 0f, 14f), obstacleMat);
            CreateBox(parent, "Steeper Ramp", new Vector3(-10f, 1.1f, -5f), new Vector3(7f, 0.5f, 4f), new Vector3(0f, 0f, 24f), obstacleMat);
            CreateBox(parent, "Wall", new Vector3(6f, 1.5f, -7f), new Vector3(8f, 3f, 0.6f), Vector3.zero, obstacleMat);
        }

        private static void CreateBox(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Vector3 euler,
            Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            box.transform.eulerAngles = euler;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateSun()
        {
            GameObject sun = new GameObject("Directional Light");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }

        private static Material GetOrCreateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader)
            {
                color = color
            };

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            string parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            string name = Path.GetFileName(assetPath);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
