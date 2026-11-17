using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace Windrunner.TerrainAuthoring.Editor
{
    public static class SetupValidation
    {
        static string Location(Component c)=>c.gameObject.scene.name+"/"+AnimationUtility.CalculateTransformPath(c.transform,null);
        internal static string RoadError(StudioRoad road)
        {
            var spline=road.GetComponent<SplineContainer>();var profile=road.profile;
            string error=null;
            if(!spline || spline.Splines.Count==0 || spline.Splines.Any(s=>s==null || s.Count<2))
                error="Restore the SplineContainer and give each spline at least two knots, or disable the road.";
            else if(!profile)error="Assign a RoadProfile or disable the road before rebuilding its old footprint.";
            else if(!profile.surface)error="Assign the road surface TerrainLayer.";
            else if(!(profile.width>0 && profile.shoulderMetres>0) || profile.shoulderFade==null || profile.shoulderFade.length==0)
                error="Set positive road and shoulder widths and a shoulder falloff curve.";
            else if(profile.carriage && (!profile.trackMaterial || !profile.trackMaterial.shader || (SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null && !profile.trackMaterial.shader.isSupported) ||
                !(profile.trackWidth>0 && profile.trackSpacing>0 && profile.textureRepeatMetres>0)))
                error="Assign a supported wheel material and positive wheel width, spacing and texture repeat.";
            return error==null?null:Location(road)+" ["+(profile?AssetDatabase.GetAssetPath(profile):"missing profile")+"] "+error;
        }
        internal static string Scope(StudioTile tile,List<RoadPath> paths,RectInt selected,bool selectionOnly)
        {
            var issue=TerrainPainter.Validate(tile,paths);
            if(issue!=null)throw new InvalidOperationException(Location(tile)+": "+issue);
            var s=tile.state;
            var regions=StudioUpdates.Regions(tile,StudioUpdates.Stamps(tile,paths),selected,selectionOnly);
            if(!TerrainPainter.Empty(s.nativeDirty))throw new InvalidOperationException(Location(tile)+": capture or release pending native paint "+s.nativeDirty+".");
            foreach(var r in regions)
            {
                var changed=PaintHistory.Differences(s,r);
                if(!TerrainPainter.Empty(changed))
                    throw new InvalidOperationException(Location(tile)+": native paint differs at "+changed+". Open Manual Artistic Overrides and capture that area before rebuilding.");
            }
            return $"{Location(tile)} · {regions.Sum(r=>(long)r.width*r.height):N0} paint cells in {regions.Count} region(s), {s.protectedPaint.Count} protected capture(s).";
        }
        public static string Inspect(StudioTile tile,CharacterController controller=null)
        {
            string where=Location(tile);
            var roadIssues=tile.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StudioRoad>())
                .Where(r=>r.isActiveAndEnabled).Select(RoadError).Where(e=>e!=null).ToList();
            var paths=roadIssues.Count==0?RoadPath.InScene(tile):new List<RoadPath>();
            var issue=TerrainPainter.Validate(tile,paths);
            if(issue!=null)roadIssues.Add(where+": "+issue);
            if(roadIssues.Count>0)return string.Join("\n",roadIssues);
            var s=tile.state;var palette=TerrainPainter.Palette(tile,paths);
            var notes=new List<string>{where+" · "+AssetDatabase.GetAssetPath(s.working)};
            notes.Add($"{s.working.size.x} × {s.working.size.z} m · {s.working.alphamapWidth} × {s.working.alphamapHeight} paint cells · {palette.Length} layers.");
            if(!controller)controller=tile.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CharacterController>()).FirstOrDefault(c=>c.GetComponent("PlayerMotor"));
            if(controller)
            {
                var heights=new TerrainPainter.Heights(tile.terrain);
                int steep=paths.SelectMany(p=>p.points).Count(p=>TerrainPainter.Heights.Contains(tile.terrain,p) &&
                    TileDiagnostics.Grade(heights.Normal(p,1,out _),controller)==GroundGrade.Aerial);
                notes.Add($"{steep} road samples exceed {controller.name}'s {controller.slopeLimit:F0}° slope limit.");
            }
            notes.AddRange(TileDiagnostics.Layout(tile,palette));
            var dirty=StudioUpdates.Regions(tile,StudioUpdates.Stamps(tile,paths),default);
            notes.Add(dirty.Count==0?"No generated regions pending.":$"Pending rebuild: {dirty.Count} region(s), {dirty.Sum(r=>(long)r.width*r.height):N0} cells.");
            var changes=TerrainPainter.Union(s.nativeDirty,PaintHistory.Differences(s,TerrainPainter.Full(s.working)));
            notes.Add(TerrainPainter.Empty(changes)?"Paint matches the last applied record.":"Unresolved native paint "+changes+". Capture it before replacing coverage.");
            foreach(var road in paths.Select(p=>p.road).Distinct())
            {
                if(road.profile.carriage && road.profile.trackSpacing+road.profile.trackWidth>road.profile.width)
                    notes.Add(Location(road)+": wheel tracks extend beyond the road core. Check the shoulders in Scene view.");
                if(!road.CompareTag("EditorOnly"))notes.Add(Location(road)+": set the authoring object tag to EditorOnly for builds.");
            }
            return string.Join("\n",notes);
        }
        [MenuItem("Tools/Windrunner/Validate Terrain Setup")]
        static void ValidateScene()
        {
            var scene=SceneManager.GetActiveScene();int count=0;
            foreach(var root in scene.GetRootGameObjects())
                foreach(var t in root.GetComponentsInChildren<Terrain>(true))
                {
                    count++;var tile=t.GetComponentInChildren<StudioTile>(true);
                    var report=tile?Inspect(tile):Location(t)+": no working copy attached. Use Terrain Studio to opt in.";
                    Debug.Log(report,t);
                }
            if(count==0)Debug.Log(scene.name+": no Terrain found.");
        }
    }
}
