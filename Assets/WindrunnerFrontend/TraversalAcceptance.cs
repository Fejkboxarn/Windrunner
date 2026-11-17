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
        UnityEngine.Object[] draftModules;
        public void OwnDraft(params UnityEngine.Object[] modules)=>draftModules=modules;
        void OnDestroy(){if(draftModules!=null)foreach(var module in draftModules)if(module)Destroy(module);}
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
            var ledge=GameObject.CreatePrimitive(PrimitiveType.Cube);
            ledge.name="Cast context drop";
            ledge.transform.SetParent(transform,true);
            ledge.transform.position=LedgeStart+Vector3.down*1.15f;
            ledge.transform.localScale=new Vector3(2.1f,2,3);
            // Eight reference rays sit below the feet; they cannot affect motor contacts.
            for (int bearing=0; bearing<360; bearing+=45)
            {
                var marker=GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name="Heading "+bearing;
                var collider=marker.GetComponent<Collider>();
                collider.enabled=false;
                Destroy(collider);
                marker.transform.SetParent(deck.transform.parent,true);
                var rotation=Quaternion.Euler(0,bearing,0);
                marker.transform.SetPositionAndRotation(start+rotation*(Vector3.forward*5)+Vector3.down*.14f,rotation);
                marker.transform.localScale=new Vector3(bearing==0?.25f:.09f,.025f,8);
                var properties=new MaterialPropertyBlock();
                properties.SetColor("_BaseColor",bearing==0?Color.yellow:Color.cyan);
                marker.GetComponent<Renderer>().SetPropertyBlock(properties);
            }
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
        Vector3 LedgeStart => start+new Vector3(-6,2,-2);
        void Home() => ResetAt(start);
        void ThrowLedge() => ResetAt(LedgeStart);
        void ResetAt(Vector3 origin)
        {
            session.character.enabled=false;
            session.character.transform.SetPositionAndRotation(origin,Quaternion.identity);
            session.character.enabled=true;
            session.orbit.ResetAfterTeleport(0);
            Physics.SyncTransforms();
        }
        void Update()
        {
            var keyboard=Keyboard.current;
            if(keyboard==null || WindrunnerInputHost.GameplayPresentationBlocked) return;
            if(keyboard.f5Key.wasPressedThisFrame){if(keyboard.shiftKey.isPressed)ThrowLedge();else Home();}
            if(keyboard.f6Key.wasPressedThisFrame) Targets.gameObject.SetActive(!Targets.gameObject.activeSelf);
            if(keyboard.f7Key.wasPressedThisFrame) session.daggers.power.Spend(session.daggers.power.Max*.25f);
            if(keyboard.f8Key.wasPressedThisFrame)
                for(int i=0;i<4;i++) session.daggers.power.GrantReward(new AirPowerReward(AirPowerRewardSource.AirNode),5);
        }
        void OnGUI()
        {
            if(WindrunnerInputHost.GameplayPresentationBlocked) return;
            GUI.Box(new Rect(12,12,535,167),
                "DEVELOPMENT  ·  Traversal and rewards\nAim forward: hold/release Cast on an Air Node. Sling to the target at right.\n"+
                "Compare an early release with the marked perfect window. Equip Farseer in Arsenal.\n"+
                "F5 return to deck   ·   F6 remove/restore targets   ·   F7 spend 25% AirPower\n"+
                "F8 deliver four test rewards. Use the equipped ability to transfer reserve.\n"+
                "Insert: input + pose. Follow heading rays at 45°, 90°, 180°; reverse and strafe.\n"+
                "Shift+F5: Cast ledge. Release at rest, then strafe right off the edge.\n"+
                "Use partial stick for walking. Open Arsenal to compare grips.\n"+
                "Stop Play and relaunch the acceptance lane for a fresh session.");
        }
    }
}
#endif
