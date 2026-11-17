using AirflowPrototype;
using UnityEngine;
using UnityEngine.InputSystem;
using Windrunner.Production.Input;
namespace Windrunner.MovementLab
{
    [DisallowMultipleComponent]
    public sealed class MovementLabPlayer : MonoBehaviour
    {
        public PlayerMotor motor;
        public CharacterController controller;
        public AirPower power;
        public AirSlingController sling;
        public WindrunnerInputHost inputHost;
        public MovementLabProfile profile;
        public GymLane[] lanes;
        public WindTunnel[] volumes;
        public bool experimentalPairEquipped = true, diagnostics = true;
        public float recoveryHeight = -25;
        public int destinationIndex;
        private PlayerOrbitCamera orbit;
        private WindContact[] contacts;
        private readonly OverdriveSession drive = new OverdriveSession();
        private InputAction overdrive, next, previous, reset, overlay;
        private int interruption;
        public int WindCount { get; private set; }
        public float LastWindDelta { get; private set; }
        public float WindGain { get; private set; }
        public bool Boosting => drive.Active;
        public float Distance { get; private set; }
        public float Peak { get; private set; }
        private Vector3 lastPosition, lastVelocity;
        private float elapsed, airborne, acceleration, nextHud;
        private string display;
        public LineRenderer trail;
        private readonly Vector3[] points = new Vector3[128];
        private int pointCount;
        private void OnEnable()
        {
            contacts = new WindContact[volumes.Length];
            if (motor.MovementInfluence != null) throw new System.InvalidOperationException("Player already has an influence provider.");
            motor.MovementInfluence = Resolve;
        }
        private void Start()
        {
            var map = inputHost.Input.Actions.FindActionMap("Gameplay", true);
            overdrive = map.FindAction("Overdrive", true);
            next = map.FindAction("LabNext", true); previous = map.FindAction("LabPrevious", true);
            reset = map.FindAction("LabReset", true); overlay = map.FindAction("LabDiagnostics", true);
            orbit = motor.ViewCamera.GetComponent<PlayerOrbitCamera>();
            Teleport(destinationIndex);
        }
        private void OnDisable() { if (motor != null && motor.MovementInfluence == Resolve) motor.MovementInfluence = null; drive.Interrupt(); }
        private void OnApplicationFocus(bool focus) { if (!focus) drive.Interrupt(); }

