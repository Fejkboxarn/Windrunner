using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Windrunner.TerrainAuthoring.Editor
{
    [Serializable] public sealed class EnvironmentSource
    {
        public string name, folder, biomes = "", include = "", exclude = "Demo;Examples", categories = "";
        public bool enabled = true;
        public bool Accept(string path)
        {
            if (!enabled || string.IsNullOrEmpty(folder) || !(path == folder || path.StartsWith(folder.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))) return false;
            bool Match(string terms) => terms.Split(';').Where(t => !string.IsNullOrWhiteSpace(t)).Any(t => path.IndexOf(t.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
            return (string.IsNullOrWhiteSpace(include) || Match(include)) && !Match(exclude);
        }
    }
    [Serializable] public sealed class EnvironmentMetadata
    {
        public string guid, category = "", biomes = "", publisher = "", notes = "", usage = "";
        public bool favorite;
        public int approval; // 0 follows approved registry, 1 explicitly approved, 2 rejected
        public long recent;
    }
    [Serializable] public sealed class EnvironmentPlacementSettings
    {
        public bool alignNormal, randomYaw, varyScale, snap = true, grid;
        public float yaw, scale = 1, minScale = .9f, maxScale = 1.1f, offset, gridSize = 1;
        public EnvironmentPlacementSettings Copy() => JsonUtility.FromJson<EnvironmentPlacementSettings>(JsonUtility.ToJson(this));
    }
    [Serializable] public sealed class EnvironmentCollection
    {
        public string name = "New collection", notes = "", biomes = "";
        public List<string> guids = new List<string>();
        public EnvironmentPlacementSettings placement = new EnvironmentPlacementSettings();
    }
    [Serializable] public sealed class EnvironmentLibrarySettings
    {
        public bool initialized;
        public List<EnvironmentSource> sources = new List<EnvironmentSource>();
        public List<EnvironmentMetadata> metadata = new List<EnvironmentMetadata>();
        public List<EnvironmentCollection> collections = new List<EnvironmentCollection>();
        public EnvironmentMetadata For(string guid)
        {
            var item = metadata.FirstOrDefault(m => m.guid == guid);
            if (item == null) { item = new EnvironmentMetadata { guid = guid }; metadata.Add(item); }
            return item;
        }
        public void Defaults()
        {
            if (initialized) return;
            initialized = true;
            // Verified installed roots, not publisher guesses from individual filenames.
            sources.Add(new EnvironmentSource { name = "TriForge / Fantasy Forest Environment", folder = "Assets/TriForge Assets/Fantasy Forest Environment", biomes = "Woodland;Meadow" });
            sources.Add(new EnvironmentSource { name = "Raygeas / Grasslands - Stylized Nature", folder = "Assets/Raygeas/Grasslands - Stylized Nature", biomes = "Grassland;Shoreline" });
            sources.Add(new EnvironmentSource { name = "Raygeas / HIGHLANDS - Stylized Environment", folder = "Assets/Raygeas/HIGHLANDS - Stylized Environment", biomes = "Highlands;Alpine" });
            foreach (string n in new[] { "Woodland palette", "Meadow palette", "Shoreline palette", "Highlands palette" }) collections.Add(new EnvironmentCollection { name = n });
        }
    }
    [FilePath("ProjectSettings/WindrunnerEnvironmentAuthoring.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class EnvironmentPreferences : ScriptableSingleton<EnvironmentPreferences>
    {
        public EnvironmentLibrarySettings library = new EnvironmentLibrarySettings();
        public void Persist() { library.Defaults(); Save(true); }
    }
    public sealed class EnvironmentEntry
    {
        public string guid, path, name, type, source, category, biomes;
        public bool missing;
    }
    // All AssetDatabase operations run in budgeted Editor update callbacks on the main thread.
    public sealed class EnvironmentCatalog : IDisposable
    {
        public static event Action<string[]> Imported;
        internal static void Notify(string[] paths) => Imported?.Invoke(paths);
        readonly EnvironmentLibrarySettings settings;
        readonly Queue<string> queue = new Queue<string>();
        readonly HashSet<string> queued = new HashSet<string>();
        public readonly Dictionary<string, EnvironmentEntry> entries = new Dictionary<string, EnvironmentEntry>();
        public event Action Changed;
        public int Pending => queue.Count;
        public int Processed { get; private set; }
        public EnvironmentCatalog(EnvironmentLibrarySettings settings)
        { this.settings = settings; Imported += OnImport; EditorApplication.update += Tick; }
        public void Dispose() { Imported -= OnImport; EditorApplication.update -= Tick; }
        public void Rescan()
        {
            foreach (var s in settings.sources.Where(s => s.enabled && AssetDatabase.IsValidFolder(s.folder)))
                foreach (var guid in AssetDatabase.FindAssets("", new[] { s.folder })) Enqueue(guid);
            foreach (var guid in entries.Keys.Concat(settings.metadata.Select(m => m.guid)).Concat(settings.collections.SelectMany(c => c.guids)).Distinct().ToArray()) Enqueue(guid);
        }
        void Enqueue(string guid) { if (!string.IsNullOrEmpty(guid) && queued.Add(guid)) queue.Enqueue(guid); }
        void OnImport(string[] paths)
        {
            foreach (string path in paths)
            {
                Enqueue(AssetDatabase.AssetPathToGUID(path));
                foreach (var e in entries.Values.Where(e => e.path == path || e.path.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase)).ToArray()) Enqueue(e.guid);
                // Imported/moved folders can contain assets without individual callbacks.
                if (AssetDatabase.IsValidFolder(path) && settings.sources.Any(s => s.Accept(path)))
                    foreach (var g in AssetDatabase.FindAssets("", new[] { path })) Enqueue(g);
            }
        }
        void Tick() => Process(40, .006);
        public void Process(int count = 10000, double seconds = 10)
        {
            double until = EditorApplication.timeSinceStartup + seconds; bool changed = false;
            while (queue.Count > 0 && count-- > 0 && EditorApplication.timeSinceStartup < until)
            {
                string guid = queue.Dequeue(); queued.Remove(guid); Processed++;
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var source = settings.sources.FirstOrDefault(s => s.Accept(path));
                var type = string.IsNullOrEmpty(path) ? null : AssetDatabase.GetMainAssetTypeAtPath(path);
                bool supported = type != null && (typeof(GameObject).IsAssignableFrom(type) || typeof(TerrainLayer).IsAssignableFrom(type) || typeof(Texture2D).IsAssignableFrom(type) || typeof(Material).IsAssignableFrom(type));
                if (source != null && supported)
                {
                    string category = Suggest(path, type);
                    if (!string.IsNullOrWhiteSpace(source.categories) && !source.categories.Split(';').Contains(category)) { entries.Remove(guid); changed = true; continue; }
                    entries[guid] = new EnvironmentEntry { guid = guid, path = path, name = System.IO.Path.GetFileNameWithoutExtension(path), source = source.name, type = typeof(GameObject).IsAssignableFrom(type) ? (path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase)?"Prefab":"Model") : type.Name, category = category, biomes = source.biomes }; changed = true;
                }
                else if (settings.metadata.Any(m => m.guid == guid) || settings.collections.Any(c => c.guids.Contains(guid)))
                {
                    entries.TryGetValue(guid, out var old);
                    entries[guid] = new EnvironmentEntry { guid = guid, path = path, name = old?.name ?? (string.IsNullOrEmpty(path) ? "Missing asset " + guid.Substring(0, Math.Min(8,guid.Length)) : System.IO.Path.GetFileNameWithoutExtension(path)), source = source?.name ?? "Outside enabled sources / unavailable", type = type?.Name ?? "Missing", category = "Unclassified", biomes = "", missing = true }; changed = true;
                }
                else { changed |= entries.Remove(guid); }
            }
            if (changed) Changed?.Invoke();
        }
        public static string Suggest(string path, Type type)
        {
            if (type == typeof(TerrainLayer)) return "TerrainLayers";
            // Folder evidence is a suggestion only. No inference from an asset's filename.
            string[] dirs = path.Replace('\\','/').Split('/');
            foreach (string category in Categories.Skip(1)) if (dirs.Take(dirs.Length - 1).Any(d => d.Equals(category, StringComparison.OrdinalIgnoreCase))) return category;
            return "Unclassified";
        }
        public static readonly string[] Categories = { "Unclassified", "Trees", "Bushes", "Grass", "Flowers", "Ground Decoration", "Rocks", "Cliffs", "Mountains", "Terrain Textures", "TerrainLayers", "Water", "Shoreline Assets", "Buildings", "Structures", "Miscellaneous Environment Props" };
        public List<EnvironmentEntry> Filter(string search, string category, string source, string biome, bool favorites, bool approved, EnvironmentCollection collection, bool recent)
        {
            var meta = settings.metadata.ToDictionary(m => m.guid);
            return entries.Values.Where(e => {
                meta.TryGetValue(e.guid, out var m);
                string c = string.IsNullOrEmpty(m?.category) ? e.category : m.category;
                string b = string.IsNullOrEmpty(m?.biomes) ? e.biomes : m.biomes;
                string s = string.IsNullOrEmpty(m?.publisher) ? e.source : m.publisher;
                return (string.IsNullOrEmpty(search) || (e.name+" "+c+" "+s+" "+b+" "+m?.notes).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0) &&
                    (string.IsNullOrEmpty(category) || c==category) && (string.IsNullOrEmpty(source) || s.IndexOf(source,StringComparison.OrdinalIgnoreCase)>=0) &&
                    (string.IsNullOrEmpty(biome) || b.IndexOf(biome,StringComparison.OrdinalIgnoreCase)>=0) && (!favorites || m?.favorite==true) &&
                    (!approved || (!e.missing && m?.approval!=2)) && (collection==null || collection.guids.Contains(e.guid));
            }).OrderByDescending(e => recent && meta.TryGetValue(e.guid,out var m) ? m.recent : 0).ThenBy(e=>e.name).ToList();
        }
    }
    sealed class EnvironmentAssetChanges : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
            => EnvironmentCatalog.Notify(imported.Concat(deleted).Concat(moved).Concat(oldPaths).ToArray());
    }
}
