using System.Collections.Generic;
using UnityEngine;

namespace AirflowPrototype
{
    public sealed class AirNode : MonoBehaviour
    {
        private static readonly List<AirNode> ActiveNodes =
            new List<AirNode>();

        [SerializeField] private Transform targetPoint;
        [SerializeField, Min(0.05f)] private float gizmoRadius = 0.45f;

        [Header("Route Reward")]
        [Tooltip("Scales Air Power, planar boost and aerial lift from this node. 1 = normal reward.")]
        [SerializeField, Min(0.1f)] private float rewardMultiplier = 1f;

        private bool _selected;
        private float _cooldownRemaining;
        private float _cooldownDuration;

        public static IReadOnlyList<AirNode> All => ActiveNodes;

        public Vector3 TargetPosition =>
            targetPoint != null
                ? targetPoint.position
                : transform.position;

        public bool IsSelected => _selected;
        public bool IsCoolingDown => _cooldownRemaining > 0f;

        public float RewardMultiplier =>
            rewardMultiplier > 0f
                ? rewardMultiplier
                : 1f;

        public float CooldownRemaining =>
            Mathf.Max(0f, _cooldownRemaining);

        public float Cooldown01 =>
            _cooldownDuration > 0f
                ? Mathf.Clamp01(
                    _cooldownRemaining /
                    _cooldownDuration)
                : 0f;

        public bool IsAvailable =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            _cooldownRemaining <= 0f;

        public void SetSelected(bool selected)
        {
            _selected =
                selected &&
                IsAvailable;
        }

        public void SetRewardMultiplier(float multiplier)
        {
            rewardMultiplier =
                Mathf.Max(
                    0.1f,
                    multiplier);
        }

        public void BeginCooldown(float duration)
        {
            float safeDuration =
                Mathf.Max(
                    0f,
                    duration);

            _cooldownDuration =
                Mathf.Max(
                    _cooldownDuration,
                    safeDuration);

            _cooldownRemaining =
                Mathf.Max(
                    _cooldownRemaining,
                    safeDuration);

            _selected = false;
        }

        private void Update()
        {
            if (_cooldownRemaining <= 0f)
                return;

            _cooldownRemaining =
                Mathf.Max(
                    0f,
                    _cooldownRemaining -
                    Time.deltaTime);

            if (_cooldownRemaining <= 0f)
                _cooldownDuration = 0f;
        }

        private void OnEnable()
        {
            if (!ActiveNodes.Contains(this))
                ActiveNodes.Add(this);
        }

        private void OnDisable()
        {
            ActiveNodes.Remove(this);

            _selected = false;
            _cooldownRemaining = 0f;
            _cooldownDuration = 0f;
        }

        private void OnDestroy()
        {
            ActiveNodes.Remove(this);
        }

        private void OnValidate()
        {
            rewardMultiplier =
                Mathf.Max(
                    0.1f,
                    rewardMultiplier);
        }

        private void OnDrawGizmos()
        {
            if (IsCoolingDown)
            {
                Gizmos.color =
                    new Color(
                        0.28f,
                        0.34f,
                        0.38f,
                        0.45f);
            }
            else if (_selected)
            {
                Gizmos.color =
                    new Color(
                        1f,
                        1f,
                        1f,
                        0.95f);
            }
            else
            {
                float reward01 =
                    Mathf.InverseLerp(
                        0.75f,
                        1.5f,
                        RewardMultiplier);

                Gizmos.color =
                    Color.Lerp(
                        new Color(
                            0.45f,
                            0.8f,
                            1f,
                            0.55f),
                        new Color(
                            1f,
                            0.78f,
                            0.35f,
                            0.75f),
                        reward01);
            }

            Gizmos.DrawWireSphere(
                TargetPosition,
                gizmoRadius);
        }
    }
}
