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
        public const string ZoneScene="Assets/WindrunnerProduction/World/Scenes/World_Zone01.unity";
        public ProgressionCatalog legacyCatalog, zoneCatalog;
        public ProjectRoot project;
        public WindrunnerInputHost input;
        public ProgressionCatalog catalog;
        public Windrunner.Daggers.DaggerPlayer daggers;
        public CharacterController character;
        public Transform arrival;
        public PlayerOrbitCamera orbit;
        public bool IsTitle => gameObject.scene.path==TitleScene;
        public string WorldId {get;private set;}
        public bool CanCopy => WorldId!=null && Progression!=null && !Busy && !retiredSlot;
        public bool Busy {get;private set;}
        public string SlotId {get;private set;}
        public string Error {get;private set;}
        public PlaythroughSlots Slots {get;private set;}
        public ProgressionService Progression {get;private set;}
        private IProgressionStore store;
        private float nextSave;
        private double retryAt;
        private int failedAttempts;
        private bool retiredSlot;
        private int? quitPreparedFrame;
        private AsyncOperation loading;
        private bool OwnsSlot => SlotId != null && Progression != null && !retiredSlot;

        private sealed class Transfer
        {
            public PlaythroughSlots slots;
            public string id, world;
            public IProgressionStore store;
            public bool reloadActiveSlot;
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
                // Disposable painted previews have no persistent route. They remain playable in memory.
                WorldId=gameObject.scene.path switch { WorldScene=>PlaythroughSlots.World, ZoneScene=>PlaythroughSlots.Zone01, _=>null };
                if(handoff!=null && (WorldId!=handoff.world || !handoff.service.MatchesDefinitions(catalog.definitions)))
                    throw new InvalidDataException("Loaded scene does not match the selected playthrough.");
                SlotId=handoff?.id;
                if(handoff?.reloadActiveSlot==true)
                {
                    // A return stream can commit to this slot after Load preflight and before scene teardown.
                    handoff.service.Dispose();
                    store=Slots.Open(SlotId,out var latestWorld);
                    if(latestWorld!=WorldId)throw new InvalidDataException("Playthrough destination changed during reload.");
                    Progression=new ProgressionService(catalog.definitions,"local-player",store);
                }
                else
                {
                    store=handoff?.store ?? new MemoryProgressionStore();
                    Progression=handoff?.service ?? new ProgressionService(catalog.definitions,"local-player",store);
                }
                project.Runtime.Services.Register(Progression);
                character.enabled=false;
                character.transform.SetPositionAndRotation(arrival.position,arrival.rotation);
                character.enabled=true;
                nextSave=Time.unscaledTime+60;
                if (OwnsSlot)
                {
                    Application.wantsToQuit += PrepareForQuit;
                    Progression.ReservePersistenceFailed += ReportSaveError;
                    Progression.OpenReserveFrame(Time.frameCount);
                }
            }
            catch(Exception e){Error=e.Message;Debug.LogError("Session startup: "+e.Message,this);}
        }
        private void Start()
        {
            if(IsTitle || Error!=null)input.SetPaused(true);
        }
        private void Update()
        {
            if (!OwnsSlot || Busy || quitPreparedFrame==Time.frameCount) return;
            quitPreparedFrame=null; // Another wantsToQuit subscriber can cancel a request.
            Progression.OpenReserveFrame(Time.frameCount);
            if(Time.unscaledTime<nextSave || input.IsPaused || Progression.ReserveWriteError!=null)return;
            nextSave=Time.unscaledTime+60;
            try{Save();}catch(Exception e){Error=e.Message;Debug.LogWarning("Autosave failed: "+Error,this);}
        }
        private void LateUpdate()
        {
            if (!OwnsSlot) return;
            Progression.CloseReserveFrame();
            if (!Progression.HasPendingReserve)
            {
                if (failedAttempts > 0) ClearSaveError();
                return;
            }
            var fault=Progression.ReserveWriteError;
            if (fault is PlaythroughRevisionException || Time.realtimeSinceStartupAsDouble < retryAt) return;
            TryFlush();
        }

        private void ReportSaveError(Exception e)
        {
            Error="Save failed. "+e.Message;
            // A late return can arrive after preflight but before scene activation. Keep
            // the outgoing service and its exact bound revision alive until it is durable.
            if (loading != null && !loading.isDone) loading.allowSceneActivation=false;
            if (failedAttempts == 0)
            {
                Debug.LogWarning(Error,this);
                if (input) input.SetPaused(true);
            }
            failedAttempts++;
            double delay=failedAttempts==1 ? .25 : failedAttempts==2 ? 1 : 5;
            retryAt=Time.realtimeSinceStartupAsDouble+delay;
        }

        private void ClearSaveError()
        {
            Error=null;failedAttempts=0;retryAt=0;
            if (loading != null && !loading.isDone) loading.allowSceneActivation=true;
        }

        private void FlushReserve()
        {
            if (!OwnsSlot) return;
            Progression.FlushPendingReserve();
            ClearSaveError();
        }

        public void RetryPendingReserve() => FlushReserve();

        private void TryFlush()
        {
            try { FlushReserve(); }
            catch (Exception) when (Progression.ReserveWriteError != null) { /* retained for retry */ }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && OwnsSlot) { Progression.CloseReserveFrame(); TryFlush(); }
        }

        private void OnDisable()
        {
            if (OwnsSlot) { Progression.CloseReserveFrame(); TryFlush(); }
        }

        private void OnDestroy()
        {
            Application.wantsToQuit -= PrepareForQuit;
            if (Progression != null) Progression.ReservePersistenceFailed -= ReportSaveError;
        }

        public bool PrepareForQuit()
        {
            if (!OwnsSlot || (quitPreparedFrame.HasValue && !Progression.HasPendingReserve)) return true;
            Progression.CloseReserveFrame();
            try
            {
                Progression.SaveSnapshot();
                quitPreparedFrame=Time.frameCount;
                return true;
            }
            catch (Exception e)
            {
                quitPreparedFrame=null;
                if (Progression.ReserveWriteError == null)
                {
                    Error="Save failed. "+e.Message;
                    Debug.LogWarning(Error,this);
                    if(input)input.SetPaused(true);
                }
                return false;
            }
        }

        private void OnApplicationQuit()
        {
            if (!PrepareForQuit()) Debug.LogWarning("Could not save on exit: "+Error,this);
        }

        public void Save()
        {
            if(Busy || !OwnsSlot)throw new InvalidOperationException("Choose a playthrough before saving.");
            Progression.SaveSnapshot();
            ClearSaveError();nextSave=Time.unscaledTime+60;
        }
        public void SaveCopy(string label,string replace=null,bool confirmed=false)
        {
            if(!CanCopy || (replace!=null && replace==SlotId))throw new InvalidOperationException("Choose a supported world and a separate destination slot.");
            FlushReserve();
            Slots.Create(label,replace,confirmed,Progression.Snapshot(),WorldId);
        }

        private static string SceneFor(string world)=>world switch
        {
            PlaythroughSlots.Zone01=>ZoneScene,
            PlaythroughSlots.World=>WorldScene,
            _=>throw new InvalidDataException("Unknown playthrough world.")
        };

        public ProgressionCatalog CatalogFor(string world)
        {
            string path=SceneFor(world);
            if(path==gameObject.scene.path)return catalog;
            var selected=world==PlaythroughSlots.World?legacyCatalog:zoneCatalog;
            if(!selected && IsTitle && world==PlaythroughSlots.Zone01)selected=catalog;
            if(!selected)throw new InvalidDataException("Missing catalog for "+world);
            return selected;
        }
        public void Load(string id)
        {
            FlushReserve();
            var selected=Slots.Open(id,out var world);
            string destination=SceneFor(world);
            EnsureDestination(destination);
            var destinationCatalog=CatalogFor(world);
            var validated=new ProgressionService(destinationCatalog.definitions,"local-player",selected);
            Transition(destination,new Transfer{slots=Slots,id=id,world=world,store=selected,service=validated,reloadActiveSlot=id==SlotId});
        }
        public void Continue()
        {
            FlushReserve();
            var latest=Slots.Latest();
            if(latest==null)throw new InvalidOperationException("No resumable playthrough is available.");
            Load(latest.Id);
        }
        public void NewGame(string label,string replace=null,bool confirmed=false,ProgressionState import=null)
        {
            EnsureDestination(ZoneScene);
            var candidate=new MemoryProgressionStore();
            if(import!=null)candidate.Commit(import);
            using var preflight=new ProgressionService(CatalogFor(PlaythroughSlots.Zone01).definitions,"local-player",candidate);
            FlushReserve();
            var created=Slots.Create(label,replace,confirmed,import,PlaythroughSlots.Zone01);
            if (replace!=null && replace==SlotId)
            {
                retiredSlot=true;
                Progression.Dispose();
            }
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
            Progression?.CloseReserveFrame();
            arriving=handoff;Busy=true;
            try
            {
                loading=SceneManager.LoadSceneAsync(path,LoadSceneMode.Single);
                if(loading==null)throw new InvalidOperationException("Could not start scene load: "+path);
            }
            catch{Busy=false;arriving=null;handoff.service?.Dispose();throw;}
        }
#if UNITY_EDITOR || ENABLE_PROFILER
        public void UseTestDirectory(string directory)
        {
            if(!IsTitle || Busy)throw new InvalidOperationException();
            Slots=new PlaythroughSlots(directory);
        }
#endif
    }
}
