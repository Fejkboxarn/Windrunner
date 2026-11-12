using System;
using UnityEngine;

namespace AirflowPrototype
{
    public enum PlayerMovementPhase { Locomotion, SlingPath, SlingImpact, SlingHold, Swimming }

    /// <summary>
    /// The only CharacterController.Move boundary for player movement. Requests
    /// execute immediately: never coalesce steps, defer collision callbacks or
    /// reorder motor -> Sling -> swimming. Simulation velocity belongs to the
    /// motor; traversal observation remains explicitly reported by Sling.
    /// </summary>
    public sealed class PlayerDisplacementAuthority
    {
        public CharacterController Controller { get; }
        public PlayerMovementPhase LastPhase { get; private set; }
        public int Frame { get; private set; } = -1;
        public int StepsThisFrame { get; private set; }

        public PlayerDisplacementAuthority(CharacterController controller)
        {
            Controller = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
        }

        public CollisionFlags Apply(Vector3 displacement, PlayerMovementPhase phase, int frame)
        {
            if (Frame != frame) { Frame = frame; StepsThisFrame = 0; }
            LastPhase = phase;
            StepsThisFrame++;
            return Controller.Move(displacement);
        }
    }
}
