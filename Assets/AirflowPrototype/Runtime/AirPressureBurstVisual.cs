using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Short-lived procedural pressure rings used for release, node impact,
    /// and reward arrival. No prefab or VFX Graph dependency.
    /// </summary>
    public sealed class AirPressureBurstVisual : MonoBehaviour
    {
        public enum BurstKind
        {
            Release,
            NodeImpact,
            RewardArrival
        }

        private const int Segments = 36;

        private LineRenderer[] _rings;
        private Material _material;

        private Vector3 _forward;
        private float _startRadius;
        private float _endRadius;
        private float _duration;
        private float _width;
        private float _age;

        public static void Spawn(
            Vector3 position,
            Vector3 forward,
            BurstKind kind,
            AirPresentationSettings settings)
        {
            if (settings == null)
                return;

            GameObject go =
                new GameObject($"Air Pressure Burst - {kind}");

            go.transform.position = position;

            AirPressureBurstVisual visual =
                go.AddComponent<AirPressureBurstVisual>();

            visual.Initialize(
                forward,
                kind,
                settings);
        }

        private void Initialize(
            Vector3 forward,
            BurstKind kind,
            AirPresentationSettings settings)
        {
            _forward =
                forward.sqrMagnitude > 0.0001f
                    ? forward.normalized
                    : Vector3.forward;

            int count;

            switch (kind)
            {
                case BurstKind.NodeImpact:
                    count = settings.nodeImpactRingCount;
                    _startRadius = settings.nodeImpactStartRadius;
                    _endRadius = settings.nodeImpactEndRadius;
                    _duration = settings.nodeImpactDuration;
                    _width = settings.nodeImpactRingWidth;
                    break;

                case BurstKind.RewardArrival:
                    count = settings.rewardRingCount;
                    _startRadius = settings.rewardStartRadius;
                    _endRadius = settings.rewardEndRadius;
                    _duration = settings.rewardDuration;
                    _width = settings.rewardRingWidth;
                    break;

                default:
                    count = settings.releaseRingCount;
                    _startRadius = settings.releaseStartRadius;
                    _endRadius = settings.releaseEndRadius;
                    _duration = settings.releaseDuration;
                    _width = settings.releaseRingWidth;
                    break;
            }

            Shader shader =
                Shader.Find("Sprites/Default");

            if (shader == null)
                shader =
                    Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
            {
                Destroy(gameObject);
                return;
            }

            _material = new Material(shader);
            _material.color = settings.brightAirColor;

            _rings =
                new LineRenderer[
                    Mathf.Max(1, count)];

            for (int i = 0; i < _rings.Length; i++)
            {
                GameObject child =
                    new GameObject($"Pressure Ring {i + 1}");

                child.transform.SetParent(
                    transform,
                    false);

                LineRenderer line =
                    child.AddComponent<LineRenderer>();

                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = Segments;
                line.material = _material;
                line.startWidth = _width;
                line.endWidth = _width;
                line.numCornerVertices = 2;
                line.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;

                _rings[i] = line;
            }

            transform.rotation =
                Quaternion.LookRotation(
                    _forward,
                    Vector3.up);
        }

        private void Update()
        {
            if (_rings == null ||
                _rings.Length == 0)
            {
                Destroy(gameObject);
                return;
            }

            _age += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    _age /
                    Mathf.Max(0.03f, _duration));

            for (int i = 0; i < _rings.Length; i++)
            {
                float stagger =
                    i /
                    (float)Mathf.Max(
                        1,
                        _rings.Length);

                float localT =
                    Mathf.Clamp01(
                        (t - stagger * 0.28f) /
                        0.72f);

                float eased =
                    1f -
                    Mathf.Pow(
                        1f - localT,
                        2.3f);

                float radius =
                    Mathf.Lerp(
                        _startRadius,
                        _endRadius,
                        eased);

                float alpha =
                    (1f - localT) *
                    0.9f;

                LineRenderer line =
                    _rings[i];

                Color c =
                    new Color(
                        0.88f,
                        0.98f,
                        1f,
                        alpha);

                line.startColor = c;
                line.endColor = c;

                DrawRing(
                    line,
                    radius);
            }

            if (t >= 1f)
                Destroy(gameObject);
        }

        private static void DrawRing(
            LineRenderer line,
            float radius)
        {
            for (int i = 0; i < Segments; i++)
            {
                float angle =
                    (i /
                     (float)Segments) *
                    Mathf.PI *
                    2f;

                line.SetPosition(
                    i,
                    new Vector3(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius,
                        0f));
            }
        }

        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
