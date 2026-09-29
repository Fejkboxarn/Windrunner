using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "PlayerDustSettings",
        menuName = "Airflow Prototype/Player Dust Settings")]
    public sealed class PlayerDustSettings : ScriptableObject
    {
        [Header("Movement Dust")]
        [Min(0f)] public float movementDustStartSpeed = 2.2f;
        [Min(0.02f)] public float walkEmissionInterval = 0.24f;
        [Min(0.02f)] public float sprintEmissionInterval = 0.075f;
        [Range(1, 10)] public int movementBurstCount = 2;
        [Min(0.01f)] public float movementParticleSize = 0.18f;
        [Min(0f)] public float movementParticleSpeed = 0.85f;

        [Header("Landing Dust")]
        [Range(1, 40)] public int landingMinCount = 5;
        [Range(1, 60)] public int landingMaxCount = 18;
        [Min(0.01f)] public float landingParticleSize = 0.28f;
        [Min(0f)] public float landingParticleSpeed = 2.1f;

        [Header("Particle Life")]
        [Min(0.05f)] public float lifetime = 0.62f;
        public float gravityModifier = -0.08f;
        [Range(0f, 5f)] public float drag = 1.15f;
        [Min(0f)] public float groundOffset = 0.06f;
        [Min(0f)] public float spawnRadius = 0.32f;

        [Header("Look")]
        public Color dustColor = new Color(0.72f, 0.69f, 0.62f, 0.48f);
    }
}
