using UnityEngine;

namespace Windrunner.Production.Progression
{
    public sealed class RaceHost : MonoBehaviour
    {
        public ProgressionCatalog catalog;
        public string raceId, regionId;
        public RaceSession Session { get; private set; }
        private ProgressionService owner;
        public bool StartAttempt(ProgressionActor actor)
        {
            if (actor == null || actor.Service == null || catalog == null || !isActiveAndEnabled) return false;
            var definition = catalog.definitions.races.Find(r => r.id == raceId);
            if (definition == null) return false;
            if (owner != null && owner != actor.Service) return false;
            owner = actor.Service; owner.DiscoverChallenge(definition.challengeId);
            if (Session == null) Session = new RaceSession(definition, owner);
            return Session.Start();
        }
        public bool Reach(string checkpoint, ProgressionActor actor) => actor != null && actor.Service == owner &&
            Session != null && Session.Reach(checkpoint, owner.PlayerId, regionId);
        private void Update() => Session?.Tick(Time.deltaTime);
        private void OnDisable() => Session?.Cancel();
    }
}
