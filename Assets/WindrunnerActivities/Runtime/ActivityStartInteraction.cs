using UnityEngine;
using Windrunner.Production.Progression;
namespace Windrunner.Activities
{
    public sealed class ActivityStartInteraction:ProgressionInteraction
    {
        public WindRelayHost relay;
        public PursuitHost pursuit;
        public override bool TryUse(ProgressionActor actor,InteractionChannel channel,string occurrence=null)
        {
            if(channel!=InteractionChannel.Input||!CanUse(actor,channel))return false;
            if(relay&&relay.actor==actor)return relay.Begin();
            if(pursuit&&pursuit.actor==actor)return pursuit.Begin();return false;
        }
    }
}
