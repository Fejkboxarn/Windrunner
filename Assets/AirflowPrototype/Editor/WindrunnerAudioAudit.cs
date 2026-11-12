using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AirflowPrototype.Editor
{
    public static class WindrunnerAudioAudit
    {
        [Serializable] private class Entry { public string path, clip, scene, mixer; public Vector3 position, scale; public float volume, spatialBlend, min, max; public string rolloff; public bool loop, playing, enabled; }
        [Serializable] private class Report { public Entry[] sources; public string[] listeners; }
        private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
        [MenuItem("Tools/Windrunner/Acceptance/Report loaded audio sources")]
        public static void ReportLoaded()
        {
            var report = new Report {
                sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(s => new Entry {
                    path=PathOf(s.transform), clip=s.clip != null ? AssetDatabase.GetAssetPath(s.clip) : "<none>", scene=s.gameObject.scene.name,
                    position=s.transform.position, scale=s.transform.lossyScale, volume=s.volume, spatialBlend=s.spatialBlend,
                    min=s.minDistance, max=s.maxDistance, rolloff=s.rolloffMode.ToString(), loop=s.loop, playing=s.isPlaying,
                    enabled=s.isActiveAndEnabled, mixer=s.outputAudioMixerGroup != null ? s.outputAudioMixerGroup.name : "<direct>" }).ToArray(),
                listeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Where(l=>l.enabled).Select(l=>PathOf(l.transform)).ToArray() };
            Directory.CreateDirectory("Logs/ProductionMigration");
            File.WriteAllText("Logs/ProductionMigration/waterfall-audio-audit.json", JsonUtility.ToJson(report,true));
            Debug.Log(JsonUtility.ToJson(report,true));
        }
        public static void Run() { EditorSceneManager.OpenScene("Assets/AirflowPrototype/Generated/Batch1_Playground.unity"); ReportLoaded(); }
        [MenuItem("Tools/Windrunner/Acceptance/Select waterfall sources")]
        public static void SelectWaterfalls() { Selection.objects=UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(s=>s.name.Contains("Waterfall") || (s.clip != null && s.clip.name.Contains("Waterfall"))).Select(s=>(UnityEngine.Object)s.gameObject).ToArray(); }
    }
}
