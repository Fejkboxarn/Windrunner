using System;
using System.IO;
using AirflowPrototype;
using UnityEngine;
using UnityEngine.SceneManagement;
using Windrunner.Production.Input;
using Windrunner.Production.Progression;
using Windrunner.Production.Unity;

namespace Windrunner.Frontend
{
    [DefaultExecutionOrder(-8500)]
    public sealed class PlayerSession : MonoBehaviour
    {
        public const string TitleScene="Assets/WindrunnerFrontend/Scenes/MainMenu.unity";
        public const string WorldScene="Assets/WindrunnerFrontend/Scenes/StartingWorld.unity";
        public ProjectRoot project;
        public WindrunnerInputHost input;
        public ProgressionCatalog catalog;
        public CharacterController character;
        public Transform arrival;
        public PlayerOrbitCamera orbit;
        public bool IsTitle => gameObject.scene.path==TitleScene;
        public bool Busy {get;private set;}
        public string SlotId {get;private set;}
        public string Error {get;private set;}
        public PlaythroughSlots Slots {get;private set;}
        public ProgressionService Progression {get;private set;}
        private IProgressionStore store;
        private float nextSave;
        private sealed class Transfer
        {
            public PlaythroughSlots slots;
            public string id;
            public IProgressionStore store;
            public ProgressionService service;
        }
        private static Transfer arriving;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTransfer(){arriving=null;}
        private void Awake()
        {
            var handoff=arriving;arriving=null;
            string folder=Application.isEditor?"EditorPlaythroughs-v1":"Playthroughs-v1";
            Slots=handoff?.slots ?? new PlaythroughSlots(Path.Combine(Application.persistentDataPath,folder));
            if(!project || !input || input.Input==null || !catalog)
            {
                Error="Session requires a project root, configured input host and progression catalog.";
                Debug.LogError(Error,this);enabled=false;return;
            }
            if(IsTitle)return;
            try
            {
                if(!arrival || !character || !orbit)throw new InvalidDataException("Starting world is missing its arrival bindings.");
                SlotId=handoff?.id;
                store=handoff?.store ?? new MemoryProgressionStore();
                Progression=handoff?.service ?? new ProgressionService(catalog.definitions,"local-player",store);
                project.Runtime.Services.Register(Progression);
                character.enabled=false;
                character.transform.SetPositionAndRotation(arrival.position,arrival.rotation);
                character.enabled=true;
                nextSave=Time.unscaledTime+60;
            }
            catch(Exception e){Error=e.Message;Debug.LogError("Session startup: "+e.Message,this);}
        }
        private void Start()
        {
            if(IsTitle || Error!=null)input.SetPaused(true);
        }
        private void Update()
        {
            if(IsTitle || Busy || SlotId==null || Time.unscaledTime<nextSave || input.IsPaused)return;
            nextSave=Time.unscaledTime+60;
            try{Save();}catch(Exception e){Error=e.Message;Debug.LogWarning("Autosave failed: "+Error,this);}
        }
        public void Save()
        {
            if(Busy || Progression==null || SlotId==null)throw new InvalidOperationException("Choose a playthrough before saving.");
            store.Commit(Progression.Snapshot());
            Error=null;nextSave=Time.unscaledTime+60;
        }
        public void Load(string id)
        {
            EnsureDestination(WorldScene);
            var selected=Slots.Open(id);
            var validated=new ProgressionService(catalog.definitions,"local-player",selected);
            Transition(WorldScene,new Transfer{slots=Slots,id=id,store=selected,service=validated});
        }
        public void Continue()
        {
            var latest=Slots.Latest();
            if(latest==null)throw new InvalidOperationException("No resumable playthrough is available.");
            Load(latest.Id);
        }
        public void NewGame(string label,string replace=null,bool confirmed=false,ProgressionState import=null)
        {
            EnsureDestination(WorldScene);
            var candidate=new MemoryProgressionStore();
            if(import!=null)candidate.Commit(import);
            using var preflight=new ProgressionService(catalog.definitions,"local-player",candidate);
            var created=Slots.Create(label,replace,confirmed,import);
            Load(created.slotId);
        }
        public void ReturnToTitle()
        {
            EnsureDestination(TitleScene);
            if(SlotId!=null)Save();
            Transition(TitleScene,new Transfer{slots=Slots});
        }
        private void EnsureDestination(string path)
        {
            if(Busy || arriving!=null)throw new InvalidOperationException("A world transition is already underway.");
            if(!Application.CanStreamedLevelBeLoaded(path))throw new InvalidOperationException("Scene missing from the build: "+path);
        }
        private void Transition(string path,Transfer handoff)
        {
            arriving=handoff;Busy=true;
            try
            {
                if(SceneManager.LoadSceneAsync(path,LoadSceneMode.Single)==null)throw new InvalidOperationException("Could not start scene load: "+path);
            }
            catch{Busy=false;arriving=null;handoff.service?.Dispose();throw;}
        }
#if UNITY_EDITOR
        public void UseTestDirectory(string directory)
        {
            if(!IsTitle || Busy)throw new InvalidOperationException();
            Slots=new PlaythroughSlots(directory);
        }
#endif
    }
}
