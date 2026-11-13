using System;
using UnityEngine;
using Windrunner.Production.Progression;

namespace AirflowPrototype
{
    /// <summary>Credits this explicitly bound player's lethal Sling impact, not arbitrary health changes.</summary>
    public sealed class EnemyProgressionAdapter : MonoBehaviour
    {
        public ProgressionActor actor;
        public AirSlingController sling;
        public AirSlingNode node;
        public AirSlingNodeHealth health;
        public string stableId, enemyDefinitionId, regionId;
        private string lifetimeOccurrence;
        private void Awake() => Respawned();
        private void OnEnable()
        {
            if (sling != null) sling.NodeImpact += Impact;
            if (health != null) health.Respawned += Respawned;
        }
        private void OnDisable()
        {
            if (sling != null) sling.NodeImpact -= Impact;
            if (health != null) health.Respawned -= Respawned;
        }
        private void Respawned() => lifetimeOccurrence = Guid.NewGuid().ToString("N");
        private void Impact(AirSlingNode target)
        {
            if (target != node || health == null || health.IsAlive || actor?.Service == null || string.IsNullOrWhiteSpace(stableId)) return;
            actor.Service.Observe(new GameplayFact { occurrenceId = lifetimeOccurrence, playerId = actor.Service.PlayerId,
                definitionId = enemyDefinitionId, targetId = stableId, regionId = regionId, kind = FactKind.EnemyDefeated, verified = true });
        }
    }
}
