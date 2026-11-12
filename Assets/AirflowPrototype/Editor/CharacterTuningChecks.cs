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
                    // Unchanged save must be a no-op for every authored asset.
                    var bytes=session.Entries.ToDictionary(e=>e.Source,e=>File.ReadAllBytes(AssetDatabase.GetAssetPath(e.Source)));
                    session.Save();
                    foreach(var pair in bytes)if(!pair.Value.SequenceEqual(File.ReadAllBytes(AssetDatabase.GetAssetPath(pair.Key))))throw new Exception("No-op save rewrote asset "+pair.Key.name);
                    Debug.Log("[Character Tuning] "+session.Entries.Count+" profile clones and "+session.Bindings.Count+" bindings preserve authored data; draft/revert/range/preset/no-op-save checks passed.");
                }
            }
            finally
            {
                if(setup.Any(s=>s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
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
