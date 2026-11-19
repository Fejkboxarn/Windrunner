using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Windrunner.Suntemple
{
    // Opt-in development capture only. No normal gameplay initialization or persistent state.
    public sealed class SuntempleRenderProbe : MonoBehaviour
    {
        [Serializable] public class View {public string name;public Vector3 position,extent;}
        [Serializable] public class Views {public View[] cameras;}
        string output;readonly List<string> errors=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if(!Debug.isDebugBuild||SceneManager.GetActiveScene().name!="DemoMap")return;
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-suntempleProbe");
            if(index<0||index+1>=args.Length)return;
            Application.runInBackground=true;
            if(Camera.main)foreach(var b in Camera.main.GetComponents<MonoBehaviour>())if(b is Flythrough||b is SmoothMouseLook)b.enabled=false;
            var probe=new GameObject("Suntemple opt-in render capture").AddComponent<SuntempleRenderProbe>();probe.output=Path.GetFullPath(args[index+1]);
        }
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);yield return null;
            var camera=Camera.main;
            if(!camera){errors.Add("No demo camera");Finish();yield break;}
            // Demo input is disabled only during the explicitly requested deterministic capture.
            foreach(var b in camera.GetComponents<MonoBehaviour>())if(b is Flythrough||b is SmoothMouseLook)b.enabled=false;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            for(int i=0;i<120;i++)yield return null;
            yield return Capture(camera,"runtime-entry");
            string viewpoints=Path.Combine(output,"viewpoints.json");
            if(File.Exists(viewpoints))foreach(var v in JsonUtility.FromJson<Views>(File.ReadAllText(viewpoints)).cameras)
            {
                camera.transform.SetPositionAndRotation(v.position,Quaternion.Euler(v.extent));
                for(int i=0;i<30;i++)yield return null;
                yield return Capture(camera,"runtime-"+v.name.Split(':')[0]);
            }
            Finish();
        }
        IEnumerator Capture(Camera camera,string name)
        {
            yield return new WaitForEndOfFrame();
            var rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32);
            var old=camera.targetTexture;var active=RenderTexture.active;
            var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();
                var pixels=texture.GetPixels32();int visible=0;
                for(int i=0;i<pixels.Length;i+=100)if(pixels[i].r+pixels[i].g+pixels[i].b>20)visible++;
                if(visible<100)errors.Add(name+": rejected empty/dark capture");
                File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());
            }
            finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Destroy(texture);}
        }
        void Finish(){File.WriteAllText(Path.Combine(output,"runtime-results.txt"),"Error count: "+errors.Count+"\n"+string.Join("\n",errors));Application.Quit(errors.Count==0?0:1);}
    }
}
