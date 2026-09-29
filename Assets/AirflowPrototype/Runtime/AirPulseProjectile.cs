using UnityEngine;

namespace AirflowPrototype
{
    public sealed class AirPulseProjectile : MonoBehaviour
    {
        private AirCaster _owner;
        private AirNode _target;
        private AirCastSettings _settings;
        private Vector3 _direction;
        private float _charge01;
        private float _age;
        private bool _resolved;
        private TrailRenderer _trail;
        private Material _runtimeMaterial;

        public static void Spawn(
            AirCaster owner,
            AirNode target,
            Vector3 origin,
            Vector3 direction,
            float charge01,
            bool ready,
            AirCastSettings settings)
        {
            GameObject pulse = new GameObject("Air Pulse");
            pulse.transform.position = origin;

            AirPulseProjectile projectile =
                pulse.AddComponent<AirPulseProjectile>();

            projectile.Initialize(
                owner,
                target,
                direction,
                charge01,
                ready,
                settings);
        }

        private void Initialize(
            AirCaster owner,
            AirNode target,
            Vector3 direction,
            float charge01,
            bool ready,
            AirCastSettings settings)
        {
            _owner = owner;
            _target = target;
            _direction = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : transform.forward;
            _charge01 = charge01;
            _settings = settings;

            _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time = settings.trailLifetime;
            _trail.startWidth =
                settings.trailStartWidth * Mathf.Lerp(0.75f, 1.15f, charge01);
            _trail.endWidth = settings.trailEndWidth;
            _trail.minVertexDistance = 0.025f;
            _trail.numCapVertices = 3;
            _trail.numCornerVertices = 2;
            _trail.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            _runtimeMaterial = new Material(shader);
            _runtimeMaterial.color = ready
                ? new Color(0.84f, 0.97f, 1f, 1f)
                : new Color(0.60f, 0.82f, 0.92f, 1f);

            _trail.material = _runtimeMaterial;
            _trail.startColor = _runtimeMaterial.color;
            _trail.endColor =
                new Color(
                    _runtimeMaterial.color.r,
                    _runtimeMaterial.color.g,
                    _runtimeMaterial.color.b,
                    0.08f);
        }

        private void Update()
        {
            if (_resolved || _settings == null)
                return;

            float dt = Time.deltaTime;
            _age += dt;

            Vector3 previous = transform.position;
            Vector3 next =
                previous + _direction * (_settings.pulseSpeed * dt);

            transform.position = next;

            if (_target != null && _target.IsAvailable)
            {
                Vector3 targetPosition = _target.TargetPosition;

                float distanceNow =
                    Vector3.Distance(transform.position, targetPosition);

                float segmentDistance =
                    DistancePointToSegment(
                        targetPosition,
                        previous,
                        next);

                if (distanceNow <= _settings.nodeHitRadius ||
                    segmentDistance <= _settings.nodeHitRadius)
                {
                    ResolveHit();
                    return;
                }
            }

            if (_age >= _settings.pulseLifetime)
                ResolveMiss();
        }

        private void ResolveHit()
        {
            if (_resolved)
                return;

            _resolved = true;

            if (_owner != null)
                _owner.NotifyPulseHit(_target, _charge01);

            FinishVisual();
        }

        private void ResolveMiss()
        {
            if (_resolved)
                return;

            _resolved = true;

            if (_owner != null)
                _owner.NotifyPulseMiss();

            FinishVisual();
        }

        private void FinishVisual()
        {
            if (_trail != null)
            {
                _trail.emitting = false;
                Destroy(gameObject, Mathf.Max(0.05f, _trail.time));
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);
        }

        private static float DistancePointToSegment(
            Vector3 point,
            Vector3 a,
            Vector3 b)
        {
            Vector3 ab = b - a;
            float lengthSq = ab.sqrMagnitude;

            if (lengthSq <= 0.000001f)
                return Vector3.Distance(point, a);

            float t = Vector3.Dot(point - a, ab) / lengthSq;
            t = Mathf.Clamp01(t);

            Vector3 closest = a + ab * t;
            return Vector3.Distance(point, closest);
        }
    }
}
