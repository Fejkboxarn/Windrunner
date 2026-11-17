using System;
using System.IO;
using System.Linq;
using Windrunner.Daggers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;
using UnityEngine.UI;
using UnityEngine.Audio;
using Windrunner.Production.Progression;

namespace Windrunner.Frontend
{
    public sealed class SessionMenu : MonoBehaviour
    {
        public PlayerSession session;
        public AudioMixer worldMix;
        private MenuLayout ui;
        private readonly DesktopPreferences prefs=new DesktopPreferences();
        private Action back;
        private int backFrame=-1;
        private bool closing, arsenalFromGameplay;
        private DaggerPreview portrait;
        private RawImage portraitSurface;
        private DaggerPair inspected;
        private string selected;
        private DesktopOptions draft,previous;
        private bool preview;
        private float expires;
        private VideoPlayer replay;
        private RawImage picture;
        private const string Checkpoint="Saving records your progress. Loading returns you to the starting clearing.";
        private void Start()
        {
            if(!session || !session.input || session.input.Input==null || !session.catalog)
            { Debug.LogError("Frontend requires a configured player session, input host and catalog.",this);enabled=false;return; }
            prefs.Restore();ui=new MenuLayout(transform);session.input.HandleMenuBack=HandleBack;
            if(worldMix!=null)prefs.RestoreMix(worldMix);
            replay=gameObject.AddComponent<VideoPlayer>();replay.playOnAwake=false;replay.renderMode=VideoRenderMode.APIOnly;
            replay.timeUpdateMode=VideoTimeUpdateMode.UnscaledGameTime;
            replay.loopPointReached+=ReplayEnded;replay.errorReceived+=ReplayFailed;
            if(session.IsTitle)Home();
        }
        private void Update()
        {
            if(session.Busy || ui==null)return;
            if(picture!=null && replay.texture!=null)picture.texture=replay.texture;
            if(preview && Time.unscaledTime>=expires){Revert();Video();}
            if(!session.input.IsPaused)
            {
                ClosePortrait();ui.Root.SetActive(false);
                if(session.daggers && session.daggers.Equipment!=null && session.daggers.GameplayAllowed &&
                    session.input.Input.ButtonPressed("DaggerArsenal"))
                {
                    session.input.SetPaused(true);backFrame=Time.frameCount;
                    OpenArsenal(true);
                }
                return;
            }
            if(!ui.Root.activeSelf){Home();backFrame=Time.frameCount;return;}
            if(!session.input.Input.IsRebinding && session.input.Input.GetAction("UI","Cancel")?.WasPressedThisFrame()==true)HandleBack();
            ui.RevealSelection();
        }
        private bool HandleBack()
        {
            if(!session.input.IsPaused)return false;
            if(!session.Busy && backFrame!=Time.frameCount){backFrame=Time.frameCount;back?.Invoke();}
            return true;
        }
        private void Page(string title,string note,Action returnTo)
        {
            if(title!="Dagger Arsenal")ClosePortrait();
            back=returnTo;ui.Begin(title,note);
        }
        private void End(bool addBack=true){if(addBack)ui.Choice("Back",()=>back?.Invoke());ui.Focus();}
        private void Home()
        {
            if(session.IsTitle)
            {
                SlotEntry latest=null;
                string problem=null;
                try{latest=session.Slots.Latest();}
                catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException)
                {problem="Saved games could not be read. "+ex.Message;}
                string summary=latest==null?"":latest.Record.displayName+"\nResume at the starting clearing.\n"+
                    new DateTime(latest.Record.savedUtc,DateTimeKind.Utc).ToLocalTime().ToString("g")+
                    (latest.Recovered?"  ·  Recovered backup":"");
                Page("Main menu",problem??summary,null);
                ui.Choice("Continue",session.Continue,latest!=null);
                ui.Choice("New game",()=>Slots("New game"));
                ui.Choice("Load game",()=>Slots("Load game"));
                ui.Choice("Settings",Settings);ui.Choice("Gallery",()=>Gallery(Home));
                ui.Choice("Quit game",()=>Confirm("Quit game?","Close Windrunner.",Quit,Home));
            }
            else
            {
                Page("Paused",session.Error??Checkpoint,Resume);
                ui.Choice("Resume",Resume);if(session.daggers && session.daggers.Equipment!=null)ui.Choice("Dagger Arsenal",()=>OpenArsenal(false));ui.Choice("Save game",()=>Slots("Save game"));
                ui.Choice("Load game",()=>Slots("Load game"));ui.Choice("Settings",Settings);
                ui.Choice("Return to main menu",()=>Confirm("Return to main menu?",Checkpoint,session.ReturnToTitle,Home));
            }
            End(false);
        }
        private void OpenArsenal(bool fromGameplay)
        {
            arsenalFromGameplay=fromGameplay;
            var equipment=session.daggers.Equipment;
            inspected=equipment.Equipped??equipment.Catalog.pairs.FirstOrDefault(equipment.Owns);
            ArsenalPage();
        }
        private void ArsenalBack(){ClosePortrait();if(arsenalFromGameplay)Resume();else Home();}
        private void ArsenalPage()
        {
            var equipment=session.daggers.Equipment;
            string detail=inspected?inspected.displayName+"\n"+inspected.description:"No daggers owned";
            if(inspected && inspected.active)
                detail+="\n\n"+inspected.active.displayName+"\n"+inspected.active.description+"\n"+equipment.Status(inspected);
            else detail+="\nNo active ability";
            if(inspected)foreach(var passive in inspected.passives)detail+="\n"+passive.displayName+" · "+passive.description;
            Page("Dagger Arsenal",detail,ArsenalBack);
            if(portrait==null)
            {
                portraitSurface=ui.Portrait();
                portrait=new DaggerPreview(session.daggers,portraitSurface);
            }
            portrait.Select(inspected);
            foreach(var pair in equipment.Catalog.pairs)
                if(equipment.Owns(pair))
                    ui.Choice((inspected==pair?"› ":"")+pair.displayName+(equipment.Equipped==pair?"   ·   Equipped":""),()=>{inspected=pair;ArsenalPage();});
            ui.Choice("Equip",()=>{equipment.Equip(inspected);ArsenalPage();},inspected && equipment.Equipped!=inspected);
            ui.Choice("Unequip",()=>{equipment.Equip(null);ArsenalPage();},equipment.Equipped);
            ui.Choice("Rotate character",()=>portrait.Rotate(45));
            End();
        }
        private void ClosePortrait()
        {
            if(portrait==null)return;
            if(portraitSurface){portraitSurface.texture=null;Destroy(portraitSurface.transform.parent.gameObject);}
            portrait.Dispose();portrait=null;portraitSurface=null;
            if(ui.Root)ui.RestoreMessage();
        }
        private void Resume(){session.input.SetPaused(false);ui.Root.SetActive(false);}
        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        private void Confirm(string title,string note,Action yes,Action no)
        {Page(title,note,no);ui.Choice("Cancel",no);ui.Choice("Confirm",yes);End(false);}
        private void Slots(string mode)
        {
            var entries=session.Slots.List();Page(mode,Checkpoint,Home);
            string label="Journey "+(entries.Count+1);
            if(mode=="New game")
            {
                ui.Choice("Start a new playthrough",()=>session.NewGame(label));
                string legacy=Path.Combine(Application.persistentDataPath,"progression.json");
                if(File.Exists(legacy))ui.Choice("Import previous progression",()=>Confirm("Import progression?","Create a separate playthrough from the original local-player save.",
                    ()=>session.NewGame("Imported journey",import:ProgressionCodec.Decode(File.ReadAllText(legacy))),()=>Slots(mode)));
            }
            if(mode=="Save game")
            {
                ui.Choice("Save current playthrough",()=>Confirm("Update current save?",Checkpoint,()=>{session.Save();Home();ui.Status("Progress saved. "+Checkpoint);},()=>Slots(mode)),session.SlotId!=null);
                ui.Choice("Save an independent copy",()=>{session.SaveCopy(label);Slots(mode);ui.Status("Copy saved. "+Checkpoint);},session.CanCopy);
            }
            foreach(var entry in entries)
            {
                string name=entry.Record?.displayName??"Unavailable save";
                string info=entry.Record==null?entry.Problem:new DateTime(entry.Record.savedUtc,DateTimeKind.Utc).ToLocalTime().ToString("g")+"   ·   Starting clearing";
                ui.Choice(name+"   /   "+info+(entry.Recovered?"   (backup)":""),
                    ()=>Details(entry.Id,mode));
            }
            if(entries.Count==0 && mode=="Load game")ui.Status("There are no saved playthroughs yet.");
            End();
        }
        private void Details(string id,string mode)
        {
            var entry=session.Slots.Inspect(id);selected=id;
            string name=entry.Record?.displayName??"Unavailable save";
            Action parent=()=>Slots(mode);
            string details=entry.Record==null?entry.Problem:
                (entry.Recovered?"Recovered from backup.\n":"")+Checkpoint+"\n"+
                entry.Record.progression.quests.Count(q=>q.status==QuestStatus.Completed)+" quests completed  ·  "+
                entry.Record.progression.persistentFacts.Count+" discoveries";
            Page(name,details,parent);
            if(mode=="Load game")ui.Choice("Load this playthrough",()=>session.Load(id),entry.Record!=null);
            if(mode=="New game")ui.Choice("Start over in this slot",()=>Confirm("Replace "+name+"?","This replaces the selected playthrough.",()=>session.NewGame(name,id,true),parent));
            if(mode=="Save game" && id!=session.SlotId)ui.Choice("Overwrite with current progress",()=>Confirm("Replace "+name+"?",Checkpoint,()=>{
                session.SaveCopy(name,id,true);Slots(mode);},parent),session.CanCopy);
            if(entry.Record!=null)ui.Choice("View gallery",()=>Gallery(()=>Details(id,mode)));
            ui.Choice("Delete playthrough",()=>Confirm("Delete "+name+"?","Remove this slot from saved games.",()=>{
                session.Slots.Delete(id,true);selected=null;Slots(mode);},parent),id!=session.SlotId);End();
        }
        private void Settings()
        {
            Page("Settings","Preferences apply to every playthrough.",Home);
            ui.Choice("Sound",Sound);ui.Choice("Display",()=>{draft=DesktopOptions.Current();Video();});
            ui.Choice("Controls",Controls);End();
        }
        private void Sound()
        {
            Page("Sound","Volume levels apply across playthroughs.",Settings);
            VolumeRows("Master",prefs.Volume,prefs.SetVolume);
            if(worldMix!=null)
            {
                // A single ambience control covers the three independent environmental parent buses.
                VolumeRows("Music",prefs.Music,value=>prefs.SetMix(worldMix,true,value));
                VolumeRows("Ambience",prefs.Ambience,value=>prefs.SetMix(worldMix,false,value));
            }
            End();
        }
        private void VolumeRows(string name,float level,Action<float> set)
        {
            ui.Choice(name+"    "+Mathf.RoundToInt(100*level)+"%    −",()=>{set(level-.1f);Sound();},level>0);
            ui.Choice(name+"    +",()=>{set(level+.1f);Sound();},level<1);
        }
        private static T Next<T>(T[] values,T current)=>values[(Array.IndexOf(values,current)+1)%values.Length];
        private void Video()
        {
            Page("Display","Preview changes before keeping them.",Settings);
            ui.Choice("Resolution    "+draft.width+" × "+draft.height,()=>{
                var sizes=Screen.resolutions.Select(r=>new Vector2Int(r.width,r.height)).Concat(new[]{new Vector2Int(1280,720),new Vector2Int(1600,900),new Vector2Int(1920,1080)})
                    .Distinct().OrderBy(v=>v.x).ThenBy(v=>v.y).ToArray();
                var next=Next(sizes,new Vector2Int(draft.width,draft.height));draft.width=next.x;draft.height=next.y;Video();});
            ui.Choice("Display mode    "+draft.mode,()=>{draft.mode=Next(new[]{FullScreenMode.Windowed,FullScreenMode.FullScreenWindow,FullScreenMode.ExclusiveFullScreen},draft.mode);Video();});
            ui.Choice("Quality    "+QualitySettings.names[draft.quality],()=>{draft.quality=(draft.quality+1)%QualitySettings.names.Length;Video();});
            ui.Choice("VSync    "+(draft.vsync==0?"Off":"On"),()=>{draft.vsync=draft.vsync==0?1:0;Video();});
            ui.Choice("Frame limit    "+(draft.limit<0?"Unlimited":draft.limit.ToString()),()=>{draft.limit=Next(new[]{-1,30,60,90,120,144,240},draft.limit);Video();},draft.vsync==0);
            ui.Choice("Apply changes",()=>{
                previous=DesktopOptions.Current();draft.Apply();preview=true;expires=Time.unscaledTime+15;
                Confirm("Keep display settings?","Previous settings return in 15 seconds.",()=>{
                    if(Screen.fullScreenMode!=draft.mode || (draft.mode!=FullScreenMode.FullScreenWindow && (Screen.width!=draft.width || Screen.height!=draft.height)))
                    {Revert();Video();ui.Status("Requested display mode was unavailable.");return;}
                    prefs.Confirm(DesktopOptions.Current());preview=false;Settings();},()=>{Revert();Video();});});End();
        }
        private void Revert()
        {
            if(!preview)return;
            preview=false;
            draft=previous.Supported()?previous:DesktopOptions.SafeWindow();
            draft.Apply();
        }
        private void Controls()
        {
            var input=session.input.Input;
            Page("Controls","Select a binding, then press its replacement. Backspace cancels.",Settings);
            ui.Choice("Invert vertical look    "+(input.InvertLookY?"On":"Off"),()=>{input.SetInvertLookY(!input.InvertLookY);Controls();});
            ui.Choice("Mouse sensitivity    "+input.MouseSensitivity.ToString("F1")+"    −",()=>{input.SetMouseSensitivity(input.MouseSensitivity-.1f);Controls();});
            ui.Choice("Mouse sensitivity    +",()=>{input.SetMouseSensitivity(input.MouseSensitivity+.1f);Controls();});
            ui.Choice("Controller sensitivity    "+input.StickSensitivity.ToString("F1")+"    −",()=>{input.SetStickSensitivity(input.StickSensitivity-.1f);Controls();});
            ui.Choice("Controller sensitivity    +",()=>{input.SetStickSensitivity(input.StickSensitivity+.1f);Controls();});
            ui.Choice("Reset bindings",()=>Confirm("Reset bindings?","Restore this input template’s bindings for every playthrough. Bindings exclusive to other templates are kept.",()=>{input.ResetBindings();Controls();},Controls));
            foreach(var map in input.Actions.actionMaps)
            foreach(var action in map.actions)
            for(int i=0;i<action.bindings.Count;i++)
            {
                var binding=action.bindings[i];int index=i;
                if(binding.isComposite || (map.name=="UI" && action.name!="Submit" && action.name!="Navigate" && action.name!="Cancel") || action.name.StartsWith("Fixture",StringComparison.Ordinal))continue;
                ui.Choice(action.name+" "+binding.name+"    "+action.GetBindingDisplayString(i),()=>{
                    bool conflict=false;
                    if(input.BeginRebind(map.name,action.name,index,ok=>{if(closing || this==null || !ui.Root)return;Controls();ui.Status(conflict?"That control is already bound in this action map.":ok?"Binding saved.":"Binding canceled.");},
                        path=>{
                            if(string.Equals(binding.path,path,StringComparison.OrdinalIgnoreCase))return true;
                            conflict=input.Actions.actionMaps.Where(other=>!(map.name=="UI" && other.name=="Gameplay") &&
                                !(map.name=="Gameplay" && other.name=="UI")).SelectMany(other=>other.actions)
                                .SelectMany(other=>other.bindings).Any(b=>b.id!=binding.id && !b.isComposite &&
                                    string.Equals(b.effectivePath,path,StringComparison.OrdinalIgnoreCase));
                            return !conflict;
                        }))
                    {Page("Rebind "+action.name,"Press a new control, or Backspace to cancel.",input.CancelRebind);ui.Choice("Cancel",input.CancelRebind);End(false);}
                });
            }
            End();
        }
        private Action afterReplay;
        private void Gallery(Action parent)
        {
            string id=selected??session.SlotId??session.Slots.Latest()?.Id;
            var record=id==null?null:session.Slots.Inspect(id).Record;
            var entries=(record==null?session.catalog:session.CatalogFor(record.world)).definitions.cutscenes.Where(c=>c.content!=null || c.galleryReplay!=null).ToList();
            Page("Gallery",entries.Count==0?"No cinematics are available yet.":record?.displayName??"Select a playthrough to view its unlocks.",parent);
            foreach(var entry in entries)
            {
                bool unlocked=GalleryAccess.Unlocked(record?.progression,entry.id);
                string status=!unlocked?"    Locked":entry.galleryReplay==null?"    Replay unavailable":"";
                ui.Choice(entry.displayName+status,()=>GalleryAccess.Replay(record?.progression,entry.id,()=>{
                    afterReplay=()=>Gallery(parent);Page(entry.displayName,"",StopReplay);ui.Choice("End playback",StopReplay);End(false);
                    picture=ui.VideoSurface((float)entry.galleryReplay.width/Math.Max(1f,entry.galleryReplay.height));
                    replay.clip=entry.galleryReplay;replay.audioOutputMode=VideoAudioOutputMode.Direct;
                    if(entry.galleryReplay.audioTrackCount>0)replay.SetDirectAudioVolume(0,prefs.Volume);
                    replay.Play();
                }),unlocked && entry.galleryReplay!=null);
            }
            End();
        }
        private void ReplayEnded(VideoPlayer player)=>StopReplay();
        private void ReplayFailed(VideoPlayer player,string error){StopReplay();ui.Status("Playback failed. "+error);}
        private void StopReplay()
        {
            replay.Stop();
            if(picture!=null)Destroy(picture.transform.parent.gameObject);
            ui.RestoreList();var done=afterReplay;afterReplay=null;done?.Invoke();
        }
        private void OnDestroy()
        {
            ClosePortrait();closing=true;
            if(session!=null && session.input!=null)session.input.HandleMenuBack=null;
            if(replay!=null){replay.loopPointReached-=ReplayEnded;replay.errorReceived-=ReplayFailed;}
            Revert();
        }
    }
}
