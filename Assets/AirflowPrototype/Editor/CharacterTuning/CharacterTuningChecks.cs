using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AirflowPrototype.Editor
{
    public static class CharacterTuningChecks
    {
        public static void Run()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/AirflowPrototype/Generated/Batch1_Playground.unity");
                var motor=UnityEngine.Object.FindAnyObjectByType<PlayerMotor>();
                using(var session=new CharacterTuningSession(motor.gameObject))
                {
                    if(session.Entries.Count<8)throw new Exception("Character profile discovery incomplete.");
                    foreach(var entry in session.Entries)
                        if(CharacterTuningSession.Content(entry.Source)!=CharacterTuningSession.Content(entry.Draft))throw new Exception("Numerical/reference clone parity failed: "+entry.Source.name);
                    var movement=session.Entries.Single(e=>e.Source==motor.Settings);
                    if(session.Bindings.Count(b=>b.Entry==movement)<3)throw new Exception("Motor/camera/ability settings sharing was lost.");
                    var original=EditorJsonUtility.ToJson(motor.Settings);
                    ((AirflowMovementSettings)movement.Draft).maxGroundSpeed+=2;
                    if(EditorJsonUtility.ToJson(motor.Settings)!=original)throw new Exception("Draft mutated authored data.");
                    session.Revert();
                    if(CharacterTuningSession.Content(movement.Source)!=CharacterTuningSession.Content(movement.Draft))throw new Exception("Revert failed.");
                    ((AirflowMovementSettings)movement.Draft).gravity=-1;
                    if(session.Validate().Count==0)throw new Exception("Invalid range was accepted.");
                    session.Revert();
                    ((AirflowMovementSettings)movement.Draft).minCameraPitch=90;
                    if(session.Validate().Count==0)throw new Exception("Conflicting camera range was accepted.");
                    session.Revert();
                    var preset=session.Capture("Petra");
                    if(preset.bindings.Count!=session.Bindings.Count || preset.bindings.Any(b=>b.profile==null))throw new Exception("Preset lost a binding.");
                    UnityEngine.Object.DestroyImmediate(preset);
                    var animation=session.Entries.Single(e=>e.Draft is Windrunner.Production.Animation.WindrunnerAnimationProfile);
                    var profile=(Windrunner.Production.Animation.WindrunnerAnimationProfile)animation.Draft;
                    var actions=session.Entries.Single(e=>e.Source==profile.standaloneActionProfile);
                    int count=profile.standaloneActionProfile.Count;
                    using(var so=new SerializedObject(actions.Draft))
                    {so.FindProperty("actions").arraySize=count-1;so.ApplyModifiedPropertiesWithoutUndo();}
                    if(session.Validate().Count==0)throw new Exception("Removed referenced action slot was accepted against original child.");
                    session.Revert();
                    using(var so=new SerializedObject(actions.Draft))
                    {var list=so.FindProperty("actions");list.arraySize=count+1;list.GetArrayElementAtIndex(count).FindPropertyRelative("id").stringValue="Additional reserved action";so.ApplyModifiedPropertiesWithoutUndo();}
                    profile.actionBindings[0].montageSlot=count;
                    if(session.Validate().Count!=0)throw new Exception("New action and binding were not validated together: "+string.Join(";",session.Validate()));
                    session.Revert();
                    // Unchanged save must be a no-op for every authored asset.
                    var bytes=session.Entries.ToDictionary(e=>e.Source,e=>File.ReadAllBytes(AssetDatabase.GetAssetPath(e.Source)));
                    session.Save();
                    foreach(var pair in bytes)if(!pair.Value.SequenceEqual(File.ReadAllBytes(AssetDatabase.GetAssetPath(pair.Key))))throw new Exception("No-op save rewrote asset "+pair.Key.name);
                    Debug.Log("[Character Tuning] "+session.Entries.Count+" profile clones and "+session.Bindings.Count+" bindings preserve authored data; draft/revert/range/preset/no-op-save checks passed.");
                }
                VerifyExplicitSaveAndUndo(motor.Settings);
            }
            finally
            {
                if(setup.Any(s=>s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
        private static void VerifyExplicitSaveAndUndo(AirflowMovementSettings source)
        {
            string path=AssetDatabase.GenerateUniqueAssetPath("Assets/AirflowPrototype/Generated/TuningSaveTest.asset");
            var temporary=UnityEngine.Object.Instantiate(source);temporary.name="TuningSaveTest";
            var owner=new GameObject("Temporary tuning test owner");owner.SetActive(false);
            try
            {
                AssetDatabase.CreateAsset(temporary,path);
                var motor=owner.AddComponent<PlayerMotor>();
                using(var so=new SerializedObject(motor)){so.FindProperty("settings").objectReferenceValue=temporary;so.ApplyModifiedPropertiesWithoutUndo();}
                float initial=temporary.maxGroundSpeed;
                using(var session=new CharacterTuningSession(owner))
                {
                    var draft=(AirflowMovementSettings)session.Entries.Single().Draft;draft.maxGroundSpeed=initial+2;
                    if(temporary.maxGroundSpeed!=initial)throw new Exception("Draft leaked before Save.");
                    byte[] before=File.ReadAllBytes(path);
                    try{session.SaveTransaction(_=>throw new InvalidOperationException("Injected persistence failure"));throw new Exception("Rollback seam did not run");}
                    catch(InvalidOperationException ex){if(ex.Message!="Injected persistence failure")throw;}
                    if(temporary.maxGroundSpeed!=initial || !before.SequenceEqual(File.ReadAllBytes(path)))throw new Exception("Failed save left partial memory/disk changes.");
                    session.Save();
                    if(temporary.maxGroundSpeed!=initial+2 || EditorUtility.IsDirty(temporary))throw new Exception("Explicit save failed to persist.");
                    Undo.FlushUndoRecordObjects();Undo.PerformUndo();
                    if(temporary.maxGroundSpeed!=initial)throw new Exception("Saved value was not undoable.");
                    Undo.ClearUndo(temporary);
                    var preset=ScriptableObject.CreateInstance<CharacterTuningPreset>();
                    preset.bindings.Add(new CharacterTuningPreset.Binding{ownerPath="",componentType=typeof(PlayerMotor).AssemblyQualifiedName,propertyPath="settings",profile=temporary});
                    try
                    {
                        using(var so=new SerializedObject(motor)){so.FindProperty("settings").objectReferenceValue=null;so.ApplyModifiedPropertiesWithoutUndo();}
                        using(var empty=new CharacterTuningSession(owner))empty.ApplyPreset(preset);
                        if(motor.Settings!=temporary)throw new Exception("Preset failed to assign null profile slot.");
                        Undo.PerformUndo();if(motor.Settings!=null)throw new Exception("Preset Undo failed.");
                        temporary.gravity=-1;
                        try{using(var empty=new CharacterTuningSession(owner))empty.ApplyPreset(preset);throw new Exception("Invalid preset accepted");}
                        catch(InvalidOperationException){if(motor.Settings!=null)throw new Exception("Rejected preset left binding changed");}
                        temporary.gravity=source.gravity;
                    }
                    finally{UnityEngine.Object.DestroyImmediate(preset);Undo.ClearUndo(motor);}

                }
            }
            finally {Undo.ClearUndo(temporary);UnityEngine.Object.DestroyImmediate(owner);AssetDatabase.DeleteAsset(path);}
        }
        public static void CapturePetra()
        {
            EditorSceneManager.OpenScene("Assets/AirflowPrototype/Generated/Batch1_Playground.unity");
            var motor=UnityEngine.Object.FindAnyObjectByType<PlayerMotor>();
            using(var session=new CharacterTuningSession(motor.gameObject))
            {
                const string path="Assets/AirflowPrototype/Generated/PetraCharacterTuning.asset";
                if(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)!=null)throw new InvalidOperationException("Petra preset already exists; do not overwrite authored choices.");
                var preset=session.Capture("Petra");AssetDatabase.CreateAsset(preset,path);AssetDatabase.SaveAssetIfDirty(preset);
            }
        }
    }
}
