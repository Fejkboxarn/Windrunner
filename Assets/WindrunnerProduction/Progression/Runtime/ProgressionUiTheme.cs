using UnityEngine;

namespace Windrunner.Production.Progression
{
    public static class ProgressionUiTheme
    {
        public static GUIStyle Title, Heading, Body, Muted, Button, Card;
        public static Color Accent => WindrunnerUITheme.Current.accent;
        private static Texture2D background;
        public static void Ensure()
        {
            if (Body != null && background != null) return;
            background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            background.SetPixel(0, 0, WindrunnerUITheme.Current.panel); background.Apply();
            Body = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, richText = false, padding = new RectOffset(4, 4, 4, 4) };
            Body.font = WindrunnerUITheme.Current.Font;
            Body.normal.textColor = WindrunnerUITheme.Current.ink;
            Title = new GUIStyle(Body) { fontSize = 32, fontStyle = FontStyle.Bold };
            Heading = new GUIStyle(Body) { fontSize = 22, fontStyle = FontStyle.Bold }; Heading.normal.textColor = Accent;
            Muted = new GUIStyle(Body) { fontSize = 16 }; Muted.normal.textColor = WindrunnerUITheme.Current.muted;
            Button = new GUIStyle(GUI.skin.button) { fontSize = 18, wordWrap = true, padding = new RectOffset(12, 12, 10, 10), richText = false };
            Button.normal.background=background;Button.hover.background=background;Button.active.background=background;
            Button.onNormal.background=background;Button.onHover.background=background;Button.onActive.background=background;
            Button.normal.textColor=WindrunnerUITheme.Current.ink;Button.hover.textColor=Accent;Button.active.textColor=Accent;
            Button.onNormal.textColor=Accent;Button.onHover.textColor=Accent;Button.border=new RectOffset();
            Button.font=WindrunnerUITheme.Current.Font;Button.alignment=TextAnchor.MiddleLeft;
            Card = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18, 18, 14, 14) }; Card.normal.background = background;
        }
        public static Matrix4x4 BeginCanvas()
        {
            Ensure(); var old = GUI.matrix;
            var safe=Screen.safeArea;
            float scale = Mathf.Min(safe.width / 1280f, safe.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x+(safe.width - 1280 * scale) / 2, Screen.height-safe.yMax+(safe.height - 720 * scale) / 2), Quaternion.identity, new Vector3(scale, scale, 1));
            return old;
        }
    }
}
