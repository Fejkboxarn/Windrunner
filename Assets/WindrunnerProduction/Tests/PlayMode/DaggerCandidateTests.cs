#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Windrunner.Production.Input;
using Windrunner.Production.Progression;
using Windrunner.Production.Unity;
using Object=UnityEngine.Object;

namespace Windrunner.Production.Tests
{
    public sealed partial class MainMenuFlowTests
    {
        static Component DaggerOwner()=> (Component)Object.FindAnyObjectByType(Type.GetType("Windrunner.Daggers.DaggerPlayer, Assembly-CSharp",true));
        static object InvokeRule(object owner,string method,params object[] args)=>owner.GetType().GetMethod(method).Invoke(owner,args);
        static object Gear(Component player)=>Prop<object>(player,"Equipment");
        static object Definition(object gear,string id)=>InvokeRule(gear.GetType().GetField("Catalog").GetValue(gear),"Find",id);
        static Component Resource(Component player)=>(Component)player.GetType().GetField("power").GetValue(player);
        static void NodeReturn(Component power,float amount)
        {
            var source=Type.GetType("AirflowPrototype.AirPowerRewardSource, Assembly-CSharp",true);
            var reward=Activator.CreateInstance(Type.GetType("AirflowPrototype.AirPowerReward, Assembly-CSharp",true),Enum.Parse(source,"AirNode"));
            Call(power,"GrantReward",reward,amount);
        }
        static IEnumerator StartCandidateSlot(string folder)
        {
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Title,new LoadSceneParameters(LoadSceneMode.Single));yield return null;
            var title=Session();Call(title,"UseTestDirectory",folder);
            Submit("New game");Submit("Start a new playthrough");yield return Changed(World,title);
        }
        static IEnumerator UnloadCandidateWorld()
        {
            var previous=SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Dagger candidate teardown"));
            yield return SceneManager.UnloadSceneAsync(previous);
        }
        [UnityTest] public IEnumerator SaveBetweenSlingAndNodeDeliveriesCapturesOnlyEarnedReserve()
        {
            string folder=Path.Combine(Path.GetTempPath(),"Windrunner-returns-"+Guid.NewGuid().ToString("N"));
            try
            {
                yield return StartCandidateSlot(folder);
                var session=Session();
                var slots=Prop<PlaythroughSlots>(session,"Slots");
                string id=Prop<string>(session,"SlotId");
                var player=DaggerOwner();var gear=Gear(player);var power=Resource(player);
                InvokeRule(gear,"Equip",Definition(gear,"farseer"));
                Call(power,"Refill");Call(power,"Spend",5f);
                var source=Type.GetType("AirflowPrototype.AirPowerRewardSource, Assembly-CSharp",true);
                var sling=Activator.CreateInstance(Type.GetType("AirflowPrototype.AirPowerReward, Assembly-CSharp",true),Enum.Parse(source,"SlingNode"));
                Call(power,"GrantReward",sling,14f);Call(power,"GrantReward",sling,14f);
                Call(session,"Save");
                Assert.AreEqual(9,slots.Open(id).Load().daggers.Stored("farseer"));
                Call(session,"SaveCopy","Between deliveries",null,false);
                string copied=slots.List().Single(x=>x.Id!=id).Id;
                NodeReturn(power,4);
                Assert.AreEqual(13,slots.Open(id).Load().daggers.Stored("farseer"));
                Call(power,"Spend",3f);
                Assert.IsTrue((bool)InvokeRule(gear,"TryActivate",true,Time.frameCount));
                Assert.AreEqual(Prop<float>(power,"Max"),Prop<float>(power,"Current"),.001);
                Assert.Zero(slots.Open(id).Load().daggers.Stored("farseer"));
                Call(session,"Load",copied);yield return Changed(World,session);
                var saved=Prop<ProgressionService>(Session(),"Progression");
                Assert.AreEqual("farseer",saved.EquippedDagger);
                Assert.AreEqual(9,saved.StoredDaggerReserve("farseer"));
            }
            finally
            {
                if(ProjectRoot.Active)Object.Destroy(ProjectRoot.Active.gameObject);
                if(Directory.Exists(folder))Directory.Delete(folder,true);
            }
            yield return UnloadCandidateWorld();
        }

