using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "AutumnSandboxSettings",
        menuName = "Airflow Prototype/Autumn Sandbox Settings")]
    public sealed class AutumnSandboxSettings : ScriptableObject
    {
        [SerializeField, HideInInspector]
        private int settingsVersion;

        [Header("Optional Asset Overrides")]
        [Tooltip("Optional terrain layers. Leave empty to auto-detect existing TerrainLayer assets.")]
        public TerrainLayer[] terrainLayerOverrides;

        [Tooltip("Optional water material. Leave empty to auto-detect a material containing Water/River/Lake in its name/path.")]
        public Material waterMaterialOverride;

        [Header("Terrain")]
        [Min(500f)]
        public float terrainSize = 2000f;

        [Min(100f)]
        public float terrainHeight = 340f;

        public int heightmapResolution = 1025;

        public int alphamapResolution = 512;

        [Header("Terrain Y Shape")]
        [Tooltip("General ground level before procedural height variation.")]
        [Range(0f, 120f)]
        public float baseHeight = 34f;

        [Tooltip("Very broad rolling landforms.")]
        [Range(0f, 160f)]
        public float largeNoiseAmplitude = 88f;

        [Tooltip("Medium hills layered over the broad landforms.")]
        [Range(0f, 90f)]
        public float mediumNoiseAmplitude = 34f;

        [Tooltip("Small terrain breakup. Keep this much lower than the large noise.")]
        [Range(0f, 40f)]
        public float smallNoiseAmplitude = 9f;

        [Tooltip("Adds broad ridge shapes without creating vertical cliffs.")]
        [Range(0f, 70f)]
        public float ridgeAmplitude = 22f;

        [Tooltip("Raises the outer region slightly so the map does not feel like an endless flat plane.")]
        [Range(0f, 100f)]
        public float edgeLift = 24f;

        [Header("Noise Scale")]
        [Min(100f)]
        public float largeNoiseScale = 720f;

        [Min(50f)]
        public float mediumNoiseScale = 260f;

        [Min(10f)]
        public float smallNoiseScale = 72f;

        [Min(50f)]
        public float ridgeNoiseScale = 390f;

        [Header("Water")]
        public bool generateWater = true;

        [Range(6f, 100f)]
        public float riverWidth = 34f;

        [Tooltip("Maximum terrain depression under the river center. Shoreline remains at the original terrain height.")]
        [Range(0f, 12f)]
        public float riverBedDepth = 4.0f;

        [Tooltip("Maximum terrain depression under lake centers. The depression smoothly fades to zero at the shoreline.")]
        [Range(0f, 20f)]
        public float lakeBedDepth = 6.5f;

        [Tooltip(
            "Controls how gently the terrain slopes down under water. " +
            "Higher values keep more of the depth toward the center and soften the shoreline transition.")]
        [Range(0.5f, 6f)]
        public float waterBedProfilePower = 2.2f;

        [Tooltip(
            "Water follows the original, un-depressed terrain surface and then applies this tiny render offset. " +
            "A slight negative value hides the seam at the shoreline.")]
        [Range(-0.10f, 0.10f)]
        public float waterSurfaceOffset = -0.01f;

        [Tooltip("Higher values let the river follow the original terrain surface more accurately.")]
        [Range(24, 256)]
        public int riverLengthSamples = 128;

        [Range(2, 16)]
        public int riverCrossSegments = 8;

        [Range(4, 32)]
        public int lakeRadialRings = 16;

        [Range(24, 192)]
        public int lakeAngularSegments = 96;

        [Header("Generation")]
        public int randomSeed = 84621;

        public bool replaceExistingSandbox = true;

        public int SettingsVersion => settingsVersion;

        private void OnEnable()
        {
            EnsureCurrentDefaults();
        }

        public void EnsureCurrentDefaults()
        {
            if (settingsVersion >= 154)
                return;

            ResetNumericDefaultsV154();
        }

        public void ResetNumericDefaultsV154()
        {
            TerrainLayer[] layers = terrainLayerOverrides;
            Material water = waterMaterialOverride;

            terrainSize = 2000f;
            terrainHeight = 340f;
            heightmapResolution = 1025;
            alphamapResolution = 512;

            baseHeight = 34f;
            largeNoiseAmplitude = 88f;
            mediumNoiseAmplitude = 34f;
            smallNoiseAmplitude = 9f;
            ridgeAmplitude = 22f;
            edgeLift = 24f;

            largeNoiseScale = 720f;
            mediumNoiseScale = 260f;
            smallNoiseScale = 72f;
            ridgeNoiseScale = 390f;

            generateWater = true;
            riverWidth = 34f;
            riverBedDepth = 4.0f;
            lakeBedDepth = 6.5f;
            waterBedProfilePower = 2.2f;
            waterSurfaceOffset = -0.01f;
            riverLengthSamples = 128;
            riverCrossSegments = 8;
            lakeRadialRings = 16;
            lakeAngularSegments = 96;

            randomSeed = 84621;
            replaceExistingSandbox = true;

            terrainLayerOverrides = layers;
            waterMaterialOverride = water;

            settingsVersion = 154;
        }

        private void OnValidate()
        {
            heightmapResolution =
                ClosestSupportedHeightmapResolution(
                    heightmapResolution);

            alphamapResolution =
                Mathf.Clamp(
                    Mathf.ClosestPowerOfTwo(
                        Mathf.Max(16, alphamapResolution)),
                    16,
                    2048);

            riverLengthSamples =
                Mathf.Clamp(
                    riverLengthSamples,
                    24,
                    256);

            riverCrossSegments =
                Mathf.Clamp(
                    riverCrossSegments,
                    2,
                    16);

            lakeRadialRings =
                Mathf.Clamp(
                    lakeRadialRings,
                    4,
                    32);

            lakeAngularSegments =
                Mathf.Clamp(
                    lakeAngularSegments,
                    24,
                    192);
        }

        private static int ClosestSupportedHeightmapResolution(
            int value)
        {
            int[] supported =
            {
                129,
                257,
                513,
                1025,
                2049,
                4097
            };

            int best = supported[0];
            int bestDistance =
                Mathf.Abs(value - best);

            for (int i = 1;
                 i < supported.Length;
                 i++)
            {
                int distance =
                    Mathf.Abs(
                        value - supported[i]);

                if (distance < bestDistance)
                {
                    best = supported[i];
                    bestDistance = distance;
                }
            }

            return best;
        }
    }
}
