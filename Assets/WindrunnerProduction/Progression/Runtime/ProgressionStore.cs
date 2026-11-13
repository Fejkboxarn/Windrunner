using System;
using System.IO;
using UnityEngine;

namespace Windrunner.Production.Progression
{
    public interface IProgressionStore
    {
        ProgressionState Load();
        void Commit(ProgressionState state);
    }
    public static class ProgressionCodec
    {
        public static string Encode(ProgressionState state) => JsonUtility.ToJson(state);
        public static ProgressionState Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
                throw new InvalidDataException("Invalid progression save.");
            var state = JsonUtility.FromJson<ProgressionState>(json);
            if (state == null || string.IsNullOrWhiteSpace(state.playerId)) throw new InvalidDataException("Missing player identity.");
            // v0 prototype schema has the same records but no version marker.
            if (state.schemaVersion == 0) state.schemaVersion = 1;
            if (state.schemaVersion != 1) throw new InvalidDataException("Unsupported progression schema: " + state.schemaVersion);
            if (state.quests == null || state.relationships == null || state.rewards == null || state.cutscenes == null ||
                state.persistentFacts == null || state.challenges == null || state.puzzles == null || state.equipmentReferences == null)
                throw new InvalidDataException("Incomplete progression save.");
            foreach (var c in state.cutscenes)
                if (c.status == CinematicStatus.Playing) c.status = CinematicStatus.Pending;
            return state;
        }
        internal static ProgressionState Copy(ProgressionState state) => JsonUtility.FromJson<ProgressionState>(Encode(state));
    }
    /// <summary>Same-volume replacement; a failed write never advances the service's in-memory state.</summary>
    public sealed class FileProgressionStore : IProgressionStore
    {
        private readonly string path;
        public FileProgressionStore(string path) { this.path = Path.GetFullPath(path); }
        public ProgressionState Load()
        {
            if (!File.Exists(path)) return null;
            // Fail closed rather than silently overwrite an unsupported/corrupt save.
            return ProgressionCodec.Decode(File.ReadAllText(path));
        }
        public void Commit(ProgressionState state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(ProgressionCodec.Encode(state)); writer.Flush(); stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
    public sealed class MemoryProgressionStore : IProgressionStore
    {
        private string json;
        public ProgressionState Load() => json == null ? null : ProgressionCodec.Decode(json);
        public void Commit(ProgressionState state) { json = ProgressionCodec.Encode(state); }
    }
}
