using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    internal static class AutumnSandboxAssetResolver
    {
        public static List<GameObject> FindAutumnTreePrefabs(
            int maximum = 10)
        {
            List<ScoredAsset<GameObject>> scored =
                FindPrefabAssets(
                    path =>
                    {
                        string s = path.ToLowerInvariant();

                        if (!ContainsAny(
                                s,
                                "tree",
                                "maple",
                                "birch",
                                "oak",
                                "aspen",
                                "beech"))
                        {
                            return -1;
                        }

                        int score = 10;

                        if (ContainsAny(
                                s,
                                "autumn",
                                "fall",
                                "orange",
                                "yellow",
                                "red"))
                        {
                            score += 20;
                        }

                        if (ContainsAny(
                                s,
                                "dead",
                                "stump",
                                "sapling"))
                        {
                            score -= 6;
                        }

                        return score;
                    });

            List<GameObject> preferred =
                scored
                    .Where(x => x.score >= 20)
                    .Select(x => x.asset)
                    .Take(maximum)
                    .ToList();

            return preferred.Count > 0
                ? preferred
                : scored
                    .Select(x => x.asset)
                    .Take(maximum)
                    .ToList();
        }

        public static List<GameObject> FindRockPrefabs(
            int maximum = 14)
        {
            return FindPrefabAssets(
                    path =>
                    {
                        string s = path.ToLowerInvariant();

                        if (!ContainsAny(
                                s,
                                "rock",
                                "stone",
                                "boulder",
                                "cliff"))
                        {
                            return -1;
                        }

                        int score = 10;

                        if (ContainsAny(
                                s,
                                "large",
                                "big",
                                "cliff",
                                "boulder"))
                        {
                            score += 6;
                        }

                        if (ContainsAny(
                                s,
                                "decal",
                                "particle",
                                "vfx"))
                        {
                            score -= 12;
                        }

                        return score;
                    })
                .Select(x => x.asset)
                .Take(maximum)
                .ToList();
        }

        public static List<GameObject> FindGrassPrefabs(
            int maximum = 3)
        {
            return FindPrefabAssets(
                    path =>
                    {
                        string s = path.ToLowerInvariant();

                        if (!ContainsAny(
                                s,
                                "grass",
                                "fern",
                                "reed",
                                "meadow",
                                "groundcover"))
                        {
                            return -1;
                        }

                        if (ContainsAny(
                                s,
                                "tree",
                                "rock",
                                "decal",
                                "vfx"))
                        {
                            return -1;
                        }

                        int score = 10;

                        if (ContainsAny(
                                s,
                                "autumn",
                                "fall",
                                "dry",
                                "meadow"))
                        {
                            score += 5;
                        }

                        return score;
                    })
                .Select(x => x.asset)
                .Where(x =>
                    x.GetComponentInChildren<MeshFilter>(
                        true) != null)
                .Take(maximum)
                .ToList();
        }

        public static List<GameObject> FindFlowerPrefabs(
            int maximum = 2)
        {
            return FindPrefabAssets(
                    path =>
                    {
                        string s = path.ToLowerInvariant();

                        if (!ContainsAny(
                                s,
                                "flower",
                                "wildflower",
                                "daisy",
                                "poppy",
                                "heather"))
                        {
                            return -1;
                        }

                        if (ContainsAny(
                                s,
                                "tree",
                                "decal",
                                "vfx"))
                        {
                            return -1;
                        }

                        return 12;
                    })
                .Select(x => x.asset)
                .Where(x =>
                    x.GetComponentInChildren<MeshFilter>(
                        true) != null)
                .Take(maximum)
                .ToList();
        }

        public static TerrainLayer[] FindTerrainLayers()
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    "t:TerrainLayer");

            List<ScoredAsset<TerrainLayer>> layers =
                new List<ScoredAsset<TerrainLayer>>();

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

                TerrainLayer layer =
                    AssetDatabase.LoadAssetAtPath<TerrainLayer>(
                        path);

                if (layer == null)
                    continue;

                string s =
                    path.ToLowerInvariant();

                int score = 1;

                if (ContainsAny(
                        s,
                        "grass",
                        "moss",
                        "forest",
                        "autumn"))
                {
                    score += 14;
                }

                if (ContainsAny(
                        s,
                        "dirt",
                        "soil",
                        "ground",
                        "mud",
                        "path",
                        "road"))
                {
                    score += 12;
                }

                if (ContainsAny(
                        s,
                        "rock",
                        "stone",
                        "cliff"))
                {
                    score += 9;
                }

                layers.Add(
                    new ScoredAsset<TerrainLayer>(
                        layer,
                        score,
                        path));
            }

            List<TerrainLayer> result =
                new List<TerrainLayer>();

            AddBestMatchingLayer(
                layers,
                result,
                "grass",
                "moss",
                "forest",
                "autumn");

            AddBestMatchingLayer(
                layers,
                result,
                "dirt",
                "soil",
                "ground",
                "mud",
                "path",
                "road");

            AddBestMatchingLayer(
                layers,
                result,
                "rock",
                "stone",
                "cliff");

            foreach (ScoredAsset<TerrainLayer> candidate in
                     layers.OrderByDescending(x => x.score))
            {
                if (result.Count >= 4)
                    break;

                if (!result.Contains(candidate.asset))
                    result.Add(candidate.asset);
            }

            return result.ToArray();
        }

        public static List<Texture2D> FindTerrainTextures(
            int maximum = 6)
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    "t:Texture2D");

            List<ScoredAsset<Texture2D>> textures =
                new List<ScoredAsset<Texture2D>>();

            foreach (string guid in guids)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(guid);

                if (!path.StartsWith(
                        "Assets/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string s =
                    path.ToLowerInvariant();

                if (!ContainsAny(
                        s,
                        "grass",
                        "moss",
                        "ground",
                        "dirt",
                        "soil",
                        "mud",
                        "road",
                        "path",
                        "rock",
                        "stone",
                        "cliff"))
                {
                    continue;
                }

                if (ContainsAny(
                        s,
                        "normal",
                        "_n.",
                        "_n_",
                        "mask",
                        "height",
                        "rough",
                        "metal"))
                {
                    continue;
                }

                int score = 1;

                if (ContainsAny(
                        s,
                        "autumn",
                        "forest",
                        "grass",
                        "moss"))
                {
                    score += 10;
                }

                if (ContainsAny(
                        s,
                        "dirt",
                        "ground",
                        "soil",
                        "mud",
                        "road",
                        "path"))
                {
                    score += 9;
                }

                if (ContainsAny(
                        s,
                        "rock",
                        "stone",
                        "cliff"))
                {
                    score += 8;
                }

                Texture2D texture =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(
                        path);

                if (texture != null)
                {
                    textures.Add(
                        new ScoredAsset<Texture2D>(
                            texture,
                            score,
                            path));
                }
            }

            return textures
                .OrderByDescending(x => x.score)
                .ThenBy(x => x.path)
                .Select(x => x.asset)
                .Distinct()
                .Take(maximum)
                .ToList();
        }

        public static Material FindWaterMaterial()
        {
            return FindBestMaterial(
                "water",
                "river",
                "lake",
                "stream");
        }

        public static Material FindGroundOrRockMaterial()
        {
            return FindBestMaterial(
                "rock",
                "stone",
                "cliff",
                "ground",
                "grass",
                "moss");
        }

        public static string DescribeAssets(
            IReadOnlyList<GameObject> trees,
            IReadOnlyList<GameObject> rocks,
            IReadOnlyList<GameObject> grass,
            IReadOnlyList<GameObject> flowers,
            IReadOnlyList<TerrainLayer> layers,
            Material water)
        {
            return
                $"trees: {trees.Count}, " +
                $"rocks: {rocks.Count}, " +
                $"grass details: {grass.Count}, " +
                $"flowers: {flowers.Count}, " +
                $"terrain layers: {layers.Count}, " +
                $"water: {(water != null ? water.name : "fallback")}";
        }

        private static List<ScoredAsset<GameObject>> FindPrefabAssets(
            Func<string, int> scorePath)
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    "t:Prefab");

            List<ScoredAsset<GameObject>> result =
                new List<ScoredAsset<GameObject>>();

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

                int score =
                    scorePath(path);

                if (score < 0)
                    continue;

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        path);

                if (prefab == null ||
                    prefab.GetComponentInChildren<Renderer>(
                        true) == null)
                {
                    continue;
                }

                result.Add(
                    new ScoredAsset<GameObject>(
                        prefab,
                        score,
                        path));
            }

            return result
                .OrderByDescending(x => x.score)
                .ThenBy(x => x.path)
                .ToList();
        }

        private static Material FindBestMaterial(
            params string[] keywords)
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    "t:Material");

            ScoredAsset<Material> best =
                default;

            bool hasBest = false;

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

                string s =
                    path.ToLowerInvariant();

                int score = 0;

                foreach (string keyword in keywords)
                {
                    if (s.Contains(keyword))
                        score += 8;
                }

                if (score <= 0)
                    continue;

                Material material =
                    AssetDatabase.LoadAssetAtPath<Material>(
                        path);

                if (material == null)
                    continue;

                if (!hasBest ||
                    score > best.score)
                {
                    best =
                        new ScoredAsset<Material>(
                            material,
                            score,
                            path);

                    hasBest = true;
                }
            }

            return hasBest
                ? best.asset
                : null;
        }

        private static void AddBestMatchingLayer(
            List<ScoredAsset<TerrainLayer>> all,
            List<TerrainLayer> result,
            params string[] keywords)
        {
            ScoredAsset<TerrainLayer>? best = null;

            foreach (ScoredAsset<TerrainLayer> candidate in all)
            {
                string lower =
                    candidate.path.ToLowerInvariant();

                if (!ContainsAny(
                        lower,
                        keywords))
                {
                    continue;
                }

                if (best == null ||
                    candidate.score > best.Value.score)
                {
                    best = candidate;
                }
            }

            if (best != null &&
                !result.Contains(best.Value.asset))
            {
                result.Add(
                    best.Value.asset);
            }
        }

        private static bool ContainsAny(
            string source,
            params string[] values)
        {
            foreach (string value in values)
            {
                if (source.Contains(value))
                    return true;
            }

            return false;
        }

        private readonly struct ScoredAsset<T>
            where T : UnityEngine.Object
        {
            public readonly T asset;
            public readonly int score;
            public readonly string path;

            public ScoredAsset(
                T asset,
                int score,
                string path)
            {
                this.asset = asset;
                this.score = score;
                this.path = path;
            }
        }
    }
}
