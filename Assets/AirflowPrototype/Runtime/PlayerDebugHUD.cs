using UnityEngine;

namespace AirflowPrototype
{
    public sealed class PlayerDebugHUD : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private AirPower airPower;
        [SerializeField] private AirDash airDash;

        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;

        public void Configure(
            PlayerMotor newMotor,
            PlayerInputReader newInput,
            AirPower newAirPower = null,
            AirDash newAirDash = null)
        {
            motor = newMotor;
            input = newInput;
            airPower = newAirPower;
            airDash = newAirDash;
        }

        private void OnGUI()
        {
            if (motor == null || input == null)
                return;

            EnsureStyles();

            const float width = 350f;
            GUILayout.BeginArea(new Rect(16f, 16f, width, 310f), GUI.skin.box);
            GUILayout.Label("AIRFLOW — BATCH 2", _headerStyle);
            GUILayout.Space(4f);
            GUILayout.Label($"Speed          {motor.HorizontalSpeed,6:0.00} m/s", _labelStyle);
            GUILayout.Label($"Vertical       {motor.VerticalSpeed,6:0.00} m/s", _labelStyle);
            GUILayout.Label($"Grounded       {(motor.IsGrounded ? "YES" : "NO")}", _labelStyle);
            GUILayout.Label($"Dash           {(airDash != null && airDash.IsDashing ? "ACTIVE" : "OFF")}", _labelStyle);
            GUILayout.Label($"Move Input     {input.Move}", _labelStyle);
            GUILayout.Label($"Coyote Left    {motor.CoyoteRemaining,6:0.000} s", _labelStyle);
            GUILayout.Label($"Buffer Left    {motor.JumpBufferRemaining,6:0.000} s", _labelStyle);

            if (airPower != null)
            {
                GUILayout.Space(7f);
                GUILayout.Label($"Air Power      {airPower.Current,6:0.0} / {airPower.Max:0.0}", _labelStyle);

                Rect bar = GUILayoutUtility.GetRect(width - 24f, 14f);
                GUI.Box(bar, GUIContent.none);
                Rect fill = new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * airPower.Normalized, bar.height - 4f);
                GUI.DrawTexture(fill, Texture2D.whiteTexture);
            }

            GUILayout.Space(8f);
            GUILayout.Label("WASD / Left Stick — Move", _labelStyle);
            GUILayout.Label("Left Shift / Right Trigger — Air Dash", _labelStyle);
            GUILayout.Label("Space / South Button — Jump", _labelStyle);
            GUILayout.Label("Mouse / Right Stick — Camera", _labelStyle);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null)
                return;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
