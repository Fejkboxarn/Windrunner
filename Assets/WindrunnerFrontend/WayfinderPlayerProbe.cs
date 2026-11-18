#if UNITY_EDITOR || ENABLE_PROFILER
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Windrunner.Production;
using Windrunner.Production.Progression;
namespace Windrunner.Frontend
{
    // Opt-in disposable player verification; never runs during ordinary gameplay.
    public sealed class WayfinderPlayerProbe : MonoBehaviour
    {
        string output; bool hadPreference; int preference; bool restored, capturedPreference;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-windrunnerWayfinderProbe");
            if(Application.isEditor || !Debug.isDebugBuild || index<0 || index+1>=args.Length)return;
            var go=new GameObject("Disposable Wayfinder verification");DontDestroyOnLoad(go);
            var probe=go.AddComponent<WayfinderPlayerProbe>();probe.output=Path.GetFullPath(args[index+1]);
            if(Directory.Exists(probe.output))throw new IOException("Use a fresh probe output directory.");
            Directory.CreateDirectory(probe.output);probe.StartCoroutine(probe.Guard(probe.Run()));
        }
        IEnumerator Guard(IEnumerator work)
        {
            var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(work);
            while(stack.Count>0)
            {
                object next;
                try{var current=stack.Peek();if(!current.MoveNext()){(current as IDisposable)?.Dispose();stack.Pop();continue;}next=current.Current;}
                catch(Exception error){Debug.LogException(error);File.WriteAllText(Path.Combine(output,"failure.txt"),error.ToString());Restore();Application.Quit(1);yield break;}
                if(next is IEnumerator nested){stack.Push(nested);continue;}
                yield return next;
            }
            Restore();Application.Quit();
        }
        void Restore(){if(restored || !capturedPreference)return;restored=true;if(hadPreference)PlayerPrefs.SetInt("Windrunner.UI.ObjectiveWayfinder",preference);else PlayerPrefs.DeleteKey("Windrunner.UI.ObjectiveWayfinder");PlayerPrefs.Save();}
        void OnDestroy()=>Restore();
        static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static void Click(string name){var button=FindObjectsByType<Button>().Single(b=>b.gameObject.activeInHierarchy&&b.name==name);Check(button.interactable,name);button.onClick.Invoke();}
        IEnumerator Shot(string name)
        {
            var args=Environment.GetCommandLineArgs();int width=Array.IndexOf(args,"-screen-width"),height=Array.IndexOf(args,"-screen-height");
            if(width>=0 && height>=0 && width+1<args.Length && height+1<args.Length)
            {
                int w=int.Parse(args[width+1]),h=int.Parse(args[height+1]);
                if(Screen.width!=w || Screen.height!=h)Screen.SetResolution(w,h,FullScreenMode.Windowed);
            }
            yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();
            var capture=ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(Path.Combine(output,name+".png"),capture.EncodeToPNG());
                var pixels=capture.GetPixels32();int lit=0;
                for(int i=0;i<pixels.Length;i+=Math.Max(1,pixels.Length/1024))if(pixels[i].r>8 || pixels[i].g>8 || pixels[i].b>8)lit++;
                Check(lit>10,"Black screenshot: visible player window required for rendered verification.");
                File.AppendAllText(Path.Combine(output,"captures.txt"),name+" "+capture.width+"x"+capture.height+" lit samples="+lit+"\n");
            }
            finally{Destroy(capture);}
        }
        static void Approach(ProgressionActor actor,Transform target)
        {
            var controller=actor.GetComponent<CharacterController>();controller.enabled=false;
            var p=target.position-Vector3.forward*3;p.y=Terrain.activeTerrain.SampleHeight(p)+.1f;actor.transform.position=p;controller.enabled=true;Physics.SyncTransforms();
        }
        IEnumerator Run()
        {
            hadPreference=PlayerPrefs.HasKey("Windrunner.UI.ObjectiveWayfinder");preference=PlayerPrefs.GetInt("Windrunner.UI.ObjectiveWayfinder");capturedPreference=true;
            WindrunnerUITheme.ObjectiveWayfinder=true;yield return null;
            Check(Application.version=="0.5.0","Wrong player version "+Application.version);Debug.Log("Wayfinder player version "+Application.version);
            var title=FindAnyObjectByType<PlayerSession>();title.UseTestDirectory(Path.Combine(output,"disposable-slots"));
            Click("Settings");Click("Interface");yield return Shot("01-setting-on");Click("Objective Wayfinder: ON");Check(!WindrunnerUITheme.ObjectiveWayfinder,"Toggle off");yield return Shot("02-setting-off");Click("Objective Wayfinder: OFF");Click("Back");Click("Back");
            title.NewGame("Disposable Wayfinder verification");while(title)yield return null;yield return new WaitForSecondsRealtime(1);
            var session=FindAnyObjectByType<PlayerSession>();var journal=FindAnyObjectByType<ProgressionJournal>();var actor=journal.actor;var npc=FindAnyObjectByType<RelationshipContact>();var service=actor.Service;
            Check(service.Accept("basin-hunt") && service.Track("basin-hunt"),"Accept hunt");
            session.orbit.enabled=false;var camera=journal.interaction.view;
            camera.transform.position=actor.transform.position+Vector3.up*5-Vector3.forward*8;
            var anchor=journal.objectiveAnchors.First(a=>a.questId=="basin-hunt"&&a.phase==WayfinderPhase.Objective).anchor;
            camera.transform.LookAt(anchor.position+Vector3.up*2);yield return Shot("03-hunt-world-distance");Check(journal.Wayfinder.Visible&&!journal.Wayfinder.Offscreen,"Onscreen hunt marker");
            camera.transform.Rotate(0,140,0,Space.World);yield return Shot("04-hunt-edge-distance");Check(journal.Wayfinder.Offscreen,"Offscreen hunt marker");
            session.input.SetPaused(true);yield return null;Click("Settings");Click("Interface");yield return Shot("05-pause-setting");Check(!journal.Wayfinder.Visible,"Menu suppression");Click("Objective Wayfinder: ON");Click("Back");Click("Back");Click("Resume");yield return Shot("06-gameplay-wayfinder-off");Check(!journal.Wayfinder.Visible,"Gameplay toggle suppression");WindrunnerUITheme.ObjectiveWayfinder=true;
            for(int i=0;i<3;i++)Check(service.Observe(new GameplayFact{playerId=service.PlayerId,occurrenceId="probe-hunt-"+i,definitionId="basin-creature",regionId="North Basin",targetId="probe-target-"+i,kind=FactKind.EnemyDefeated,verified=true}),"Hunt reward");
            camera.transform.LookAt(npc.transform.position+Vector3.up*2);yield return Shot("07-hunt-return");Check(journal.Wayfinder.Target==npc.transform,"Turn-in destination");
            Approach(actor,npc.transform);yield return new WaitForSecondsRealtime(1);
            Check(npc.TryUse(actor,InteractionChannel.Input),"Hunt turn in");if(!npc.cinematics.IsPlaying)Check(npc.cinematics.Play("basin-hunt-milestone"),"Cinematic start");
            Check(npc.cinematics.IsPlaceholderPlaying,"Placeholder flag");yield return Shot("08-placeholder-warning");Check(npc.cinematics.Skip(),"Skip");Check(!npc.cinematics.IsPlaceholderPlaying,"Warning cleanup");
            Check(npc.TryUse(actor,InteractionChannel.Input),"Accept retrieval");yield return null;
            var item=npc.retrieval;camera.transform.LookAt(item.transform.position+Vector3.up*2);yield return Shot("09-retrieval-search");
            Approach(actor,item.transform);yield return new WaitForSecondsRealtime(.4f);Check(item.TryUse(actor,InteractionChannel.Input),"Retrieval pickup");
            camera.transform.position=actor.transform.position+Vector3.up*5;camera.transform.LookAt(npc.transform.position+Vector3.up*2);yield return Shot("10-retrieval-return");Check(journal.Wayfinder.Target==npc.transform,"Carried destination");
            session.Save();Check(session.Error==null,"Manual save");string slot=session.SlotId;session.ReturnToTitle();while(session)yield return null;yield return null;
            title=FindAnyObjectByType<PlayerSession>();title.Load(slot);while(title)yield return null;yield return new WaitForSecondsRealtime(.4f);
            journal=FindAnyObjectByType<ProgressionJournal>();Check(journal.Wayfinder.Binding.areaId=="East overlook"&&!journal.Wayfinder.Binding.retrieval.Carrying,"Reload restarts attempt at search area");
            File.WriteAllText(Path.Combine(output,"passed.txt"),"Application.version="+Application.version+"\nProduction quest guidance, settings both hosts, menu suppression, placeholder skip, save/load recovery. Captures="+Directory.GetFiles(output,"*.png").Length);
        }
    }
}
#endif
