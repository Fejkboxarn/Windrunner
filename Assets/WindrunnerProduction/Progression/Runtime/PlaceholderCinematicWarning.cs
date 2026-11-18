using UnityEngine;
namespace Windrunner.Production.Progression
{
    /// <summary>Presentation only. No input controls, Timeline state or progression writes.</summary>
    public static class PlaceholderCinematicWarning
    {
        static GUIStyle label;
        public static void Draw(bool visible)
        {
            if(!visible)return;
            var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;
            try
            {
                GUI.matrix=Matrix4x4.identity;GUI.depth=-1000;
                float w=Screen.width,h=Screen.height;
                GUI.color=new Color(1,.04f,.04f,.40f);
                Line(new Vector2(w*.08f,h*.10f),new Vector2(w*.92f,h*.90f),Mathf.Max(10,h*.018f));
                Line(new Vector2(w*.92f,h*.10f),new Vector2(w*.08f,h*.90f),Mathf.Max(10,h*.018f));
                if(label==null)label=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,wordWrap=true};
                label.font=WindrunnerUITheme.Current.Font;label.fontSize=Mathf.RoundToInt(Mathf.Min(w/22,h/16));
                GUI.color=new Color(.08f,0,0,.82f);GUI.DrawTexture(new Rect(w*.08f,h*.23f,w*.84f,h*.13f),Texture2D.whiteTexture);
                GUI.color=Color.white;label.normal.textColor=new Color(1,.23f,.20f);
                GUI.Label(new Rect(w*.08f,h*.23f,w*.84f,h*.13f),"CUTSCENES ARE NOT FINAL",label);
            }
            finally{GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;}
        }
        public static void Line(Vector2 start,Vector2 end,float width)
        {
            var matrix=GUI.matrix;var delta=end-start;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,start);
            GUI.DrawTexture(new Rect(start.x,start.y-width*.5f,delta.magnitude,width),Texture2D.whiteTexture);GUI.matrix=matrix;
        }
    }
}