        [UnityTest] public IEnumerator LateRewardDuringSameSlotLoadAndRepeatedCopies()
        {
            string folder=Path.Combine(Path.GetTempPath(),"Windrunner-copy-"+Guid.NewGuid().ToString("N"));
            try
            {
                yield return StartCandidateSlot(folder);
                var session=Session();var slots=Prop<PlaythroughSlots>(session,"Slots");
                string id=Prop<string>(session,"SlotId");
                var player=DaggerOwner();var power=Resource(player);var gear=Gear(player);
                InvokeRule(gear,"Equip",Definition(gear,"farseer"));
                var tuning=Activator.CreateInstance(Type.GetType("AirflowPrototype.Editor.CharacterTuningSession, Assembly-CSharp-Editor",true),new object[]{player.gameObject});
                try
                {
                    float original=Prop<float>(power,"Max");
                    InvokeRule(tuning,"Preview",true);
                    var motor=player.GetComponent(Type.GetType("AirflowPrototype.PlayerMotor, Assembly-CSharp",true));
                    var draft=Prop<ScriptableObject>(motor,"Settings");
                    draft.GetType().GetField("maxAirPower").SetValue(draft,original+10);
                    NodeReturn(power,19);
                    Assert.AreEqual(original+10,Prop<float>(power,"Current"),.001);
                }
                finally{((IDisposable)tuning).Dispose();}
                Assert.AreEqual(9,Prop<ProgressionService>(session,"Progression").StoredDaggerReserve("farseer"));
                // The asynchronous load has validated its destination while the outgoing world can still finish a return.
                Call(session,"Load",id);NodeReturn(power,4);
                Assert.AreEqual(13,slots.Open(id).Load().daggers.Stored("farseer"));
                yield return Changed(World,session);
                session=Session();Assert.AreEqual(13,Prop<ProgressionService>(session,"Progression").StoredDaggerReserve("farseer"));
                Call(session,"Save");
                string destination=slots.Create("Destination",world:PlaythroughSlots.World).slotId;
                for(int round=0;round<3;round++)
                {
                    player=DaggerOwner();power=Resource(player);gear=Gear(player);
                    float before=Prop<ProgressionService>(session,"Progression").StoredDaggerReserve("farseer");
                    NodeReturn(power,2);
                    Call(session,"SaveCopy","Copy",destination,true);
                    Assert.AreEqual(PlaythroughSlots.Zone01,slots.Inspect(destination).Record.world);
                    Assert.AreEqual(before+2,slots.Open(destination).Load().daggers.Stored("farseer"));
                    var service=Prop<ProgressionService>(session,"Progression");
                    string occurrence="original-only-"+round;
                    Assert.IsTrue(service.Observe(new GameplayFact{playerId=service.PlayerId,occurrenceId=occurrence,
                        verified=true,persistent=true,kind=FactKind.ItemCollected,targetId=occurrence,definitionId=occurrence}));
                    Assert.IsFalse(slots.Open(destination).Load().persistentFacts.Any(f=>f.occurrenceId==occurrence));
                    InvokeRule(gear,"Equip",Definition(gear,"training"));Call(session,"Save");
                    Assert.AreEqual("farseer",slots.Open(destination).Load().daggers.equipped);
                    Call(session,"ReturnToTitle");yield return Changed(Title,session);
                    var title=Session();
                    Submit("New game");Submit("Start a new playthrough");yield return Changed(World,title);
                    var fresh=Session();var empty=Prop<ProgressionService>(fresh,"Progression");
                    Assert.AreEqual("training",empty.EquippedDagger);Assert.Zero(empty.StoredDaggerReserve("farseer"));
                    Call(fresh,"Save");Call(fresh,"ReturnToTitle");yield return Changed(Title,fresh);
                    title=Session();Call(title,"Load",destination);yield return Changed(World,title);session=Session();
                    Assert.AreEqual("farseer",Prop<ProgressionService>(session,"Progression").EquippedDagger);
                    string loaded=destination;destination=id;id=loaded;
                }
                Call(session,"ReturnToTitle");yield return Changed(Title,session);var lastTitle=Session();
                Submit("New game");Submit("Start a new playthrough");yield return Changed(World,lastTitle);
                Assert.AreEqual("training",Prop<ProgressionService>(Session(),"Progression").EquippedDagger);
                Assert.Zero(Prop<ProgressionService>(Session(),"Progression").StoredDaggerReserve("farseer"));
            }
            finally{if(ProjectRoot.Active)Object.Destroy(ProjectRoot.Active.gameObject);if(Directory.Exists(folder))Directory.Delete(folder,true);}
            yield return UnloadCandidateWorld();
        }

