using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Prototype node presentation:
    /// - punch on hit
    /// - dim/shrink during targeting cooldown
    /// - restore cleanly when available again
    /// </summary>
    public sealed class AirNodePulseFeedback : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float hitScalePunch = 0.34f;
        [SerializeField, Min(0.1f)] private float hitResponse = 12f;

        [Header("Cooldown")]
        [SerializeField, Range(0.25f, 1f)] private float cooldownScale = 0.70f;
        [SerializeField, Range(0.05f, 1f)] private float cooldownBrightness = 0.30f;
        [SerializeField, Min(0.1f)] private float cooldownVisualResponse = 12f;

        private AirNode _node;
        private Renderer _renderer;
        private Material _material;

        private Vector3 _baseScale;
        private Color _baseColor;

        private float _hitPulse;
        private float _cooldownVisual;

        private void Awake()
        {
            _node = GetComponent<AirNode>();
            _renderer = GetComponent<Renderer>();

            _baseScale = transform.localScale;

            if (_renderer != null)
            {
                // Renderer.material gives this node its own runtime instance,
                // so cooling one node does not dim every shared-material node.
                _material = _renderer.material;
                _baseColor = _material.color;
            }
        }

        public void PlayHit(float intensity)
        {
            _hitPulse =
                Mathf.Max(
                    _hitPulse,
                    Mathf.Clamp01(intensity));
        }

        private void Update()
        {
            if (_node == null)
                return;

            float cooldownTarget =
                _node.IsCoolingDown ? 1f : 0f;

            float cooldownT =
                1f -
                Mathf.Exp(
                    -cooldownVisualResponse *
                    Time.deltaTime);

            _cooldownVisual =
                Mathf.Lerp(
                    _cooldownVisual,
                    cooldownTarget,
                    cooldownT);

            UpdateScale();
            UpdateMaterial();

            _hitPulse =
                Mathf.MoveTowards(
                    _hitPulse,
                    0f,
                    hitResponse *
                    Time.deltaTime *
                    0.1f);
        }

        private void UpdateScale()
        {
            float hitPunch =
                Mathf.Sin(_hitPulse * Mathf.PI) *
                hitScalePunch;

            float cooldownMultiplier =
                Mathf.Lerp(
                    1f,
                    cooldownScale,
                    _cooldownVisual);

            transform.localScale =
                _baseScale *
                cooldownMultiplier *
                (1f + hitPunch);
        }

        private void UpdateMaterial()
        {
            if (_material == null)
                return;

            float brightness =
                Mathf.Lerp(
                    1f,
                    cooldownBrightness,
                    _cooldownVisual);

            _material.color =
                new Color(
                    _baseColor.r * brightness,
                    _baseColor.g * brightness,
                    _baseColor.b * brightness,
                    _baseColor.a);
        }

        private void OnDisable()
        {
            transform.localScale = _baseScale;
            _hitPulse = 0f;
            _cooldownVisual = 0f;

            if (_material != null)
                _material.color = _baseColor;
        }

        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
