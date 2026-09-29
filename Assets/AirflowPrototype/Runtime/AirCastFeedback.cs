using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Prototype timing feedback around the selected node.
    /// The target bar now represents target attunement rather than raw button-hold time.
    /// </summary>
    public sealed class AirCastFeedback : MonoBehaviour
    {
        [SerializeField] private AirCaster caster;
        [SerializeField] private Camera targetCamera;

        private Texture2D _white;

        public void Configure(AirCaster newCaster, Camera newCamera)
        {
            caster = newCaster;
            targetCamera = newCamera;
        }

        private void Awake()
        {
            if (caster == null)
                caster = GetComponent<AirCaster>();

            if (targetCamera == null)
                targetCamera = Camera.main;

            _white = Texture2D.whiteTexture;
        }

        private void OnGUI()
        {
            if (caster == null ||
                targetCamera == null ||
                !caster.IsCharging ||
                caster.CurrentTarget == null)
            {
                return;
            }

            Vector3 screen =
                targetCamera.WorldToScreenPoint(
                    caster.CurrentTarget.TargetPosition);

            if (screen.z <= 0f)
                return;

            float x = screen.x;
            float y = Screen.height - screen.y + 28f;

            const float width = 52f;
            const float height = 5f;

            Rect background =
                new Rect(x - width * 0.5f, y, width, height);

            Color old = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(background, _white);

            float lock01 = caster.TargetLock01;

            GUI.color = caster.IsReady
                ? new Color(0.88f, 0.98f, 1f, 1f)
                : new Color(0.48f, 0.79f, 0.93f, 0.95f);

            GUI.DrawTexture(
                new Rect(
                    background.x + 1f,
                    background.y + 1f,
                    (background.width - 2f) * lock01,
                    background.height - 2f),
                _white);

            if (caster.IsReady)
            {
                const float marker = 3f;
                GUI.DrawTexture(
                    new Rect(
                        x - marker * 0.5f,
                        y - 4f,
                        marker,
                        height + 8f),
                    _white);
            }

            GUI.color = old;
        }
    }
}
