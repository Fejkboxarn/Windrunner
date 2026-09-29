using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Curved return-stream visual. Gameplay reward timing still uses the same
    /// AirFlowHitSettings.returnStreamDuration, so presentation and reward land together.
    /// </summary>
    public sealed class AirReturnStreamVisual : MonoBehaviour
    {
        private Transform _destination;
        private Vector3 _start;
        private Vector3 _curveSideDirection;
        private float _duration;
        private float _age;

        private AirPresentationSettings _presentation;

        private TrailRenderer _trail;
        private Material _runtimeMaterial;

        public static void Spawn(
            Vector3 start,
            Transform destination,
            AirFlowHitSettings flowSettings,
            AirPresentationSettings presentationSettings)
        {
            if (destination == null ||
                flowSettings == null)
            {
                return;
            }

            GameObject go =
                new GameObject(
                    "Air Return Stream");

            go.transform.position = start;

            AirReturnStreamVisual visual =
                go.AddComponent<AirReturnStreamVisual>();

            visual.Initialize(
                start,
                destination,
                flowSettings,
                presentationSettings);
        }

        private void Initialize(
            Vector3 start,
            Transform destination,
            AirFlowHitSettings flowSettings,
            AirPresentationSettings presentationSettings)
        {
            _start = start;
            _destination = destination;
            _presentation = presentationSettings;

            _duration =
                Mathf.Max(
                    0.03f,
                    flowSettings.returnStreamDuration);

            Vector3 toPlayer =
                destination.position -
                start;

            _curveSideDirection =
                Vector3.Cross(
                    toPlayer.normalized,
                    Vector3.up);

            if (_curveSideDirection.sqrMagnitude <
                0.001f)
            {
                _curveSideDirection =
                    Vector3.right;
            }

            _curveSideDirection.Normalize();

            _trail =
                gameObject.AddComponent<TrailRenderer>();

            _trail.time =
                flowSettings.returnTrailTime;

            _trail.startWidth =
                flowSettings.returnTrailStartWidth;

            _trail.endWidth =
                flowSettings.returnTrailEndWidth;

            _trail.minVertexDistance = 0.015f;
            _trail.numCapVertices = 3;
            _trail.numCornerVertices = 4;

            _trail.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            _trail.receiveShadows = false;

            Shader shader =
                Shader.Find("Sprites/Default");

            if (shader == null)
                shader =
                    Shader.Find(
                        "Universal Render Pipeline/Unlit");

            if (shader == null)
                return;

            _runtimeMaterial =
                new Material(shader);

            Color bright =
                presentationSettings != null
                    ? presentationSettings.brightAirColor
                    : new Color(
                        0.86f,
                        0.98f,
                        1f,
                        1f);

            _runtimeMaterial.color =
                bright;

            _trail.material =
                _runtimeMaterial;

            _trail.startColor =
                bright;

            _trail.endColor =
                new Color(
                    bright.r,
                    bright.g,
                    bright.b,
                    0.03f);
        }

        private void Update()
        {
            if (_destination == null)
            {
                Finish();
                return;
            }

            _age +=
                Time.deltaTime;

            float rawT =
                Mathf.Clamp01(
                    _age /
                    _duration);

            float easePower =
                _presentation != null
                    ? _presentation.returnEasePower
                    : 2.4f;

            float t =
                1f -
                Mathf.Pow(
                    1f - rawT,
                    easePower);

            Vector3 destination =
                _destination.position +
                Vector3.up *
                1.0f;

            Vector3 midpoint =
                (_start + destination) *
                0.5f;

            if (_presentation != null)
            {
                midpoint +=
                    _curveSideDirection *
                    _presentation.returnCurveSide;

                midpoint +=
                    Vector3.up *
                    _presentation.returnCurveUp;
            }

            transform.position =
                QuadraticBezier(
                    _start,
                    midpoint,
                    destination,
                    t);

            if (rawT >= 1f)
                Finish();
        }

        private static Vector3 QuadraticBezier(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            float t)
        {
            float u =
                1f - t;

            return
                u * u * a +
                2f * u * t * b +
                t * t * c;
        }

        private void Finish()
        {
            enabled = false;

            if (_trail != null)
            {
                _trail.emitting = false;

                Destroy(
                    gameObject,
                    Mathf.Max(
                        0.05f,
                        _trail.time));
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
    }
}
