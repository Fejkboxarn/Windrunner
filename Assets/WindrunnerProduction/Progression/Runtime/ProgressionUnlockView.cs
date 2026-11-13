using UnityEngine;

namespace Windrunner.Production.Progression
{
    public sealed class ProgressionUnlockView : MonoBehaviour
    {
        public ProgressionActor actor;
        public string contentId;
        public ProgressionWorldStateView presentation;
        private ProgressionService service;
        private void Start() => Bind();
        private void OnEnable() { if (service != null) { service.Changed += Refresh; Refresh(); } }
        private void Bind()
        {
            if (service != null || actor?.Service == null) return;
            service = actor.Service; service.Changed += Refresh; Refresh();
        }
        private void Refresh() { if (presentation != null) presentation.SetOpen(service.HasUnlock(contentId)); }
        private void OnDisable() { if (service != null) service.Changed -= Refresh; }
    }
}
