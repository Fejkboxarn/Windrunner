#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Windrunner.Production.Input;
using Windrunner.Production.Unity;
using Object=UnityEngine.Object;

namespace Windrunner.Production.Tests
{
    public sealed partial class MainMenuFlowTests
    {
        [UnityTest] public IEnumerator DaggerCastRetargetsProductionRigAndRecoversAfterRelease()
        {
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Legacy,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var settings=InputSystem.settings;
            var flags=settings.hideFlags;
            if(flags==HideFlags.HideAndDontSave) settings.hideFlags=HideFlags.DontSave;
            var isolated=Object.Instantiate(settings);
            isolated.hideFlags=HideFlags.DontSave;
            isolated.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            isolated.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings=isolated;
            var mouse=InputSystem.AddDevice<Mouse>();
            float capture=Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime=1f/60;
                var lane=Type.GetType("Windrunner.Frontend.TraversalAcceptance, Assembly-CSharp",true);
                lane.GetMethod("Install").Invoke(null,new object[]{Session()});
                var bridge=(Component)Object.FindAnyObjectByType(Type.GetType("AirflowPrototype.AirflowPortableAnimationBridge, Assembly-CSharp",true));
                var animator=Prop<Animator>(bridge,"HumanoidAnimator");
                // Batch Game view can be culled between screenshots. Sample the actual pose every fixture frame.
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                var input=WindrunnerInputHost.Active.Input;
                input.Actions.RemoveAllBindingOverrides();
                for(int i=0;i<24;i++)yield return null;
                var position=bridge.transform.position;
                var wrist=hand.rotation;
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left,true));
                for(int i=0;i<36;i++)yield return null;
                Assert.AreEqual("CastCharge",Prop<string>(bridge,"ActiveAnimationAction"));
                var rotationDelta=Quaternion.Angle(wrist,hand.rotation);
                Debug.Log($"Cast charge wrist delta {rotationDelta}, rig {animator.name}, enabled {animator.enabled}, active {animator.gameObject.activeInHierarchy}, culling {animator.cullingMode}, dt {Time.deltaTime}, hand {hand.rotation}, scale {bridge.transform.lossyScale}.");
                Assert.Greater(rotationDelta,1f);
                Assert.Less(Vector2.Distance(new Vector2(position.x,position.z),
                    new Vector2(bridge.transform.position.x,bridge.transform.position.z)),.05f);
                Assert.IsFalse(animator.applyRootMotion);
                Assert.IsNull(animator.runtimeAnimatorController);
                // Hold the real Cast input while a transient semantic presentation interrupts it.
                var feedback=Windrunner.Production.Feedback.WindrunnerFeedbackHost.Active.Service;
                int observed=feedback.ObservedCount;
                feedback.Publish(Windrunner.Production.Feedback.FeedbackKey.Flip,
                    Windrunner.Production.Feedback.FeedbackRequest.At(bridge.transform));
                Assert.AreEqual("DoubleJumpFlip",Prop<string>(bridge,"ActiveAnimationAction"));
                for(int i=0;i<70;i++)yield return null;
                Assert.AreEqual("CastCharge",Prop<string>(bridge,"ActiveAnimationAction"));
                Assert.AreEqual(observed+1,feedback.ObservedCount,"Pose recovery must not replay gameplay feedback.");
                InputSystem.QueueStateEvent(mouse,new MouseState());
                yield return null;yield return null;
                Assert.AreEqual("CastRelease",Prop<string>(bridge,"ActiveAnimationAction"));
                if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    var file=Path.GetFullPath("Logs/ProductionMigration/dagger-cast-release-"+DateTime.UtcNow.Ticks+".png");
                    var view=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                    view.Repaint();yield return null;
                    ScreenCapture.CaptureScreenshot(file);
                    for(int i=0;i<120 && !File.Exists(file);i++){view.Repaint();yield return null;}
                    Assert.IsTrue(File.Exists(file));
                }
                for(int i=0;i<75;i++)yield return null;
                Assert.AreNotEqual("CastRelease",Prop<string>(bridge,"ActiveAnimationAction"));
            }
            finally
            {
                Time.captureDeltaTime=capture;
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings=settings;settings.hideFlags=flags;Object.Destroy(isolated);
                if(ProjectRoot.Active)Object.Destroy(ProjectRoot.Active.gameObject);
            }
            yield return null;
            var prior=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Dagger motion teardown"));
            yield return SceneManager.UnloadSceneAsync(prior);
        }
    }
}
#endif
