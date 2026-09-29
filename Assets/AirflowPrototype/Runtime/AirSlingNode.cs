using System.Collections.Generic;
using UnityEngine;

namespace AirflowPrototype
{
    public sealed class AirSlingNode : MonoBehaviour
    {
        private static readonly List<AirSlingNode> ActiveNodes =
            new List<AirSlingNode>();

        [Header("Targeting")]
        [SerializeField] private Transform targetPoint;

        [Header("Landing")]
        [Tooltip(
            "Where the CharacterController's feet should land. " +
            "If empty, the node root plus Fallback Top Offset is used.")]
        [SerializeField] private Transform landingPoint;

        [Min(0f)]
        [SerializeField] private float fallbackTopOffset = 1.0f;

        [Header("Impact VFX")]
        [SerializeField]
        private AirSlingImpactVFX impactVFX;

        [Header("Enemy / Visual")]
        [SerializeField]
        private AirSlingNodeHealth health;

        [SerializeField]
        private AirSlingNodeVisualAnimator visualAnimator;

        [Header("Impact Audio")]
        [Tooltip(
            "0 = only the shared blood splash sound. " +
            "1+ = also play the matching sound from the Sling Impact Audio Library.")]
        [Min(0)]
        [SerializeField]
        private int impactAudioId;

        [Header("Debug")]
        [SerializeField, Min(0.05f)]
        private float gizmoRadius = 0.65f;

        private bool _selected;
        private bool _interactionActive;

        public static IReadOnlyList<AirSlingNode> All =>
            ActiveNodes;

        public Vector3 TargetPosition =>
            targetPoint != null
                ? targetPoint.position
                : transform.position;

        public Vector3 LandingPosition =>
            landingPoint != null
                ? landingPoint.position
                : transform.position +
                  Vector3.up *
                  fallbackTopOffset;

        public bool IsSelected =>
            _selected;

        public bool InteractionActive =>
            _interactionActive;

        public bool IsAvailable =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            (health == null ||
             health.IsAlive);

        public int ImpactAudioId =>
            impactAudioId;

        public AirSlingNodeHealth Health =>
            health;

        public void SetHealth(
            AirSlingNodeHealth newHealth)
        {
            health =
                newHealth;
        }

        public void SetVisualAnimator(
            AirSlingNodeVisualAnimator newVisualAnimator)
        {
            visualAnimator =
                newVisualAnimator;
        }

        public void PlayEngagedAnimation()
        {
            ResolveEnemyReferences();

            if (visualAnimator != null &&
                IsAvailable)
            {
                visualAnimator.PlayWalk();
            }
        }

        public void PlayIdleAnimation()
        {
            ResolveEnemyReferences();

            if (visualAnimator != null &&
                IsAvailable)
            {
                visualAnimator.PlayIdle();
            }
        }

        public bool ApplyImpactDamage(
            int damage)
        {
            ResolveEnemyReferences();

            return
                health != null &&
                health.TakeDamage(
                    damage);
        }

        public void SetLandingPoint(
            Transform newLandingPoint)
        {
            landingPoint =
                newLandingPoint;
        }

        public void SetImpactVFX(
            AirSlingImpactVFX newImpactVFX)
        {
            impactVFX =
                newImpactVFX;
        }

        public void SetImpactAudioId(
            int newImpactAudioId)
        {
            impactAudioId =
                Mathf.Max(
                    0,
                    newImpactAudioId);
        }

        public void PlayImpactAudio(
            AirSlingImpactAudioLibrary audioLibrary)
        {
            if (audioLibrary == null)
                return;

            audioLibrary.PlayImpact(
                LandingPosition,
                impactAudioId);
        }

        public void PlayImpactVFX()
        {
            if (impactVFX == null)
            {
                impactVFX =
                    GetComponentInChildren<AirSlingImpactVFX>(
                        true);
            }

            if (impactVFX != null)
            {
                impactVFX.transform.position =
                    LandingPosition;

                impactVFX.PlayImpact();
            }
        }

        public void SetInteractionActive(
            bool active)
        {
            _interactionActive =
                active &&
                IsAvailable;
        }

        public void SetSelected(
            bool selected)
        {
            _selected =
                selected &&
                IsAvailable;
        }

        private void OnEnable()
        {
            ResolveEnemyReferences();

            if (!ActiveNodes.Contains(this))
                ActiveNodes.Add(this);
        }

        private void ResolveEnemyReferences()
        {
            if (health == null)
            {
                health =
                    GetComponent<AirSlingNodeHealth>();
            }

            if (visualAnimator == null)
            {
                visualAnimator =
                    GetComponent<AirSlingNodeVisualAnimator>();
            }
        }

        private void OnDisable()
        {
            ActiveNodes.Remove(this);
            _selected = false;
            _interactionActive = false;
        }

        private void OnDestroy()
        {
            ActiveNodes.Remove(this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color =
                _selected
                    ? new Color(
                        1f,
                        0.78f,
                        0.58f,
                        1f)
                    : new Color(
                        1f,
                        0.18f,
                        0.10f,
                        0.82f);

            Gizmos.DrawWireSphere(
                TargetPosition,
                gizmoRadius);

            Gizmos.color =
                new Color(
                    1f,
                    0.82f,
                    0.15f,
                    0.95f);

            Vector3 landing =
                LandingPosition;

            Gizmos.DrawWireSphere(
                landing,
                gizmoRadius * 0.45f);

            Gizmos.DrawLine(
                landing +
                Vector3.left *
                gizmoRadius,
                landing +
                Vector3.right *
                gizmoRadius);

            Gizmos.DrawLine(
                landing +
                Vector3.forward *
                gizmoRadius,
                landing +
                Vector3.back *
                gizmoRadius);
        }
    }
}
