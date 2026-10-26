using UnityEngine;
using UnityEngine.Rendering;

namespace AirflowPrototype
{
    public sealed class PlayerKnifeFlickProjectileVisual : MonoBehaviour
    {
        private AirNode _target;
        private Vector3 _fixedTargetPosition;
        private Vector3 _originPosition;

        private float _duration;
        private float _age;
        private float _spinDegreesPerSecond;

        private TrailRenderer _trail;
        private Material _runtimeMaterial;

        public static void Spawn(
            Vector3 originPosition,
            Quaternion originRotation,
            AirNode target,
            Vector3 fixedTargetPosition,
            GameObject knifeVisualPrefab,
            float duration,
            float trailWidth,
            Color trailColor,
            float spinDegreesPerSecond)
        {
            GameObject root =
                new GameObject(
                    "Procedural Knife Flick Visual");

            root.transform.SetPositionAndRotation(
                originPosition,
                originRotation);

            PlayerKnifeFlickProjectileVisual visual =
                root.AddComponent<PlayerKnifeFlickProjectileVisual>();

            visual.Initialize(
                target,
                fixedTargetPosition,
                knifeVisualPrefab,
                duration,
                trailWidth,
                trailColor,
                spinDegreesPerSecond);
        }

        private void Initialize(
            AirNode target,
            Vector3 fixedTargetPosition,
            GameObject knifeVisualPrefab,
            float duration,
            float trailWidth,
            Color trailColor,
            float spinDegreesPerSecond)
        {
            _target = target;
            _fixedTargetPosition =
                fixedTargetPosition;

            _originPosition =
                transform.position;

            _duration =
                Mathf.Max(
                    0.03f,
                    duration);

            _spinDegreesPerSecond =
                Mathf.Max(
                    0f,
                    spinDegreesPerSecond);

            if (knifeVisualPrefab != null)
            {
                GameObject knife =
                    Instantiate(
                        knifeVisualPrefab,
                        transform);

                knife.name =
                    "Thrown Flick Knife";

                knife.transform.localPosition =
                    Vector3.zero;

                knife.transform.localRotation =
                    Quaternion.identity;

                knife.transform.localScale =
                    Vector3.one;
            }

            _trail =
                gameObject.AddComponent<TrailRenderer>();

            _trail.time = 0.11f;
            _trail.startWidth =
                Mathf.Max(
                    0.001f,
                    trailWidth);

            _trail.endWidth =
                Mathf.Max(
                    0.0005f,
                    trailWidth * 0.1f);

            _trail.minVertexDistance = 0.01f;
            _trail.numCapVertices = 2;
            _trail.numCornerVertices = 2;

            _trail.shadowCastingMode =
                ShadowCastingMode.Off;

            _trail.receiveShadows =
                false;

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
                    trailColor;

                _trail.sharedMaterial =
                    _runtimeMaterial;
            }

            _trail.startColor =
                trailColor;

            Color end =
                trailColor;

            end.a = 0f;

            _trail.endColor =
                end;
        }

        private void Update()
        {
            float dt =
                Time.unscaledDeltaTime;

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

            Vector3 targetPosition =
                _target != null &&
                _target.IsAvailable
                    ? _target.TargetPosition
                    : _fixedTargetPosition;

            transform.position =
                Vector3.Lerp(
                    _originPosition,
                    targetPosition,
                    eased);

            Vector3 travel =
                targetPosition -
                transform.position;

            if (travel.sqrMagnitude >
                0.0001f)
            {
                Quaternion facing =
                    Quaternion.LookRotation(
                        travel.normalized,
                        Vector3.up);

                Quaternion spin =
                    Quaternion.AngleAxis(
                        _spinDegreesPerSecond *
                        _age,
                        Vector3.forward);

                transform.rotation =
                    facing *
                    spin;
            }

            if (t < 1f)
                return;

            _trail.emitting = false;
            enabled = false;

            Destroy(
                gameObject,
                Mathf.Max(
                    0.05f,
                    _trail.time));
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
