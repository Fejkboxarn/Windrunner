#if UNITY_EDITOR || ENABLE_PROFILER
using System;
using Windrunner.Production.Diagnostics;
using System.Collections;
using System.Diagnostics;
using System.IO;
using AirflowPrototype;
using UnityEngine;
using Windrunner.Frontend;
using Windrunner.Production.Progression;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Debug = UnityEngine.Debug;

namespace Windrunner.Daggers
{
    public sealed class RewardBenchmark : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartPlayerBenchmark()
        {
            if (Application.isEditor || !Debug.isDebugBuild || Array.IndexOf(Environment.GetCommandLineArgs(), "-windrunnerRewardBenchmark") < 0) return;
            var runner = new GameObject("Disposable reward benchmark");
            DontDestroyOnLoad(runner);
            runner.AddComponent<RewardBenchmark>().StartCoroutine(RunPlayer());
        }

        static IEnumerator RunPlayer()
        {
            yield return null;
            var title = FindAnyObjectByType<PlayerSession>();
            string directory = Path.Combine(Path.GetTempPath(),"Windrunner-rewards-"+Guid.NewGuid().ToString("N"));
            title.UseTestDirectory(directory);
            title.NewGame("Disposable benchmark");
            while (title) yield return null;
            yield return null;
            yield return Measure();
            var session = FindAnyObjectByType<PlayerSession>();
            var player = session.daggers;
            player.power.Refill();
            player.power.GrantReward(new AirPowerReward(AirPowerRewardSource.SlingNode),13);
            var beforeQuit=session.Slots.Inspect(session.SlotId).Record;
            if(beforeQuit.progression.daggers.Stored("farseer")!=13) throw new InvalidDataException("Final reward failed");
            Debug.Log("Reward shutdown fixture "+directory+" "+session.SlotId+" "+beforeQuit.revision);
            Application.Quit();
        }

        // Kernel/user times exclude time the gameplay thread spends blocked in durable I/O.
        // Windows accounts these coarsely, so aggregate differences across all measured samples.
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentThread();
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool GetThreadTimes(IntPtr thread,out long created,out long exited,out long kernel,out long user);
#endif
        static long CurrentThreadCpuTicks()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (GetThreadTimes(GetCurrentThread(),out _,out _,out long kernel,out long user))
                return kernel+user;
#endif
            return -1;
        }

        static double Percentile(double[] values, double fraction)
        {
            Array.Sort(values);
            return values[(int)Math.Ceiling(values.Length*fraction)-1];
        }

        static IEnumerator MeasureTraversal(DaggerPlayer player,Unity.Profiling.ProfilerRecorder gc)
        {
            var pad=InputSystem.AddDevice<Gamepad>();
            var frames=new double[120];
            var bytes=new double[frames.Length];
            float distance=0;
            try
            {
                for(int i=-20;i<frames.Length;i++)
                {
                    var before=player.transform.position;
                    InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=i%60<30?Vector2.right:Vector2.left});
                    yield return null;
                    if(i<0)continue;
                    var step=player.transform.position-before;
                    distance+=new Vector2(step.x,step.z).magnitude;
                    frames[i]=Time.unscaledDeltaTime*1000;
                    bytes[i]=gc.LastValue;
                }
                if(distance<.1f)throw new InvalidOperationException("Traversal control did not move the production player");
                Debug.Log(FormattableString.Invariant($"REWARD_TRAVERSAL frames=120 distance={distance:F2} frame50={Percentile(frames,.5):F2} frame95={Percentile(frames,.95):F2} frameBytes50={Percentile(bytes,.5)}"));
            }
            finally {InputSystem.RemoveDevice(pad);}
        }

        public static IEnumerator Measure()
        {
            var player = FindAnyObjectByType<DaggerPlayer>();
            var gear = player.Equipment;
            var power = player.power;
            gear.Equip(gear.Catalog.Find("farseer"));
            using var gc = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory,"GC Allocated In Frame",4);
            if(!gc.Valid) throw new InvalidOperationException("Missing frame allocation recorder");
            var calibration=new byte[1024*1024];
            long observed=0;
            for(int j=0;j<4;j++) {yield return null;observed=Math.Max(observed,gc.LastValue);}
            GC.KeepAlive(calibration);
            if(observed<calibration.Length) throw new InvalidOperationException("Frame allocation recorder failed calibration");
            yield return MeasureTraversal(player,gc);
            const int samples=40;
            foreach (var mode in new[]{"burst","single","main-only"})
            {
                var times=new double[samples];
                var frames=new double[samples];
                var bytes=new double[samples];
                var phases=new double[RewardProbe.Ticks.Length][];
                for(int j=0;j<phases.Length;j++) phases[j]=new double[samples];
                int count=mode=="burst"?4:1;
                int writes=0;
                long cpuTicks=0;
                bool cpuAvailable=true;
                for(int i=-4;i<samples;i++)
                {
                    power.Refill();
                    if(mode=="main-only") power.Spend(5);
                    yield return null;
                    Array.Clear(RewardProbe.Ticks,0,phases.Length);
                    RewardProbe.Writes=0;
                    long cpuStart=CurrentThreadCpuTicks();
                    long start=Stopwatch.GetTimestamp();
                    RewardProbe.Active=true;
                    try
                    {
                        for(int k=0;k<count;k++) power.GrantReward(new AirPowerReward(AirPowerRewardSource.AirNode),3);
                    }
                    finally { RewardProbe.Active=false; }
                    long elapsed=Stopwatch.GetTimestamp()-start;
                    long cpuEnd=CurrentThreadCpuTicks();
                    if(i>=0)
                    {
                        cpuAvailable &= cpuStart>=0 && cpuEnd>=cpuStart;
                        cpuTicks += cpuEnd-cpuStart;
                        times[i]=elapsed*1000d/Stopwatch.Frequency;
                        writes+=RewardProbe.Writes;
                        for(int j=0;j<phases.Length;j++) phases[j][i]=RewardProbe.Ticks[j]*1000d/Stopwatch.Frequency;
                    }
                    yield return null;
                    if(i>=0) {frames[i]=Time.unscaledDeltaTime*1000;bytes[i]=gc.LastValue;}
                    if(mode!="main-only")
                    {
                        if(gear.Stored(gear.Equipped)!=3*count) throw new InvalidDataException("Unexpected reserve");
                        power.Spend(12);
                        if(!gear.TryActivate(true,Time.frameCount)) throw new InvalidDataException("Transfer failed");
                    }
                    yield return null;
                }
                string breakdown=cpuAvailable ? FormattableString.Invariant($" threadCpuMeanMs={TimeSpan.FromTicks(cpuTicks).TotalMilliseconds/samples:F3}") : " threadCpuMeanMs=unavailable";
                for(int j=0;j<phases.Length;j++)
                    breakdown+=" "+(RewardProbe.Phase)j+"="+Percentile(phases[j],.5).ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
                Debug.Log(FormattableString.Invariant($"REWARD_PROFILE {mode} n={samples} writes={writes} ms50={Percentile(times,.5):F3} ms95={Percentile(times,.95):F3} frameBytes50={Percentile(bytes,.5)} frame50={Percentile(frames,.5):F2} frame95={Percentile(frames,.95):F2}{breakdown}"));
            }
        }
    }
}
#endif
