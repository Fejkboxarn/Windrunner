using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AirflowPrototype.Editor
{
    public static class AirVisualBatch14Setup
    {
        private const string RootFolder =
            "Assets/AirflowPrototype";

        private const string GeneratedFolder =
            RootFolder + "/Generated";

        private const string AutumnSettingsPath =
            GeneratedFolder +
            "/Batch14_AutumnLookSettings.asset";

        private const string GlowSettingsPath =
            GeneratedFolder +
            "/Batch14_RewardGlowSettings.asset";

        private const string VolumeProfilePath =
            GeneratedFolder +
            "/Batch14_AutumnVolumeProfile.asset";

        private const string VolumeObjectName =
            "Airflow Autumn Global Volume";

        private const string PresentationSettingsPath =
            GeneratedFolder +
            "/Batch10_AirPresentationSettings.asset";

        [MenuItem(
            "Tools/Airflow Prototype/Batch 14/Install Autumn Look + Reward Glow")]
        public static void Install()
        {
            Camera camera =
                Camera.main;

            if (camera == null)
                camera =
                    Object.FindFirstObjectByType<Camera>();

            PlayerMotor player =
                Object.FindFirstObjectByType<PlayerMotor>();

            AirFlowHitSystem hitSystem =
                Object.FindFirstObjectByType<AirFlowHitSystem>();

            if (camera == null ||
                player == null ||
                hitSystem == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 14",
                    "Batch 14 needs a Camera, PlayerMotor and AirFlowHitSystem in the scene.",
                    "OK");

                return;
            }

            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            AutumnLookSettings autumnSettings =
                GetOrCreateAsset<AutumnLookSettings>(
                    AutumnSettingsPath);

            AirRewardGlowSettings glowSettings =
                GetOrCreateAsset<AirRewardGlowSettings>(
                    GlowSettingsPath);

            VolumeProfile profile =
                GetOrCreateVolumeProfile();

            EnsureVolumeComponents(
                profile);

            GameObject volumeObject =
                GameObject.Find(
                    VolumeObjectName);

            if (volumeObject == null)
            {
                volumeObject =
                    new GameObject(
                        VolumeObjectName);

                Undo.RegisterCreatedObjectUndo(
                    volumeObject,
                    "Create Autumn Global Volume");
            }

            // Put the volume on the camera layer, then explicitly include that
            // layer in the camera's volume mask.
            volumeObject.layer =
                camera.gameObject.layer;

            Volume volume =
                volumeObject.GetComponent<Volume>();

            if (volume == null)
                volume = Undo.AddComponent<Volume>(volumeObject);

            volume.isGlobal = true;
            volume.priority = 20f;
            volume.weight = 1f;
            volume.sharedProfile = profile;

            AutumnPostProcessController autumnController =
                volumeObject.GetComponent<AutumnPostProcessController>();

            if (autumnController == null)
            {
                autumnController =
                    Undo.AddComponent<AutumnPostProcessController>(
                        volumeObject);
            }

            autumnController.Configure(
                volume,
                autumnSettings);

            UniversalAdditionalCameraData cameraData =
                camera.GetUniversalAdditionalCameraData();

            cameraData.renderPostProcessing = true;

            LayerMask volumeMask =
                cameraData.volumeLayerMask;

            volumeMask.value |=
                1 << volumeObject.layer;

            cameraData.volumeLayerMask =
                volumeMask;

            camera.allowHDR = true;

            Transform visualRoot =
                player.transform.Find(
                    "Capsule Animation Root");

            if (visualRoot == null)
                visualRoot = player.transform;

            AirPresentationSettings presentationSettings =
                AssetDatabase.LoadAssetAtPath<AirPresentationSettings>(
                    PresentationSettingsPath);

            if (presentationSettings == null)
            {
                string[] guids =
                    AssetDatabase.FindAssets(
                        "t:AirPresentationSettings");

                if (guids.Length > 0)
                {
                    presentationSettings =
                        AssetDatabase.LoadAssetAtPath<AirPresentationSettings>(
                            AssetDatabase.GUIDToAssetPath(
                                guids[0]));
                }
            }

            AirRewardGlowFeedback glow =
                player.GetComponent<AirRewardGlowFeedback>();

            if (glow == null)
            {
                glow =
                    Undo.AddComponent<AirRewardGlowFeedback>(
                        player.gameObject);
            }

            glow.Configure(
                hitSystem,
                visualRoot,
                presentationSettings,
                glowSettings);

            EditorUtility.SetDirty(volume);
            EditorUtility.SetDirty(autumnController);
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(cameraData);
            EditorUtility.SetDirty(glow);

            EditorSceneManager.MarkSceneDirty(
                player.gameObject.scene);

            AssetDatabase.SaveAssets();

            Selection.activeObject =
                autumnSettings;

            Debug.Log(
                "Batch 14 installed. URP post-processing is enabled on the camera, " +
                "an autumn Global Volume is active, and reward arrival now pulses the player " +
                "using AirPresentationSettings.brightAirColor.");
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 14/Select Autumn Look Settings")]
        public static void SelectAutumnSettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            Selection.activeObject =
                GetOrCreateAsset<AutumnLookSettings>(
                    AutumnSettingsPath);
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 14/Select Reward Glow Settings")]
        public static void SelectGlowSettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            Selection.activeObject =
                GetOrCreateAsset<AirRewardGlowSettings>(
                    GlowSettingsPath);
        }

        private static VolumeProfile GetOrCreateVolumeProfile()
        {
            VolumeProfile profile =
                AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                    VolumeProfilePath);

            if (profile != null)
                return profile;

            profile =
                ScriptableObject.CreateInstance<VolumeProfile>();

            AssetDatabase.CreateAsset(
                profile,
                VolumeProfilePath);

            AssetDatabase.SaveAssets();

            return profile;
        }

        private static void EnsureVolumeComponents(
            VolumeProfile profile)
        {
            EnsureVolumeComponent<ColorAdjustments>(profile);
            EnsureVolumeComponent<WhiteBalance>(profile);
            EnsureVolumeComponent<Bloom>(profile);
            EnsureVolumeComponent<Vignette>(profile);
            EnsureVolumeComponent<Tonemapping>(profile);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        private static T EnsureVolumeComponent<T>(
            VolumeProfile profile)
            where T : VolumeComponent
        {
            if (profile.TryGet<T>(
                    out T existing))
            {
                return existing;
            }

            T component =
                profile.Add<T>(true);

            if (!AssetDatabase.Contains(component))
            {
                AssetDatabase.AddObjectToAsset(
                    component,
                    profile);
            }

            EditorUtility.SetDirty(component);

            return component;
        }

        private static T GetOrCreateAsset<T>(
            string path)
            where T : ScriptableObject
        {
            T asset =
                AssetDatabase.LoadAssetAtPath<T>(
                    path);

            if (asset != null)
                return asset;

            asset =
                ScriptableObject.CreateInstance<T>();

            AssetDatabase.CreateAsset(
                asset,
                path);

            AssetDatabase.SaveAssets();

            return asset;
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
