using UnityEngine;

namespace AirflowPrototype
{
    public sealed class PlayerDualThrowTracerVisual : MonoBehaviour
    {
        private Transform _origin;
        private AirNode _target;
        private Vector3 _fixedTarget;

        private float _delay;
        private float _duration;
        private float _age;

        private TrailRenderer _trail;
        private Material _runtimeMaterial;

        public static void Spawn(
            Transform origin,
            AirNode target,
            float delay,
            float duration,
            float width,
            Color color)
        {
            if (origin == null ||
                target == null)
            {
                return;
            }

            GameObject go =
                new GameObject(
                    "Dual Throw Tracer");

            PlayerDualThrowTracerVisual visual =
                go.AddComponent<PlayerDualThrowTracerVisual>();

            visual.Initialize(
                origin,
                target,
                delay,
                duration,
                width,
                color);
        }

        private void Initialize(
            Transform origin,
            AirNode target,
            float delay,
            float duration,
            float width,
            Color color)
        {
            _origin = origin;
            _target = target;
            _fixedTarget =
                target.TargetPosition;

            _delay =
                Mathf.Max(
                    0f,
                    delay);

            _duration =
                Mathf.Max(
                    0.03f,
                    duration);

            transform.position =
                origin.position;

            _trail =
                gameObject.AddComponent<TrailRenderer>();

            _trail.time = 0.10f;
            _trail.startWidth =
                Mathf.Max(
                    0.001f,
                    width);

            _trail.endWidth =
                Mathf.Max(
                    0.0005f,
                    width * 0.12f);

            _trail.minVertexDistance = 0.01f;
            _trail.numCapVertices = 2;
            _trail.numCornerVertices = 2;

            _trail.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            _trail.receiveShadows = false;

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit");

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Sprites/Default");
            }

            if (shader != null)
            {
                _runtimeMaterial =
                    new Material(
                        shader);

                _runtimeMaterial.color =
                    color;

                _trail.material =
                    _runtimeMaterial;
            }

            _trail.startColor =
                color;

            Color end =
                color;

            end.a = 0f;

            _trail.endColor =
                end;

            _trail.emitting =
                false;
        }

        private void Update()
        {
            float dt =
                Time.unscaledDeltaTime;

            if (_delay > 0f)
            {
                _delay -= dt;

                if (_origin != null)
                {
                    transform.position =
                        _origin.position;
                }

                return;
            }

            if (!_trail.emitting)
                _trail.emitting = true;

            _age += dt;

            float t =
                Mathf.Clamp01(
                    _age /
                    _duration);

            float eased =
                1f -
                Mathf.Pow(
                    1f - t,
                    3f);

            Vector3 start =
                _origin != null
                    ? _origin.position
                    : transform.position;

            Vector3 target =
                _target != null &&
                _target.IsAvailable
                    ? _target.TargetPosition
                    : _fixedTarget;

            if (_age <= dt * 1.5f)
            {
                transform.position =
                    start;
            }

            transform.position =
                Vector3.Lerp(
                    start,
                    target,
                    eased);

            if (t >= 1f)
            {
                _trail.emitting = false;
                enabled = false;

                Destroy(
                    gameObject,
                    Mathf.Max(
                        0.05f,
                        _trail.time));
            }
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(
                    _runtimeMaterial);
            }
        }
    }
}
