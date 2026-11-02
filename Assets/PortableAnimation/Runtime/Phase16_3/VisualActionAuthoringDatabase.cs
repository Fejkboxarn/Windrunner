using System;
using System.Collections.Generic;
using UnityEngine;

namespace PortableAnimation.Authoring
{
    /// <summary>
    /// ScriptableObject authoring database. Filename matches the class name
    /// to satisfy Unity's MonoScript asset binding.
    /// </summary>
    [CreateAssetMenu(menuName = "Portable Animation/Phase 16.3/Visual Action Authoring Database", fileName = "VisualActionAuthoringDatabase")]
    public class VisualActionAuthoringDatabase : ScriptableObject
    {
        public GameObject previewCharacterPrefab;
        public List<VisualActionEntry> actions = new List<VisualActionEntry>();
        // All world-space look settings are stored on the existing authoring asset.
        // No extra dependency profile or generated scene is required.
        public WorldLookAimSettings worldLook = new WorldLookAimSettings();
        public List<MovementLookAimRule> movementLookRules = new List<MovementLookAimRule>();

        public bool WorldLookAllowed(AuthoringMovementContext movement)
        {
            if (movementLookRules != null)
                foreach (var rule in movementLookRules)
                    if (rule != null && rule.movementContext == movement)
                        return !rule.suppressWorldLookAim;
            // Incapacitation is disabled by default; states are freely overridable.
            return movement != AuthoringMovementContext.Dead &&
                   movement != AuthoringMovementContext.KnockedDown &&
                   movement != AuthoringMovementContext.Stunned;
        }

        public void SetWorldLookAllowed(AuthoringMovementContext movement, bool enabled)
        {
            if (movementLookRules == null)
                movementLookRules = new List<MovementLookAimRule>();
            foreach (var rule in movementLookRules)
            {
                if (rule == null || rule.movementContext != movement) continue;
                rule.suppressWorldLookAim = !enabled;
                return;
            }
            movementLookRules.Add(new MovementLookAimRule
            {
                movementContext = movement,
                suppressWorldLookAim = !enabled
            });
        }


        public VisualActionEntry FindOrCreate(string actionId)
        {
            foreach (var entry in actions)
            {
                if (entry != null && string.Equals(entry.actionId, actionId, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            var created = new VisualActionEntry { actionId = actionId };
            actions.Add(created);
            return created;
        }
    }
}
