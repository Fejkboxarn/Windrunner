using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
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
            public Type ProfileType;
            public Entry Entry;
        }
        public readonly List<Entry> Entries = new List<Entry>();
        public readonly List<Binding> Bindings = new List<Binding>();
        public readonly GameObject Character;
        public bool Previewing { get; private set; }
        private double nextPreviewCheck;
        public string PreviewIssue { get; private set; }
        private void WatchPreview()
        {
            if(!Previewing || EditorApplication.timeSinceStartup<nextPreviewCheck)return;
            nextPreviewCheck=EditorApplication.timeSinceStartup+0.1;
            ValidatePreview();
        }
        public void ValidatePreview()
        {
            if(!Previewing)return;
            var errors=Validate();
            if(errors.Count>0){PreviewIssue=string.Join("\n",errors);Preview(false);}
            foreach(var power in Character.GetComponentsInChildren<AirPower>(true))power.ReconcileCapacity();
        }
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
                        if(property.propertyType != SerializedPropertyType.ObjectReference)continue;
                        var source=property.objectReferenceValue as ScriptableObject;
                        var fieldType=owner.GetType().GetField(property.name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.FieldType;
                        var profileType=source!=null?source.GetType():fieldType;
                        if(profileType==null || !typeof(ScriptableObject).IsAssignableFrom(profileType) || !SupportedType(profileType))continue;
                        var entry = source!=null?Add(source):null;
                        string ownerPath = owner.transform.IsChildOf(character.transform) || owner.transform==character.transform
                            ? AnimationUtility.CalculateTransformPath(owner.transform, character.transform)
                            : "@camera/" + AnimationUtility.CalculateTransformPath(owner.transform, motor.ViewCamera.transform);
                        Bindings.Add(new Binding { Owner=owner, Path=property.propertyPath, OwnerPath=ownerPath, Entry=entry, ProfileType=profileType });
                        entry?.Owners.Add(ownerPath + " : " + owner.GetType().Name + "." + property.propertyPath);
                    }
                }
            }
            Undo.undoRedoPerformed+=ValidatePreview;EditorApplication.update+=WatchPreview;
        }
        private static bool Supported(ScriptableObject source)
        {
            return SupportedType(source.GetType());
        }
        private static bool SupportedType(Type type)
        {
            string ns=type.Namespace ?? "";
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
            // Validate the entire proposed graph, never a parent draft against
            // its unchanged authored child. Restore reference identities afterward.
            for (int i=0;i<Entries.Count;i++)
                using(var so=new SerializedObject(Entries[i].Draft))
                {
                    var p=so.GetIterator();
                    while(p.Next(true))
                        if(p.propertyType==SerializedPropertyType.ObjectReference && p.objectReferenceValue is ScriptableObject child && Supported(child) && !Entries.Any(e=>e.Draft==child)) Add(child);
                }
            var restored=new List<(ScriptableObject owner,string path,Object value)>();
            try
            {
                foreach(var e in Entries)
                    using(var so=new SerializedObject(e.Draft))
                    {
                        var p=so.GetIterator();
                        while(p.Next(true))
                        {
                            if(p.propertyType!=SerializedPropertyType.ObjectReference)continue;
                            var child=Entries.Find(x=>x.Source==p.objectReferenceValue);
                            if(child==null)continue;
                            restored.Add((e.Draft,p.propertyPath,p.objectReferenceValue));p.objectReferenceValue=child.Draft;
                        }
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                return ValidateProposedGraph();
            }
            finally
            {
                foreach(var r in restored)using(var so=new SerializedObject(r.owner))
                {so.FindProperty(r.path).objectReferenceValue=r.value;so.ApplyModifiedPropertiesWithoutUndo();}
            }
        }
        private List<string> ValidateProposedGraph()
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
                if(entry.Draft is Windrunner.Production.Animation.WindrunnerAnimationProfile animation &&
                    !animation.ValidateForLiveGraph(out string reason)) errors.Add(entry.Source.name+": "+reason);
                bool standaloneGround=Entries.Any(e=>e.Draft is Windrunner.Production.Animation.WindrunnerAnimationProfile p &&
                    p.useStandaloneCharacterGraph && p.standaloneGroundProfile==entry.Draft);
                var validate=type.GetMethod(standaloneGround?"AssertStandaloneValid":"AssertValid",Type.EmptyTypes);
                if(validate!=null)try{validate.Invoke(entry.Draft,null);}catch(Exception ex){errors.Add(entry.Source.name+": "+(ex.InnerException??ex).Message);}
            }
            if(Entries.Count==0) errors.Add("No supported character module profiles found. Select the player root.");
            if(Character.GetComponent<PlayerMotor>() is PlayerMotor motor && motor.Settings==null) errors.Add("PlayerMotor has no movement profile.");
            var motorBinding=Bindings.Find(b=>b.Owner is PlayerMotor && b.Path=="settings");
            if(motorBinding?.Entry!=null)
                foreach(var b in Bindings.Where(b=>b.Path=="settings" && b.Entry!=null && b.Entry.Source is AirflowMovementSettings))
                    if(b.Entry.Source!=motorBinding.Entry.Source)errors.Add("Conflicting movement/resource/camera profile on "+b.Owner.GetType().Name+"; expected the motor's shared character module.");
            foreach(var component in Character.GetComponents<MonoBehaviour>())
            {
                if(component==null)continue;
                string name=component.GetType().Name;
                if(new[]{"PlayerMotor","AirDash","AirCaster","AirSlingController","AirPower","AirTargeter","AirSlingTargeter"}.Contains(name))
                    using(var so=new SerializedObject(component))
                    {var p=so.FindProperty("settings");if(p!=null && p.objectReferenceValue==null)errors.Add(name+" has no settings profile.");}
            }
            return errors;
        }
        public void Preview(bool enable)
        {
            if(enable && !Application.isPlaying) throw new InvalidOperationException("Live preview requires Play Mode.");
            if(enable && Validate().Count!=0) throw new InvalidOperationException(string.Join("\n",Validate()));
            foreach(var binding in Bindings)
            {
                if(binding.Owner==null || binding.Entry==null || !binding.Entry.PreviewSafe) continue;
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
            if(enable)PreviewIssue=null;
            foreach(var power in Character.GetComponentsInChildren<AirPower>(true))power.ReconcileCapacity();
        }
        public void Revert()
        {
            foreach(var e in Entries) { Undo.RecordObject(e.Draft,"Revert character draft"); EditorUtility.CopySerialized(e.Baseline,e.Draft); e.Draft.hideFlags=HideFlags.HideAndDontSave; }
            ValidatePreview();
        }
        public string SaveRestriction => Application.isPlaying && Entries.Any(e=>!e.PreviewSafe && Content(e.Draft)!=Content(e.Baseline))
            ? "Restart-only profiles changed: Save is blocked in Play Mode. Revert these drafts and author in Edit Mode. Stopping Play discards unsaved drafts." : null;
        public void Save() => SaveTransaction(null);
        // The callback is used by Editor regression checks to verify rollback after a write.
        internal void SaveTransaction(Action<int> afterPersist)
        {
            var errors=Validate(); if(errors.Count>0) throw new InvalidOperationException(string.Join("\n",errors));
            if(SaveRestriction!=null)throw new InvalidOperationException(SaveRestriction);
            foreach(var e in Entries) if(EditorJsonUtility.ToJson(e.Source)!=e.SavedJson) throw new InvalidOperationException(e.Source.name+" changed outside this session. Reload before saving to avoid overwriting it.");
            var changed=Entries.Where(e=>Content(e.Draft)!=Content(e.Baseline)).ToArray();
            var bytes=new Dictionary<Entry,byte[]>();var paths=new Dictionary<Entry,string>();
            foreach(var e in changed)
            {
                string path=AssetDatabase.GetAssetPath(e.Source);
                if(string.IsNullOrEmpty(path)||!File.Exists(path)||!AssetDatabase.IsOpenForEdit(e.Source))throw new InvalidOperationException("Profile is not an editable persistent asset: "+e.Source.name);
                paths[e]=path;bytes[e]=File.ReadAllBytes(path);
            }
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Save character tuning");
            try
            {
                foreach(var e in changed)
                {Undo.RecordObject(e.Source,"Save character tuning");var flags=e.Source.hideFlags;EditorUtility.CopySerialized(e.Draft,e.Source);e.Source.hideFlags=flags;EditorUtility.SetDirty(e.Source);}
                for(int i=0;i<changed.Length;i++){AssetDatabase.SaveAssetIfDirty(changed[i].Source);afterPersist?.Invoke(i);}
                Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
                foreach(var e in changed){e.SavedJson=EditorJsonUtility.ToJson(e.Source);EditorUtility.CopySerialized(e.Source,e.Baseline);e.Baseline.hideFlags=HideFlags.HideAndDontSave;}
            }
            catch
            {
                Undo.FlushUndoRecordObjects();Undo.RevertAllDownToGroup(group);
                foreach(var e in changed){EditorJsonUtility.FromJsonOverwrite(e.SavedJson,e.Source);File.WriteAllBytes(paths[e],bytes[e]);EditorUtility.ClearDirty(e.Source);}
                throw;
            }
        }
        public static string Content(ScriptableObject profile) => System.Text.RegularExpressions.Regex.Replace(
            EditorJsonUtility.ToJson(profile), "\"m_ObjectHideFlags\":\\s*\\d+", "\"m_ObjectHideFlags\":0");
        public void ApplyPreset(CharacterTuningPreset preset)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Apply presets only in Edit Mode.");
            if(preset==null)throw new ArgumentNullException(nameof(preset));
            var proposed=new List<(Binding binding,ScriptableObject value,Object original)>();
            var seen=new HashSet<Binding>();
            foreach(var p in preset.bindings)
            {
                var matches=Bindings.Where(b=>b.OwnerPath==p.ownerPath && b.Owner.GetType().AssemblyQualifiedName==p.componentType && b.Path==p.propertyPath).ToArray();
                if(matches.Length!=1 || p.profile==null || !matches[0].ProfileType.IsInstanceOfType(p.profile) || !EditorUtility.IsPersistent(p.profile))
                    throw new InvalidOperationException("Missing, ambiguous or incompatible preset binding: "+p.ownerPath+" / "+p.propertyPath);
                var b=matches[0];if(!seen.Add(b))throw new InvalidOperationException("Duplicate preset binding: "+p.propertyPath);
                using(var so=new SerializedObject(b.Owner))proposed.Add((b,p.profile,so.FindProperty(b.Path).objectReferenceValue));
            }
            // Validate the whole candidate composition with temporary reference
            // assignment, restoring exact bindings even if validation throws.
            try
            {
                foreach(var p in proposed)SetBinding(p.binding,p.value);
                using(var candidate=new CharacterTuningSession(Character))
                {var errors=candidate.Validate();if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));}
            }
            finally{foreach(var p in proposed)SetBinding(p.binding,p.original);}
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Apply character preset");
            try
            {
                Undo.RecordObjects(proposed.Select(p=>(Object)p.binding.Owner).Distinct().ToArray(),"Apply character preset");
                foreach(var p in proposed){SetBinding(p.binding,p.value);PrefabUtility.RecordPrefabInstancePropertyModifications(p.binding.Owner);}
                Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
            }
            catch{Undo.RevertAllDownToGroup(group);foreach(var p in proposed)SetBinding(p.binding,p.original);throw;}
        }
        private static void SetBinding(Binding binding,Object value)
        {using(var so=new SerializedObject(binding.Owner)){so.FindProperty(binding.Path).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}}
        public CharacterTuningPreset Capture(string name)
        {
            var preset=ScriptableObject.CreateInstance<CharacterTuningPreset>(); preset.characterName=name;
            foreach(var b in Bindings.Where(b=>b.Entry!=null)) preset.bindings.Add(new CharacterTuningPreset.Binding {ownerPath=b.OwnerPath, componentType=b.Owner.GetType().AssemblyQualifiedName,propertyPath=b.Path,profile=b.Entry.Source});
            return preset;
        }
        public void Dispose()
        {
            Undo.undoRedoPerformed-=ValidatePreview;EditorApplication.update-=WatchPreview;
            Preview(false);
            foreach(var e in Entries) {Object.DestroyImmediate(e.Draft);Object.DestroyImmediate(e.Baseline);}
            Entries.Clear();Bindings.Clear();
        }
    }
}
