#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using AirflowPrototype;
using UnityEngine;
using UnityEngine.InputSystem;
using Windrunner.Production.Input;

namespace Windrunner.Frontend
{
    // Installed explicitly after direct Play Mode startup. Never part of a player build.
    public sealed class TraversalAcceptance : MonoBehaviour
    {
        PlayerSession session;
        Vector3 start;
        public Transform Targets { get; private set; }
        public static TraversalAcceptance Install(PlayerSession session)
        {
            if (!session || session.gameObject.scene.path != PlayerSession.WorldScene || session.SlotId != null ||
                session.Busy || session.Progression == null) throw new InvalidOperationException("Open StartingWorld directly in Play Mode.");
            if (session.TryGetComponent<TraversalAcceptance>(out var existing)) return existing;
            var lane=session.gameObject.AddComponent<TraversalAcceptance>();
            lane.session=session;
            session.UseTestDirectory(Path.Combine(Application.temporaryCachePath,"Acceptance",Guid.NewGuid().ToString("N")));
            lane.Build();
            return lane;
        }
        void Build()
        {
            start=session.arrival.position+Vector3.up*100;
            var deck=GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name="Acceptance deck";
            deck.transform.SetParent(transform,true);
            deck.transform.position=start+new Vector3(0,-.65f,9);
            deck.transform.localScale=new Vector3(46,1,64);
            Targets=new GameObject("Acceptance targets").transform;
            Targets.SetParent(transform,false);
            var scene=gameObject.scene;
            var air=AirNode.All.First(n=>n.gameObject.scene==scene && n.name=="Air Node 01");
            var sling=AirSlingNode.All.First(n=>n.gameObject.scene==scene && n.Health && n.Health.IsAlive);
            for(int i=0;i<3;i++)
                Place(air.gameObject,start+new Vector3((i-1)*6,2,11+Mathf.Abs(i-1)*3));
            Place(sling.gameObject,start+new Vector3(10,1,10));
            Home();
        }
        void Place(GameObject source,Vector3 position)
        {
            var node=Instantiate(source,position,source.transform.rotation,Targets);
            node.name="Acceptance "+source.name;
            if(node.TryGetComponent<AirSlingNodeWander>(out var wander)) wander.enabled=false;
        }
        void Home()
        {
            session.character.enabled=false;
            session.character.transform.SetPositionAndRotation(start,Quaternion.identity);
            session.character.enabled=true;
            session.orbit.ResetAfterTeleport(0);
            Physics.SyncTransforms();
        }
        void Update()
        {
            var keyboard=Keyboard.current;
            if(keyboard==null || WindrunnerInputHost.GameplayPresentationBlocked) return;
            if(keyboard.f5Key.wasPressedThisFrame) Home();
            if(keyboard.f6Key.wasPressedThisFrame) Targets.gameObject.SetActive(!Targets.gameObject.activeSelf);
            if(keyboard.f7Key.wasPressedThisFrame) session.daggers.power.Spend(session.daggers.power.Max*.25f);
            if(keyboard.f8Key.wasPressedThisFrame)
                for(int i=0;i<4;i++) session.daggers.power.GrantReward(new AirPowerReward(AirPowerRewardSource.AirNode),5);
        }
        void OnGUI()
        {
            if(WindrunnerInputHost.GameplayPresentationBlocked) return;
            GUI.Box(new Rect(12,12,530,112),
                "DEVELOPMENT  ·  Traversal and rewards\nAim forward: hold/release Cast on an Air Node. Sling to the target at right.\n"+
                "Compare an early release with the marked perfect window. Equip Farseer in Arsenal.\n"+
                "F5 return to deck   ·   F6 remove/restore targets   ·   F7 spend 25% AirPower\n"+
                "F8 deliver four test rewards. Use the equipped ability to transfer reserve.\n"+
                "Stop Play and relaunch the acceptance lane for a fresh session.");
        }
    }
}
#endif
