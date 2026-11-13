using System;
using UnityEngine;
using Windrunner.Production.Progression;

namespace AirflowPrototype
{
    /// <summary>Explicit player binding; listens only to authoritative hit/traversal callbacks.</summary>
    public sealed class AirflowProgressionAdapter : MonoBehaviour, IProgressionControlSafety
    {
        [Serializable] public sealed class CastTarget
        {
            public AirNode node;
            public ProgressionInteraction interaction;
        }
        public ProgressionActor actor;
        public PlayerMotor motor;
        public AirCaster caster;
        public AirDash dash;
        public AirSlingController sling;
        public CastTarget[] castTargets = Array.Empty<CastTarget>();
        public bool CanStartCinematic => motor != null && caster != null && dash != null && sling != null &&
            motor.IsGrounded && motor.PlanarVelocity.sqrMagnitude < 0.01f && !motor.HasExternalTraversalVelocity &&
            !caster.IsCharging && !dash.IsDashing && !sling.IsCharging && !sling.IsSlinging;
        private void OnEnable() { if (caster != null) caster.PulseHit += CastHit; }
        private void OnDisable() { if (caster != null) caster.PulseHit -= CastHit; }
        private void CastHit(AirNode target, float charge)
        {
            foreach (var binding in castTargets)
                if (binding.node == target && binding.interaction != null)
                    binding.interaction.TryUse(actor, InteractionChannel.AbilityHit);
        }
    }
}
