#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Windrunner.Production.Input;
using Windrunner.Production.Unity;
using Object=UnityEngine.Object;

namespace Windrunner.Production.Tests
{
    public sealed partial class MainMenuFlowTests
    {
        [UnityTest] public IEnumerator AcceptanceLaneReticlesRespectModalOwnershipAcrossReload()
        {
            const string path="Assets/WindrunnerFrontend/Scenes/StartingWorld.unity";
            var fixture=Type.GetType("Windrunner.Frontend.TraversalAcceptance, Assembly-CSharp",true);
            Type Runtime(string name)=>Type.GetType("AirflowPrototype."+name+", Assembly-CSharp",true);
            const BindingFlags fields=BindingFlags.Instance|BindingFlags.NonPublic;
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            CanvasGroup previous=null;
            for(int load=0;load<2;load++)
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                Assert.IsTrue(previous==null,"Old reticle canvas survived reload.");
                var session=Session();
                Assert.IsNull(Prop<string>(session,"SlotId"));
                var lane=(Component)fixture.GetMethod("Install").Invoke(null,new object[]{session});
                Assert.AreSame(lane,fixture.GetMethod("Install").Invoke(null,new object[]{session}));
                var targets=Prop<Transform>(lane,"Targets");
                Assert.AreEqual(3,targets.GetComponentsInChildren(Runtime("AirNode")).Length);
                Assert.AreEqual(1,targets.GetComponentsInChildren(Runtime("AirSlingNode")).Length);
                for(int frame=0;frame<8;frame++) yield return null;
                var targeter=(Component)Object.FindAnyObjectByType(Runtime("AirTargeter"));
                var acquired=Prop<Component>(targeter,"CurrentTarget");
                Assert.IsTrue(acquired && acquired.transform.IsChildOf(targets),"The production Cast targeter must acquire the lane from its spawn.");
                var host=WindrunnerInputHost.Active;
                foreach(bool sling in new[]{false,true})
                {
                    var type=Runtime(sling?"AirSlingTargetChargeUI":"AirTargetChargeUI");
                    var ui=(Component)Object.FindAnyObjectByType(type);
                    var group=(CanvasGroup)type.GetField("_canvasGroup",fields).GetValue(ui);
                    var flash=type.GetMethod(sling?"OnPerfectRMBReleased":"OnPerfectReleased",fields);
                    var node=targets.GetComponentInChildren(Runtime(sling?"AirSlingNode":"AirNode"));
                    flash.Invoke(ui,new object[]{node,1f});
                    yield return null;yield return null;
                    Assert.Greater(group.alpha,0f,"A valid production target must be visible before blocking.");
                    host.SetPaused(true);
                    Canvas.ForceUpdateCanvases();
                    Assert.AreEqual(0f,group.alpha);
                    yield return null;
                    if (!sling && load==0)
                    {
                        foreach(var page in new[]{"Settings","Save game","Load game","Dagger Arsenal"})
                        {
                            Submit(page);yield return null;
                            Assert.AreEqual(0f,group.alpha,page);
                            Submit("Back");yield return null;
                            Assert.IsTrue(host.IsPaused);Assert.AreEqual(0f,group.alpha);
                        }
                    }
                    using(host.Input.BlockGameplay())
                    {
                        host.SetPaused(false);
                        flash.Invoke(ui,new object[]{node,1f});
                        Assert.IsNull(type.GetField("_perfectFlashTarget",fields).GetValue(ui));
                        if (!sling) Object.Destroy(node.gameObject);
                        targets.gameObject.SetActive(false);
                        yield return null; Assert.AreEqual(0f,group.alpha);
                    }
                    yield return null;yield return null;
                    Assert.AreEqual(0f,group.alpha,"Invalid target reappeared on modal exit.");
                    targets.gameObject.SetActive(true);
                    if (!sling)
                    {
                        Assert.IsTrue(node==null);
                        for(int frame=0;frame<8;frame++) yield return null;
                        acquired=Prop<Component>(targeter,"CurrentTarget");
                        Assert.IsTrue(acquired && acquired.transform.IsChildOf(targets));
                        Assert.Greater(group.alpha,0f,"A newly acquired target should return without a perfect release.");
                    }
                    else flash.Invoke(ui,new object[]{node,1f});
                    yield return null;yield return null;
                    Assert.Greater(group.alpha,0f);
                    previous=group;
                }
                Object.Destroy(ProjectRoot.Active.gameObject);yield return null;
            }
            var world=SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Lane teardown"));
            yield return SceneManager.UnloadSceneAsync(world);
        }
    }
}
#endif
