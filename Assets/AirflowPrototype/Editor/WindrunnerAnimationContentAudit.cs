using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class WindrunnerAnimationContentAudit
    {
        [Serializable] private sealed class ClipEntry
        {
            public string field, name, path;
            public float seconds;
            public bool humanoid, legacy;
        }
        [Serializable] private sealed class Report { public List<ClipEntry> clips = new List<ClipEntry>(); }

        public static void Run()
        {
            var profile = AssetDatabase.LoadAssetAtPath<PlayerHumanoidAnimationProfile>("Assets/AirflowPrototype/Generated/Batch19_PlayerAnimationProfile.asset");
            if (profile == null) throw new InvalidOperationException("Installed legacy character profile missing.");
            var report = new Report();
            using (var serialized = new SerializedObject(profile))
            {
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (property.objectReferenceValue is AnimationClip clip)
                        report.clips.Add(new ClipEntry { field = property.propertyPath, name = clip.name,
                            path = AssetDatabase.GetAssetPath(clip), seconds = clip.length,
                            humanoid = clip.humanMotion, legacy = clip.legacy });
                }
            }
            Directory.CreateDirectory("Logs/ProductionMigration");
            File.WriteAllText("Logs/ProductionMigration/animation-content-audit.json", JsonUtility.ToJson(report, true));
            Debug.Log("Animation audit recorded " + report.clips.Count + " configured clip references.");
        }
    }
}
