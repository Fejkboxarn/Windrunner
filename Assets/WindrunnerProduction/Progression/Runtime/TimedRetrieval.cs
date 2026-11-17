using System.Linq;
using UnityEngine;

namespace Windrunner.Production.Progression
{
    // Scene-local attempts intentionally have no persistent carried state.
    public sealed class TimedRetrieval : ProgressionInteraction
    {
        public ProgressionActor owner;
        public ProgressionCatalog catalog;
        public string questId, objectiveId;
        public ProgressionInteraction destination;
        public GameObject itemModel;
        [Min(0)] public float respawnSeconds = 3;
        private readonly TimedCarryAttempt attempt = new TimedCarryAttempt();
        public bool Carrying => attempt.Carrying;
        public double Remaining => attempt.Remaining(Time.timeAsDouble);
        private double noticeUntil;
        private string notice;
        private ObjectiveDefinition objective;
        private ProgressionService service;
        private bool questActive;
        void Start()
        {
            objective = catalog?.definitions.quests.Find(q => q.id == questId)?.groups
                .SelectMany(g => g.objectives).FirstOrDefault(o => o.id == objectiveId);
            if (objective == null || objective.maximumSeconds <= 0 || !destination || !itemModel || !owner || owner.Service == null)
            { Debug.LogError("Incomplete timed retrieval bindings.", this); enabled = false; return; }
            service = owner.Service;
            service.Changed += Refresh;
            Refresh();
        }
        void Refresh() => questActive = service.ObjectiveOpen(questId, objectiveId);
        void OnDestroy() { if (service != null) service.Changed -= Refresh; }
        public override string RejectionReason(ProgressionActor actor, InteractionChannel channel)
        {
            string reason = base.RejectionReason(actor, channel);
            if (reason != null) return reason;
            if (owner != actor || objective == null || !questActive) return "No active retrieval.";
            if (!attempt.Available(Time.timeAsDouble)) return "Object unavailable.";
            return null;
        }
        public override bool TryUse(ProgressionActor actor, InteractionChannel channel, string occurrence = null)
        {
            if (!CanUse(actor,channel) || !attempt.Collect(Time.timeAsDouble,objective.maximumSeconds)) return false;
            itemModel.SetActive(false);
            return true;
        }
        public bool Deliver(ProgressionActor actor, ProgressionInteraction receiver)
        {
            CheckExpiry();
            if (!Carrying || actor != owner || receiver != destination || !receiver ||
                !receiver.CanUse(actor, InteractionChannel.Input) || !questActive) return false;
            if (!actor.Service.Observe(new GameplayFact { verified = true, playerId = actor.Service.PlayerId,
                occurrenceId = "delivery/" + questId + "/" + stableId, definitionId = objective.definitionId,
                regionId = objective.regionId, targetId = stableId, kind = objective.kind,
                seconds = (float)(objective.maximumSeconds - Remaining) })) return false;
            attempt.Delivered();
            return true;
        }
        void CheckExpiry()
        {
            if (!attempt.Expire(Time.timeAsDouble,respawnSeconds)) return;
            notice = "The object expired. Retrieve another at its source.";
            noticeUntil = Time.unscaledTimeAsDouble + 5;
        }
        void Update()
        {
            CheckExpiry();
            bool visible = attempt.Available(Time.timeAsDouble) && questActive;
            if (visible && allowTraversal && (owner.Position-transform.position).sqrMagnitude <= range*range)
                TryUse(owner,InteractionChannel.Traversal);
            else if (itemModel.activeSelf != visible) itemModel.SetActive(visible);
        }
        void OnGUI()
        {
            if (Carrying || Time.unscaledTimeAsDouble < noticeUntil)
                GUI.Box(new Rect(Screen.width / 2 - 210, 22, 420, 34), Carrying ?
                    $"Carrying {itemModel.name}   ·   Return in {Mathf.CeilToInt((float)Remaining)} s" : notice);
        }
    }
}
