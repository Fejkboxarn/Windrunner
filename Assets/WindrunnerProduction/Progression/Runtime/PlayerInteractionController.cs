using UnityEngine;

namespace Windrunner.Production.Progression
{
    public sealed class PlayerInteractionController : MonoBehaviour
    {
        public ProgressionActor actor;
        public Camera view;
        public LayerMask interactionLayers = ~0;
        public float reach = 4;
        public ProgressionInteraction Target { get; private set; }
        public string Prompt => Target != null && actor?.Input != null ? actor.Input.ProgressionBindingLabel("Interact") + " — " + Target.prompt : string.Empty;
        private void Update()
        {
            Target = null;
            if (actor == null || actor.Input == null || actor.Input.IsGameplayBlocked || view == null) return;
            if (Physics.Raycast(view.transform.position, view.transform.forward, out var hit, reach, interactionLayers, QueryTriggerInteraction.Collide))
            {
                var interaction = hit.collider.GetComponent<ProgressionInteraction>();
                if (interaction != null && interaction.CanUse(actor, InteractionChannel.Input)) Target = interaction;
            }
            if (Target != null && actor.Input.ProgressionPressed("Interact")) Target.TryUse(actor, InteractionChannel.Input);
        }
    }
}
