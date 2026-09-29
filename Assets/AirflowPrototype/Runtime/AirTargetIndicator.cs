using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Temporary prototype reticle. Its purpose is to make target acquisition
    /// readable before final VFX replace it.
    /// </summary>
    public sealed class AirTargetIndicator : MonoBehaviour
    {
        [SerializeField] private AirTargeter targeter;
        [SerializeField] private Camera targetCamera;
        [SerializeField, Min(4f)] private float size = 28f;
        [SerializeField, Min(1f)] private float thickness = 2f;

        private Texture2D _texture;

        public void Configure(AirTargeter newTargeter, Camera newCamera)
        {
            targeter = newTargeter;
            targetCamera = newCamera;
        }

        private void Awake()
        {
            if (targeter == null)
                targeter = GetComponent<AirTargeter>();

            if (targetCamera == null)
                targetCamera = Camera.main;

            _texture = Texture2D.whiteTexture;
        }

        private void OnGUI()
        {
            if (targeter == null || targetCamera == null || targeter.CurrentTarget == null)
                return;

            Vector3 screen = targetCamera.WorldToScreenPoint(
                targeter.CurrentTarget.TargetPosition);

            if (screen.z <= 0f)
                return;

            float x = screen.x;
            float y = Screen.height - screen.y;
            float half = size * 0.5f;
            float corner = size * 0.34f;

            Color old = GUI.color;
            GUI.color = new Color(0.88f, 0.97f, 1f, 0.95f);

            DrawRect(x - half, y - half, corner, thickness);
            DrawRect(x - half, y - half, thickness, corner);

            DrawRect(x + half - corner, y - half, corner, thickness);
            DrawRect(x + half - thickness, y - half, thickness, corner);

            DrawRect(x - half, y + half - thickness, corner, thickness);
            DrawRect(x - half, y + half - corner, thickness, corner);

            DrawRect(x + half - corner, y + half - thickness, corner, thickness);
            DrawRect(x + half - thickness, y + half - corner, thickness, corner);

            GUI.color = old;
        }

        private void DrawRect(float x, float y, float width, float height)
        {
            GUI.DrawTexture(new Rect(x, y, width, height), _texture);
        }
    }
}
