using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Windrunner.TerrainAuthoring.Editor;

namespace Windrunner.TerrainAuthoring.Tests
{
    // Opt-in direct-play packaging check. Generate only in a new, uniquely owned test directory.
    public static class PaintedZoneCandidate
    {
        const string Source="Assets/WindrunnerProduction/World/Scenes/World_Zone01.unity";
        [Serializable] class Evidence
        {
            public string result,executable;
            public int errors,warnings,ribbonVertices;
            public double seconds;
            public string[] warningHeaders;
        }
        public static void Build()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Run this isolated packaging check in batch mode.");
            var args=Environment.GetCommandLineArgs();
            int flag=Array.IndexOf(args,"-windrunnerBuildLabel");
            string label=flag>=0 && flag+1<args.Length?args[flag+1]:"terrain-studio-painted";
            if(!System.Text.RegularExpressions.Regex.IsMatch(label,"^[a-zA-Z0-9_-]+$"))throw new ArgumentException("Invalid candidate label.");
            string output="Builds/"+label;
            if(Directory.Exists(output))throw new IOException("The painted candidate already exists; keep prior evidence.");
            string folder="Assets/TerrainStudioBuild-"+Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));
            try
            {
                string path=folder+"/PaintedBasin.unity";
                if(!AssetDatabase.CopyAsset(Source,path))throw new IOException("Could not copy the source zone.");
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                var terrain=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>()).Single();
                var tile=StudioSetup.Attach(terrain,StudioSetup.Preset<SurfaceProfile>("Basin surfaces"),folder);
                var road=StudioSetup.CreateRoad(tile,new Vector2(5,12),StudioSetup.Preset<RoadProfile>("Carriage road"));
                var paths=RoadPath.InScene(tile);
                StudioUpdates.Rebuild(tile,TerrainPainter.Full(tile.state.working),paths,false);
                if(tile.ribbons.sharedMesh.vertexCount==0 || !road.CompareTag("EditorOnly") || !tile.CompareTag("EditorOnly"))
                    throw new InvalidOperationException("Painted preview is missing baked geometry or authoring-strip tags.");
                int vertices=tile.ribbons.sharedMesh.vertexCount;
                string workingPath=AssetDatabase.GetAssetPath(tile.state.working),meshPath=AssetDatabase.GetAssetPath(tile.ribbons.sharedMesh),historyPath=AssetDatabase.GetAssetPath(tile.state);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save the isolated scene.");
                AssetDatabase.SaveAssets();
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{path},locationPathName=output+"/Windrunner.exe",
                    target=BuildTarget.StandaloneWindows64,options=BuildOptions.DetailedBuildReport});
                var summary=report.summary;
                if(summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Painted preview build failed.");
                var packed=report.packedAssets.SelectMany(p=>p.contents).Select(p=>p.sourceAssetPath).Distinct().ToArray();
                if(!packed.Contains(workingPath) || !packed.Contains(meshPath) || packed.Contains(historyPath))
                    throw new InvalidOperationException("Built asset packing does not match baked terrain/mesh and stripped history.");
                var evidence=new Evidence{result=summary.result.ToString(),executable=summary.outputPath,errors=(int)summary.totalErrors,
                    warnings=(int)summary.totalWarnings,seconds=summary.totalTime.TotalSeconds,ribbonVertices=vertices,
                    warningHeaders=report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Warning).Select(m=>m.content.Split('\n')[0]).ToArray()};
                File.WriteAllText("Logs/ProductionMigration/"+label+"-package.json",JsonUtility.ToJson(evidence,true));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
