using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Windrunner.Production.Input;
using Windrunner.Production.Unity;

namespace Windrunner.Production.Tests
{
    public sealed class AbilityExecutionTests
    {
        private Scene scene;
        private GameObject player, node;
        private Component motor, input, power, sling, slingTargeter, caster, targeter, dash;
        private Camera camera;
        private ScriptableObject movement, slingSettings, castSettings;
        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        private InputSettings originalInput, testInput;
        private HideFlags originalFlags;
        private Keyboard keyboard;
        private Mouse mouse;
        private float capture;
        private WindrunnerInputHost host;
        private static Type T(string name) => Type.GetType("AirflowPrototype." + name + ", Assembly-CSharp", true);
        private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
        private static object Call(object target, string name, params object[] args) => target.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
        private ScriptableObject Asset(string name)
        {
            var value = ScriptableObject.CreateInstance(T(name)); assets.Add(value); return value;
        }
        private Component Add(GameObject owner, string name) => owner.AddComponent(T(name));
        private static void Observe(object target, string name, Action callback)
        {
            var ev = target.GetType().GetEvent(name);
            var parameters = ev.EventHandlerType.GetMethod("Invoke").GetParameters()
                .Select(p => Expression.Parameter(p.ParameterType)).ToArray();
            var handler = Expression.Lambda(ev.EventHandlerType,
                Expression.Call(Expression.Constant(callback), typeof(Action).GetMethod("Invoke")), parameters).Compile();
            ev.AddEventHandler(target, handler);
        }
        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private void MouseButton(bool held) => InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, held));
        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }

        [UnitySetUp]
        public IEnumerator Setup()
        {
            Assert.That(WindrunnerInputHost.Active, Is.Null);
            scene = SceneManager.CreateScene("Ability execution fixture"); SceneManager.SetActiveScene(scene);
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60f;
            originalInput = InputSystem.settings; originalFlags = originalInput.hideFlags;
            if (originalFlags == HideFlags.HideAndDontSave) originalInput.hideFlags = HideFlags.DontSave;
            testInput = UnityEngine.Object.Instantiate(originalInput); testInput.hideFlags = HideFlags.DontSave;
            testInput.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testInput.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testInput;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            var actions = WindrunnerInputActionsFactory.CreateDefaults(); assets.Add(actions);
            var root = new GameObject("Input composition"); root.SetActive(false);
            host = root.AddComponent<WindrunnerInputHost>(); host.Configure(actions, null, null); root.SetActive(true);
            // Runtime-only override proves Sling uses the composed action rather
            // than Mouse.current. Never save overrides to the user's PlayerPrefs.
            host.Input.Actions.RemoveAllBindingOverrides();
            host.Input.GetAction("Gameplay", "Sling").ApplyBindingOverride(0, "<Keyboard>/f");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0); ground.transform.localScale = new Vector3(100, 1, 100);
            camera = new GameObject("Ability camera").AddComponent<Camera>(); camera.transform.position = new Vector3(0, 2, -4);
            player = new GameObject("Player"); player.transform.position = new Vector3(0, 1.05f, 0);
            var controller = player.AddComponent<CharacterController>();
            input = Add(player, "PlayerInputReader"); motor = Add(player, "PlayerMotor");
            movement = Asset("AirflowMovementSettings");
            Call(motor, "Configure", controller, input, camera.transform, null, movement);
            power = Add(player, "AirPower"); Call(power, "Configure", movement, true);
            dash = Add(player, "AirDash"); Call(dash, "Configure", motor, input, power, movement);
            slingSettings = Asset("AirSlingSettings"); Set(slingSettings, "useAirPower", true);
            Set(slingSettings, "requirePerfectReleaseForSequence", true);
            Set(slingSettings, "airPowerCost", 15f); Set(slingSettings, "delayBeforeNodeDash", 0f);
            Set(slingSettings, "obstructionMask", (LayerMask)0);
            slingTargeter = Add(player, "AirSlingTargeter"); Call(slingTargeter, "Configure", camera, motor, slingSettings);
            sling = Add(player, "AirSlingController");
            Call(sling, "Configure", motor, controller, slingTargeter, power, camera, null, slingSettings, null, null, null, null, null);
            targeter = Add(player, "AirTargeter"); var targeting = Asset("AirTargetingSettings"); Set(targeting, "obstructionMask", (LayerMask)0);
            Call(targeter, "Configure", camera, motor, targeting);
            var origin = new GameObject("Air Cast Origin"); origin.transform.SetParent(player.transform); origin.transform.localPosition = Vector3.up;
            castSettings = Asset("AirCastSettings"); caster = Add(player, "AirCaster");
            Call(caster, "Configure", input, targeter, camera, origin.transform, castSettings);
            Physics.SyncTransforms(); yield return Frames(3);
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            var empty = SceneManager.GetSceneByName("Ability cleanup");
            if (!empty.IsValid() || !empty.isLoaded) empty = SceneManager.CreateScene("Ability cleanup");
            SceneManager.SetActiveScene(empty);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings = originalInput; originalInput.hideFlags = originalFlags;
            UnityEngine.Object.Destroy(testInput);
            foreach (var asset in assets) UnityEngine.Object.Destroy(asset); assets.Clear();
            Time.captureDeltaTime = capture;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SwimmingPausesExitsAndRecoversThroughWaterRemoval()
        {
            var water = new GameObject("Test water");
            var mesh = new Mesh(); assets.Add(mesh);
            mesh.vertices = new[] { new Vector3(-20, 0, -20), new Vector3(20, 0, -20), new Vector3(20, 0, 20), new Vector3(-20, 0, 20) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.RecalculateBounds();
            water.AddComponent<MeshFilter>().sharedMesh = mesh; water.transform.position = Vector3.up * 1.5f;
            Add(water, "AirWaterSurface");
            var swim = Add(player, "PlayerWaterSwim"); var swimSettings = Asset("WaterSwimSettings");
            Call(swim, "Configure", motor, swimSettings);
            int changes = 0; Observe(swim, "SwimmingChanged", () => changes++);
            yield return Frames(8);
            Assert.That(Get(swim, "IsSwimming"), Is.EqualTo(true));
            Assert.That(Get(swim, "CurrentWaterSurfaceY"), Is.EqualTo(1.5f));
            using (var pause = UnityTimeAuthority.Pause())
            {
                var position = player.transform.position;
                Keys(Key.LeftCtrl); yield return Frames(5);
                Assert.That(player.transform.position, Is.EqualTo(position));
                water.SetActive(false); yield return Frames(5);
                Assert.That(Get(swim, "IsSwimming"), Is.EqualTo(true));
            }
            Keys(); yield return Frames(12);
            Assert.That(Get(swim, "IsSwimming"), Is.EqualTo(false));
            water.SetActive(true); yield return Frames(8);
            Assert.That(Get(swim, "IsSwimming"), Is.EqualTo(true));
            ((Behaviour)swim).enabled = false;
            Assert.That(Get(swim, "IsSwimming"), Is.EqualTo(false));
            Assert.That(changes, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator DisconnectedSlingInputCancelsAndReconnectedDeviceCanCharge()
        {
            node = new GameObject("Sling node"); node.transform.position = new Vector3(0, 2, 8); Add(node, "AirSlingNode");
            yield return Frames(3);
            Keys(Key.F); yield return Frames(19);
            Assert.That(Get(sling, "IsCharging"), Is.EqualTo(true));
            InputSystem.RemoveDevice(keyboard); yield return Frames(3);
            Assert.That(Get(sling, "IsCharging"), Is.EqualTo(false));
            Assert.That(Get(power, "Current"), Is.EqualTo(100f));
            InputSystem.AddDevice(keyboard); Keys(); yield return Frames(2);
            Keys(Key.F); yield return Frames(3);
            Assert.That(Get(sling, "IsCharging"), Is.EqualTo(true));
            Call(sling, "CancelSling");
        }

        [UnityTest]
        public IEnumerator DisconnectedCastInputCancelsWithoutProjectileAndRecovers()
        {
            node = new GameObject("Air node"); node.transform.position = new Vector3(0, 2, 8); Add(node, "AirNode");
            yield return Frames(3);
            int releases = 0; Observe(caster, "PulseReleased", () => releases++);
            MouseButton(true); yield return Frames(18);
            Assert.That(Get(caster, "IsCharging"), Is.EqualTo(true));
            InputSystem.RemoveDevice(mouse); yield return Frames(3);
            Assert.That(Get(caster, "IsCharging"), Is.EqualTo(false));
            Assert.That(releases, Is.Zero);
            InputSystem.AddDevice(mouse); MouseButton(false); yield return Frames(2);
            MouseButton(true); yield return Frames(18); MouseButton(false); yield return Frames(2);
            Assert.That(releases, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ExplicitPlayerInputBindingStaysIsolatedThroughDisposal()
        {
            var template = WindrunnerInputActionsFactory.CreateDefaults(); assets.Add(template);
            var other = new WindrunnerInputService(template);
            try
            {
                other.Actions.RemoveAllBindingOverrides();
                other.GetAction("Gameplay", "Sling").ApplyBindingOverride(0, "<Keyboard>/g");
                Call(input, "BindProductionInput", other);
                Keys(Key.F); yield return Frames(2);
                Assert.That(host.Input.ButtonHeld("Sling"), Is.True);
                Assert.That(Get(input, "SlingHeld"), Is.EqualTo(false));
                Keys(Key.G); yield return Frames(2);
                Assert.That(host.Input.ButtonHeld("Sling"), Is.False);
                Assert.That(Get(input, "SlingHeld"), Is.EqualTo(true));
                other.SetMenuOpen(true);
                Assert.That(Get(input, "IsGameplaySuspended"), Is.EqualTo(true));
                Assert.That(host.Input.IsMenuOpen, Is.False);
                other.Dispose(); Keys(Key.F); yield return Frames(2);
                Assert.That(Get(input, "SlingHeld"), Is.EqualTo(false), "Disposed player binding must not switch to global host.");
            }
            finally { other.Dispose(); }
        }

        [UnityTest]
        public IEnumerator RebindCancellationRestoresSystemActionAndRespectsMenuState()
        {
            var service = host.Input;
            var pause = service.GetAction("System", "Pause");
            Assert.That(service.BeginRebind("System", "Pause", 0, null), Is.True);
            Assert.That(pause.enabled, Is.False);
            service.CancelRebind();
            Assert.That(pause.enabled, Is.True, "Cancel must restore the only action in its map.");
            Assert.That(service.BeginRebind("Gameplay", "Sling", 0, null), Is.True);
            service.SetMenuOpen(true);
            service.CancelRebind();
            Assert.That(service.GetAction("Gameplay", "Sling").enabled, Is.False);
            service.SetMenuOpen(false);
            Assert.That(service.GetAction("Gameplay", "Sling").enabled, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InputDisposalRejectsReentrantRebinding()
        {
            var service = host.Input;
            bool restarted = false;
            Assert.That(service.BeginRebind("Gameplay", "Sling", 0,
                success => restarted = service.BeginRebind("System", "Pause", 0, null)), Is.True);
            service.Dispose();
            Assert.That(restarted, Is.False);
            Assert.That(service.IsRebinding, Is.False);
            Assert.That(service.Actions, Is.Null);
            service.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SlingPerfectInputChargeCurveImpactLaunchPreservesEventOrder()
        {
            node = new GameObject("Sling node"); node.transform.position = new Vector3(0, 2, 8); Add(node, "AirSlingNode");
            yield return Frames(3);
            Assert.That(Get(slingTargeter, "CurrentTarget"), Is.Not.Null);
            var events = new List<string>();
            foreach (var name in new[] { "SlingStarted", "SlingCommitted", "InitialBoostDelivered", "NodeDashStarted", "NodeImpact", "SlingLaunched", "PerfectSlingLaunched" })
            { string captureName = name; Observe(sling, name, () => events.Add(captureName)); }
            Observe(power, "Changed", () => events.Add("Payment"));
            Keys(Key.F); yield return Frames(1);
            Assert.That(Get(sling, "IsCharging"), Is.EqualTo(true));
            yield return Frames(18); Keys(); yield return Frames(1);
            for (float deadline = Time.realtimeSinceStartup + 3f; Time.realtimeSinceStartup < deadline && !events.Contains("SlingLaunched");) yield return null;
            CollectionAssert.AreEqual(new[] { "SlingStarted", "Payment", "SlingCommitted", "InitialBoostDelivered", "NodeDashStarted", "NodeImpact", "SlingLaunched", "PerfectSlingLaunched" }, events);
            Assert.That(Get(power, "Current"), Is.EqualTo(85f));
            Assert.That(Get(sling, "IsSlinging"), Is.EqualTo(false));
            yield return null; // ActualVelocity is measured on the next motor physics step.
            Assert.That((float)Get(motor, "VerticalSpeed"), Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator SlingCancellationPauseAndNormalReleaseRecoverCleanly()
        {
            node = new GameObject("Sling node"); node.transform.position = new Vector3(0, 2, 8); Add(node, "AirSlingNode");
            yield return Frames(3);
            int canceled = 0, launched = 0, perfect = 0;
            Observe(sling, "SlingCanceled", () => canceled++); Observe(sling, "SlingLaunched", () => launched++);
            Observe(sling, "PerfectSlingLaunched", () => perfect++);
            Keys(Key.F); yield return Frames(4); Keys(); yield return Frames(2);
            Assert.That(canceled, Is.EqualTo(1)); Assert.That(Get(power, "Current"), Is.EqualTo(100f));
            Keys(Key.F); yield return Frames(3);
            using (var pause = UnityTimeAuthority.Pause())
            { Keys(); yield return Frames(5); Assert.That(Get(sling, "IsCharging"), Is.EqualTo(true)); }
            yield return Frames(2); Assert.That(canceled, Is.EqualTo(2));
            Set(slingSettings, "requirePerfectReleaseForSequence", false);
            Keys(Key.F); yield return Frames(4); Keys(); yield return Frames(1);
            for (float deadline = Time.realtimeSinceStartup + 3f; Time.realtimeSinceStartup < deadline && launched == 0;) yield return null;
            Assert.That(launched, Is.EqualTo(1)); Assert.That(perfect, Is.Zero);
            Assert.That(Get(power, "Current"), Is.EqualTo(85f));
        }

        [UnityTest]
        public IEnumerator DashDepletionRewardReactivationPauseAndInterruption()
        {
            Set(movement, "dashDrainPerSecond", 600f);
            int starts = 0, stops = 0; Observe(dash, "DashStarted", () => starts++); Observe(dash, "DashStopped", () => stops++);
            Keys(Key.W, Key.LeftShift); yield return Frames(3); Assert.That(Get(dash, "IsDashing"), Is.EqualTo(true));
            using (var pause = UnityTimeAuthority.Pause())
            { float before = (float)Get(power, "Current"); yield return Frames(5); Assert.That(Get(power, "Current"), Is.EqualTo(before)); }
            yield return Frames(15); Assert.That(Get(power, "Current"), Is.EqualTo(0f)); Assert.That(Get(dash, "IsDashing"), Is.EqualTo(false));
            Set(movement, "dashDrainPerSecond", 1f); Call(power, "Restore", 30f); Call(dash, "TriggerAutoDash");
            Keys(Key.W); yield return Frames(3); Assert.That(Get(dash, "IsAutoDashLatched"), Is.EqualTo(true));
            Keys(); yield return Frames(2); Assert.That(Get(dash, "IsDashing"), Is.EqualTo(false));
            Assert.That(starts, Is.EqualTo(2)); Assert.That(stops, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator CastPerfectReleaseProjectileAndRewardPreserveOrder()
        {
            node = new GameObject("Air node"); node.transform.position = new Vector3(0, 2, 8); Add(node, "AirNode");
            var flow = Add(player, "AirFlowHitSystem"); var rewards = Asset("AirFlowHitSettings");
            Call(flow, "Configure", caster, motor, power, camera, rewards);
            Call(dash, "Configure", motor, input, power, movement);
            Call(power, "Spend", 90f); yield return Frames(3);
            Assert.That(Get(targeter, "CurrentTarget"), Is.Not.Null);
            var events = new List<string>();
            foreach (var name in new[] { "ChargeStarted", "TargetedChargeStarted", "TargetedPulseReleased", "PulseReleased", "PerfectReleased", "PulseHit" })
            { string captured = name; Observe(caster, name, () => events.Add(captured)); }
            Observe(flow, "RewardDelivered", () =>
            {
                events.Add("Reward");
                Assert.That(Get(dash, "IsAutoDashLatched"), Is.EqualTo(true), "Node reward must reach the composed Dash subscriber.");
            });
            MouseButton(true); yield return Frames(18); MouseButton(false); yield return Frames(1);
            for (int i = 0; i < 180 && !events.Contains("Reward"); i++) yield return null;
            CollectionAssert.AreEqual(new[] { "ChargeStarted", "TargetedChargeStarted", "TargetedPulseReleased", "PulseReleased", "PerfectReleased", "PulseHit", "Reward" }, events);
            Assert.That(Get(caster, "IsCharging"), Is.EqualTo(false)); Assert.That((float)Get(power, "Current"), Is.GreaterThan(10f));
        }
    }
}
