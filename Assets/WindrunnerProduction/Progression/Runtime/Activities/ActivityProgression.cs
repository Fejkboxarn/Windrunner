using System;
namespace Windrunner.Production.Progression.Activities
{
    /// <summary>Uses the installed transaction ledger, not a second quest/save authority. Mid-attempt state restarts on load.</summary>
    public sealed class ActivityProgression
    {
        readonly ProgressionService owner;
        readonly string id,region,attempt;
        public ActivityProgression(ProgressionService owner,string challengeId,string regionId)
        {this.owner=owner??throw new ArgumentNullException(nameof(owner));id=challengeId;region=regionId;attempt=Guid.NewGuid().ToString("N");}
        public bool Begin(){owner.DiscoverChallenge(id);return owner.ChallengeAvailable(id);}
        public bool Complete(float elapsed)=>owner.CompleteChallenge(id,attempt,elapsed,region);
        public void Credit(string nodeId,string ticket)
        {
            owner.Observe(new GameplayFact{playerId=owner.PlayerId,occurrenceId="activity/"+id+"/"+attempt+"/"+ticket,definitionId=id+"/"+nodeId,targetId=nodeId,
                regionId=region,kind=FactKind.Interaction,verified=true});
        }
    }
}
