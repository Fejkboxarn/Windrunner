using System;
using System.Collections;
using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// On Sling Node death, spawns the same return-stream visual used by a
    /// normal Air Node and restores Air Power when it reaches the player.
    /// This deliberately does NOT apply the normal node movement boost.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AirSlingDeathReward : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AirSlingNode node;
        [SerializeField] private AirSlingNodeHealth health;
        [SerializeField] private AirPower airPower;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private AirFlowHitSettings flowSettings;
        [SerializeField] private AirPresentationSettings presentationSettings;

        [Header("Reward")]
        [Tooltip(
            "1 = restore exactly the same Air Power amount as a full normal Air Node reward.")]
        [Min(0f)]
        [SerializeField] private float airPowerRewardMultiplier = 1f;

        private Coroutine _deliveryRoutine;
        private bool _subscribed;

        public event Action RewardStarted;
        public event Action RewardDelivered;

        public void Configure(
            AirSlingNode newNode,
            AirSlingNodeHealth newHealth,
            AirPower newAirPower,
            Transform newPlayerTransform,
            AirFlowHitSettings newFlowSettings,
            AirPresentationSettings newPresentationSettings)
        {
            Unsubscribe();

            if (newNode != null)
                node = newNode;

            if (newHealth != null)
                health = newHealth;

            if (newAirPower != null)
                airPower = newAirPower;

            if (newPlayerTransform != null)
            {
                playerTransform =
                    newPlayerTransform;
            }

            if (newFlowSettings != null)
            {
                flowSettings =
                    newFlowSettings;
            }

            if (newPresentationSettings != null)
            {
                presentationSettings =
                    newPresentationSettings;
            }

            if (isActiveAndEnabled)
                Subscribe();
        }

        private void Awake()
        {
            ResolveLocalReferences();
        }

        private void OnEnable()
        {
            ResolveLocalReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();

            if (_deliveryRoutine != null)
            {
                StopCoroutine(
                    _deliveryRoutine);

                _deliveryRoutine = null;
            }
        }

        private void ResolveLocalReferences()
        {
            if (node == null)
                node = GetComponent<AirSlingNode>();

            if (health == null)
                health = GetComponent<AirSlingNodeHealth>();

            if (airPower == null)
            {
                airPower =
                    FindAnyObjectByType<AirPower>();
            }

            if (playerTransform == null &&
                airPower != null)
            {
                playerTransform =
                    airPower.transform;
            }
        }

        private void Subscribe()
        {
            if (_subscribed ||
                health == null)
            {
                return;
            }

            health.Died +=
                OnDied;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed ||
                health == null)
            {
                return;
            }

            health.Died -=
                OnDied;

            _subscribed = false;
        }

        private void OnDied()
        {
            if (node == null ||
                playerTransform == null ||
                airPower == null ||
                flowSettings == null)
            {
                return;
            }

            if (_deliveryRoutine != null)
            {
                StopCoroutine(
                    _deliveryRoutine);
            }

            Vector3 rewardOrigin =
                node.TargetPosition;

            AirReturnStreamVisual.Spawn(
                rewardOrigin,
                playerTransform,
                flowSettings,
                presentationSettings);

            RewardStarted?.Invoke();

            _deliveryRoutine =
                StartCoroutine(
                    DeliverAirPowerAfterDelay());
        }

        private IEnumerator DeliverAirPowerAfterDelay()
        {
            float delay =
                Mathf.Max(
                    0.03f,
                    flowSettings.returnStreamDuration);

            yield return
                new WaitForSeconds(
                    delay);

            _deliveryRoutine = null;

            if (airPower == null ||
                flowSettings == null)
            {
                yield break;
            }

            float amount =
                flowSettings.airPowerRestore *
                Mathf.Max(
                    0f,
                    airPowerRewardMultiplier);

            airPower.Restore(
                amount);

            RewardDelivered?.Invoke();
        }

        private void OnValidate()
        {
            airPowerRewardMultiplier =
                Mathf.Max(
                    0f,
                    airPowerRewardMultiplier);
        }
    }
}
