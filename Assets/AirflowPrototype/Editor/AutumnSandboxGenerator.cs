using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class AutumnSandboxGenerator
    {
        private const string SandboxRootName =
            "AUTUMN SANDBOX - GENERATED";

        private const string RootFolder =
            "Assets/AirflowPrototype";

        private const string GeneratedFolder =
            RootFolder + "/Generated";

        private const string SettingsPath =
            GeneratedFolder +
            "/Batch15_AutumnSandboxSettings.asset";

        private const string TerrainDataPath =
            GeneratedFolder +
            "/Batch15_AutumnSandboxTerrain.asset";

        private const string RiverMeshPath =
            GeneratedFolder +
            "/Batch15_RiverMesh.asset";

        private const string LakeMeshPrefix =
            GeneratedFolder +
            "/Batch15_LakeMesh_";

        private const string FallbackWaterMaterialPath =
            GeneratedFolder +
            "/Batch15_FallbackWater.mat";

        private static readonly Vector2[] BaseRiverPath =
        {
            new Vector2(-900f, 230f),
            new Vector2(-690f, 255f),
            new Vector2(-470f, 300f),
            new Vector2(-270f, 260f),
            new Vector2(-90f, 155f),
            new Vector2(110f, 45f),
            new Vector2(310f, -120f),
            new Vector2(510f, -350f),
            new Vector2(735f, -515f),
            new Vector2(940f, -600f)
        };

        private static readonly Vector2[] BaseLakeCenters =
        {
            new Vector2(-285f, 270f),
            new Vector2(505f, -350f)
        };

        private static readonly float[] BaseLakeRadii =
        {
            185f,
            155f
        };

        [MenuItem(
            "Tools/Airflow Prototype/Batch 15/Build Stable Terrain + Water")]
        public static void Build()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            AutumnSandboxSettings settings =
                GetOrCreateSettings();

            settings.EnsureCurrentDefaults();
            EditorUtility.SetDirty(settings);

            GameObject existing =
                GameObject.Find(
                    SandboxRootName);

            if (existing != null)
            {
                if (!settings.replaceExistingSandbox)
                {
                    EditorUtility.DisplayDialog(
                        "Batch 15",
                        "A generated sandbox already exists and Replace Existing Sandbox is disabled.",
                        "OK");

                    return;
                }

                UnityEngine.Object.DestroyImmediate(
                    existing);
            }

            GameObject root =
                new GameObject(
                    SandboxRootName);

            GameObject terrainRoot =
                CreateChild(
                    root.transform,
                    "Terrain");

            GameObject waterRoot =
                CreateChild(
                    root.transform,
                    "Water");

            Terrain terrain =
                CreateTerrain(
                    terrainRoot.transform,
                    settings);

            if (settings.generateWater)
            {
                Material waterMaterial =
                    settings.waterMaterialOverride != null
                        ? settings.waterMaterialOverride
                        : FindWaterMaterial();

                if (waterMaterial == null)
                    waterMaterial =
                        GetOrCreateFallbackWaterMaterial();

                CreateWater(
                    terrain,
                    settings,
                    waterMaterial,
                    waterRoot.transform);
            }

            EditorSceneManager.MarkSceneDirty(
                root.scene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject =
                root;

            Debug.Log(
                "Batch 15 v15.4 built: stable regenerated terrain + shallow water beds.");
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 15/Select Terrain + Water Settings")]
        public static void SelectSettings()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(GeneratedFolder);

            Selection.activeObject =
                GetOrCreateSettings();
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 15/Reset Terrain + Water Settings to v15.4 Defaults")]
        public static void ResetSettings()
        {
            AutumnSandboxSettings settings =
                GetOrCreateSettings();

            settings.ResetNumericDefaultsV154();

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Selection.activeObject =
                settings;
        }

        [MenuItem(
            "Tools/Airflow Prototype/Batch 15/Delete Generated Sandbox")]
        public static void DeleteGeneratedSandbox()
        {
            GameObject existing =
                GameObject.Find(
                    SandboxRootName);

            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);

            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager.GetActiveScene());
        }

        private static Terrain CreateTerrain(
            Transform parent,
            AutumnSandboxSettings settings)
        {
            // IMPORTANT:
            // Reuse the TerrainData asset instead of deleting/recreating it.
            // This makes repeated builds deterministic and avoids stale
            // TerrainData references/import timing causing a flat rebuild.
            TerrainData data =
                AssetDatabase.LoadAssetAtPath<TerrainData>(
                    TerrainDataPath);

            if (data == null)
            {
                data =
                    new TerrainData();

                AssetDatabase.CreateAsset(
                    data,
                    TerrainDataPath);
            }

            data.heightmapResolution =
                settings.heightmapResolution;

            data.size =
                new Vector3(
                    settings.terrainSize,
                    settings.terrainHeight,
                    settings.terrainSize);

            data.alphamapResolution =
                settings.alphamapResolution;

            data.baseMapResolution =
                1024;

            // Clear leftovers from older Batch 15 versions. This minimal
            // builder owns only terrain height/texture and water.
            data.SetTreeInstances(
                Array.Empty<TreeInstance>(),
                true);

            data.treePrototypes =
                Array.Empty<TreePrototype>();

            data.detailPrototypes =
                Array.Empty<DetailPrototype>();

            TerrainLayer[] layers =
                GetTerrainLayers(
                    settings);

            data.terrainLayers =
                layers;

            float[,] heights =
                GenerateHeights(
                    settings);

            // Full rewrite every build. Same settings + same seed = same map.
            data.SetHeights(
                0,
                0,
                heights);

            data.RefreshPrototypes();

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            GameObject terrainObject =
                Terrain.CreateTerrainGameObject(
                    data);

            terrainObject.name =
                "Autumn Sandbox Terrain";

            terrainObject.transform.SetParent(
                parent,
                false);

            terrainObject.transform.position =
                new Vector3(
                    -settings.terrainSize * 0.5f,
                    0f,
                    -settings.terrainSize * 0.5f);

            Terrain terrain =
                terrainObject.GetComponent<Terrain>();

            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 6f;
            terrain.basemapDistance = 800f;

            if (layers.Length > 0)
                PaintTerrainSimply(terrain);

            return terrain;
        }

        private static float[,] GenerateHeights(
            AutumnSandboxSettings settings)
        {
            int resolution =
                settings.heightmapResolution;

            float[,] heights =
                new float[
                    resolution,
                    resolution];

            float half =
                settings.terrainSize * 0.5f;

            for (int z = 0;
                 z < resolution;
                 z++)
            {
                float nz =
                    z /
                    (float)(resolution - 1);

                float worldZ =
                    nz *
                    settings.terrainSize -
                    half;

                for (int x = 0;
                     x < resolution;
                     x++)
                {
                    float nx =
                        x /
                        (float)(resolution - 1);

                    float worldX =
                        nx *
                        settings.terrainSize -
                        half;

                    float worldHeight =
                        EvaluateBaseTerrainHeight(
                            settings,
                            worldX,
                            worldZ);

                    if (settings.generateWater)
                    {
                        worldHeight -=
                            EvaluateWaterBedDepth(
                                settings,
                                new Vector2(
                                    worldX,
                                    worldZ));
                    }

                    worldHeight =
                        Mathf.Clamp(
                            worldHeight,
                            2f,
                            settings.terrainHeight - 4f);

                    heights[z, x] =
                        worldHeight /
                        settings.terrainHeight;
                }
            }

            return heights;
        }

        private static float EvaluateBaseTerrainHeight(
            AutumnSandboxSettings settings,
            float worldX,
            float worldZ)
        {
            float half =
                settings.terrainSize * 0.5f;

            float seedX =
                settings.randomSeed * 0.173f;

            float seedZ =
                settings.randomSeed * 0.287f;

            float warpX =
                (Mathf.PerlinNoise(
                    worldX / 520f + seedX + 71f,
                    worldZ / 520f + seedZ + 14f) -
                 0.5f) *
                90f;

            float warpZ =
                (Mathf.PerlinNoise(
                    worldX / 520f + seedX + 8f,
                    worldZ / 520f + seedZ + 93f) -
                 0.5f) *
                90f;

            float sx =
                worldX + warpX;

            float sz =
                worldZ + warpZ;

            float large =
                FractalNoise(
                    sx,
                    sz,
                    settings.largeNoiseScale,
                    seedX,
                    seedZ,
                    3,
                    0.52f);

            float medium =
                FractalNoise(
                    sx,
                    sz,
                    settings.mediumNoiseScale,
                    seedX + 43f,
                    seedZ + 19f,
                    3,
                    0.5f);

            float small =
                FractalNoise(
                    sx,
                    sz,
                    settings.smallNoiseScale,
                    seedX + 110f,
                    seedZ + 57f,
                    2,
                    0.48f);

            float ridgeSource =
                FractalNoise(
                    sx,
                    sz,
                    settings.ridgeNoiseScale,
                    seedX + 222f,
                    seedZ + 131f,
                    2,
                    0.52f);

            float ridge =
                1f -
                Mathf.Abs(
                    ridgeSource * 2f -
                    1f);

            ridge =
                ridge *
                ridge;

            float worldHeight =
                settings.baseHeight +
                (large - 0.5f) *
                2f *
                settings.largeNoiseAmplitude +
                (medium - 0.5f) *
                2f *
                settings.mediumNoiseAmplitude +
                (small - 0.5f) *
                2f *
                settings.smallNoiseAmplitude +
                ridge *
                settings.ridgeAmplitude;

            float edge =
                Mathf.Max(
                    Mathf.Abs(worldX),
                    Mathf.Abs(worldZ));

            float edge01 =
                Mathf.InverseLerp(
                    settings.terrainSize * 0.34f,
                    half,
                    edge);

            worldHeight +=
                Mathf.SmoothStep(
                    0f,
                    1f,
                    edge01) *
                settings.edgeLift;

            return worldHeight;
        }

        private static float EvaluateWaterBedDepth(
            AutumnSandboxSettings settings,
            Vector2 point)
        {
            float scale =
                settings.terrainSize /
                2000f;

            float deepest =
                0f;

            Vector2[] river =
                GetScaledRiverPath(
                    scale);

            float along;

            float riverDistance =
                DistanceToPolyline(
                    point,
                    river,
                    out along);

            float riverHalfWidth =
                settings.riverWidth *
                0.5f;

            if (riverDistance <
                riverHalfWidth)
            {
                float edge01 =
                    Mathf.Clamp01(
                        riverDistance /
                        Mathf.Max(
                            0.001f,
                            riverHalfWidth));

                float towardCenter =
                    1f -
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        edge01);

                float profile =
                    Mathf.Pow(
                        towardCenter,
                        settings.waterBedProfilePower);

                deepest =
                    Mathf.Max(
                        deepest,
                        settings.riverBedDepth *
                        profile);
            }

            for (int i = 0;
                 i < BaseLakeCenters.Length;
                 i++)
            {
                Vector2 center =
                    BaseLakeCenters[i] *
                    scale;

                float radius =
                    BaseLakeRadii[i] *
                    scale;

                float distance =
                    Vector2.Distance(
                        point,
                        center);

                if (distance >= radius)
                    continue;

                float radial =
                    Mathf.Clamp01(
                        distance /
                        Mathf.Max(
                            0.001f,
                            radius));

                float towardCenter =
                    1f -
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        radial);

                float profile =
                    Mathf.Pow(
                        towardCenter,
                        settings.waterBedProfilePower);

                deepest =
                    Mathf.Max(
                        deepest,
                        settings.lakeBedDepth *
                        profile);
            }

            return deepest;
        }

        private static float FractalNoise(
            float x,
            float z,
            float scale,
            float seedX,
            float seedZ,
            int octaves,
            float persistence)
        {
            float frequency = 1f;
            float amplitude = 1f;
            float total = 0f;
            float normalization = 0f;

            float safeScale =
                Mathf.Max(
                    0.001f,
                    scale);

            for (int i = 0;
                 i < octaves;
                 i++)
            {
                total +=
                    Mathf.PerlinNoise(
                        x / safeScale *
                        frequency +
                        seedX,
                        z / safeScale *
                        frequency +
                        seedZ) *
                    amplitude;

                normalization +=
                    amplitude;

                frequency *= 2f;
                amplitude *= persistence;
            }

            return normalization > 0f
                ? total / normalization
                : 0.5f;
        }

        private static void CreateWater(
            Terrain terrain,
            AutumnSandboxSettings settings,
            Material waterMaterial,
            Transform parent)
        {
            float scale =
                settings.terrainSize /
                2000f;

            Vector2[] riverPath =
                GetScaledRiverPath(
                    scale);

            GameObject river =
                new GameObject(
                    "Terrain Matched River");

            river.transform.SetParent(
                parent,
                false);

            MeshFilter riverFilter =
                river.AddComponent<MeshFilter>();

            MeshRenderer riverRenderer =
                river.AddComponent<MeshRenderer>();

            riverFilter.sharedMesh =
                SaveMesh(
                    BuildRiverMesh(
                        riverPath,
                        settings),
                    RiverMeshPath);

            riverRenderer.sharedMaterial =
                waterMaterial;

            int waterLayer =
                LayerMask.NameToLayer(
                    "Water");

            if (waterLayer >= 0)
                river.layer = waterLayer;

            river.AddComponent<AirWaterSurface>();

            for (int i = 0;
                 i < BaseLakeCenters.Length;
                 i++)
            {
                Vector2 center =
                    BaseLakeCenters[i] *
                    scale;

                float radius =
                    BaseLakeRadii[i] *
                    scale;

                GameObject lake =
                    new GameObject(
                        $"Terrain Matched Lake {i + 1:00}");

                lake.transform.SetParent(
                    parent,
                    false);

                MeshFilter filter =
                    lake.AddComponent<MeshFilter>();

                MeshRenderer renderer =
                    lake.AddComponent<MeshRenderer>();

                filter.sharedMesh =
                    SaveMesh(
                        BuildLakeMesh(
                            center,
                            radius,
                            settings),
                        LakeMeshPrefix +
                        $"{i + 1:00}.asset");

                renderer.sharedMaterial =
                    waterMaterial;

                if (waterLayer >= 0)
                    lake.layer = waterLayer;

                lake.AddComponent<AirWaterSurface>();
            }
        }

        private static Mesh BuildRiverMesh(
            Vector2[] path,
            AutumnSandboxSettings settings)
        {
            int rows =
                settings.riverLengthSamples;

            int crossSegments =
                settings.riverCrossSegments;

            int columns =
                crossSegments + 1;

            Vector3[] vertices =
                new Vector3[
                    rows *
                    columns];

            Vector2[] uvs =
                new Vector2[
                    vertices.Length];

            int[] triangles =
                new int[
                    (rows - 1) *
                    crossSegments *
                    6];

            float[] cumulative =
                BuildCumulativeLengths(
                    path);

            float totalLength =
                cumulative[
                    cumulative.Length - 1];

            float halfWidth =
                settings.riverWidth *
                0.5f;

            for (int row = 0;
                 row < rows;
                 row++)
            {
                float t =
                    row /
                    (float)(rows - 1);

                float distance =
                    totalLength *
                    t;

                Vector2 center =
                    SamplePolyline(
                        path,
                        cumulative,
                        distance);

                float step =
                    Mathf.Max(
                        1f,
                        totalLength /
                        rows);

                Vector2 before =
                    SamplePolyline(
                        path,
                        cumulative,
                        Mathf.Max(
                            0f,
                            distance - step));

                Vector2 after =
                    SamplePolyline(
                        path,
                        cumulative,
                        Mathf.Min(
                            totalLength,
                            distance + step));

                Vector2 tangent =
                    (after - before).normalized;

                if (tangent.sqrMagnitude <
                    0.0001f)
                {
                    tangent =
                        Vector2.right;
                }

                Vector2 normal =
                    new Vector2(
                        -tangent.y,
                        tangent.x);

                for (int col = 0;
                     col < columns;
                     col++)
                {
                    float across =
                        col /
                        (float)crossSegments;

                    float signedWidth =
                        Mathf.Lerp(
                            -halfWidth,
                            halfWidth,
                            across);

                    Vector2 xz =
                        center +
                        normal *
                        signedWidth;

                    // Water follows the ORIGINAL natural terrain surface.
                    // The final Terrain is depressed beneath this inside the
                    // water footprint, creating real depth.
                    float y =
                        EvaluateBaseTerrainHeight(
                            settings,
                            xz.x,
                            xz.y) +
                        settings.waterSurfaceOffset;

                    int index =
                        row *
                        columns +
                        col;

                    vertices[index] =
                        new Vector3(
                            xz.x,
                            y,
                            xz.y);

                    uvs[index] =
                        new Vector2(
                            across,
                            t * 12f);
                }
            }

            int tri = 0;

            for (int row = 0;
                 row < rows - 1;
                 row++)
            {
                for (int col = 0;
                     col < crossSegments;
                     col++)
                {
                    int a =
                        row *
                        columns +
                        col;

                    int b = a + 1;

                    int c =
                        (row + 1) *
                        columns +
                        col;

                    int d = c + 1;

                    triangles[tri++] = a;
                    triangles[tri++] = b;
                    triangles[tri++] = c;

                    triangles[tri++] = b;
                    triangles[tri++] = d;
                    triangles[tri++] = c;
                }
            }

            Mesh mesh =
                new Mesh();

            mesh.name =
                "Batch15 Stable River Surface";

            mesh.vertices =
                vertices;

            mesh.uv =
                uvs;

            mesh.triangles =
                triangles;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private static Mesh BuildLakeMesh(
            Vector2 center,
            float radius,
            AutumnSandboxSettings settings)
        {
            int rings =
                settings.lakeRadialRings;

            int segments =
                settings.lakeAngularSegments;

            int vertexCount =
                1 +
                rings *
                segments;

            Vector3[] vertices =
                new Vector3[
                    vertexCount];

            Vector2[] uvs =
                new Vector2[
                    vertexCount];

            vertices[0] =
                new Vector3(
                    center.x,
                    EvaluateBaseTerrainHeight(
                        settings,
                        center.x,
                        center.y) +
                    settings.waterSurfaceOffset,
                    center.y);

            uvs[0] =
                new Vector2(
                    0.5f,
                    0.5f);

            for (int ring = 1;
                 ring <= rings;
                 ring++)
            {
                float radial =
                    ring /
                    (float)rings;

                float ringRadius =
                    radius *
                    radial;

                for (int segment = 0;
                     segment < segments;
                     segment++)
                {
                    float angle =
                        segment /
                        (float)segments *
                        Mathf.PI *
                        2f;

                    Vector2 xz =
                        center +
                        new Vector2(
                            Mathf.Cos(angle),
                            Mathf.Sin(angle)) *
                        ringRadius;

                    int index =
                        1 +
                        (ring - 1) *
                        segments +
                        segment;

                    vertices[index] =
                        new Vector3(
                            xz.x,
                            EvaluateBaseTerrainHeight(
                                settings,
                                xz.x,
                                xz.y) +
                            settings.waterSurfaceOffset,
                            xz.y);

                    uvs[index] =
                        new Vector2(
                            0.5f +
                            Mathf.Cos(angle) *
                            radial *
                            0.5f,
                            0.5f +
                            Mathf.Sin(angle) *
                            radial *
                            0.5f);
                }
            }

            int[] triangles =
                new int[
                    segments * 3 +
                    (rings - 1) *
                    segments *
                    6];

            int tri = 0;

            for (int segment = 0;
                 segment < segments;
                 segment++)
            {
                int next =
                    (segment + 1) %
                    segments;

                triangles[tri++] = 0;
                triangles[tri++] =
                    1 + next;
                triangles[tri++] =
                    1 + segment;
            }

            for (int ring = 1;
                 ring < rings;
                 ring++)
            {
                int innerStart =
                    1 +
                    (ring - 1) *
                    segments;

                int outerStart =
                    1 +
                    ring *
                    segments;

                for (int segment = 0;
                     segment < segments;
                     segment++)
                {
                    int next =
                        (segment + 1) %
                        segments;

                    int a =
                        innerStart +
                        segment;

                    int b =
                        innerStart +
                        next;

                    int c =
                        outerStart +
                        segment;

                    int d =
                        outerStart +
                        next;

                    triangles[tri++] = a;
                    triangles[tri++] = b;
                    triangles[tri++] = c;

                    triangles[tri++] = b;
                    triangles[tri++] = d;
                    triangles[tri++] = c;
                }
            }

            Mesh mesh =
                new Mesh();

            mesh.name =
                "Batch15 Stable Lake Surface";

            mesh.vertices =
                vertices;

            mesh.uv =
                uvs;

            mesh.triangles =
                triangles;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private static Vector2[] GetScaledRiverPath(
            float scale)
        {
            Vector2[] river =
                new Vector2[
                    BaseRiverPath.Length];

            for (int i = 0;
                 i < river.Length;
                 i++)
            {
                river[i] =
                    BaseRiverPath[i] *
                    scale;
            }

            return river;
        }

        private static float DistanceToPolyline(
            Vector2 point,
            Vector2[] path,
            out float normalizedAlong)
        {
            float bestDistance =
                float.MaxValue;

            float bestAlong =
                0f;

            float totalLength =
                0f;

            float[] segmentLengths =
                new float[
                    path.Length - 1];

            for (int i = 0;
                 i < path.Length - 1;
                 i++)
            {
                segmentLengths[i] =
                    Vector2.Distance(
                        path[i],
                        path[i + 1]);

                totalLength +=
                    segmentLengths[i];
            }

            float traversed =
                0f;

            for (int i = 0;
                 i < path.Length - 1;
                 i++)
            {
                Vector2 a =
                    path[i];

                Vector2 b =
                    path[i + 1];

                Vector2 ab =
                    b - a;

                float lengthSq =
                    ab.sqrMagnitude;

                float t =
                    lengthSq >
                    0.0001f
                        ? Mathf.Clamp01(
                            Vector2.Dot(
                                point - a,
                                ab) /
                            lengthSq)
                        : 0f;

                Vector2 closest =
                    a +
                    ab *
                    t;

                float distance =
                    Vector2.Distance(
                        point,
                        closest);

                if (distance <
                    bestDistance)
                {
                    bestDistance =
                        distance;

                    bestAlong =
                        totalLength > 0f
                            ? (traversed +
                               segmentLengths[i] *
                               t) /
                              totalLength
                            : 0f;
                }

                traversed +=
                    segmentLengths[i];
            }

            normalizedAlong =
                bestAlong;

            return bestDistance;
        }

        private static float[] BuildCumulativeLengths(
            Vector2[] path)
        {
            float[] cumulative =
                new float[
                    path.Length];

            for (int i = 1;
                 i < path.Length;
                 i++)
            {
                cumulative[i] =
                    cumulative[i - 1] +
                    Vector2.Distance(
                        path[i - 1],
                        path[i]);
            }

            return cumulative;
        }

        private static Vector2 SamplePolyline(
            Vector2[] path,
            float[] cumulative,
            float distance)
        {
            if (path.Length == 0)
                return Vector2.zero;

            if (path.Length == 1)
                return path[0];

            float total =
                cumulative[
                    cumulative.Length - 1];

            distance =
                Mathf.Clamp(
                    distance,
                    0f,
                    total);

            for (int i = 0;
                 i < path.Length - 1;
                 i++)
            {
                float a =
                    cumulative[i];

                float b =
                    cumulative[i + 1];

                if (distance <= b ||
                    i == path.Length - 2)
                {
                    float t =
                        Mathf.InverseLerp(
                            a,
                            b,
                            distance);

                    return Vector2.Lerp(
                        path[i],
                        path[i + 1],
                        t);
                }
            }

            return path[
                path.Length - 1];
        }

        private static float SampleGround(
            Terrain terrain,
            Vector2 xz)
        {
            return
                terrain.SampleHeight(
                    new Vector3(
                        xz.x,
                        0f,
                        xz.y)) +
                terrain.transform.position.y;
        }

        private static TerrainLayer[] GetTerrainLayers(
            AutumnSandboxSettings settings)
        {
            if (settings.terrainLayerOverrides != null)
            {
                TerrainLayer[] valid =
                    settings.terrainLayerOverrides
                        .Where(x => x != null)
                        .Distinct()
                        .ToArray();

                if (valid.Length > 0)
                    return valid;
            }

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:TerrainLayer");

            return guids
                .Select(
                    AssetDatabase.GUIDToAssetPath)
                .Where(
                    path =>
                        path.StartsWith(
                            "Assets/",
                            StringComparison.OrdinalIgnoreCase))
                .Select(
                    path =>
                        AssetDatabase.LoadAssetAtPath<TerrainLayer>(
                            path))
                .Where(x => x != null)
                .Take(4)
                .ToArray();
        }

        private static void PaintTerrainSimply(
            Terrain terrain)
        {
            TerrainData data =
                terrain.terrainData;

            int layerCount =
                data.terrainLayers.Length;

            if (layerCount == 0)
                return;

            int width =
                data.alphamapWidth;

            int height =
                data.alphamapHeight;

            float[,,] map =
                new float[
                    height,
                    width,
                    layerCount];

            for (int z = 0;
                 z < height;
                 z++)
            {
                float nz =
                    z /
                    (float)(height - 1);

                for (int x = 0;
                     x < width;
                     x++)
                {
                    float nx =
                        x /
                        (float)(width - 1);

                    float slope =
                        data.GetSteepness(
                            nx,
                            nz);

                    float rock =
                        Mathf.InverseLerp(
                            24f,
                            52f,
                            slope);

                    float baseWeight =
                        1f - rock;

                    for (int layer = 0;
                         layer < layerCount;
                         layer++)
                    {
                        map[z, x, layer] =
                            0.001f;
                    }

                    map[z, x, 0] =
                        Mathf.Max(
                            0.001f,
                            baseWeight);

                    if (layerCount > 1)
                    {
                        map[z, x, 1] =
                            Mathf.Max(
                                0.001f,
                                rock);
                    }

                    float total = 0f;

                    for (int layer = 0;
                         layer < layerCount;
                         layer++)
                    {
                        total +=
                            map[z, x, layer];
                    }

                    for (int layer = 0;
                         layer < layerCount;
                         layer++)
                    {
                        map[z, x, layer] /=
                            total;
                    }
                }
            }

            data.SetAlphamaps(
                0,
                0,
                map);
        }

        private static Material FindWaterMaterial()
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    "t:Material");

            string[] keywords =
            {
                "water",
                "river",
                "lake"
            };

            foreach (string keyword in keywords)
            {
                foreach (string guid in guids)
                {
                    string path =
                        AssetDatabase.GUIDToAssetPath(
                            guid);

                    if (!path.StartsWith(
                            "Assets/",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!path.ToLowerInvariant()
                             .Contains(keyword))
                    {
                        continue;
                    }

                    Material material =
                        AssetDatabase.LoadAssetAtPath<Material>(
                            path);

                    if (material != null)
                        return material;
                }
            }

            return null;
        }

        private static Material GetOrCreateFallbackWaterMaterial()
        {
            Material existing =
                AssetDatabase.LoadAssetAtPath<Material>(
                    FallbackWaterMaterialPath);

            if (existing != null)
                return existing;

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit");

            if (shader == null)
                shader =
                    Shader.Find(
                        "Universal Render Pipeline/Simple Lit");

            if (shader == null)
                shader =
                    Shader.Find("Standard");

            Material material =
                new Material(shader);

            material.name =
                "Batch15 Minimal Fallback Water";

            if (material.HasProperty(
                    "_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    new Color(
                        0.08f,
                        0.28f,
                        0.34f,
                        0.72f));
            }

            if (material.HasProperty(
                    "_Smoothness"))
            {
                material.SetFloat(
                    "_Smoothness",
                    0.9f);
            }

            AssetDatabase.CreateAsset(
                material,
                FallbackWaterMaterialPath);

            AssetDatabase.SaveAssets();

            return material;
        }

        private static Mesh SaveMesh(
            Mesh mesh,
            string path)
        {
            Mesh old =
                AssetDatabase.LoadAssetAtPath<Mesh>(
                    path);

            if (old != null)
                AssetDatabase.DeleteAsset(path);

            AssetDatabase.CreateAsset(
                mesh,
                path);

            return mesh;
        }

        private static AutumnSandboxSettings GetOrCreateSettings()
        {
            AutumnSandboxSettings settings =
                AssetDatabase.LoadAssetAtPath<AutumnSandboxSettings>(
                    SettingsPath);

            if (settings == null)
            {
                settings =
                    ScriptableObject.CreateInstance<AutumnSandboxSettings>();

                settings.EnsureCurrentDefaults();

                AssetDatabase.CreateAsset(
                    settings,
                    SettingsPath);
            }
            else
            {
                settings.EnsureCurrentDefaults();
                EditorUtility.SetDirty(settings);
            }

            AssetDatabase.SaveAssets();

            return settings;
        }

        private static GameObject CreateChild(
            Transform parent,
            string name)
        {
            GameObject child =
                new GameObject(name);

            child.transform.SetParent(
                parent,
                false);

            return child;
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
