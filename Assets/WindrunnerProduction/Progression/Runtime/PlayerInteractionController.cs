using UnityEngine;

namespace Windrunner.Production.Progression
{
    [DefaultExecutionOrder(10000)]
    public sealed class PlayerInteractionController : MonoBehaviour
    {
        public ProgressionActor actor;
        public Camera view;
        public LayerMask interactionLayers = ~0;
        public float reach = 4;
        public ProgressionInteraction Target { get; private set; }
        public ProgressionInteraction Candidate { get; private set; }
        public string Rejection { get; private set; }
        public float TargetDistance => Candidate != null && actor != null ? Vector3.Distance(actor.Position, Candidate.transform.position) : 0;
        private readonly RaycastHit[] hits = new RaycastHit[64];
        public string Prompt => Target != null && actor?.Input != null ? actor.Input.ProgressionBindingLabel("Interact") + " — " + Target.prompt : string.Empty;
        private void LateUpdate()
        {
            Probe();
            if (Target != null && actor.Input.ProgressionPressed("Interact") && !Target.TryUse(actor, InteractionChannel.Input))
                Rejection = "No new progress: this action is already recorded or its objective is not active.";
        }
        public void Probe()
        {
            Target = null; Candidate = null; Rejection = "Aim the center reticle at a station.";
            if (actor == null || actor.Input == null || actor.Input.IsGameplayBlocked || view == null) return;
            int count = Physics.RaycastNonAlloc(view.transform.position, view.transform.forward, hits, reach, interactionLayers, QueryTriggerInteraction.Collide);
            float nearest = float.PositiveInfinity; Collider selected = null;
            // Ignore only this actor and non-interactive volume triggers; solid walls still occlude.
            if (count == hits.Length) { Rejection = "Target query is saturated; narrow interaction layers."; return; }
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (collider.transform.IsChildOf(actor.transform)) continue;
                var interaction = collider.GetComponentInParent<ProgressionInteraction>();
                if (collider.isTrigger && interaction == null) continue;
                if (hits[i].distance >= nearest) continue;
                nearest = hits[i].distance; selected = collider; Candidate = interaction;
            }
            if (Candidate == null) { if (selected != null) Rejection = "View blocked by " + selected.name; return; }
            Rejection = Candidate.RejectionReason(actor, InteractionChannel.Input);
            if (string.IsNullOrEmpty(Rejection)) Target = Candidate;
        }
    }
}
