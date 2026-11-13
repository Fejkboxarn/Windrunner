using UnityEngine;

namespace Windrunner.Production.Progression
{
    /// <summary>Restores collected/discovered world presentation from the same persistent fact ledger.</summary>
    public sealed class ProgressionFactView : MonoBehaviour
    {
        public ProgressionActor actor;
        public FactKind kind = FactKind.ItemCollected;
        public string stableObjectId;
        public GameObject presentation;
        public bool activeWhenRecorded;
        private ProgressionService service;
        private void Start()
        {
            if (actor?.Service == null) return;
            service = actor.Service; service.Changed += Refresh; Refresh();
        }
        private void OnEnable() { if (service != null) { service.Changed += Refresh; Refresh(); } }
        private void OnDisable() { if (service != null) service.Changed -= Refresh; }
        private void Refresh()
        {
            // Keep the observer outside the object it hides, so it retains its lifecycle subscription.
            if (presentation == null || transform.IsChildOf(presentation.transform)) return;
            presentation.SetActive(service.HasPersistentFact(kind, stableObjectId) == activeWhenRecorded);
        }
    }
}
