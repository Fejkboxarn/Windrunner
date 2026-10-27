using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Windrunner.Production.Core;
using Windrunner.Production.Unity;

namespace Windrunner.Production.Editor
{
    /// <summary>Opt-in migration setup and baseline audit; no asset rewriting.</summary>
    public static class ProductionSetupMenu
    {
        private const string MenuRoot = "Tools/Windrunner/Production/";

        [MenuItem(MenuRoot + "Install Project Root")]
        public static void InstallRoot()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Windrunner Production] Install only in Edit Mode.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Windrunner Production] Open a scene before installing the root.");
                return;
            }

            ProjectRoot[] existing = UnityEngine.Object.FindObjectsByType<ProjectRoot>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (existing.Length > 0)
            {
                Debug.LogWarning("[Windrunner Production] Found " + existing.Length +
                                 " ProjectRoot component(s). No new root created.");
                Selection.activeObject = existing[0];
                return;
            }

            GameObject root = new GameObject("WindrunnerProjectRoot");
            Undo.RegisterCreatedObjectUndo(root, "Install Windrunner Production Root");
            root.AddComponent<ProjectRoot>();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log("[Windrunner Production] ProjectRoot installed in " + scene.name +
                      ". Save the scene, then run Audit Open Scenes.");
        }

        [MenuItem(MenuRoot + "Audit Open Scenes")]
        public static void AuditOpenScenes()
        {
            var report = new StringBuilder();
            report.AppendLine("Windrunner Production — Batch 01 preflight");

            ProjectRoot[] roots = UnityEngine.Object.FindObjectsByType<ProjectRoot>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            report.AppendLine("ProjectRoot components: " + roots.Length);
            if (roots.Length != 1)
                report.AppendLine("ACTION: Expected one project root in the currently loaded scene set.");

            var motors = new System.Collections.Generic.List<MonoBehaviour>();
            MonoBehaviour[] components = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (MonoBehaviour component in components)
            {
                if (component != null && component.GetType().Name == "PlayerMotor")
                    motors.Add(component);
            }

            report.AppendLine("Airflow PlayerMotor components: " + motors.Count);
            foreach (MonoBehaviour motor in motors)
            {
                MonoBehaviour[] attached = motor.GetComponentsInChildren<MonoBehaviour>(true);
                bool humanoid = false;
                bool custom = false;
                bool procedural = false;
                foreach (MonoBehaviour component in attached)
                {
                    if (component == null) continue;
                    string typeName = component.GetType().Name;
                    humanoid |= typeName == "PlayerHumanoidAnimatorDriver";
                    custom |= typeName == "PlayerCustomAnimationDriver";
                    procedural |= typeName == "PlayerMovementVisuals";
                }

                if (humanoid && custom)
                    report.AppendLine("REVIEW: " + motor.name +
                        " has both Humanoid and Custom Animator drivers; verify single pose ownership before Batch 04.");
                if (humanoid && procedural)
                    report.AppendLine("REVIEW: " + motor.name +
                        " has Humanoid and procedural movement presenters; verify write ordering before Batch 04.");
            }

            string[] patterns =
            {
                "Keyboard.current", "Mouse.current", "Gamepad.current",
                "UnityEngine.Input.Get", "Input.Get", "Camera.main"
            };
            int[] occurrences = new int[patterns.Length];
            int scanned = 0;
            string[] guids = AssetDatabase.FindAssets("t:MonoScript",
                new[] { "Assets/AirflowPrototype/Runtime" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                    continue;

                scanned++;
                string source = File.ReadAllText(path);
                for (int i = 0; i < patterns.Length; i++)
                    occurrences[i] += CountOccurrences(source, patterns[i]);
            }

            report.AppendLine("Airflow runtime scripts scanned: " + scanned);
            for (int i = 0; i < patterns.Length; i++)
                report.AppendLine(patterns[i] + " references: " + occurrences[i]);

            report.AppendLine("These are migration indicators, not automatic errors. No changes were made.");
            Debug.Log(report.ToString());
        }

        [MenuItem(MenuRoot + "Run Core Self-Test")]
        public static void RunCoreSelfTest()
        {
            var services = new ServiceRegistry();
            var probe = new DisposalProbe();
            services.Register<DisposalProbe>(probe);
            services.Register<IDisposable>(probe);

            DisposalProbe recovered;
            Require(services.TryGet(out recovered) && ReferenceEquals(recovered, probe),
                "Service resolution failed.");

            var machine = new GameStateMachine();
            var first = new TestState();
            var second = new TestState();
            machine.SetState(first);
            machine.Tick(0.016f);
            machine.SetState(second);
            Require(first.EnterCount == 1 && first.TickCount == 1 && first.ExitCount == 1,
                "State transition lifecycle failed.");
            Require(second.EnterCount == 1, "Next state did not enter.");

            machine.Dispose();
            services.Dispose();
            Require(second.ExitCount == 1, "Final state did not exit.");
            Require(probe.DisposalCount == 1, "Multiple service aliases must dispose only once.");

            Debug.Log("[Windrunner Production] Core self-test passed: registry, lifecycle and teardown.");
        }

        private static int CountOccurrences(string source, string needle)
        {
            int count = 0;
            int cursor = 0;
            while ((cursor = source.IndexOf(needle, cursor, StringComparison.Ordinal)) >= 0)
            {
                count++;
                cursor += needle.Length;
            }
            return count;
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private sealed class DisposalProbe : IDisposable
        {
            public int DisposalCount;
            public void Dispose() { DisposalCount++; }
        }

        private sealed class TestState : IProjectState
        {
            public int EnterCount;
            public int TickCount;
            public int ExitCount;
            public void Enter() { EnterCount++; }
            public void Tick(float dt) { TickCount++; }
            public void Exit() { ExitCount++; }
        }
    }
}
