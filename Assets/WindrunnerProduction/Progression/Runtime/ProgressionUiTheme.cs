using UnityEngine;

namespace Windrunner.Production.Progression
{
    public static class ProgressionUiTheme
    {
        public static GUIStyle Title, Heading, Body, Muted, Button, Card;
        public static readonly Color Accent = new Color(0.35f, 0.85f, 0.83f);
        private static Texture2D background;
        public static void Ensure()
        {
            if (Body != null && background != null) return;
            background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            background.SetPixel(0, 0, new Color(0.025f, 0.045f, 0.075f, 1f)); background.Apply();
            Body = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, richText = false, padding = new RectOffset(4, 4, 4, 4) };
            Body.normal.textColor = new Color(0.92f, 0.95f, 0.98f);
            Title = new GUIStyle(Body) { fontSize = 32, fontStyle = FontStyle.Bold };
            Heading = new GUIStyle(Body) { fontSize = 22, fontStyle = FontStyle.Bold }; Heading.normal.textColor = Accent;
            Muted = new GUIStyle(Body) { fontSize = 16 }; Muted.normal.textColor = new Color(0.66f, 0.75f, 0.84f);
            Button = new GUIStyle(GUI.skin.button) { fontSize = 18, wordWrap = true, padding = new RectOffset(12, 12, 10, 10), richText = false };
            Card = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18, 18, 14, 14) }; Card.normal.background = background;
        }
        public static Matrix4x4 BeginCanvas()
        {
            Ensure(); var old = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2), Quaternion.identity, new Vector3(scale, scale, 1));
            return old;
        }
    }
}
