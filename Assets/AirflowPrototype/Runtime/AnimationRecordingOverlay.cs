using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Windrunner.Production.Input;

namespace AirflowPrototype
{
    [DisallowMultipleComponent, DefaultExecutionOrder(11100)]
    public sealed class AnimationRecordingOverlay : MonoBehaviour
    {
        static readonly string[] Names = { "Jump", "Dash", "Cast", "Sling", "DaggerAbility", "Interact" };
        static readonly string[] Parts = { "up", "left", "down", "right" };
        static readonly string[] Compass = { "F", "FR", "R", "BR", "B", "BL", "L", "FL" };
        readonly InputAction[] buttons = new InputAction[Names.Length];
        readonly string[] labels = new string[Names.Length], keys = new string[4];
        readonly bool[] held = new bool[Names.Length], pressed = new bool[Names.Length], released = new bool[Names.Length];
        readonly List<(ButtonControl key, int direction)> movementKeys = new();
        readonly bool[] lit = new bool[4];
        AirflowPortableAnimationBridge bridge;
        PlayerMotor motor;
        WindrunnerInputService service;
        InputAction move;
        Gamepad pad;
        bool dirty = true;
        [SerializeField] bool visible, detailed = true;
        [SerializeField, Range(.7f, 1.3f)] float size = 1;
        [SerializeField, Range(0, 3)] int corner;
        string inputLine, poseLine, motionLine, headingLine, actionLine, layerLine, stickLine, timingLine;
        GUIStyle style, badge;
        float nextTimingUpdate;
        public bool Visible => visible;
        public Vector2 Movement { get; private set; }
        public void Configure(AirflowPortableAnimationBridge owner, PlayerMotor player)
        {
            bridge = owner;
            motor = player;
        }
        void OnEnable()
        {
            if (!Debug.isDebugBuild && !Application.isEditor) { enabled = false; return; }
            InputSystem.onActionChange += ControlsChanged;
        }
        void OnDisable()
        {
            InputSystem.onActionChange -= ControlsChanged;
            Bind(null);
            pad = null;
        }
        void ControlsChanged(object owner, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged) dirty = true;
        }
        void RefreshLater() => dirty = true;
        void Bind(WindrunnerInputService next)
        {
            if (service == next) return;
            if (service != null) service.BindingsChanged -= RefreshLater;
            service = next;
            if (service != null) service.BindingsChanged += RefreshLater;
            dirty = true;
        }
        void Refresh()
        {
            movementKeys.Clear();
            move = service.GetAction("Gameplay", "Move");
            for (int i = 0; i < keys.Length; i++) keys[i] = null;
            if (move != null)
                foreach (var control in move.controls)
                {
                    int binding = move.GetBindingIndexForControl(control);
                    if (binding < 0 || !(control is ButtonControl key)) continue;
                    int direction = System.Array.IndexOf(Parts, move.bindings[binding].name?.ToLowerInvariant());
                    if (direction < 0) continue;
                    movementKeys.Add((key, direction));
                    string label = move.GetBindingDisplayString(binding)
                        .Replace("Up Arrow", "↑").Replace("Down Arrow", "↓")
                        .Replace("Left Arrow", "←").Replace("Right Arrow", "→");
                    if (keys[direction] == null) keys[direction] = label;
                    else if (!keys[direction].Contains(label)) keys[direction] += " / " + label;
                }
            for (int i = 0; i < buttons.Length; i++)
            {
                var action = service.GetAction(i == 5 ? "Progression" : "Gameplay", Names[i]);
                buttons[i] = action;
                labels[i] = Names[i] + "  " + action?.GetBindingDisplayString();
            }
            dirty = false;
        }
        bool BoundToProduction(InputControl control)
        {
            foreach (var map in service.Actions.actionMaps)
                foreach (var action in map.actions)
                    if (action.GetBindingIndexForControl(control) >= 0) return true;
            return false;
        }
        void LateUpdate()
        {
            if (!motor || !bridge || !bridge.isActiveAndEnabled) return;
            Bind(motor.Input ? motor.Input.Production : null);
            if (service == null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.insertKey.wasPressedThisFrame && !service.IsRebinding &&
                !BoundToProduction(keyboard.insertKey))
            {
                if (keyboard.shiftKey.isPressed) detailed = !detailed;
                else if (keyboard.ctrlKey.isPressed) corner = (corner + 1) % 4;
                else if (keyboard.altKey.isPressed) size = size >= 1.2f ? .8f : size + .2f;
                else visible = !visible;
            }
            if (!visible) return;
            if (dirty) Refresh();
            Movement = motor.Input.Move;
            System.Array.Clear(lit, 0, lit.Length);
            if (!service.IsGameplayBlocked)
                foreach (var part in movementKeys) lit[part.direction] |= part.key.isPressed;
            if (move?.activeControl != null) pad = move.activeControl.device as Gamepad;
            if (pad != null && !pad.added) pad = null;
            if (pad != null)
            {
                lit[0] |= Movement.y > .01f; lit[1] |= Movement.x < -.01f;
                lit[2] |= Movement.y < -.01f; lit[3] |= Movement.x > .01f;
            }
            for (int i = 0; i < buttons.Length; i++)
            {
                bool enabled = buttons[i]?.enabled == true && !service.IsRebinding &&
                    (i == 5 || !service.IsGameplayBlocked);
                held[i] = enabled && buttons[i].IsPressed();
                pressed[i] = enabled && buttons[i].WasPressedThisFrame();
                released[i] = enabled && buttons[i].WasReleasedThisFrame();
            }
            inputLine = $"Move   lateral {Movement.x:F2}   forward {Movement.y:F2}";
            stickLine = pad == null ? null : $"Left stick  {pad.leftStick.ReadValue():F2}";
            var velocity = motor.Velocity;
            motionLine = $"Velocity {velocity:F1}   planar {motor.HorizontalSpeed:F1} m/s";
            var view = motor.ViewTransform;
            var desired = view ? PlayerMotorSimulation.CameraRelativeMove(Movement, view.forward, view.right) : Vector3.zero;
            headingLine = $"Yaw   intent {Heading(desired)}   travel {Heading(motor.PlanarVelocity)}   facing {Heading(bridge.HumanoidAnimator.transform.forward)}";
            if (bridge.ReadRecordingPose(out var p))
            {
                poseLine = $"{p.state}   {(p.clip ? p.clip.name : "—")}   {p.weight:P0}";
                actionLine = $"Portable   {p.context}   {bridge.ActiveAnimationAction}";
                layerLine = p.action==null ? null : $"{(p.actionClip ? p.actionClip.name : p.action)}   {p.actionWeight:P0}   {(p.weighted ? (p.weightSource ? p.weightSource.name+" + regions" : "local region weights") : p.mask ? p.mask.name : "full body")}   {p.actionSeconds:F2}s";
                if(detailed && Time.unscaledTime>=nextTimingUpdate)
                {
                    nextTimingUpdate=Time.unscaledTime+.1f;
                    var timing=p.timing;
                    string policy=bridge.Profile.previewOriginalTiming?"Original 1x":timing.Mode==PortableAnimation.VisualPlaybackMode.ExistingTiming?"Existing phase/cadence fit":
                        timing.Mode==PortableAnimation.VisualPlaybackMode.FixedVisualDuration?"Fixed visual duration":"Authored speed";
                    timingLine=$"{policy}   source {timing.SourceSeconds:F2}/{timing.SourceLength:F2}s";
                    timingLine+=$"\nbase {timing.BaseSpeed:F2}x / curve {timing.CurveMultiplier:F2}x / effective {timing.EffectiveSpeed:F2}x";
                }
                motionLine += $"\n{(p.direction >= 0 ? Compass[p.direction] : "—")}   walk {p.walk:P0}   run {p.run:P0}   {p.cycles:F2} cyc/s   {(p.direction >= 0 ? p.cycles*p.clip.length : 0):F2}×";
            }
            else { poseLine = "Ground graph inactive"; actionLine = bridge.ActiveAnimationAction; layerLine = null; }
        }
        static string Heading(Vector3 v) => v.sqrMagnitude < .0001f ? "—" :
            Mathf.RoundToInt(Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg).ToString();
        void OnGUI()
        {
            if (!visible || !bridge || !bridge.isActiveAndEnabled || service == null ||
                Event.current.type != EventType.Repaint) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Color.white } };
            badge ??= new GUIStyle(style) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f) * size;
            float w = 475, h = (detailed ? 341 : 233) + (stickLine == null ? 0 : 20) + (layerLine == null ? 0 : 21);
            var matrix = GUI.matrix;
            var color = GUI.color;
            GUI.matrix = Matrix4x4.TRS(new Vector3((corner & 1) == 0 ? Screen.width - (w + 10)*scale : 10*scale,
                corner < 2 ? 12*scale : Screen.height - (h + 12)*scale, 0), Quaternion.identity, Vector3.one * scale);
            void Fill(Rect r, Color tint) { GUI.color = tint; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
            Fill(new Rect(0,0,w,h), new Color(.035f,.055f,.08f,.94f));
            float y = 6;
            void Line(string s) { GUI.Label(new Rect(12,y,w-20,24),s,style); y += 21; }
            Line("INPUT     •  press     =  hold     ↑  release");
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(12 + i*112,y,106,23);
                Fill(r,lit[i] ? new Color(.04f,.48f,.42f) : Color.gray * .55f);
                GUI.Label(r, pad != null ? Parts[i] : keys[i] ?? Parts[i],badge);
            }
            y += 25;
            Line(inputLine);
            if (stickLine != null) Line(stickLine);
            for (int i = 0; i < buttons.Length; i++)
            {
                var r = new Rect(12+(i%2)*228,y,222,23);
                if (held[i] || pressed[i] || released[i]) Fill(r,new Color(.04f,.48f,.42f));
                GUI.Label(r,labels[i],style);
                GUI.Label(new Rect(r.xMax-32,y,32,23),pressed[i] ? (released[i] ? "•↑" : "•") : released[i] ? "↑" : held[i] ? "=" : "",badge);
                if (i%2 == 1) y += 25;
            }
            y += 5;
            Line("ANIMATION");
            Line(poseLine); Line(actionLine); if(layerLine!=null) Line(layerLine);
            if (detailed) { GUI.Label(new Rect(12,y,w-20,48),motionLine,style); y += 45; Line(headingLine); GUI.Label(new Rect(12,y,w-20,42),timingLine,style); y += 42; }
            GUI.color = color; GUI.matrix = matrix;
        }
    }
}
