using AirflowPrototype;
using UnityEngine;
using UnityEngine.InputSystem;
using Windrunner.Production.Input;
namespace Windrunner.MovementLab
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class MovementLabPlayer : MonoBehaviour
    {
        public PlayerMotor motor;
        public CharacterController controller;
        public Transform facingRoot;
        public AirDash dash;
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
        private InputAction overdrive, reset, overlay, flight, variant;
        private readonly InputAction[] stations = new InputAction[10];
        private int track;
        public bool Finished { get; private set; }
        public float EntrySpeed { get; private set; }
        public float InsideSpeed { get; private set; }
        public float ExitSpeed { get; private set; }
        private float burstUntil;
        private int resumedFrame;
        private int interruption;
        private Vector3 previousProbe;
        private float previousDt;
        public int WindCount { get; private set; }
        public float LastWindDelta { get; private set; }
        public float WindGain { get; private set; }
        public bool Boosting => drive.Active;
        public float Distance { get; private set; }
        public float Peak { get; private set; }
        private Vector3 lastPosition, lastVelocity;
        private float elapsed, airborne, acceleration, nextHud;
        private string display;
        private GUIStyle diagnosticsStyle;
        private readonly GUIContent hudContent = new GUIContent();
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
            for(int i=0;i<stations.Length;i++) stations[i]=map.FindAction("Station"+(i+1),true);
            flight=map.FindAction("LabFlight",true);variant=map.FindAction("LabVariant",true);
            reset = map.FindAction("LabReset", true); overlay = map.FindAction("LabDiagnostics", true);
            orbit = motor.ViewCamera.GetComponent<PlayerOrbitCamera>();
            Teleport(destinationIndex);
        }
        private void OnDisable() { if (motor != null && motor.MovementInfluence == Resolve) motor.MovementInfluence = null; drive.Interrupt(); }
        private void OnApplicationFocus(bool focus) { if (!focus) drive.Interrupt(); }

        private static readonly Unity.Profiling.ProfilerMarker influenceMarker = new Unity.Profiling.ProfilerMarker("MovementLab.ResolveInfluences");
        private Vector3 Resolve(Vector3 velocity, bool grounded, float dt)
        {
            using (influenceMarker.Auto()) return SampleInfluences(velocity,grounded,dt);
        }

        private Vector3 SampleInfluences(Vector3 velocity, bool grounded, float dt)
        {
            var input = inputHost != null ? inputHost.Input : null;
            Vector3 from = previousProbe, position = transform.position;
            float transitDt = previousDt; previousProbe = position; previousDt = dt;
            Vector3 wind = Vector3.zero;
            WindCount = 0; LastWindDelta = 0;
            if (input == null || overdrive == null) return wind;
            if (input.InterruptionVersion != interruption)
            { drive.Interrupt(); interruption = input.InterruptionVersion; }
            if (input.IsGameplayBlocked || sling.IsSlinging || motor.HasExternalTraversalVelocity)
            { drive.Interrupt(); previousDt = 0; return wind; }
            for (int i = 0; i < volumes.Length; i++)
            {
                var volume = volumes[i];
                if (!volume.isActiveAndEnabled) { contacts[i] = default; continue; }
                float depth = volume.Depth(position);
                Vector3 candidate;
                if (depth < 0 && !contacts[i].Inside && transitDt > 0 &&
                    volume.Crossed(from, position, out float fraction, out float middleDepth))
                {
                    candidate = contacts[i].Sample(volume.Settings, velocity, grounded, middleDepth, transitDt * fraction);
                    contacts[i].Sample(volume.Settings,velocity,grounded,-1,Mathf.Max(.00001f,transitDt*(1-fraction)));
                }
                else candidate = contacts[i].Sample(volume.Settings, velocity, grounded, depth, dt);
                if(volume.entryImpulse && candidate.sqrMagnitude>0) burstUntil=Time.time+.65f;
                wind = LabVelocity.Strongest(wind, candidate);
                if (volume.entryImpulse ? candidate.sqrMagnitude>0 : contacts[i].Eligible) WindCount++;
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
            if(index!=destinationIndex) track=0;
            PlaceAtStart(index,true);
        }
        internal void PlaceAtStart(int index,bool refill)
        {
            resumedFrame=Time.frameCount;
            destinationIndex = (index % lanes.Length + lanes.Length) % lanes.Length;
            var spawn = lanes[destinationIndex].destination;
            sling.CancelSling(); dash.CancelAutoDash(); motor.StopForTraversalImpact();
            controller.enabled=false;
            transform.position=spawn.position;
            if(lanes[destinationIndex].kind==LaneKind.Wind) transform.position+=spawn.right*((track-1)*18);
            facingRoot.rotation=spawn.rotation;
            controller.enabled=true;
            orbit.ResetAfterTeleport(spawn.eulerAngles.y);
            previousProbe = transform.position; previousDt = 0;
            System.Array.Clear(contacts, 0, contacts.Length); drive.Interrupt(); if(refill) power.Refill(); ResetSession();
        }
        public void ResetSession()
        {
            Finished=false; EntrySpeed=InsideSpeed=ExitSpeed=-1; burstUntil=0;
            Distance = Peak = elapsed = airborne = WindGain = acceleration = 0;
            lastPosition = transform.position; lastVelocity = motor.Velocity; pointCount = 0;
            if (trail != null) trail.positionCount = 0;
        }
        private void LateUpdate()
        {
            if (inputHost == null || inputHost.Input == null || reset == null || resumedFrame==Time.frameCount) return;
            if (inputHost.Input.IsGameplayBlocked) { drive.Interrupt(); return; }
            if (Time.deltaTime <= 0) return;
            for(int i=0;i<stations.Length;i++) if(stations[i].WasPressedThisFrame()) {Teleport(i);return;}
#if UNITY_EDITOR || WINDRUNNER_MOVEMENT_LAB
            if(Debug.isDebugBuild && flight.WasPressedThisFrame())
            {
                sling.CancelSling(); dash.CancelAutoDash();motor.StopForTraversalImpact();
                var rig=new GameObject("Movement Lab inspection flight").AddComponent<LabFlightRig>();
                rig.Enter(this,orbit.gameObject,flight,stations,reset);
                return;
            }
#endif
            if(variant.WasPressedThisFrame() && lanes[destinationIndex].kind==LaneKind.Wind)
            { track=(track+1)%3;Teleport(destinationIndex);return; }
            if(reset.WasPressedThisFrame() || transform.position.y < Mathf.Max(recoveryHeight,lanes[destinationIndex].destination.position.y-4.5f)) {Teleport(destinationIndex);return;}
            if (overlay.WasPressedThisFrame()) diagnostics = !diagnostics;
            if(trail != null) trail.enabled = diagnostics;
            float dt = Time.deltaTime;
            Vector3 velocity = motor.Velocity, position = transform.position;
            Distance += Vector3.Distance(position,lastPosition); elapsed += dt;
            if (!motor.IsGrounded) airborne += dt;
            acceleration = (velocity-lastVelocity).magnitude / dt;
            Peak = Mathf.Max(Peak, velocity.magnitude); lastPosition = position; lastVelocity = velocity;
            var lane=lanes[destinationIndex];
            float z=Vector3.Dot(position-lane.startGate.position,lane.destination.forward);
            Vector3 end=lane.endGate.InverseTransformPoint(position);
            if(!Finished && motor.IsGrounded && end.z>=-1 && Mathf.Abs(position.y-lane.endGate.position.y)<2)
            {
                Vector3 deck=lane.landing.InverseTransformPoint(position);
                Finished=Mathf.Abs(deck.x)<=.5f && Mathf.Abs(deck.z)<=.5f;
            }
            if(lane.kind==LaneKind.Wind)
            {
                float entry=4.35f, length=track==2?4:lane.tunnelSize.z;
                if(z<entry) EntrySpeed=motor.HorizontalSpeed;
                else if(z<entry+length) InsideSpeed=Mathf.Max(InsideSpeed,motor.HorizontalSpeed);
                else if(ExitSpeed<0) ExitSpeed=motor.HorizontalSpeed;
            }
            if (Time.unscaledTime < nextHud) return;
            nextHud = Time.unscaledTime + .15f;
            if (pointCount == points.Length) { System.Array.Copy(points,1,points,0,points.Length-1); pointCount--; }
            points[pointCount++] = position + Vector3.up * .25f;
            trail.positionCount = pointCount;
            for(int i=0;i<pointCount;i++) trail.SetPosition(i,points[i]);
            string state = sling.IsSlinging ? "Sling" : motor.IsDashActive ? "Dash" : motor.IsGrounded ? "Ground" : "Air";
            display=lane.kind+" / "+lane.Difficulty+(Finished?"   FINISH":"")+"    "+lane.Instruction+"\n";
            for(int i=0;i<stations.Length;i++) display+=stations[i].GetBindingDisplayString()+" "+lanes[i].kind+(i==4 || i==9?"\n":"   ");
            display+=$"{flight.GetBindingDisplayString()} inspect   {reset.GetBindingDisplayString()} restart + refill   {overlay.GetBindingDisplayString()} diagnostics\n"+
                $"{state}   {motor.HorizontalSpeed:F1} m/s   Peak {Peak:F1}   Power {power.Current:F0}/{power.Max:F0}   {overdrive.GetBindingDisplayString()} Overdrive {(Boosting?"ON":"off")}   Wind {(Time.time<burstUntil?"IMPULSE":WindCount>0?"continuous":"off")}";
            string Speed(float sample)=>sample<0?"--":sample.ToString("F1");
            if(lane.kind==LaneKind.Wind) display+=$"\n{variant.GetBindingDisplayString()} change track: {new[]{"Dry","Continuous","Impulse"}[track]}    Entry {Speed(EntrySpeed)} / inside {Speed(InsideSpeed)} / exit {Speed(ExitSpeed)} m/s";
            if(diagnostics) display+=$"\nDistance {Distance:F0} m   Air {airborne:F1} s   Acceleration {acceleration:F1} m/s²";
        }
        private void OnGUI()
        {
            if(Debug.isDebugBuild && display != null && inputHost != null && inputHost.Input != null && !inputHost.Input.IsMenuOpen)
            {
                if(diagnosticsStyle==null)
                {
                    diagnosticsStyle=new GUIStyle(GUI.skin.box) {fontSize=16,alignment=TextAnchor.MiddleLeft,wordWrap=true,
                        padding=new RectOffset(14,14,8,8)};
                    diagnosticsStyle.normal.textColor=Color.white;
                }
                float width=Mathf.Min(Screen.width-24,900);
                hudContent.text = display;
                float height=diagnosticsStyle.CalcHeight(hudContent,width);
                GUI.Box(new Rect(12,12,width,height),hudContent,diagnosticsStyle);
            }
        }
    }
}
