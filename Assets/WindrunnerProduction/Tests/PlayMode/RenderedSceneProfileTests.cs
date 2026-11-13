using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Windrunner.Production.Tests
{
    [Category("Profiling")]
    public sealed class RenderedSceneProfileTests
    {
        [Serializable]
        private sealed class Result
        {
            public string utc, gpu, api, unity;
            public int width = 1280, height = 720, frames = 180;
            public bool allocationCounterCalibrated;
            public string pacing;
            public int targetFrameRate, vSync;
            public double medianGpuMs = -1;
            public long calibrationBytes;
            public double medianFrameMs, p95FrameMs, medianMainThreadMs, medianGcBytes;
        }

        [UnityTest]
        public IEnumerator RenderedSceneAndCalibratedAllocationBaseline()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "Run with -Rendered.");
            yield return SceneManager.LoadSceneAsync("Assets/AirflowPrototype/Generated/Batch1_Playground.unity");
            yield return null; // Allow scene frame-cap startup to complete.
            int previousCap=Application.targetFrameRate,previousVsync=QualitySettings.vSyncCount;
            bool uncapped=Array.IndexOf(Environment.GetCommandLineArgs(),"-windrunnerUncappedProfile")>=0;
            if(uncapped){Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;}
            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            var texture = new RenderTexture(1280, 720, 24);
            var previous = camera.targetTexture;
            var originalActive = RenderTexture.active;
            var probe = new Texture2D(16, 16, TextureFormat.RGB24, false);
            var result = new Result { utc = DateTime.UtcNow.ToString("O"), gpu = SystemInfo.graphicsDeviceName,
                api = SystemInfo.graphicsDeviceType.ToString(), unity = Application.unityVersion, pacing=uncapped?"uncapped":"scene cap", targetFrameRate=Application.targetFrameRate,vSync=QualitySettings.vSyncCount };
            using (var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 4))
            using (var gpu = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 4))
            using (var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 4))
            {
                try
                {
                    camera.targetTexture = texture;
                    for (int i = 0; i < 90; i++) yield return null;
                    RenderTexture.active = texture;
                    probe.ReadPixels(new Rect(632, 352, 16, 16), 0, 0); probe.Apply();
                    Assert.That(probe.GetPixels().Any(c => c.r + c.g + c.b > 0.01f), Is.True, "Camera must produce nonblack rendered pixels.");
                    RenderTexture.active = originalActive;
                    Assert.That(gc.Valid, Is.True, "Native frame allocation counter unavailable.");
                    var calibration = new byte[1024 * 1024];
                    for (int i = 0; i < 3; i++)
                    {
                        yield return null;
                        result.calibrationBytes = Math.Max(result.calibrationBytes, gc.LastValue);
                    }
                    GC.KeepAlive(calibration);
                    result.allocationCounterCalibrated = result.calibrationBytes >= calibration.Length;
                    Assert.That(result.allocationCounterCalibrated, Is.True, "Counter must observe the retained 1 MiB calibration allocation.");
                    var gpuTimes=new double[result.frames];
                    var frames = new double[result.frames]; var cpu = new double[result.frames]; var bytes = new double[result.frames];
                    for (int i = 0; i < 20; i++) yield return null;
                    for (int i = 0; i < result.frames; i++)
                    {
                        yield return null;
                        frames[i] = Time.unscaledDeltaTime * 1000.0;
                        cpu[i] = main.Valid ? main.LastValue / 1000000.0 : -1;
                        bytes[i] = gc.LastValue;
                        gpuTimes[i]=gpu.Valid && gpu.LastValue>0?gpu.LastValue/1000000.0:-1;
                    }
                    Array.Sort(gpuTimes);result.medianGpuMs=gpuTimes[result.frames/2];
                    Array.Sort(frames); Array.Sort(cpu); Array.Sort(bytes);
                    result.medianFrameMs = frames[result.frames / 2]; result.p95FrameMs = frames[(int)(result.frames * 0.95)];
                    result.medianMainThreadMs = cpu[result.frames / 2]; result.medianGcBytes = bytes[result.frames / 2];
                    string json = JsonUtility.ToJson(result, true);
                    File.WriteAllText("Logs/ProductionMigration/rendered-profile-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json", json);
                    Debug.Log("[Rendered profile] " + json);
                }
                finally
                {
                    Application.targetFrameRate=previousCap;QualitySettings.vSyncCount=previousVsync;
                    camera.targetTexture = previous; RenderTexture.active = originalActive;
                    UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(probe);
                    if (Windrunner.Production.Unity.ProjectRoot.Active != null)
                        UnityEngine.Object.Destroy(Windrunner.Production.Unity.ProjectRoot.Active.gameObject);
                }
            }
            var cleanup = SceneManager.CreateScene("Profile cleanup"); SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync("Batch1_Playground");
        }
    }
}
