using UnityEngine;
namespace Windrunner.Production
{
    [CreateAssetMenu(menuName="Windrunner/UI Theme")]
    public sealed class WindrunnerUITheme : ScriptableObject
    {
        [Header("Player-facing presentation (working title, not final identity)")]
        public string displayTitle="WINDRUNNER";
        public string subtitle="";
        public Sprite logo;
        public Texture2D backgroundPresentation;
        public string tagline="";
        [Tooltip("Neutral generated atmosphere when no background texture is assigned.")]
        public bool generatedAtmosphere=true;
        [Header("UI style")]
        public Color background=new Color(.025f,.038f,.058f,1);
        public Color panel=new Color(.055f,.085f,.12f,.94f);
        public Color ink=new Color(.91f,.95f,.98f,1);
        public Color muted=new Color(.58f,.69f,.78f,1);
        public Color accent=new Color(.35f,.82f,.91f,1);
        public Color selected=new Color(.09f,.19f,.25f,1);
        public Color error=new Color(.95f,.56f,.45f,1);
        public Font font;
        public int bodySize=22,headingSize=36,brandSize=66;
        public float rowHeight=58,spacing=8,focusSeconds=.10f,enterSeconds=.16f;
        public AudioClip focusSound,confirmSound;
        static WindrunnerUITheme current;
        public static WindrunnerUITheme Current
        {
            get
            {
                if(!current)current=Resources.Load<WindrunnerUITheme>("WindrunnerUITheme");
                if(!current){current=CreateInstance<WindrunnerUITheme>();current.hideFlags=HideFlags.HideAndDontSave;}
                return current;
            }
        }
        public static bool ReducedMotion {get=>PlayerPrefs.GetInt("Windrunner.UI.ReducedMotion",0)!=0;set{PlayerPrefs.SetInt("Windrunner.UI.ReducedMotion",value?1:0);PlayerPrefs.Save();}}
        public static float Scale {get=>Mathf.Clamp(PlayerPrefs.GetFloat("Windrunner.UI.Scale",1),1,1.2f);set{PlayerPrefs.SetFloat("Windrunner.UI.Scale",Mathf.Clamp(value,1,1.2f));PlayerPrefs.Save();}}
        public void TintMeter(Material material)
        {
            if(!material)return;
            foreach(var key in new[]{"_Fill_Color_No_Gradient","_Fill_Gradient_Color_Left"})
                if(material.HasProperty(key))material.SetColor(key,accent);
            if(material.HasProperty("_Fill_Gradient_Color_Right"))material.SetColor("_Fill_Gradient_Color_Right",Color.Lerp(accent,ink,.25f));
        }
        public Font Font=>font?font:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