        [UnityTest,Category("Profiling")] public IEnumerator RenderedRewardPersistence()
        {
            string folder=Path.Combine(Path.GetTempPath(),"Windrunner-reward-profile-"+Guid.NewGuid().ToString("N"));
            try
            {
                yield return StartCandidateSlot(folder);
                var type=Type.GetType("Windrunner.Daggers.RewardBenchmark, Assembly-CSharp",true);
                yield return (IEnumerator)type.GetMethod("Measure").Invoke(null,null);
            }
            finally
            {
                if(ProjectRoot.Active)Object.Destroy(ProjectRoot.Active.gameObject);
                if(Directory.Exists(folder))Directory.Delete(folder,true);
            }
            yield return UnloadCandidateWorld();
        }

        [UnityTest,Category("Profiling")] public IEnumerator RenderedArsenalLifetimeUnderRepeatedLoad()
        {
            string folder=Path.Combine(Path.GetTempPath(),"Windrunner-arsenal-profile-"+Guid.NewGuid().ToString("N"));
            try
            {
                Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null,SystemInfo.graphicsDeviceType);
                yield return StartCandidateSlot(folder);
                var player=DaggerOwner();var gear=Gear(player);var power=Resource(player);
                var host=WindrunnerInputHost.Active;var service=Prop<ProgressionService>(Session(),"Progression");
                int listeners=Object.FindObjectsByType<AudioListener>().Length;
                var camera=Camera.main;
                int portraits()=>Resources.FindObjectsOfTypeAll<RenderTexture>().Count(rt=>rt.name=="Arsenal portrait");
                int stages()=>Resources.FindObjectsOfTypeAll<Camera>().Count(c=>c.name=="Portrait camera");
                long baselineNative=0,baselineManaged=0,peakNative=0,peakManaged=0;
                var opens=new double[40];var rewards=new double[40];var bytes=new long[40];var rewardBytes=new long[40];
                int meshBaseline=0,materialBaseline=0;
                using var gc=global::Unity.Profiling.ProfilerRecorder.StartNew(global::Unity.Profiling.ProfilerCategory.Memory,"GC Allocated In Frame",4);
                Assert.IsTrue(gc.Valid);
                var sample=new byte[1024*1024];long calibration=0;
                for(int frame=0;frame<4;frame++){yield return null;calibration=Math.Max(calibration,gc.LastValue);}
                GC.KeepAlive(sample);Assert.GreaterOrEqual(calibration,sample.Length);
                for(int i=-4;i<40;i++)
                {
                    if(i==0)
                    {
                        yield return Resources.UnloadUnusedAssets();GC.Collect();yield return null;
                        baselineNative=Profiler.GetTotalAllocatedMemoryLong();baselineManaged=GC.GetTotalMemory(false);
                        meshBaseline=Resources.FindObjectsOfTypeAll<Mesh>().Length;materialBaseline=Resources.FindObjectsOfTypeAll<Material>().Length;
                    }
                    host.SetPaused(true);yield return null;
                    double start=Time.realtimeSinceStartupAsDouble;
                    Submit("Dagger Arsenal");Submit("Farseer's Daggers");Submit("Rotate character");Submit("Equip");
                    if(i>=0){opens[i]=(Time.realtimeSinceStartupAsDouble-start)*1000;}
                    yield return null;if(i>=0)bytes[i]=gc.LastValue;
                    Assert.AreEqual(1,portraits());Assert.AreEqual(1,stages());
                    Assert.AreEqual(listeners,Object.FindObjectsByType<AudioListener>().Length);
                    peakNative=Math.Max(peakNative,Profiler.GetTotalAllocatedMemoryLong());peakManaged=Math.Max(peakManaged,GC.GetTotalMemory(false));
                    Submit("Back");Submit("Resume");yield return null;yield return null;
                    Assert.Zero(portraits());Assert.Zero(stages());Assert.AreSame(camera,Camera.main);
                    var left=Prop<GameObject>(player,"LeftModel");var right=Prop<GameObject>(player,"RightModel");
                    Assert.AreEqual(1,left.transform.parent.childCount);Assert.AreEqual(1,right.transform.parent.childCount);
                    Assert.IsTrue(GameObject.Find("Second Wind reserve").activeInHierarchy);
                    Call(power,"Refill");
                    start=Time.realtimeSinceStartupAsDouble;
                    for(int n=0;n<4;n++)NodeReturn(power,3);
                    if(i>=0){rewards[i]=(Time.realtimeSinceStartupAsDouble-start)*1000;}
                    yield return null;if(i>=0)rewardBytes[i]=gc.LastValue;
                    Assert.AreEqual(12,service.StoredDaggerReserve("farseer"));Call(power,"Spend",8f);
                    Assert.IsTrue((bool)InvokeRule(gear,"TryActivate",true,Time.frameCount));
                    Assert.AreEqual(Prop<float>(power,"Max"),Prop<float>(power,"Current"),.001);
                    InvokeRule(gear,"Equip",Definition(gear,"training"));yield return null;yield return null;
                    Assert.IsTrue(left==null && right==null);Assert.IsNull(GameObject.Find("Second Wind reserve"));
                }
                yield return new WaitForSeconds(2.1f);
                Assert.IsFalse(Object.FindObjectsByType<ParticleSystem>().Any(p=>p.name.StartsWith("Second Wind burst")));
                yield return Resources.UnloadUnusedAssets();GC.Collect();yield return null;
                Array.Sort(opens);Array.Sort(rewards);Array.Sort(bytes);Array.Sort(rewardBytes);
                Debug.Log(FormattableString.Invariant($"Arsenal measurements cycles=40 open_ms_p50={opens[19]:F2} open_ms_p95={opens[37]:F2} open_frame_bytes_p50={bytes[19]} four_rewards_ms_p50={rewards[19]:F2} four_rewards_ms_p95={rewards[37]:F2} four_rewards_frame_bytes_p50={rewardBytes[19]} native_total_bytes={baselineNative}/{peakNative}/{Profiler.GetTotalAllocatedMemoryLong()} managed_heap_bytes={baselineManaged}/{peakManaged}/{GC.GetTotalMemory(false)} meshes={meshBaseline}/{Resources.FindObjectsOfTypeAll<Mesh>().Length} materials={materialBaseline}/{Resources.FindObjectsOfTypeAll<Material>().Length}"));
            }
            finally{if(ProjectRoot.Active)Object.Destroy(ProjectRoot.Active.gameObject);if(Directory.Exists(folder))Directory.Delete(folder,true);}
            yield return UnloadCandidateWorld();
        }
    }
}
#endif
