using UnityEngine;
using Windrunner.Production.Progression;
namespace Windrunner.Activities
{
    /// <summary>Only placed in the dedicated demo. Uses its unsaved PlayerSession, not user playthroughs.</summary>
    public sealed class ActivityDemoGuide:MonoBehaviour
    {
        public ProgressionActor actor;
        public WindRelayHost relay;
        public PursuitHost[] pursuits;
        public string[] questIds;
        void Start(){if(actor?.Service!=null)foreach(string id in questIds)actor.Service.Accept(id);}
        void OnGUI()
        {
            if(!actor||actor.Input?.IsGameplayBlocked==true)return;
            GUILayout.BeginArea(new Rect(20,80,370,440),GUI.skin.box);
            GUILayout.Label("DEVELOPMENT ONLY — Relay & Pursuit");
            GUILayout.Label("Look at a START pedestal and press Interact to begin/retry. Original production controls. Lanes left to right: Relay / Ground / Air / Hybrid. Cast at cyan nodes. Ground: touch target to catch; pass ~2–3 m away to provoke reversal. Air: hit with real Air Cast. Yellow means a relay is expiring.");
            if(relay){GUILayout.Label("Relay: "+(relay.Session?.State.ToString()??"Ready"));foreach(var node in relay.nodes)if(node)GUILayout.Label(node.relayId+": "+node.State+" — "+node.SecondsRemaining.ToString("0.0")+" s");if(GUILayout.Button("Begin / retry relay"))relay.Begin();}
            foreach(var h in pursuits)if(h){GUILayout.Label(h.name+": "+(h.Session?.State.ToString()??"Ready"));if(GUILayout.Button("Begin / retry "+h.name))h.Begin();}
            GUILayout.Label("Reload this demonstration scene for a fresh unsaved session. Menus pause attempts. No Zone 01 changes.");GUILayout.EndArea();
        }
    }
}
