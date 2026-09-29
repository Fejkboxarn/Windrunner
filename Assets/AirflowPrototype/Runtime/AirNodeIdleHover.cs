using UnityEngine;

namespace AirflowPrototype
{
    public sealed class AirNodeIdleHover : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AirNode node;
        [SerializeField] private AirTargeter targeter;

        [Tooltip(
            "Only this visual transform moves. " +
            "Do not assign the gameplay/root AirNode transform if it contains targeting colliders.")]
        [SerializeField] private Transform visualRoot;

        [Header("Hover")]
        [Min(0f)]
        [SerializeField] private float hoverHeight = 0.18f;

        [Min(0f)]
        [SerializeField] private float hoverSpeed = 1.4f;

        [Header("Rotation")]
        [SerializeField] private bool rotateWhileIdle = true;

        [SerializeField] private float rotationSpeed = 12f;

        [Header("Target Transition")]
        [Tooltip(
            "How quickly the node settles when targeted " +
            "and resumes hovering afterward.")]
        [Min(0.1f)]
        [SerializeField] private float response = 8f;

        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;

        private float _hoverWeight = 1f;
        private float _phase;
        private float _idleRotation;

        private void Awake()
        {
            if (node == null)
                node = GetComponentInParent<AirNode>();

            if (targeter == null)
                targeter = FindAnyObjectByType<AirTargeter>();

            if (visualRoot == null)
                visualRoot = transform;

            _baseLocalPosition =
                visualRoot.localPosition;

            _baseLocalRotation =
                visualRoot.localRotation;

            // Gives each node a different hover phase without relying
            // on Unity instance/entity IDs.
            Vector3 worldPosition =
                transform.position;

            float phaseSeed =
                worldPosition.x * 0.173f +
                worldPosition.y * 0.317f +
                worldPosition.z * 0.491f;

            _phase =
                Mathf.Repeat(
                    phaseSeed,
                    Mathf.PI * 2f);
        }

        private void LateUpdate()
        {
            if (visualRoot == null)
                return;

            bool targeted =
                node != null &&
                targeter != null &&
                targeter.CurrentTarget == node;

            float targetWeight =
                targeted
                    ? 0f
                    : 1f;

            _hoverWeight =
                Mathf.Lerp(
                    _hoverWeight,
                    targetWeight,
                    1f -
                    Mathf.Exp(
                        -response *
                        Time.deltaTime));

            float wave =
                Mathf.Sin(
                    Time.time *
                    hoverSpeed *
                    Mathf.PI *
                    2f +
                    _phase);

            float verticalOffset =
                wave *
                hoverHeight *
                _hoverWeight;

            visualRoot.localPosition =
                _baseLocalPosition +
                Vector3.up *
                verticalOffset;

            if (rotateWhileIdle)
            {
                _idleRotation +=
                    rotationSpeed *
                    _hoverWeight *
                    Time.deltaTime;

                visualRoot.localRotation =
                    _baseLocalRotation *
                    Quaternion.Euler(
                        0f,
                        _idleRotation,
                        0f);
            }
            else
            {
                visualRoot.localRotation =
                    Quaternion.Slerp(
                        visualRoot.localRotation,
                        _baseLocalRotation,
                        1f -
                        Mathf.Exp(
                            -response *
                            Time.deltaTime));
            }
        }

        private void OnDisable()
        {
            if (visualRoot == null)
                return;

            visualRoot.localPosition =
                _baseLocalPosition;

            visualRoot.localRotation =
                _baseLocalRotation;
        }
    }
}