using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class WindrunnerCandidateBuild
    {
        [Serializable]
        private sealed class Result
        {
            public string result, outputPath, unity, utc, version;
            public int errors, warnings;
            public double seconds;
            public ulong bytes;
            public string[] warningMessages;
            public string[] scenes;
            public bool development, gymFlight;
        }

        public static void BuildWindows()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-windrunnerBuildLabel");
            string label = index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : "candidate";
            if (!Regex.IsMatch(label, "^[a-zA-Z0-9_-]+$")) throw new ArgumentException("Invalid build label.");
            string folder = Path.GetFullPath(Path.Combine("Builds", label));
            if (Directory.Exists(folder)) throw new InvalidOperationException("Use a fresh build label; existing artifacts are never overwritten.");
            bool gym = arguments.Contains("-windrunnerMovementGym");
            bool development = gym || arguments.Contains("-windrunnerDevelopment");
            var targets = new[] { "-windrunnerMovementGym", "-windrunnerProgressionFixture", "-windrunnerMainMenu" };
            if (targets.Count(arguments.Contains) > 1) throw new ArgumentException("Select one candidate scene set.");
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (arguments.Contains("-windrunnerProgressionFixture"))
                scenes = new[] { "Assets/WindrunnerProduction/Progression/Fixtures/ProgressionFixture.unity" };
            if (arguments.Contains("-windrunnerMainMenu"))
                scenes = new[] { Windrunner.Frontend.PlayerSession.TitleScene, Windrunner.Frontend.PlayerSession.ZoneScene, Windrunner.Frontend.PlayerSession.WorldScene };
            if (gym) scenes = new[] { "Assets/MovementLab/MovementGym.unity" };
            if (scenes.Length == 0) throw new InvalidOperationException("No enabled scenes.");
            Directory.CreateDirectory(folder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(folder, "Windrunner.exe"),
                target = BuildTarget.StandaloneWindows64,
                extraScriptingDefines = gym ? new[] { "WINDRUNNER_MOVEMENT_LAB" } : Array.Empty<string>(),
                options = development ? BuildOptions.Development : BuildOptions.None
            });
            var summary = report.summary;
            var result = new Result
            {
                result = summary.result.ToString(), outputPath = summary.outputPath,
                version = PlayerSettings.bundleVersion, unity = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"),
                errors = (int)summary.totalErrors, warnings = (int)summary.totalWarnings,
                seconds = summary.totalTime.TotalSeconds, bytes = summary.totalSize,
                scenes = scenes,
                development = development, gymFlight = gym,
                warningMessages = report.steps.SelectMany(step => step.messages)
                    .Where(message => message.type == LogType.Warning).Select(message => message.content).ToArray()
            };
            Directory.CreateDirectory("Logs/ProductionMigration");
            File.WriteAllText("Logs/ProductionMigration/" + label + "-build.json", JsonUtility.ToJson(result, true));
            if (summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows build failed; inspect the build log/report.");
            Debug.Log("[Windrunner Candidate] Build succeeded: " + summary.outputPath);
        }

        public static void ExportLatestWarnings()
        {
            var report = BuildReport.GetLatestReport();
            if (report == null) throw new InvalidOperationException("No previous build report.");
            var lines = report.steps.SelectMany(step => step.messages)
                .Where(message => message.type == LogType.Warning).Select(message => message.content);
            File.WriteAllLines("Logs/ProductionMigration/rc1-warning-messages.txt", lines);
            Debug.Log("Exported previous build warning messages: " + report.summary.totalWarnings);
        }
    }
}
