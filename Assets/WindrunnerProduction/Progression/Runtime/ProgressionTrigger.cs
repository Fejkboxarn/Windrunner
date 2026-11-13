using UnityEngine;

namespace Windrunner.Production.Progression
{
    [RequireComponent(typeof(Collider))]
    public sealed class ProgressionTrigger : MonoBehaviour
    {
        public ProgressionActor actor;
        public RaceHost race;
        public string checkpointId;
        public ProgressionInteraction interaction;
        public PuzzleHost puzzleArea;
        private int contacts;
        private bool BelongsToActor(Collider other) => actor != null && (other.transform == actor.transform || other.transform.IsChildOf(actor.transform));
        private void OnTriggerEnter(Collider other)
        {
            if (!BelongsToActor(other)) return;
            contacts++;
            if (contacts != 1) return;
            race?.Reach(checkpointId, actor);
            interaction?.TryUse(actor, InteractionChannel.Traversal);
        }
        private void OnTriggerExit(Collider other)
        {
            if (!BelongsToActor(other)) return;
            contacts = Mathf.Max(0, contacts - 1);
            if (contacts == 0) puzzleArea?.Session?.Leave();
        }
        private void OnDisable() { contacts = 0; puzzleArea?.Session?.Leave(); }
    }
}