        private Vector3 Resolve(Vector3 velocity, bool grounded, float dt)
        {
            var input = inputHost.Input;
            Vector3 wind = Vector3.zero;
            WindCount = 0; LastWindDelta = 0;
            if (input == null || overdrive == null) return wind;
            if (input.InterruptionVersion != interruption)
            { drive.Interrupt(); interruption = input.InterruptionVersion; }
            if (input.IsGameplayBlocked || sling.IsSlinging || motor.HasExternalTraversalVelocity)
            { drive.Interrupt(); return wind; }
            for (int i = 0; i < volumes.Length; i++)
            {
                var volume = volumes[i];
                float depth = volume.isActiveAndEnabled ? volume.Depth(transform.position) : -1;
                var candidate = contacts[i].Sample(volume.Settings, velocity, grounded, depth, dt);
                wind = LabVelocity.Strongest(wind, candidate);
                if (contacts[i].Inside) WindCount++;
            }
            wind = Vector3.ClampMagnitude(wind, Mathf.Max(0, profile.combinedSpeedCap - velocity.magnitude));
            LastWindDelta = wind.magnitude; WindGain += LastWindDelta;
            float cost = drive.Request(profile.overdrive, overdrive.IsPressed(), experimentalPairEquipped && power.CanStartDash, power.Current, dt);
            if (cost <= 0) return wind;
            bool remaining = power.Spend(cost);
            var boost = drive.Apply(profile.overdrive, velocity + wind, grounded, cost, remaining);
            return wind + Vector3.ClampMagnitude(boost, Mathf.Max(0, profile.combinedSpeedCap - (velocity + wind).magnitude));
        }
        public void Teleport(int index)
        {
            destinationIndex = (index % lanes.Length + lanes.Length) % lanes.Length;
            var spawn = lanes[destinationIndex].destination;
            sling.CancelSling(); motor.GetComponent<AirDash>().CancelAutoDash(); motor.StopForTraversalImpact();
            controller.enabled = false; transform.position = spawn.position; controller.enabled = true;
            orbit.ResetAfterTeleport(spawn.eulerAngles.y);
            System.Array.Clear(contacts, 0, contacts.Length); drive.Interrupt(); power.Refill(); ResetSession();
        }
        public void ResetSession()
        {
            Distance = Peak = elapsed = airborne = WindGain = acceleration = 0;
            lastPosition = transform.position; lastVelocity = motor.Velocity; pointCount = 0;
            if (trail != null) trail.positionCount = 0;
        }
        private void LateUpdate()
        {
            if (inputHost == null || inputHost.Input == null || next == null) return;
            if (inputHost.Input.IsGameplayBlocked) { drive.Interrupt(); return; }
            if (Time.deltaTime <= 0) return;
            if (next.WasPressedThisFrame()) Teleport(destinationIndex + 1);
            else if (previous.WasPressedThisFrame()) Teleport(destinationIndex - 1);
            else if (reset.WasPressedThisFrame() || transform.position.y < Mathf.Max(recoveryHeight, Mathf.Min(lanes[destinationIndex].destination.position.y, lanes[destinationIndex].landing.position.y) - 10)) Teleport(destinationIndex);
            if (overlay.WasPressedThisFrame()) diagnostics = !diagnostics;
            if(trail != null) trail.enabled = diagnostics;
            float dt = Time.deltaTime;
            Vector3 velocity = motor.Velocity, position = transform.position;
            Distance += Vector3.Distance(position,lastPosition); elapsed += dt;
            if (!motor.IsGrounded) airborne += dt;
            acceleration = (velocity-lastVelocity).magnitude / dt;
            Peak = Mathf.Max(Peak, velocity.magnitude); lastPosition = position; lastVelocity = velocity;
            if (!diagnostics || Time.unscaledTime < nextHud) return;
            nextHud = Time.unscaledTime + .15f;
            if (pointCount == points.Length) { System.Array.Copy(points,1,points,0,points.Length-1); pointCount--; }
            points[pointCount++] = position + Vector3.up * .25f;
            trail.positionCount = pointCount;
            for(int i=0;i<pointCount;i++) trail.SetPosition(i,points[i]);
            string state = sling.IsSlinging ? "Sling" : motor.IsDashActive ? "Dash" : motor.IsGrounded ? "Ground" : "Air";
            display = $"MOVEMENT LAB   {destinationIndex+1}/{lanes.Length}   {lanes[destinationIndex].kind}\n" +
                $"{previous.GetBindingDisplayString()} / {next.GetBindingDisplayString()} stations    {reset.GetBindingDisplayString()} respawn + refill    {overlay.GetBindingDisplayString()} HUD\n" +
                $"{overdrive.GetBindingDisplayString()} hold Overdrive   {state}   Power {power.Current:F1}/{power.Max:F0}   Boost {Boosting}   Tunnels {WindCount}\n" +
                $"Velocity {velocity:F1} m/s   Horizontal {motor.HorizontalSpeed:F1}   Accel {acceleration:F1} m/s²\n" +
                $"Distance {Distance:F0} m   Air {airborne:F1} s   Peak {Peak:F1}   Mean {Distance / Mathf.Max(elapsed,.001f):F1} m/s   Wind added {WindGain:F1} m/s";
        }
        private void OnGUI()
        {
            if(diagnostics && display != null && inputHost != null && inputHost.Input != null && !inputHost.Input.IsMenuOpen)
                GUI.Box(new Rect(12,12, Mathf.Min(Screen.width-24, 640),105),display);
        }
    }
}
