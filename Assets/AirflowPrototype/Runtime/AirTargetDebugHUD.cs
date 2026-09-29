using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Small optional diagnostic panel for target scoring.
    /// Disable the component when you no longer need it.
    /// </summary>
    public sealed class AirTargetDebugHUD : MonoBehaviour
    {
        [SerializeField] private AirTargeter targeter;
        [SerializeField, Range(1, 8)] private int rows = 4;

        private GUIStyle _style;
        private GUIStyle _header;

        public void Configure(AirTargeter newTargeter)
        {
            targeter = newTargeter;
        }

        private void Awake()
        {
            if (targeter == null)
                targeter = GetComponent<AirTargeter>();
        }

        private void OnGUI()
        {
            if (targeter == null)
                return;

            EnsureStyles();

            float width = 420f;
            float height = 76f + rows * 22f;

            GUILayout.BeginArea(
                new Rect(Screen.width - width - 16f, 16f, width, height),
                GUI.skin.box);

            GUILayout.Label("AIR TARGETING", _header);

            string current = targeter.CurrentTarget != null
                ? targeter.CurrentTarget.name
                : "None";

            GUILayout.Label(
                $"Target: {current}   Score: {targeter.CurrentScore:0.000}",
                _style);

            int count = Mathf.Min(rows, targeter.DebugCandidates.Count);

            for (int i = 0; i < count; i++)
            {
                AirTargeter.CandidateScore c = targeter.DebugCandidates[i];

                GUILayout.Label(
                    $"{i + 1}. {c.Node.name,-16} " +
                    $"TOTAL {c.Total:0.00}  " +
                    $"screen {c.Screen:0.00}  " +
                    $"move {c.Movement:0.00}  " +
                    $"dist {c.Distance:0.00}  " +
                    $"cam {c.CameraForward:0.00}",
                    _style);
            }

            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_style != null)
                return;

            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11
            };

            _header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
