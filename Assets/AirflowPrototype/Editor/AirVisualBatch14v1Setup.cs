using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AirflowPrototype.Editor
{
    public static class AirVisualBatch14v1Setup
    {
        private const string RootFolder =
            "Assets/AirflowPrototype";

        private const string GeneratedFolder =
            RootFolder + "/Generated";

        private const string BoldLookPath =
            GeneratedFolder +
            "/Batch14_v14.1_BoldAutumnLookSettings.asset";

        private const string SpeedSettingsPath =
            GeneratedFolder +
            "/Batch14_v14.1_SpeedPostProcessSettings.asset";

        private const string VolumeProfilePath =
            GeneratedFolder +
            "/Batch14_AutumnVolumeProfile.asset";

        private const string VolumeObjectName =
            "Airflow Autumn Global Volume";

        [MenuItem(
            "Tools/Airflow Prototype/Batch 14/v14.1 Install Bold Autumn + Speed Lens")]
        public static void Install()
        {
            Camera camera =
                Camera.main;

            if (camera == null)
                camera = Object.FindFirstObjectByType<Camera>();

            PlayerMotor player =
                Object.FindFirstObjectByType<PlayerMotor>();

            if (camera == null ||
                player == null)
            {
                EditorUtility.DisplayDialog(
                    "Airflow Batch 14 v14.1",
                    "A Camera and PlayerMotor are required in the current scene.",
                    "OK");

                return;
            }

            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            AutumnLookSettings look =
                GetOrCreateAsset<AutumnLookSettings>(
                    BoldLookPath);

            SpeedPostProcessSettings speed =
                GetOrCreateAsset<SpeedPostProcessSettings>(
                    SpeedSettingsPath);

            VolumeProfile profile =
                GetOrCreateVolumeProfile();

            EnsureVolumeComponent<ColorAdjustments>(profile);
            EnsureVolumeComponent<WhiteBalance>(profile);
            EnsureVolumeComponent<Bloom>(profile);
            EnsureVolumeComponent<Vignette>(profile);
            EnsureVolumeComponent<FilmGrain>(profile);
            EnsureVolumeComponent<ChromaticAberration>(profile);
            EnsureVolumeComponent<LensDistortion>(profile);
            EnsureVolumeComponent<MotionBlur>(profile);
            EnsureVolumeComponent<Tonemapping>(profile);

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
                    "Create Airflow Autumn Global Volume");
            }

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

            AutumnPostProcessController controller =
                volumeObject.GetComponent<AutumnPostProcessController>();

            if (controller == null)
            {
                controller =
                    Undo.AddComponent<AutumnPostProcessController>(
                        volumeObject);
            }

            controller.Configure(
                volume,
                look,
                speed,
                player);

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

            EditorUtility.SetDirty(volume);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(cameraData);

            EditorSceneManager.MarkSceneDirty(
                player.gameObject.scene);

            AssetDatabase.SaveAssets();

            Selection.activeObject =
                speed;

            Debug.Log(
                "Batch 14 v14.1 installed: bold autumn grade + speed-reactive Motion Blur, Vignette, Lens Distortion, Chromatic Aberration and Bloom. " +
                "Use Preview At Full Strength in SpeedPostProcessSettings to tune the high-speed look while standing still.");
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 14/v14.1 Select Bold Autumn Settings")]
        public static void SelectLook()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            Selection.activeObject =
                GetOrCreateAsset<AutumnLookSettings>(
                    BoldLookPath);
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 14/v14.1 Select Speed Lens Settings")]
        public static void SelectSpeed()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            Selection.activeObject =
                GetOrCreateAsset<SpeedPostProcessSettings>(
                    SpeedSettingsPath);
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
            EditorUtility.SetDirty(profile);

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
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            string parent =
                Path.GetDirectoryName(assetPath)?
                    .Replace("\\", "/");

            string name =
                Path.GetFileName(assetPath);

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
