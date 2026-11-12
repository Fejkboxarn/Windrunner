using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Windrunner.Production.Input;

namespace Windrunner.Production.Tests
{
    public sealed class GameplayLifecycleTests
    {
        [UnityTest]
        public IEnumerator MotorMovesJumpsAndStopsIntegrationWhilePaused()
        {
            Assert.That(WindrunnerInputHost.Active, Is.Null, "Fixtures must not leak persistent input hosts.");
            var scene = SceneManager.CreateScene("Motor integration fixture");
            SceneManager.SetActiveScene(scene);
            var player = new GameObject("Motor fixture");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(100f, 1f, 100f);
            var camera = new GameObject("Motor camera basis");
            var motorType = Type.GetType("AirflowPrototype.PlayerMotor, Assembly-CSharp", true);
            var inputType = Type.GetType("AirflowPrototype.PlayerInputReader, Assembly-CSharp", true);
            var settingsType = Type.GetType("AirflowPrototype.AirflowMovementSettings, Assembly-CSharp", true);
            var settings = ScriptableObject.CreateInstance(settingsType);
            var originalInputSettings = InputSystem.settings;
            var originalFlags = originalInputSettings.hideFlags;
            // InputManager destroys temporary HideAndDontSave settings when
            // replaced. Retain the original object so teardown can restore it.
            if (originalFlags == HideFlags.HideAndDontSave) originalInputSettings.hideFlags = HideFlags.DontSave;
            var testInputSettings = UnityEngine.Object.Instantiate(originalInputSettings);
            testInputSettings.hideFlags = HideFlags.DontSave;
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testInputSettings;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            float oldCapture = Time.captureDeltaTime;
            float oldScale = Time.timeScale;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                Time.timeScale = 1f;
                var controller = player.AddComponent<CharacterController>();
                player.transform.position = new Vector3(0f, 1.05f, 0f);
                var input = player.AddComponent(inputType);
                var motor = player.AddComponent(motorType);
                motorType.GetMethod("Configure").Invoke(motor, new object[] { controller, input, camera.transform, null, settings });
                Physics.SyncTransforms();
                for (int i = 0; i < 30; i++) yield return null;
                Assert.That((bool)motorType.GetProperty("IsGrounded").GetValue(motor), Is.True);
                var start = player.transform.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                for (int i = 0; i < 20; i++) yield return null;
                Assert.That(player.transform.position.z - start.z, Is.GreaterThan(0.1f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
                for (int i = 0; i < 3; i++) yield return null;
                Assert.That((float)motorType.GetProperty("VerticalSpeed").GetValue(motor), Is.GreaterThan(0f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return null;
                float beforeSecondPress = (float)motorType.GetProperty("VerticalSpeed").GetValue(motor);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
                for (int i = 0; i < 3; i++) yield return null;
                Assert.That((float)motorType.GetProperty("VerticalSpeed").GetValue(motor), Is.LessThan(beforeSecondPress),
                    "Baseline has no airborne double jump; a second press must not reset upward velocity.");
                Time.timeScale = 0f;
                yield return null;
                var paused = player.transform.position;
                for (int i = 0; i < 5; i++) yield return null;
                Assert.That(player.transform.position, Is.EqualTo(paused));
                Time.timeScale = 1f;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                for (int i = 0; i < 90; i++) yield return null;
                Assert.That((bool)motorType.GetProperty("IsGrounded").GetValue(motor), Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                for (int i = 0; i < 20; i++) yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                for (int i = 0; i < 40; i++) yield return null;
                Assert.That(((Vector3)motorType.GetProperty("PlanarVelocity").GetValue(motor)).z, Is.LessThan(-0.1f));
            }
            finally
            {
                Time.captureDeltaTime = oldCapture;
                Time.timeScale = oldScale;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = originalInputSettings;
                originalInputSettings.hideFlags = originalFlags;
                UnityEngine.Object.Destroy(testInputSettings);
                UnityEngine.Object.Destroy(player);
                UnityEngine.Object.Destroy(ground);
                UnityEngine.Object.Destroy(camera);
                UnityEngine.Object.Destroy(settings);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameplayReloadKeepsOneServiceOwnerAndRebindsSceneUi()
        {
            const string path = "Assets/AirflowPrototype/Generated/Batch1_Playground.unity";
            try
            {
                yield return SceneManager.LoadSceneAsync(path);
                yield return null;
                var root = Windrunner.Production.Unity.ProjectRoot.Active;
                var host = WindrunnerInputHost.Active;
                var service = host.Input;
                var menuField = typeof(WindrunnerInputHost).GetField("menu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var menu = (WindrunnerPauseMenuController)menuField.GetValue(host);
                menu.Pause();
                Assert.That(host.IsPaused, Is.True);
                int rowCount = host.PauseView.BindingsContainer.childCount;
                yield return SceneManager.LoadSceneAsync(path);
                yield return null; yield return null;
                Assert.That(Windrunner.Production.Unity.ProjectRoot.Active, Is.SameAs(root));
                Assert.That(WindrunnerInputHost.Active.Input, Is.SameAs(service));
                Assert.That(UnityEngine.Object.FindObjectsByType<Windrunner.Production.Unity.ProjectRoot>(FindObjectsInactive.Include).Length, Is.EqualTo(1));
                Assert.That(UnityEngine.Object.FindObjectsByType<WindrunnerInputHost>(FindObjectsInactive.Include).Length, Is.EqualTo(1));
                Assert.That(host.UiModule, Is.Not.Null);
                Assert.That(host.UiModule.gameObject.scene, Is.EqualTo(SceneManager.GetActiveScene()));
                Assert.That(host.UiModule.enabled, Is.True);
                Assert.That(host.IsPaused, Is.True);
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
                Assert.That(Cursor.visible, Is.True);
                Assert.That(host.PauseView.BindingsContainer.childCount, Is.EqualTo(rowCount), "Reload must not duplicate binding rows.");
                ((WindrunnerPauseMenuController)menuField.GetValue(host)).Resume();
                Assert.That(service.IsMenuOpen, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally
            {
                var root = Windrunner.Production.Unity.ProjectRoot.Active;
                if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            }
            yield return null;
            var empty = SceneManager.CreateScene("Reload cleanup"); SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync("Batch1_Playground");
        }

        [UnityTest]
        public IEnumerator GameplaySceneStartsAndPlayerRemainsFinite()
        {
#if UNITY_EDITOR
            var authored = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/INab Studio/2D Assets/Procedural Progress Bars/Demo Files/Demo Materials/Canvas/Canvas 4.mat");
            Assert.That(authored, Is.Not.Null);
            float originalFill = authored.GetFloat("_Fill_Amount");
            float originalMain = authored.GetFloat("_Main_Bar_Fill_Amount");
#endif
            yield return SceneManager.LoadSceneAsync("Assets/AirflowPrototype/Generated/Batch1_Playground.unity", LoadSceneMode.Single);
            var motorType = Type.GetType("AirflowPrototype.PlayerMotor, Assembly-CSharp", true);
            var motors = UnityEngine.Object.FindObjectsByType(motorType, FindObjectsSortMode.None);
            Assert.That(motors.Length, Is.EqualTo(1), "Gameplay scene must have one player motor.");
            var player = ((Component)motors[0]).transform;
            for (int frame = 0; frame < 60; frame++)
            {
                yield return null;
                var position = player.position;
                Assert.That(float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                    float.IsNaN(position.y) || float.IsInfinity(position.y) ||
                    float.IsNaN(position.z) || float.IsInfinity(position.z), Is.False);
            }
            Assert.That(WindrunnerInputHost.Active, Is.Not.Null);
            Assert.That(WindrunnerInputHost.Active.Input, Is.Not.Null);
            var gameplayCamera = Camera.main;
            Assert.That(gameplayCamera, Is.Not.Null);
            var speedType = Type.GetType("AirflowPrototype.PlayerSpeedFeedback, Assembly-CSharp", true);
            var speed = UnityEngine.Object.FindAnyObjectByType(speedType);
            Assert.That(speed, Is.Not.Null);
            speedType.GetMethod("PulseFovOverride").Invoke(speed, new object[] { 88f, 30f, 0.2f, 5f });
            using (var pause = Windrunner.Production.Unity.UnityTimeAuthority.Pause())
            {
                var position = gameplayCamera.transform.position; var rotation = gameplayCamera.transform.rotation;
                float fov = gameplayCamera.fieldOfView;
                for (int i = 0; i < 5; i++) yield return null;
                Assert.That(gameplayCamera.transform.position, Is.EqualTo(position));
                Assert.That(gameplayCamera.transform.rotation, Is.EqualTo(rotation));
                Assert.That(gameplayCamera.fieldOfView, Is.EqualTo(fov));
            }
            var animationType = Type.GetType("AirflowPrototype.AirflowPortableAnimationBridge, Assembly-CSharp", true);
            var animation = (Behaviour)UnityEngine.Object.FindAnyObjectByType(animationType);
            Assert.That(animation, Is.Not.Null);
            Assert.That((bool)animationType.GetProperty("ProductionOnlyReady").GetValue(animation), Is.True,
                "Installed character must start with portable ownership and no legacy pose fallback.");
            var animator = (Animator)animationType.GetProperty("HumanoidAnimator").GetValue(animation);
            var originalController = animator.runtimeAnimatorController;
            bool originalEvents = animator.fireEvents;
            bool originalRootMotion = animator.applyRootMotion;
            var writers = new System.Collections.Generic.List<Behaviour>();
            var enabledStates = new System.Collections.Generic.List<bool>();
            foreach (var name in new[] { "legacyHumanoid", "legacyCustom", "legacyArms", "legacyKnife", "legacyThrow", "legacyFootIK" })
            {
                var writer = (Behaviour)animationType.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(animation);
                if (writer == null) continue;
                writers.Add(writer);
                enabledStates.Add(writer.enabled);
            }
            Assert.That((bool)animationType.GetMethod("TryStartPortable").Invoke(animation, null), Is.True,
                (string)animationType.GetProperty("LastError").GetValue(animation));
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That((bool)animationType.GetProperty("PortableActive").GetValue(animation), Is.True);
            foreach (var writer in writers) Assert.That(writer.enabled, Is.False, writer.GetType().Name);
            Assert.That(animator.runtimeAnimatorController, Is.Null);
            Assert.That(animator.applyRootMotion, Is.False);
            var existingSession = animationType.GetField("session", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(animation);
            var duplicateSession = Activator.CreateInstance(existingSession.GetType(), new object[] {
                animator, animationType.GetProperty("Profile").GetValue(animation), writers.ToArray(), player.transform });
            try
            {
                Assert.That((bool)duplicateSession.GetType().GetMethod("TryStartPortable").Invoke(duplicateSession, null), Is.False,
                    "A second session cannot take an already-owned Animator.");
                Assert.That((bool)animationType.GetProperty("PortableActive").GetValue(animation), Is.True);
            }
            finally { ((IDisposable)duplicateSession).Dispose(); }
            using (var actions = new Windrunner.Production.Feedback.FeedbackService(null, null, null, null, null, false))
            {
                animationType.GetMethod("BindFeedbackService").Invoke(animation, new object[] { actions });
                var session = animationType.GetField("session", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(animation);
                var activeAction = session.GetType().GetProperty("ActiveMontage");
                var request = Windrunner.Production.Feedback.FeedbackRequest.At(player.transform);
                actions.Publish(Windrunner.Production.Feedback.FeedbackKey.SlingDashStarted, request);
                Assert.That(activeAction.GetValue(session), Is.EqualTo("SlingTravel"));
                for (int i = 0; i < 10; i++) yield return null;
                actions.Publish(Windrunner.Production.Feedback.FeedbackKey.SlingImpact, request);
                Assert.That(activeAction.GetValue(session), Is.EqualTo("SlingImpact"));
                actions.Publish(Windrunner.Production.Feedback.FeedbackKey.Flip, request);
                actions.Publish(Windrunner.Production.Feedback.FeedbackKey.SlingLaunched, request);
                Assert.That(activeAction.GetValue(session), Is.EqualTo("DoubleJumpFlip"), "Sling end must not cancel the launch flip.");
                int blocked = (int)session.GetType().GetProperty("BlockedActions").GetValue(session);
                actions.Publish(Windrunner.Production.Feedback.FeedbackKey.CastReleased, request);
                Assert.That((int)session.GetType().GetProperty("BlockedActions").GetValue(session), Is.EqualTo(blocked + 1));
                Assert.That(activeAction.GetValue(session), Is.EqualTo("DoubleJumpFlip"), "Missing Cast clip must not replace a compatible action.");
                for (float deadline = Time.realtimeSinceStartup + 4f;
                    Time.realtimeSinceStartup < deadline && (string)activeAction.GetValue(session) != "None";) yield return null;
                Assert.That(activeAction.GetValue(session), Is.EqualTo("None"), "One-shot must complete.");
                actions.Publish(Windrunner.Production.Feedback.FeedbackKey.SlingDashStarted, request);
                actions.Publish(Windrunner.Production.Feedback.FeedbackKey.SlingCanceled, request);
                Assert.That(activeAction.GetValue(session), Is.EqualTo("None"));
            }
            animationType.GetMethod("StopPortable").Invoke(animation, null);
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(originalController));
            Assert.That(animator.fireEvents, Is.EqualTo(originalEvents));
            Assert.That(animator.applyRootMotion, Is.EqualTo(originalRootMotion));
            for (int i = 0; i < writers.Count; i++) Assert.That(writers[i].enabled, Is.EqualTo(enabledStates[i]));
            Assert.That((bool)animationType.GetMethod("TryStartPortable").Invoke(animation, null), Is.True);
            animation.enabled = false;
            yield return null;
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(originalController));
            for (int i = 0; i < writers.Count; i++) Assert.That(writers[i].enabled, Is.EqualTo(enabledStates[i]));
            animation.enabled = true;
            yield return null;
            Assert.That((bool)animationType.GetProperty("ProductionOnlyReady").GetValue(animation), Is.True,
                "Re-enable must restart the configured production owner without legacy fallback.");
#if UNITY_EDITOR
            Assert.That(authored.GetFloat("_Fill_Amount"), Is.EqualTo(originalFill), "Runtime UI must not mutate the authored fill material.");
            Assert.That(authored.GetFloat("_Main_Bar_Fill_Amount"), Is.EqualTo(originalMain));
            var bridgeType = Type.GetType("AirflowPrototype.AirPowerProgressBarUI, Assembly-CSharp", true);
            var bridge = UnityEngine.Object.FindAnyObjectByType(bridgeType);
            Assert.That(bridge, Is.Not.Null);
            var bar = (MonoBehaviour)bridgeType.GetField("proceduralProgressBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(bridge);
            var runtimeMaterial = (Material)bar.GetType().GetField("progressBarMaterial").GetValue(bar);
            Assert.That(UnityEditor.EditorUtility.IsPersistent(runtimeMaterial), Is.False);
            Assert.That(runtimeMaterial, Is.Not.SameAs(authored));
            var empty = SceneManager.CreateScene("Migration teardown");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync("Batch1_Playground");
            yield return null;
            Assert.That(runtimeMaterial == null, Is.True, "Runtime material must be destroyed on scene unload.");
            Assert.That(authored.GetFloat("_Fill_Amount"), Is.EqualTo(originalFill));
#endif
            // ProjectRoot deliberately survives scene unload; the test owns it
            // and must dispose it before another fixture creates local input.
            var root = Windrunner.Production.Unity.ProjectRoot.Active;
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            yield return null;
        }
    }
}
