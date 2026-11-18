using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Windrunner.TerrainAuthoring.Editor
{
    [Flags] public enum PrototypeCategories { Trees = 1, Details = 2, Layers = 4, All = 7 }

    // Adapted from Fejkboxarn/TD-Production Assets/Editor/Terrain*PrototypeCopier.cs
    // and TerrainTextureLayerCopier.cs. See Documentation/TerrainPrototypeTransfer.md.
    public static class TerrainPrototypeTransfer
    {
        public sealed class Preview
        {
            internal Terrain source, target;
            internal TerrainData sourceData, targetData;
            internal string sourceStamp, targetStamp;
            internal PrototypeCategories categories;
            internal TreePrototype[] trees;
            internal DetailPrototype[] details;
            internal TerrainLayer[] layers;
            internal bool consumed;
            public string Summary { get; internal set; }
            public int Added { get; internal set; }
        }

        public static void Validate(Terrain source, Terrain target, PrototypeCategories categories)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Transfer is available only in Edit Mode.");
            if (!source || !target || !source.terrainData || !target.terrainData)
                throw new ArgumentException("Assign a source and target Terrain, each with TerrainData.");
            if (source.terrainData == target.terrainData)
                throw new ArgumentException("Source and target must use separate TerrainData assets, even in different scenes.");
            if (categories == 0 || (categories & ~PrototypeCategories.All) != 0)
                throw new ArgumentException("Select at least one transfer category.");
            string path = AssetDatabase.GetAssetPath(target.terrainData);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !AssetDatabase.IsOpenForEdit(target.terrainData))
                throw new InvalidOperationException("Target must be an editable TerrainData asset under Assets. Attach an editable working copy first.");
            foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (terrain != target && terrain.terrainData == target.terrainData)
                    throw new InvalidOperationException("Another loaded Terrain shares the target data. Attach a separate working copy first.");
            var collider = target.GetComponent<TerrainCollider>();
            if (collider && collider.terrainData != target.terrainData)
                throw new InvalidOperationException("Target TerrainCollider references different TerrainData.");
            foreach (var tile in Object.FindObjectsByType<StudioTile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (tile.terrain == target || (tile.state && tile.state.working == target.terrainData))
                {
                    if (!tile.state || tile.state.working != target.terrainData)
                        throw new InvalidOperationException("Repair the Terrain Studio working-data binding first.");
                    if (tile.state.automatic)
                        throw new InvalidOperationException("Turn off Terrain Studio Automatic Updates before transfer; no Studio settings will be changed by this tool.");
                }
        }

        static string Stamp(TerrainData data) => EditorUtility.GetDirtyCount(data) + ":" +
            Hash128.Compute(EditorJsonUtility.ToJson(data));
        static TreePrototype Copy(TreePrototype p) => new TreePrototype
            { prefab = p.prefab, bendFactor = p.bendFactor, navMeshLod = p.navMeshLod };
        static bool Same(TreePrototype a, TreePrototype b) => a.prefab == b.prefab &&
            a.bendFactor == b.bendFactor && a.navMeshLod == b.navMeshLod;
        // Unity's copy constructor/equality include all Unity 6 detail settings, not just the visual asset.
        static DetailPrototype Copy(DetailPrototype p) => new DetailPrototype(p);
        static T[] Append<T>(T[] existing, T[] incoming, Func<T, T, bool> same, Func<T, T> copy)
        {
            var list = new List<T>(existing);
            foreach (var item in incoming)
                if (!list.Any(old => same(old, item))) list.Add(copy(item));
            return list.ToArray();
        }

        public static Preview CreatePreview(Terrain source, Terrain target, PrototypeCategories categories)
        {
            Validate(source, target, categories);
            var s = source.terrainData; var d = target.terrainData;
            var plan = new Preview { source = source, target = target, sourceData = s, targetData = d,
                categories = categories, sourceStamp = Stamp(s), targetStamp = Stamp(d),
                trees = d.treePrototypes, details = d.detailPrototypes, layers = d.terrainLayers };
            if ((categories & PrototypeCategories.Trees) != 0)
            {
                if (s.treePrototypes.Concat(d.treePrototypes).Any(p => p == null || !p.prefab))
                    throw new InvalidOperationException("Selected tree prototypes contain missing prefab references.");
                plan.trees = Append(plan.trees, s.treePrototypes, Same, Copy);
            }
            if ((categories & PrototypeCategories.Details) != 0)
            {
                foreach (var p in s.detailPrototypes.Concat(d.detailPrototypes))
                    if (p == null || !p.Validate(out var error))
                        throw new InvalidOperationException("Selected detail prototypes contain invalid settings or missing assets.");
                plan.details = Append(plan.details, s.detailPrototypes, (a, b) => a.Equals(b), Copy);
            }
            if ((categories & PrototypeCategories.Layers) != 0)
            {
                if (s.terrainLayers.Concat(d.terrainLayers).Any(p => !p))
                    throw new InvalidOperationException("Selected TerrainLayers contain missing references.");
                plan.layers = Append(plan.layers, s.terrainLayers, (a, b) => a == b, p => p);
                if (d.terrainLayers.Length == 0 && plan.layers.Length > 0)
                    throw new InvalidOperationException("Initialize the target's first TerrainLayer in Unity before transfer. Unity would otherwise initialize its paint, which this tool must not change.");
            }
            int trees = plan.trees.Length - d.treePrototypes.Length;
            int details = plan.details.Length - d.detailPrototypes.Length;
            int layers = plan.layers.Length - d.terrainLayers.Length;
            plan.Added = trees + details + layers;
            plan.Summary = $"Source data: {AssetDatabase.GetAssetPath(s)}\nTarget data: {AssetDatabase.GetAssetPath(d)}\n" +
                $"Trees: {d.treePrototypes.Length} existing, +{trees}\nGrass/details: {d.detailPrototypes.Length} existing, +{details}\n" +
                $"TerrainLayers: {d.terrainLayers.Length} existing, +{layers}\n" +
                "Matching entries are skipped. Existing entries and indices stay in order. New layer channels must remain zero. No replacement or painting.";
            // Validate native setters on an isolated clone before authorizing any write to the asset.
            var trial = Object.Instantiate(d);
            try { Write(plan, trial); AssertPaintPreserved(d, trial); }
            finally { Object.DestroyImmediate(trial); }
            return plan;
        }

        static void Write(Preview p, TerrainData d)
        {
            if (p.trees.Length != d.treePrototypes.Length) d.treePrototypes = p.trees;
            if (p.details.Length != d.detailPrototypes.Length) d.detailPrototypes = p.details;
            if (p.layers.Length != d.terrainLayers.Length) d.terrainLayers = p.layers;
        }

        public static void Apply(Preview p)
        {
            if (p == null || p.consumed) throw new InvalidOperationException("Create a fresh preview before applying.");
            Validate(p.source, p.target, p.categories);
            if (p.source.terrainData != p.sourceData || p.target.terrainData != p.targetData ||
                Stamp(p.sourceData) != p.sourceStamp || Stamp(p.targetData) != p.targetStamp)
                throw new InvalidOperationException("Terrain data changed after preview. Preview again before applying.");
            p.consumed = true;
            if (p.Added == 0) return;
            var before = Object.Instantiate(p.targetData);
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Append terrain prototypes");
            Undo.RegisterCompleteObjectUndo(p.targetData, "Append terrain prototypes");
            try
            {
                Write(p, p.targetData);
                AssertPaintPreserved(before, p.targetData);
                EditorUtility.SetDirty(p.targetData);
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
            finally { Object.DestroyImmediate(before); }
        }

        // Exact checks: adding zero-weight channels is allowed; changing existing paint is not.
        public static void AssertPaintPreserved(TerrainData before, TerrainData after)
        {
            void Equal(Array a, Array b, string what)
            {
                if (a.Length != b.Length || !a.Cast<object>().SequenceEqual(b.Cast<object>()))
                    throw new InvalidOperationException("Transfer changed " + what + "; apply was cancelled.");
            }
            if (before.size != after.size || before.heightmapResolution != after.heightmapResolution ||
                before.alphamapResolution != after.alphamapResolution || before.detailResolution != after.detailResolution)
                throw new InvalidOperationException("Terrain dimensions changed.");
            Equal(before.GetHeights(0, 0, before.heightmapResolution, before.heightmapResolution),
                after.GetHeights(0, 0, after.heightmapResolution, after.heightmapResolution), "heights");
            Equal(before.GetHoles(0, 0, before.holesResolution, before.holesResolution),
                after.GetHoles(0, 0, after.holesResolution, after.holesResolution), "holes");
            Equal(before.treeInstances, after.treeInstances, "tree instances");
            for (int i = 0; before.detailResolution > 0 && i < before.detailPrototypes.Length; i++)
                Equal(before.GetDetailLayer(0, 0, before.detailWidth, before.detailHeight, i),
                    after.GetDetailLayer(0, 0, after.detailWidth, after.detailHeight, i), "detail density at index " + i);
            for (int i = before.detailPrototypes.Length; after.detailResolution > 0 && i < after.detailPrototypes.Length; i++)
                if (after.GetDetailLayer(0, 0, after.detailWidth, after.detailHeight, i).Cast<int>().Any(v => v != 0))
                    throw new InvalidOperationException("New detail layer contains painted instances.");
            var a = before.GetAlphamaps(0, 0, before.alphamapWidth, before.alphamapHeight);
            var b = after.GetAlphamaps(0, 0, after.alphamapWidth, after.alphamapHeight);
            for (int y = 0; y < b.GetLength(0); y++)
                for (int x = 0; x < b.GetLength(1); x++)
                    for (int c = 0; c < b.GetLength(2); c++)
                        if (b[y, x, c] != (c < a.GetLength(2) ? a[y, x, c] : 0))
                            throw new InvalidOperationException("Unity changed alphamap weights; transfer was cancelled.");
        }
    }
}
