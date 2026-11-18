#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Windrunner.Production.Unity;
using Object=UnityEngine.Object;
namespace Windrunner.Production.Tests
{
    public sealed partial class MainMenuFlowTests
    {
        [UnityTest] public IEnumerator ZoneProductionPlayerAcquiresEveryAuthoredAirNode()
        {
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(World,new LoadSceneParameters(LoadSceneMode.Single));
            for(int i=0;i<12;i++)yield return null;
            var session=Session();Assert.IsNull(Prop<string>(session,"SlotId"));
            var character=(CharacterController)SessionType.GetField("character").GetValue(session);
            var arrival=(Transform)SessionType.GetField("arrival").GetValue(session);
            Assert.Less(Vector3.Distance(character.transform.position,arrival.position),2f,"Original arrival must remain functional");
            var orbit=(Behaviour)SessionType.GetField("orbit").GetValue(session);orbit.enabled=false;
            var type=Type.GetType("AirflowPrototype.AirTargeter, Assembly-CSharp",true);
            var targeter=character.GetComponent(type);var camera=Prop<Camera>(targeter,"ViewCamera");
            var nodeType=Type.GetType("AirflowPrototype.AirNode, Assembly-CSharp",true);
            var nodes=GameObject.Find("WorldRoot/Traversal/AirNodes").GetComponentsInChildren(nodeType);
            Assert.AreEqual(8,nodes.Length);
            foreach(var node in nodes)
            {
                var aim=Prop<Vector3>(node,"TargetPosition");var ground=aim-Vector3.forward*10;
                ground.y=Terrain.activeTerrain.SampleHeight(ground)+.2f;
                character.enabled=false;character.transform.position=ground;character.enabled=true;Physics.SyncTransforms();
                camera.transform.position=ground+Vector3.up*2-Vector3.forward*2;camera.transform.LookAt(aim);
                for(int i=0;i<8;i++)yield return null;
                Assert.AreSame(node,Prop<Component>(targeter,"CurrentTarget"),node.name);
                Assert.IsTrue(Prop<bool>(node,"IsAvailable"));
                // Render actual production player/camera against one new node; no scene asset is saved.
                if(node==nodes[0])
                {
                    var rt=new RenderTexture(640,360,24);var image=new Texture2D(640,360,TextureFormat.RGB24,false);
                    var prior=camera.targetTexture;var active=RenderTexture.active;
                    try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,640,360),0,0);image.Apply();File.WriteAllBytes("Logs/ProductionMigration/zone-worldbuilding-gameplay.png",image.EncodeToPNG());}
                    finally{camera.targetTexture=prior;RenderTexture.active=active;rt.Release();Object.Destroy(rt);Object.Destroy(image);}
                }
            }
            Assert.AreEqual(3,Object.FindObjectsByType(Type.GetType("AirflowPrototype.AirSlingNode, Assembly-CSharp",true),FindObjectsSortMode.None).Length);
            Object.Destroy(ProjectRoot.Active.gameObject);yield return null;
            var previous=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Node validation teardown"));
            yield return SceneManager.UnloadSceneAsync(previous);
        }
    }
}
#endif
