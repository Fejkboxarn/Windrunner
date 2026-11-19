using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace Windrunner.Suntemple.Editor
{
    public static class SuntempleBuild
    {
        public static void Windows()
        {
            SuntempleDependencyAudit.Run();
            string folder="Builds/SuntempleURP_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(folder);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{SuntempleValidation.Repaired},locationPathName=folder+"/Suntemple.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            Directory.CreateDirectory("Logs/SuntempleRepair");
            File.WriteAllText("Logs/SuntempleRepair/build-summary.txt",$"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nSeconds: {report.summary.totalTime.TotalSeconds}\n{Path.GetFullPath(folder+"/Suntemple.exe")}\n");
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Suntemple build failed");
        }
    }
}
