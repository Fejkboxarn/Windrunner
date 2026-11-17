using UnityEngine;

namespace Windrunner.Production.Progression
{
    public sealed class RelationshipContact : ProgressionInteraction
    {
        public ProgressionActor owner;
        public ProgressionCatalog catalog;
        public string relationshipId;
        public TimedRetrieval retrieval;
        public ProgressionCinematicPlayer cinematics;
        [TextArea] public string greeting = "Speak with me when you are ready.";
        private RelationshipDefinition relationship;
        private ChapterDefinition current;
        private bool pending;
        private string indicator;
        private ProgressionService service;

        void Start()
        {
            relationship = catalog.definitions.relationships.Find(r => r.id == relationshipId);
            service = owner.Service;
            if (relationship == null || service == null) { enabled = false; return; }
            service.Changed += Refresh;
            Refresh();
        }
        void OnDestroy() { if (service != null) service.Changed -= Refresh; }
        void Refresh()
        {
            var snapshot = service.Snapshot();
            current = null; pending = false;
            foreach (var chapter in relationship.chapters)
            {
                pending = snapshot.cutscenes.Exists(c => c.id == chapter.cutsceneId && c.status == CinematicStatus.Pending);
                if (pending || service.Status(chapter.questId) != QuestStatus.Completed)
                { current = chapter; break; }
            }
            var status = current == null ? QuestStatus.Completed : service.Status(current.questId);
            string symbol = pending ? "◆ " : status == QuestStatus.Available ? "! " : status == QuestStatus.ReadyToTurnIn ? "✓ " : "";
            indicator = symbol + relationship.displayName;
        }
        void OnGUI()
        {
            if (string.IsNullOrEmpty(indicator) || !cinematics || !cinematics.gameplayCamera || owner.Input == null ||
                owner.Input.IsGameplayBlocked || (owner.Position-transform.position).sqrMagnitude > 28*28) return;
            var point = cinematics.gameplayCamera.WorldToScreenPoint(transform.position+Vector3.up*1.7f);
            if (point.z>0) GUI.Box(new Rect(point.x-90,Screen.height-point.y-13,180,26),indicator);
        }
        public override string RejectionReason(ProgressionActor actor, InteractionChannel channel)
        {
            string reason = base.RejectionReason(actor,channel);
            if (reason != null || service == null || actor != owner) return reason ?? "Unavailable.";
            prompt = relationship.displayName + " — " + greeting;
            if (current == null) return null;
            var quest = catalog.definitions.quests.Find(q => q.id == current.questId);
            var status = service.Status(quest.id);
            if (pending) prompt = relationship.displayName + " — View rank milestone (stand still)";
            else if (status == QuestStatus.Available) prompt = "Accept: " + quest.displayName;
            else if (status == QuestStatus.ReadyToTurnIn) prompt = "Turn in: " + quest.displayName;
            else if (retrieval && retrieval.Carrying && retrieval.questId == quest.id) prompt = "Deliver " + retrieval.itemModel.name;
            else prompt = relationship.displayName + " — " + quest.description;
            return null;
        }
        public override bool TryUse(ProgressionActor actor, InteractionChannel channel, string occurrence = null)
        {
            if (!CanUse(actor,channel)) return false;
            if (current == null) return true;
            var chapter = current;
            if (pending) return cinematics.Play(chapter.cutsceneId);
            if (service.Status(chapter.questId) == QuestStatus.Available)
            {
                if (!service.Accept(chapter.questId)) return false;
                service.Track(chapter.questId);
                return true;
            }
            if (retrieval && retrieval.Carrying && retrieval.questId == chapter.questId && !retrieval.Deliver(actor,this)) return false;
            if (service.Status(chapter.questId) != QuestStatus.ReadyToTurnIn) return true;
            if (!service.TurnIn(chapter.questId)) return false;
            cinematics.Play(chapter.cutsceneId);
            return true;
        }
        void OnDrawGizmosSelected() { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position,range); }
    }
}
