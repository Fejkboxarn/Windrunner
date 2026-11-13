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

        private delegate bool WaterQuery(Vector3 point,float padding,out float y);
        [Serializable] private sealed class HotspotResult
        {
            public string scenario,utc,scope="Rendered Editor fixture; CPU batch excludes setup; frame GC/memory includes fixture and Editor overhead";
            public int operationsPerSample,samples=20;
            public double medianBatchMs,p95BatchMs;
            public long medianFrameGcBytes,peakUsedBytes;
        }
        private static IEnumerator MeasureHotspot(string name,int operations,Action batch)
        {
            var result=new HotspotResult{scenario=name,operationsPerSample=operations,utc=DateTime.UtcNow.ToString("O")};
            var cpu=new double[20];var gcSamples=new long[20];
            using(var gc=global::Unity.Profiling.ProfilerRecorder.StartNew(global::Unity.Profiling.ProfilerCategory.Memory,"GC Allocated In Frame",4))
            using(var memory=global::Unity.Profiling.ProfilerRecorder.StartNew(global::Unity.Profiling.ProfilerCategory.Memory,"Total Used Memory",4))
            {
                Assert.That(gc.Valid && memory.Valid,Is.True);
                batch();yield return Frames(20); // Warm managed/JIT/native creation paths.
                for(int sample=0;sample<20;sample++)
                {
                    var clock=System.Diagnostics.Stopwatch.StartNew();batch();clock.Stop();cpu[sample]=clock.Elapsed.TotalMilliseconds;
                    yield return null;gcSamples[sample]=gc.LastValue;result.peakUsedBytes=Math.Max(result.peakUsedBytes,memory.LastValue);
                    yield return Frames(20);
                }
            }
            Array.Sort(cpu);Array.Sort(gcSamples);result.medianBatchMs=cpu[10];result.p95BatchMs=cpu[19];result.medianFrameGcBytes=gcSamples[10];
            System.IO.File.WriteAllText("Logs/ProductionMigration/rendered-profile-hotspot-"+name+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".json",JsonUtility.ToJson(result,true));
        }
        [UnityTest,Category("Profiling")]
        public IEnumerator TargetWaterProjectileAndCapacityHotspots()
        {
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
            var nodes=new List<GameObject>();
            var targetUpdate=(Action)slingTargeter.GetType().GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).CreateDelegate(typeof(Action),slingTargeter);
            foreach(int count in new[]{10,100})
            {
                while(nodes.Count<count){var go=new GameObject("Profile target");go.transform.position=new Vector3((nodes.Count%10-5)*0.4f,2,5+nodes.Count/10);Add(go,"AirSlingNode");nodes.Add(go);}
                yield return MeasureHotspot("targeting-"+count,100,()=>{for(int i=0;i<100;i++)targetUpdate();});
            }
            foreach(var go in nodes)UnityEngine.Object.Destroy(go);yield return null;
            foreach(int side in new[]{1,16,64})
            {
                var water=new GameObject("Profile water");var mesh=new Mesh();assets.Add(mesh);
                var vertices=new Vector3[(side+1)*(side+1)];var triangles=new int[side*side*6];int at=0;
                for(int z=0;z<=side;z++)for(int x=0;x<=side;x++)vertices[z*(side+1)+x]=new Vector3(x,1,z);
                for(int z=0;z<side;z++)for(int x=0;x<side;x++){int v=z*(side+1)+x;triangles[at++]=v;triangles[at++]=v+side+1;triangles[at++]=v+1;triangles[at++]=v+1;triangles[at++]=v+side+1;triangles[at++]=v+side+2;}
                mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateBounds();water.AddComponent<MeshFilter>().sharedMesh=mesh;
                var surface=Add(water,"AirWaterSurface");var query=(WaterQuery)surface.GetType().GetMethod("TryGetSurfaceHeight").CreateDelegate(typeof(WaterQuery),surface);
                var point=new Vector3(side*0.5f,0,side*0.5f);
                yield return MeasureHotspot("water-triangles-"+(side*side*2),100,()=>{for(int i=0;i<100;i++)query(point,0,out _);});
                UnityEngine.Object.Destroy(water);yield return null;
            }
            Set(castSettings,"pulseLifetime",0.1f);Set(castSettings,"trailLifetime",0.01f);
            var spawn=T("AirPulseProjectile").GetMethod("Spawn");var args=new object[]{caster,null,Vector3.up*2,Vector3.forward,1f,false,castSettings};
            yield return MeasureHotspot("projectile-create-expire-reflection",10,()=>{for(int i=0;i<10;i++)spawn.Invoke(null,args);});
            yield return Frames(30);Assert.That(UnityEngine.Object.FindObjectsByType(T("AirPulseProjectile"),FindObjectsSortMode.None),Is.Empty);
            var reconcile=(Action)power.GetType().GetMethod("ReconcileCapacity").CreateDelegate(typeof(Action),power);
            var maximum=movement.GetType().GetField("maxAirPower");
            yield return MeasureHotspot("capacity-change-reflection",100,()=>{for(int i=0;i<100;i++){maximum.SetValue(movement,i%2==0?80f:100f);reconcile();}});
        }

        [UnityTest]
        public IEnumerator LiveMovementDraftChangesActualMotorSpeedAndRestores()
        {
#if UNITY_EDITOR
            var sessionType=Type.GetType("AirflowPrototype.Editor.CharacterTuningSession, Assembly-CSharp-Editor",true);
            var session=Activator.CreateInstance(sessionType,new object[]{player});
            string original=UnityEditor.EditorJsonUtility.ToJson(movement);
            try
            {
                Keys(Key.W);yield return Frames(90);float baseline=(float)Get(motor,"HorizontalSpeed");Assert.That(baseline,Is.GreaterThan(8f));
                Call(session,"Preview",true);var draft=(ScriptableObject)Get(motor,"Settings");Set(draft,"maxGroundSpeed",4f);
                yield return Frames(75);Assert.That((float)Get(motor,"HorizontalSpeed"),Is.InRange(3.9f,4.1f));
                Call(session,"Preview",false);yield return Frames(75);
                Assert.That((float)Get(motor,"HorizontalSpeed"),Is.EqualTo(baseline).Within(0.1f));
                Assert.That(UnityEditor.EditorJsonUtility.ToJson(movement),Is.EqualTo(original));
            }
            finally{((IDisposable)session).Dispose();Keys();}
#else
            Assert.Ignore("Editor tuning regression");yield break;
#endif
        }

        [UnityTest]
        public IEnumerator PreviewDisposalDoesNotStrandMultipleProjectilesOrLoseRewards()
        {
#if UNITY_EDITOR
            var rewards=Asset("AirFlowHitSettings");Set(rewards,"returnStreamDuration",0.03f);
            var flow=Add(player,"AirFlowHitSystem");Call(flow,"Configure",caster,motor,power,camera,rewards);
            Call(power,"Spend",50f);
            node=new GameObject("Flight target");node.transform.position=new Vector3(0,2,4);var target=Add(node,"AirNode");
            Set(castSettings,"pulseSpeed",20f);Set(castSettings,"pulseLifetime",0.5f);
            int hits=0,misses=0,delivered=0;Observe(caster,"PulseHit",()=>hits++);Observe(caster,"PulseMissed",()=>misses++);Observe(flow,"RewardDelivered",()=>delivered++);
            var sessionType=Type.GetType("AirflowPrototype.Editor.CharacterTuningSession, Assembly-CSharp-Editor",true);
            var session=Activator.CreateInstance(sessionType,new object[]{player});
            var projectileType=T("AirPulseProjectile");
            try
            {
                Call(session,"Preview",true);
                var draft=(ScriptableObject)Get(caster,"Settings");
                projectileType.GetMethod("Spawn").Invoke(null,new object[]{caster,target,new Vector3(0,2,0),Vector3.forward,1f,true,draft});
                projectileType.GetMethod("Spawn").Invoke(null,new object[]{caster,null,new Vector3(3,2,0),Vector3.forward,1f,true,draft});
                yield return Frames(2);
                Set(draft,"pulseSpeed",0f);Set(draft,"pulseLifetime",100f);
            }
            finally{((IDisposable)session).Dispose();}
            yield return Frames(60);
            Assert.That(hits,Is.EqualTo(1));Assert.That(misses,Is.EqualTo(1));Assert.That(delivered,Is.EqualTo(1));
            Assert.That((float)Get(power,"Current"),Is.GreaterThan(50f));
            Assert.That(UnityEngine.Object.FindObjectsByType(projectileType,FindObjectsSortMode.None),Is.Empty,"Both hit and expired projectile must clean up after preview disposal.");
#else
            Assert.Ignore("Editor tuning regression");yield break;
#endif
        }

        [UnityTest]
        public IEnumerator PresentationCameraFallbackUsesAbilityOwnerAndPreservesOverride()
        {
            var decoy = new GameObject("Other player main camera").AddComponent<Camera>();
            decoy.tag = "MainCamera";
            foreach (string name in new[] { "AirPresentationController", "AirTargetChargeUI" })
            {
                var presentation = Add(player, name);
                var field = presentation.GetType().GetField("targetCamera", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field.GetValue(presentation), Is.SameAs(camera), name + " must use its ability owner.");
                field.SetValue(presentation, decoy);
                Call(presentation, "Awake");
                Assert.That(field.GetValue(presentation), Is.SameAs(decoy), "Explicit override remains authoritative.");
                UnityEngine.Object.Destroy(presentation);
                yield return null;
            }
            var cameraField = caster.GetType().GetField("targetCamera", BindingFlags.Instance | BindingFlags.NonPublic);
            cameraField.SetValue(caster, null);
            var unbound = Add(player, "AirTargetChargeUI");
            Assert.That(unbound.GetType().GetField("targetCamera", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(unbound), Is.Null,
                "An unbound owner must not borrow the other player's main camera.");
            UnityEngine.Object.Destroy(unbound); UnityEngine.Object.Destroy(decoy.gameObject);
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
        public IEnumerator DestroyedSlingTargetsCancelChargeAndPaidTraversalWithoutLaunch()
        {
            int canceled = 0, launched = 0;
            Observe(sling, "SlingCanceled", () => canceled++);
            Observe(sling, "SlingLaunched", () => launched++);
            node = new GameObject("Charge target"); node.transform.position = new Vector3(0, 2, 8); Add(node, "AirSlingNode");
            yield return Frames(3); Keys(Key.F); yield return Frames(3);
            UnityEngine.Object.Destroy(node); yield return Frames(2);
            Assert.That(Get(sling, "IsCharging"), Is.EqualTo(false));
            Assert.That(Get(power, "Current"), Is.EqualTo(100f));
            Keys(); yield return Frames(2);
            node = new GameObject("Traversal target"); node.transform.position = new Vector3(0, 2, 8); Add(node, "AirSlingNode");
            yield return Frames(3); Keys(Key.F); yield return Frames(19); Keys(); yield return Frames(1);
            Assert.That(Get(power, "Current"), Is.EqualTo(85f));
            UnityEngine.Object.Destroy(node); yield return Frames(3);
            Assert.That(Get(sling, "IsSlinging"), Is.EqualTo(false));
            Assert.That(Get(motor, "HasExternalTraversalVelocity"), Is.EqualTo(false));
            Assert.That(canceled, Is.EqualTo(2)); Assert.That(launched, Is.Zero);
            Assert.That(Get(power, "Current"), Is.EqualTo(85f), "Committed cost is not refunded by baseline cancellation.");
        }

        [UnityTest]
        public IEnumerator SlingDeathRewardUsesOnlyItsConfiguredPlayer()
        {
            var otherPlayer = new GameObject("Other resource owner");
            var otherPower = Add(otherPlayer, "AirPower"); Call(otherPower, "Configure", movement, true);
            Call(power, "Spend", 80f); Call(otherPower, "Spend", 80f);
            node = new GameObject("Reward node"); var slingNode = Add(node, "AirSlingNode");
            var health = Add(node, "AirSlingNodeHealth");
            var reward = Add(node, "AirSlingDeathReward");
            var rewardSettings = Asset("AirFlowHitSettings"); Set(rewardSettings, "returnStreamDuration", 0.05f);
            Call(reward, "Configure", slingNode, health, otherPower, otherPlayer.transform, rewardSettings, null);
            int delivered = 0; Observe(reward, "RewardDelivered", () => delivered++);
            Call(health, "TakeDamage", 100);
            yield return Frames(12);
            Assert.That(delivered, Is.EqualTo(1));
            Assert.That(Get(power, "Current"), Is.EqualTo(20f));
            Assert.That((float)Get(otherPower, "Current"), Is.GreaterThan(20f));
        }

        [UnityTest]
        public IEnumerator ExplicitFeedbackBindingRebindsAndReleasesSubscriptions()
        {
            using (var first = new Windrunner.Production.Feedback.FeedbackService(null, null, null, null, null, false))
            using (var second = new Windrunner.Production.Feedback.FeedbackService(null, null, null, null, null, false))
            {
                var emitter = Add(player, "AirflowProductionFeedbackEmitter");
                Call(emitter, "BindFeedbackService", first);
                Call(power, "Spend", 100f);
                Assert.That(first.ObservedCount, Is.EqualTo(1));
                Call(power, "Restore", 100f);
                int firstCount = first.ObservedCount;
                Call(emitter, "BindFeedbackService", second);
                Call(power, "Spend", 100f);
                Assert.That(first.ObservedCount, Is.EqualTo(firstCount));
                Assert.That(second.ObservedCount, Is.EqualTo(1));
                ((Behaviour)emitter).enabled = false;
                Call(power, "Restore", 100f);
                Assert.That(second.ObservedCount, Is.EqualTo(1));
                ((Behaviour)emitter).enabled = true;
                Call(power, "Spend", 100f);
                Assert.That(second.ObservedCount, Is.EqualTo(2));
                Call(emitter, "BindFeedbackService", new object[] { null });
                Call(power, "Restore", 100f);
                Assert.That(second.ObservedCount, Is.EqualTo(2));
                yield return null;
            }
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
