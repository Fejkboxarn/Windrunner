using UnityEngine;
using AirflowPrototype;
namespace Windrunner.MovementLab
{
    public enum LaneKind { Slopes, Jumps, Dash, Sling, AerialSteering, Momentum, Wind, HighSpeed, Overdrive, Combined }
    public enum LanePreset { Short, Medium, Long, Extreme }

    public sealed class GymLane : MonoBehaviour
    {
        public LaneKind kind;
        public LanePreset preset;
        [Min(4)] public float spacing = 30;
        public float elevation = 3;
        [Min(15)] public float runUp = 55;
        [Range(-75,75)] public float approachAngle, slopeAngle = 15;
        public Vector3 targetOffset = new Vector3(0, 5, 0), tunnelSize = new Vector3(14, 15, 32);
        public Transform destination, launch, landing, slope, target, startGate, endGate, sign;
        public WindTunnel continuous, impulse;
        public TextMesh label;

        public void SetPreset(LanePreset value)
        {
            preset = value;
            int i = (int)value;
            spacing = new[] { 18f, 55f, 145f, 350f }[i];
            elevation = new[] { 2f, 10f, 35f, 100f }[i];
            approachAngle = new[] { 0f, 10f, 25f, 45f }[i];
            runUp = new[] { 55f, 80f, 120f, 170f }[i];
        }

        public void Apply()
        {
            // Rotate the approach within the lane's local horizontal plane.
            Quaternion heading = Quaternion.Euler(0, approachAngle, 0);
            Vector3 direction = heading * Vector3.forward;
            Vector3 end = direction * spacing + Vector3.up * elevation;
            destination.localPosition = -direction * (runUp - 12) + Vector3.up * 1.2f;
            destination.localRotation = heading;
            Place(launch, direction * ((10 - runUp) * .5f) - Vector3.up, heading, new Vector3(18, 2, runUp + 10));
            Place(landing, end - Vector3.up, heading, new Vector3(26, 2, 40));
            float angle = Mathf.Deg2Rad * slopeAngle;
            Place(slope, direction * 10 + Vector3.up * (7 * Mathf.Sin(angle) - .15f),
                heading * Quaternion.Euler(-slopeAngle, 0, 0), new Vector3(14, .4f, 14));
            target.localPosition = end + heading * targetOffset;
            startGate.localPosition = direction * 7 + Vector3.up * .1f;
            endGate.localPosition = end + Vector3.up * .1f;
            sign.localPosition = destination.localPosition + heading * new Vector3(-6, 3, 12);
            sign.localRotation = heading;
            Place(continuous.transform, -direction * 10 + Vector3.up * (tunnelSize.y * .45f), heading, SafeSize(tunnelSize));
            Place(impulse.transform, direction * 5 + Vector3.up * (tunnelSize.y * .45f), heading, SafeSize(new Vector3(tunnelSize.x,tunnelSize.y,tunnelSize.z * .35f)));
            bool wind = kind == LaneKind.Wind || kind == LaneKind.HighSpeed || kind == LaneKind.Combined;
            continuous.gameObject.SetActive(wind); impulse.gameObject.SetActive(wind);
            target.gameObject.SetActive(kind == LaneKind.Sling || kind == LaneKind.Combined);
            slope.gameObject.SetActive(kind != LaneKind.Dash && kind != LaneKind.Overdrive);
            label.text = kind + "  /  " + preset + "\n" + spacing.ToString("0") + " m     rise " + elevation.ToString("0") + " m";
        }
        private static Vector3 SafeSize(Vector3 size) => Vector3.Max(Vector3.one, size);
        private static void Place(Transform t, Vector3 p, Quaternion q, Vector3 scale)
        { t.localPosition = p; t.localRotation = q; t.localScale = scale; }
    }
}
