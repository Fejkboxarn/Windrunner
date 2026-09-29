using System;
using UnityEngine;

namespace AirflowPrototype
{
    [DisallowMultipleComponent]
    public sealed class AirSlingNodeHealth : MonoBehaviour
    {
        private enum LifeState
        {
            Alive,
            PlayingDeath,
            WaitingToRespawn
        }

        [Header("References")]
        [SerializeField] private AirSlingNode node;
        [SerializeField] private AirSlingNodeVisualAnimator visualAnimator;

        [Header("Health")]
        [Min(1)]
        [SerializeField] private int maxHealth = 4;

        [Header("Death / Respawn")]
        [Tooltip(
            "How long the dead bear/node stays visible so the death animation can play.")]
        [Min(0f)]
        [SerializeField] private float deathDespawnDelay = 1.35f;

        [Tooltip(
            "Time after despawning before the node becomes alive again.")]
        [Min(0f)]
        [SerializeField] private float respawnDelay = 5f;

        [Tooltip(
            "Optional scene Transform used as the respawn location. " +
            "If empty, the node respawns at its original scene position.")]
        [SerializeField] private Transform respawnPoint;

        private LifeState _state =
            LifeState.Alive;

        private int _currentHealth;
        private float _stateTimer;

        private Vector3 _initialPosition;
        private Quaternion _initialRotation;

        public int CurrentHealth =>
            _currentHealth;

        public int MaxHealth =>
            maxHealth;

        public bool IsAlive =>
            _state ==
            LifeState.Alive;

        public event Action<int, int> HealthChanged;
        public event Action<int> Damaged;
        public event Action Died;
        public event Action Respawned;

        public void Configure(
            AirSlingNode newNode,
            AirSlingNodeVisualAnimator newVisualAnimator)
        {
            if (newNode != null)
                node = newNode;

            if (newVisualAnimator != null)
            {
                visualAnimator =
                    newVisualAnimator;
            }
        }

        private void Awake()
        {
            if (node == null)
                node = GetComponent<AirSlingNode>();

            if (visualAnimator == null)
            {
                visualAnimator =
                    GetComponent<AirSlingNodeVisualAnimator>();
            }

            _initialPosition =
                transform.position;

            _initialRotation =
                transform.rotation;

            _currentHealth =
                Mathf.Max(
                    1,
                    maxHealth);

            _state =
                LifeState.Alive;

            if (visualAnimator != null)
            {
                visualAnimator
                    .ResetForRespawn();
            }
        }

        private void Update()
        {
            if (_state ==
                LifeState.Alive)
            {
                return;
            }

            _stateTimer -=
                Time.unscaledDeltaTime;

            if (_stateTimer > 0f)
                return;

            if (_state ==
                LifeState.PlayingDeath)
            {
                DespawnAfterDeath();
                return;
            }

            if (_state ==
                LifeState.WaitingToRespawn)
            {
                Respawn();
            }
        }

        public bool TakeDamage(
            int damage)
        {
            if (!IsAlive ||
                damage <= 0)
            {
                return false;
            }

            _currentHealth =
                Mathf.Max(
                    0,
                    _currentHealth -
                    damage);

            Damaged?.Invoke(
                damage);

            HealthChanged?.Invoke(
                _currentHealth,
                maxHealth);

            if (_currentHealth <= 0)
            {
                BeginDeath();
            }
            else if (visualAnimator != null)
            {
                visualAnimator
                    .PlayHurt();
            }

            return true;
        }

        private void BeginDeath()
        {
            _state =
                LifeState.PlayingDeath;

            _stateTimer =
                deathDespawnDelay;

            if (node != null)
            {
                node.SetSelected(
                    false);
            }

            if (visualAnimator != null)
            {
                visualAnimator
                    .PlayDie();
            }

            Died?.Invoke();

            if (_stateTimer <= 0f)
                DespawnAfterDeath();
        }

        private void DespawnAfterDeath()
        {
            if (visualAnimator != null)
            {
                visualAnimator.SetVisible(
                    false);
            }

            _state =
                LifeState.WaitingToRespawn;

            _stateTimer =
                respawnDelay;

            if (_stateTimer <= 0f)
                Respawn();
        }

        private void Respawn()
        {
            Vector3 position =
                respawnPoint != null
                    ? respawnPoint.position
                    : _initialPosition;

            Quaternion rotation =
                respawnPoint != null
                    ? respawnPoint.rotation
                    : _initialRotation;

            transform.SetPositionAndRotation(
                position,
                rotation);

            _currentHealth =
                Mathf.Max(
                    1,
                    maxHealth);

            _state =
                LifeState.Alive;

            _stateTimer = 0f;

            if (visualAnimator != null)
            {
                visualAnimator
                    .ResetForRespawn();
            }

            HealthChanged?.Invoke(
                _currentHealth,
                maxHealth);

            Respawned?.Invoke();
        }

        private void OnValidate()
        {
            maxHealth =
                Mathf.Max(
                    1,
                    maxHealth);

            deathDespawnDelay =
                Mathf.Max(
                    0f,
                    deathDespawnDelay);

            respawnDelay =
                Mathf.Max(
                    0f,
                    respawnDelay);
        }
    }
}
