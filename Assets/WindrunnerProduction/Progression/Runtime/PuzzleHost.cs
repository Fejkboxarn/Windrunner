using UnityEngine;
using UnityEngine.Events;

namespace Windrunner.Production.Progression
{
    public sealed class PuzzleHost : MonoBehaviour
    {
        public ProgressionActor actor;
        public ProgressionCatalog catalog;
        public string puzzleId, regionId;
        public UnityEvent<bool> solvedStateChanged;
        public PuzzleSession Session { get; private set; }
        private ProgressionService owner;
        private void Start() => EnsureSession();
        private bool EnsureSession()
        {
            if (Session != null) return true;
            if (actor == null || actor.Service == null || catalog == null) return false;
            var definition = catalog.definitions.puzzles.Find(x => x.id == puzzleId);
            if (definition == null) return false;
            owner = actor.Service; owner.DiscoverChallenge(definition.challengeId);
            Session = new PuzzleSession(definition, owner); Session.Changed += Present;
            Session.Begin(); Present(); return true;
        }
        public bool Activate(string mechanism, ProgressionActor source)
        {
            if (!isActiveAndEnabled || !EnsureSession() || source == null || source.Service != owner) return false;
            if (Session.Status == PuzzleStatus.Dormant) Session.Begin();
            return Session.Activate(mechanism, owner.PlayerId, regionId);
        }
        public bool Reach(string destination, ProgressionActor source) => isActiveAndEnabled && EnsureSession() && source != null &&
            source.Service == owner && Session.ReachDestination(destination, owner.PlayerId, regionId);
        private void Present() => solvedStateChanged?.Invoke(Session.Status == PuzzleStatus.Solved);
        private void Update() => Session?.Tick(Time.deltaTime);
        private void OnDisable() => Session?.Leave();
        private void OnDestroy() { if (Session != null) Session.Changed -= Present; }
    }
}
