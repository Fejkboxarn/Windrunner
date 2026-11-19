#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Windrunner.Production.Input;
using Windrunner.Production.Progression;
namespace Windrunner.Production.Tests
{
    public sealed partial class MainMenuFlowTests
    {
        static EditorWindow zoneCaptureView;
        static string zoneCaptureFolder;
        static int zoneCaptureIndex;
        static IEnumerator ZoneUIFrame(string name)
        {
            if(!zoneCaptureView)yield break;
            var journal=UnityEngine.Object.FindAnyObjectByType<ProgressionJournal>();
            var camera=journal ? journal.interaction.view : null;
            Vector3 priorPosition=camera ? camera.transform.position : default;Quaternion priorRotation=camera ? camera.transform.rotation : default;
            Behaviour orbit=null;bool orbitEnabled=false;
            if(camera && journal.Wayfinder.Target && (name=="08-ground-tracking" || name=="09-air-tracking" || name=="07-relay-guidance"))
            {
                orbit=(Behaviour)camera.GetComponent(Type.GetType("AirflowPrototype.PlayerOrbitCamera, Assembly-CSharp",true));orbitEnabled=orbit&&orbit.enabled;if(orbit)orbit.enabled=false;
                var target=journal.Wayfinder.Target.position;camera.transform.position=target+new Vector3(4,4,-18);camera.transform.LookAt(target+Vector3.up*2);
            }
            yield return new WaitForSecondsRealtime(.15f);Canvas.ForceUpdateCanvases();zoneCaptureView.Repaint();yield return null;
            string file=Path.Combine(zoneCaptureFolder,(zoneCaptureIndex++).ToString("D2")+"-"+name+".png");ScreenCapture.CaptureScreenshot(file);
            for(int i=0;i<120&&!File.Exists(file);i++){zoneCaptureView.Repaint();yield return null;}
            Assert.IsTrue(File.Exists(file),file);
            if(camera)camera.transform.SetPositionAndRotation(priorPosition,priorRotation);if(orbit)orbit.enabled=orbitEnabled;
        }
        static IEnumerator AssertZoneGuide(string objective)
        {
            var journal=UnityEngine.Object.FindAnyObjectByType<ProgressionJournal>();
            var provider=(IObjectiveAnchorProvider)Stage(objective);
            float deadline=Time.realtimeSinceStartup+1;
            while((!journal.Wayfinder.Visible || journal.Wayfinder.Target!=provider.ObjectiveAnchor)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(journal.Wayfinder.Visible,objective);Assert.AreSame(provider.ObjectiveAnchor,journal.Wayfinder.Target,objective);
            Assert.AreEqual(objective,journal.Wayfinder.Binding.objectiveId);
            for(int i=0;i<5;i++){yield return null;Assert.AreSame(provider.ObjectiveAnchor,journal.Wayfinder.Target,"Live position/anchor tracking");}
        }
        static IEnumerator ZoneMenuFrames()
        {
            var j=UnityEngine.Object.FindAnyObjectByType<ProgressionJournal>();
            WindrunnerInputHost.Active.SetPaused(true);yield return null;Assert.IsFalse(j.Wayfinder.Visible);yield return ZoneUIFrame("02-pause");
            Submit("Settings");yield return ZoneUIFrame("03-settings");Submit("Interface");Submit("Objective Wayfinder: ON");Assert.IsFalse(WindrunnerUITheme.ObjectiveWayfinder);Submit("Back");Submit("Back");Submit("Resume");yield return null;Assert.IsFalse(j.Wayfinder.Visible);
            WindrunnerInputHost.Active.SetPaused(true);yield return null;Submit("Settings");Submit("Interface");Submit("Objective Wayfinder: OFF");Submit("Back");Submit("Back");
            Submit("Dagger Arsenal");yield return ZoneUIFrame("04-arsenal");Assert.IsFalse(j.Wayfinder.Visible);Submit("Back");
            Submit("Save game");yield return ZoneUIFrame("05-save");Assert.IsFalse(j.Wayfinder.Visible);Submit("Back");Submit("Load game");yield return null;Assert.IsFalse(j.Wayfinder.Visible);Submit("Back");Submit("Resume");yield return null;
            Assert.IsTrue(j.SetOpen(true));yield return ZoneUIFrame("06-journal");yield return null;Assert.IsFalse(j.Wayfinder.Visible);j.SetOpen(false);yield return AssertZoneGuide("relay");
        }
        [UnityTest,Category("Profiling")] public IEnumerator CinematicZoneFourQuestRenderedIntegration()
        {
            var t=typeof(Editor).Assembly.GetType("UnityEditor.GameView");zoneCaptureView=EditorWindow.GetWindow(t);zoneCaptureView.Show();zoneCaptureView.Focus();
            var selected=t.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);int prior=(int)selected.GetValue(zoneCaptureView);
            var sizesType=typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");var sizes=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{GameViewSizeGroupType.Standalone});var gt=group.GetType();int count=(int)gt.GetMethod("GetTotalCount").Invoke(group,null);
            var sizeType=typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");var enumType=typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
            gt.GetMethod("AddCustomSize").Invoke(group,new[]{Activator.CreateInstance(sizeType,Enum.Parse(enumType,"FixedResolution"),1920,1080,"Zone UI integration capture")});selected.SetValue(zoneCaptureView,count);
            bool asyncShaders=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            zoneCaptureFolder=Path.GetFullPath("Logs/CinematicUIIntegration/rendered-"+DateTime.UtcNow.Ticks);Directory.CreateDirectory(zoneCaptureFolder);zoneCaptureIndex=0;
            try { yield return FourZoneQuestsUseNpcAbilitiesRetriesAndRealSaveReload();Debug.Log("ZONE_UI_CAPTURES: "+zoneCaptureFolder); }
            finally { ShaderUtil.allowAsyncCompilation=asyncShaders;selected.SetValue(zoneCaptureView,prior);gt.GetMethod("RemoveCustomSize").Invoke(group,new object[]{count});zoneCaptureView=null; }
        }
    }
}
#endif
