using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "AirRewardGlowSettings",
        menuName = "Airflow Prototype/Air Reward Glow Settings")]
    public sealed class AirRewardGlowSettings : ScriptableObject
    {
        [Header("Timing")]
        [Min(0.03f)]
        public float duration = 0.34f;

        [Range(0.01f, 0.45f)]
        public float attackFraction = 0.16f;

        [Header("Character Glow")]
        [Tooltip("HDR emission multiplier applied over the player's visible mesh.")]
        [Min(0f)]
        public float emissionIntensity = 3.2f;

        [Tooltip("How much the player's base surface color shifts toward the returning-air color.")]
        [Range(0f, 1f)]
        public float surfaceTint = 0.34f;

        [Header("Light Pulse")]
        public bool usePointLight = true;

        [Min(0f)]
        public float lightIntensity = 2.4f;

        [Min(0.1f)]
        public float lightRange = 4.2f;

        [Tooltip("Height above the player root. The installer uses the CharacterController center when possible.")]
        public float lightHeight = 1f;
    }
}
