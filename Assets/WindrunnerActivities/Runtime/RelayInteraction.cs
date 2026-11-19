using System;
using AirflowPrototype;
using UnityEngine;
using UnityEngine.Events;
using Windrunner.Production.Progression;
using Windrunner.Production.Progression.Activities;
namespace Windrunner.Activities
{
    public sealed class RelayInteraction:ProgressionInteraction
    {
        public WindRelayHost relay;
        public string relayId;
        public AirNode castNode;
        public AirSlingNode slingNode;
        public Renderer visual;
        public AudioSource audioSource;
        public AudioClip availableClip,activatedClip,expiredClip;
        public UnityEvent<int> visualStateChanged;
        public RelayState State {get;private set;}=RelayState.Inactive;
        public float SecondsRemaining {get;private set;}
        MaterialPropertyBlock block;
        public override string RejectionReason(ProgressionActor source,InteractionChannel channel)
        {
            if(!isActiveAndEnabled||!relay||relay.actor!=source||source==null||source.Input?.IsGameplayBlocked==true)return "Relay unavailable.";
            if(channel!=InteractionChannel.Input)return "Use the bound gameplay callback.";
            if((source.Position-transform.position).sqrMagnitude>range*range)return "Move closer.";
            if(State!=RelayState.Available&&State!=RelayState.Expiring)return "Relay not available.";
            return null;
        }
        public override bool TryUse(ProgressionActor source,InteractionChannel channel,string occurrence=null)
            =>RejectionReason(source,channel)==null&&relay.Activate(this,ActivationMethod.Interact,occurrence??Guid.NewGuid().ToString("N"));
        public bool ActivateExternal(string occurrence)=>relay&&relay.Activate(this,ActivationMethod.External,occurrence);
        public void Present(RelayState value,float remaining)
        {
            SecondsRemaining=remaining;if(State==value)return;State=value;
            if(visual)
            {
                block??=new MaterialPropertyBlock();visual.GetPropertyBlock(block);
                Color color=value==RelayState.Completed||value==RelayState.Activated?Color.green:value==RelayState.Available?Color.cyan:value==RelayState.Expiring?Color.yellow:value==RelayState.Expired?Color.red:Color.gray;
                block.SetColor("_BaseColor",color);block.SetColor("_Color",color);visual.SetPropertyBlock(block);
            }
            var clip=value==RelayState.Available?availableClip:value==RelayState.Activated||value==RelayState.Completed?activatedClip:value==RelayState.Expired?expiredClip:null;
            if(clip&&audioSource)audioSource.PlayOneShot(clip);
            visualStateChanged?.Invoke((int)value);
        }
    }
}
