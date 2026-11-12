using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace AirflowPrototype.Editor
{
    public sealed class CharacterTuningSession : IDisposable
    {
        public sealed class Entry
        {
            public ScriptableObject Source, Draft, Baseline;
            public string SavedJson;
            public bool PreviewSafe;
            public readonly List<string> Owners = new List<string>();
        }
        public sealed class Binding
        {
            public MonoBehaviour Owner;
            public string Path, OwnerPath;
            public Entry Entry;
        }
        public readonly List<Entry> Entries = new List<Entry>();
        public readonly List<Binding> Bindings = new List<Binding>();
        public readonly GameObject Character;
        public bool Previewing { get; private set; }
        private static readonly HashSet<string> LiveTypes = new HashSet<string> {
            "AirflowMovementSettings", "AirSlingSettings", "AirCastSettings", "AirTargetingSettings", "AirFlowHitSettings", "WaterSwimSettings" };
        public CharacterTuningSession(GameObject character)
        {
            Character = character != null ? character : throw new ArgumentNullException(nameof(character));
            var owners = character.GetComponentsInChildren<MonoBehaviour>(true).ToList();
            var motor = character.GetComponent<PlayerMotor>();
            if (motor != null && motor.ViewCamera != null) owners.AddRange(motor.ViewCamera.GetComponentsInChildren<MonoBehaviour>(true));
            foreach (var owner in owners.Where(x=>x!=null).Distinct())
            {
                if (!(owner.GetType().Namespace ?? "").StartsWith("AirflowPrototype") && !(owner.GetType().Namespace ?? "").StartsWith("Windrunner")) continue;
                using(var serialized = new SerializedObject(owner))
                {
                    var property = serialized.GetIterator();
                    while(property.NextVisible(true))
                    {
                        if(property.propertyType != SerializedPropertyType.ObjectReference || !(property.objectReferenceValue is ScriptableObject source) || !Supported(source)) continue;
                        var entry = Add(source);
                        string ownerPath = owner.transform.IsChildOf(character.transform) || owner.transform==character.transform
                            ? AnimationUtility.CalculateTransformPath(owner.transform, character.transform)
                            : "@camera/" + AnimationUtility.CalculateTransformPath(owner.transform, motor.ViewCamera.transform);
                        Bindings.Add(new Binding { Owner=owner, Path=property.propertyPath, OwnerPath=ownerPath, Entry=entry });
                        entry.Owners.Add(ownerPath + " : " + owner.GetType().Name + "." + property.propertyPath);
                    }
                }
            }
        }
        private static bool Supported(ScriptableObject source)
        {
            string ns=source.GetType().Namespace ?? "";
            return ns.StartsWith("AirflowPrototype") || ns.StartsWith("Windrunner") || ns.StartsWith("PortableAnimation");
        }
        private Entry Add(ScriptableObject source)
        {
            var existing=Entries.Find(e=>e.Source==source); if(existing!=null) return existing;
            var entry=new Entry { Source=source, Draft=Object.Instantiate(source), Baseline=Object.Instantiate(source), SavedJson=EditorJsonUtility.ToJson(source), PreviewSafe=LiveTypes.Contains(source.GetType().Name) };
            entry.Draft.name=source.name; entry.Baseline.name=source.name;
            entry.Draft.hideFlags=entry.Baseline.hideFlags=HideFlags.HideAndDontSave;
            Entries.Add(entry);
            using(var serialized=new SerializedObject(source))
            {
                var property=serialized.GetIterator();
                while(property.NextVisible(true)) if(property.propertyType==SerializedPropertyType.ObjectReference && property.objectReferenceValue is ScriptableObject child && Supported(child)) Add(child);
            }
            return entry;
        }
        public List<string> Validate()
        {
            var errors=new List<string>();
            foreach(var entry in Entries)
            {
                var type=entry.Draft.GetType();
                foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                {
                    if(field.FieldType!=typeof(float) && field.FieldType!=typeof(int)) continue;
                    double value=Convert.ToDouble(field.GetValue(entry.Draft));
                    var min=field.GetCustomAttribute<MinAttribute>(); var range=field.GetCustomAttribute<RangeAttribute>();
                    if(double.IsNaN(value)||double.IsInfinity(value)||(min!=null && value<min.min)||(range!=null && (value<range.min||value>range.max))) errors.Add(entry.Source.name+" / "+field.Name+": outside authored finite range.");
                }
                if(entry.Draft is AirflowMovementSettings movement && movement.minCameraPitch>=movement.maxCameraPitch) errors.Add("Camera minimum pitch must be below maximum pitch.");
                if(entry.Draft is AirSlingSettings sling && sling.perfectTime>sling.fullTimingTime) errors.Add("Sling perfect time exceeds full timing time.");
            }
            if(Entries.Count==0) errors.Add("No supported character module profiles found. Select the player root.");
            if(Character.GetComponent<PlayerMotor>() is PlayerMotor motor && motor.Settings==null) errors.Add("PlayerMotor has no movement profile.");
            return errors;
        }
        public void Preview(bool enable)
        {
            if(enable && !Application.isPlaying) throw new InvalidOperationException("Live preview requires Play Mode.");
            if(enable && Validate().Count!=0) throw new InvalidOperationException(string.Join("\n",Validate()));
            foreach(var binding in Bindings)
            {
                if(binding.Owner==null || !binding.Entry.PreviewSafe) continue;
                using(var serialized=new SerializedObject(binding.Owner))
                {
                    var property=serialized.FindProperty(binding.Path);
                    if(property==null) continue;
                    // Do not overwrite a runtime rebind made by another owner.
                    if(enable && property.objectReferenceValue!=binding.Entry.Source && property.objectReferenceValue!=binding.Entry.Draft) continue;
                    if(!enable && property.objectReferenceValue!=binding.Entry.Draft) continue;
                    property.objectReferenceValue=enable?binding.Entry.Draft:binding.Entry.Source;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            Previewing=enable;
        }
        public void Revert()
        {
            foreach(var e in Entries) { Undo.RecordObject(e.Draft,"Revert character draft"); EditorUtility.CopySerialized(e.Baseline,e.Draft); e.Draft.hideFlags=HideFlags.HideAndDontSave; }
        }
        public void Save()
        {
            var errors=Validate(); if(errors.Count>0) throw new InvalidOperationException(string.Join("\n",errors));
            foreach(var e in Entries) if(EditorJsonUtility.ToJson(e.Source)!=e.SavedJson) throw new InvalidOperationException(e.Source.name+" changed outside this session. Reload before saving to avoid overwriting it.");
            foreach(var e in Entries)
            {
                if(Content(e.Draft)==Content(e.Baseline)) continue;
                Undo.RecordObject(e.Source,"Save character tuning");
                var flags=e.Source.hideFlags; EditorUtility.CopySerialized(e.Draft,e.Source); e.Source.hideFlags=flags;
                EditorUtility.SetDirty(e.Source); AssetDatabase.SaveAssetIfDirty(e.Source);
                e.SavedJson=EditorJsonUtility.ToJson(e.Source); EditorUtility.CopySerialized(e.Source,e.Baseline); e.Baseline.hideFlags=HideFlags.HideAndDontSave;
            }
        }
        public static string Content(ScriptableObject profile) => System.Text.RegularExpressions.Regex.Replace(
            EditorJsonUtility.ToJson(profile), "\"m_ObjectHideFlags\":\\s*\\d+", "\"m_ObjectHideFlags\":0");
        public CharacterTuningPreset Capture(string name)
        {
            var preset=ScriptableObject.CreateInstance<CharacterTuningPreset>(); preset.characterName=name;
            foreach(var b in Bindings) preset.bindings.Add(new CharacterTuningPreset.Binding {ownerPath=b.OwnerPath, componentType=b.Owner.GetType().AssemblyQualifiedName,propertyPath=b.Path,profile=b.Entry.Source});
            return preset;
        }
        public void Dispose()
        {
            Preview(false);
            foreach(var e in Entries) {Object.DestroyImmediate(e.Draft);Object.DestroyImmediate(e.Baseline);}
            Entries.Clear();Bindings.Clear();
        }
    }
}
