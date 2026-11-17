using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

namespace Windrunner.Frontend
{
    [Serializable] public struct DesktopOptions
    {
        public int width,height,quality,vsync,limit;
        public FullScreenMode mode;
        public static DesktopOptions Current()=>new DesktopOptions{width=Screen.width,height=Screen.height,mode=Screen.fullScreenMode,
            quality=QualitySettings.GetQualityLevel(),vsync=QualitySettings.vSyncCount,limit=Application.targetFrameRate};
        public bool Supported()
        {
            int w=width,h=height;
            return width>=640 && height>=360 && width<=16384 && height<=16384 && Enum.IsDefined(typeof(FullScreenMode),mode) && quality>=0 && quality<QualitySettings.names.Length &&
                vsync>=0 && vsync<=4 && (limit==-1 || limit>=15 && limit<=1000) &&
                (mode==FullScreenMode.Windowed || mode==FullScreenMode.FullScreenWindow || Screen.resolutions.Any(r=>r.width==w && r.height==h));
        }
        public void Apply()
        {
            if(!Supported())throw new ArgumentException("These display settings are unavailable.");
            if(Screen.width!=width || Screen.height!=height || Screen.fullScreenMode!=mode)Screen.SetResolution(width,height,mode);
            if(quality!=QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(quality,true);
            QualitySettings.vSyncCount=vsync;Application.targetFrameRate=limit;
        }
        public static DesktopOptions SafeWindow()
        {
            var safe=Current();safe.mode=FullScreenMode.Windowed;safe.width=1280;safe.height=720;
            safe.limit=-1;safe.vsync=1;return safe;
        }
    }
    public sealed class DesktopPreferences
    {
        private readonly string prefix;
        public DesktopPreferences(string prefix="Windrunner.Desktop.v1.") {this.prefix=prefix;}
        public float Volume => Mathf.Clamp01(PlayerPrefs.GetFloat(prefix+"master",1));
        public float Music => Mathf.Clamp01(PlayerPrefs.GetFloat(prefix+"music",1));
        public float Ambience => Mathf.Clamp01(PlayerPrefs.GetFloat(prefix+"ambience",1));
        public void SetMix(AudioMixer mixer,bool music,float value)
        {
            value=Mathf.Clamp01(value);
            ApplyBus(mixer,music,value);
            PlayerPrefs.SetFloat(prefix+(music?"music":"ambience"),value);PlayerPrefs.Save();
        }
        public void RestoreMix(AudioMixer mixer)
        {
            ApplyBus(mixer,true,Music);ApplyBus(mixer,false,Ambience);
        }
        private static readonly string[] MusicBus={"PlayerMusic"};
        private static readonly string[] EnvironmentBuses={"PlayerForest","PlayerWater","PlayerEnvironment"};
        private static void ApplyBus(AudioMixer mixer,bool music,float level)
        {
            foreach(string parameter in music?MusicBus:EnvironmentBuses)
            {
                // WorldAudio's starting snapshot is -15 dB for Music and 0 dB for the three parents.
                // Unity can return the previous override immediately after ClearFloat.
                float authored=music?-15f:0f;
                if(!mixer.SetFloat(parameter,level<=0?-80:Mathf.Max(-80,authored+20*Mathf.Log10(level))))
                    throw new InvalidOperationException("Could not set mixer fader "+parameter);
            }
        }
        public void SetVolume(float value)
        {
            AudioListener.volume=Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(prefix+"master",AudioListener.volume);PlayerPrefs.Save();
        }
        public DesktopOptions Confirmed()
        {
            var current=DesktopOptions.Current();
            if(!PlayerPrefs.HasKey(prefix+"display"))return current;
            try
            {
                var saved=JsonUtility.FromJson<DesktopOptions>(PlayerPrefs.GetString(prefix+"display"));
                if(saved.Supported())return saved;
            }
            catch(ArgumentException){ }
            return DesktopOptions.SafeWindow();
        }
        public void Restore()
        {
            AudioListener.volume=Volume;
            if(PlayerPrefs.HasKey(prefix+"display"))Confirmed().Apply();
        }
        public void Confirm(DesktopOptions value)
        {
            if(!value.Supported())throw new ArgumentException("Unsupported display mode.");
            PlayerPrefs.SetString(prefix+"display",JsonUtility.ToJson(value));PlayerPrefs.Save();
        }
    }
}
