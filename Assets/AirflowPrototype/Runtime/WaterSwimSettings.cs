using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "WaterSwimSettings",
        menuName = "Airflow Prototype/Water Swim Settings")]
    public sealed class WaterSwimSettings : ScriptableObject
    {
        [Header("Detection")]
        [Tooltip("How far above the visual water surface the character's feet may be before swimming begins.")]
        [Range(0f, 0.5f)]
        public float enterSurfaceAllowance = 0.08f;

        [Tooltip("How far below the surface we still search before considering the player fully outside this water body.")]
        [Range(1f, 30f)]
        public float maximumDetectionDepth = 12f;

        [Header("Buoyancy")]
        [Tooltip("How far the bottom of the CharacterController naturally sits below the water surface.")]
        [Range(0f, 2f)]
        public float floatDepth = 0.72f;

        [Tooltip("How quickly the character is pulled toward the floating depth.")]
        [Range(0.5f, 20f)]
        public float buoyancyStrength = 7.5f;

        [Tooltip("Maximum upward correction applied by buoyancy.")]
        [Range(0.5f, 20f)]
        public float maximumBuoyancySpeed = 7f;

        [Tooltip("Cancels downward movement produced by the normal movement controller while swimming.")]
        [Range(0f, 1.25f)]
        public float fallingMotionCancellation = 1f;

        [Tooltip("Small upward kick the instant the player first enters water.")]
        [Range(0f, 4f)]
        public float entryLift = 0.55f;

        [Header("Horizontal Water Drag")]
        public bool applyHorizontalDrag = true;

        [Tooltip("1 = normal movement speed. 0.7 means roughly 70% of the normal horizontal displacement while swimming.")]
        [Range(0.25f, 1f)]
        public float horizontalMovementMultiplier = 0.72f;

        [Header("Manual Swim")]
        [Tooltip("Space / gamepad South button adds upward swimming while submerged.")]
        public bool allowSwimUpInput = true;

        [Range(0f, 8f)]
        public float swimUpSpeed = 3.8f;

        [Tooltip("Left Ctrl / gamepad East button pushes slightly downward.")]
        public bool allowSwimDownInput = true;

        [Range(0f, 8f)]
        public float swimDownSpeed = 2.6f;

        [Header("Surface Feel")]
        [Tooltip("Prevents tiny up/down corrections from making the character jitter at the surface.")]
        [Range(0f, 0.25f)]
        public float surfaceDeadZone = 0.045f;

        [Tooltip("How quickly the swimming state is allowed to turn off after losing the water footprint.")]
        [Range(0f, 0.5f)]
        public float exitGraceTime = 0.10f;
    }
}
