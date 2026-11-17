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
            public string result, outputPath, unity, utc;
            public int errors, warnings;
            public double seconds;
            public ulong bytes;
            public string[] warningMessages;
            public string[] scenes;
        }

        public static void BuildWindows()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-windrunnerBuildLabel");
            string label = index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : "candidate";
            if (!Regex.IsMatch(label, "^[a-zA-Z0-9_-]+$")) throw new ArgumentException("Invalid build label.");
            string folder = Path.GetFullPath(Path.Combine("Builds", label));
            if (Directory.Exists(folder)) throw new InvalidOperationException("Use a fresh build label; existing artifacts are never overwritten.");
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (arguments.Contains("-windrunnerProgressionFixture"))
                scenes = new[] { "Assets/WindrunnerProduction/Progression/Fixtures/ProgressionFixture.unity" };
            if (arguments.Contains("-windrunnerMovementGym"))
                scenes = new[] { "Assets/MovementLab/MovementGym.unity" };
            if (scenes.Length == 0) throw new InvalidOperationException("No enabled scenes.");
            Directory.CreateDirectory(folder);
            bool developmentGym=arguments.Contains("-windrunnerMovementGym");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(folder, "Windrunner.exe"),
                target = BuildTarget.StandaloneWindows64,
                extraScriptingDefines = developmentGym ? new[] { "WINDRUNNER_MOVEMENT_LAB" } : Array.Empty<string>(),
                options = developmentGym ? BuildOptions.Development : BuildOptions.None
            });
            var summary = report.summary;
            var result = new Result
            {
                result = summary.result.ToString(), outputPath = summary.outputPath,
                unity = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"),
                errors = (int)summary.totalErrors, warnings = (int)summary.totalWarnings,
                seconds = summary.totalTime.TotalSeconds, bytes = summary.totalSize,
                scenes = scenes,
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
