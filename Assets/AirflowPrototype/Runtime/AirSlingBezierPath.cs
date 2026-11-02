using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Character-independent sling trajectory: four control points, cubic position,
    /// tangent and arc-length approximation. Owns no MonoBehaviour, scene object or clock.
    /// </summary>
    public sealed class AirSlingBezierPath
    {
        private Vector3 _p0;
        private Vector3 _p1;
        private Vector3 _p2;
        private Vector3 _p3;

        public Vector3 End => _p3;

        public void Build(
            Vector3 startRootPosition,
            Vector3 landingRootPosition,
            Vector3 fallbackForward,
            Vector3 incomingPlanarVelocity,
            bool hasCamera,
            Vector3 cameraRight,
            AirSlingSettings settings)
        {
            _p0 = startRootPosition;
            _p3 = landingRootPosition;

            Vector3 direct = _p3 - _p0;
            Vector3 directDirection = direct.sqrMagnitude > 0.0001f
                ? direct.normalized
                : fallbackForward;

            Vector3 directPlanar = directDirection;
            directPlanar.y = 0f;

            if (directPlanar.sqrMagnitude < 0.0001f)
            {
                directPlanar = fallbackForward;
                directPlanar.y = 0f;
            }

            directPlanar.Normalize();

            Vector3 incoming = incomingPlanarVelocity;
            incoming.y = 0f;
            if (incoming.sqrMagnitude < 0.25f)
                incoming = directPlanar;
            else
                incoming.Normalize();

            Vector3 startDirection = directPlanar + incoming * settings.startMomentumInfluence;
            if (startDirection.sqrMagnitude < 0.0001f)
                startDirection = directPlanar;
            startDirection.Normalize();

            Vector3 side = hasCamera
                ? cameraRight
                : Vector3.Cross(Vector3.up, directPlanar);
            side.y = 0f;
            if (side.sqrMagnitude < 0.0001f)
                side = Vector3.Cross(Vector3.up, directPlanar);
            side.Normalize();

            float sideDot = Vector3.Dot(incoming, side);
            float sideSign = Mathf.Abs(sideDot) > 0.08f ? Mathf.Sign(sideDot) : 1f;
            Vector3 lateral = side * sideSign * settings.bezierLateralOffset;

            _p1 = _p0 + startDirection * settings.startTangentDistance +
                  Vector3.up * settings.bezierVerticalLift + lateral;
            _p2 = _p3 - directPlanar * settings.approachTangentDistance +
                  Vector3.up * (settings.bezierVerticalLift * 0.55f) + lateral * 0.55f;
        }

        public Vector3 Evaluate(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return u * u * u * _p0 +
                   3f * u * u * t * _p1 +
                   3f * u * t * t * _p2 +
                   t * t * t * _p3;
        }

        public Vector3 EvaluateTangent(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            Vector3 tangent = 3f * u * u * (_p1 - _p0) +
                              6f * u * t * (_p2 - _p1) +
                              3f * t * t * (_p3 - _p2);
            return tangent;
        }

        public float EstimateLength(int samples)
        {
            samples = Mathf.Max(2, samples);
            float length = 0f;
            Vector3 previous = Evaluate(0f);
            for (int i = 1; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector3 point = Evaluate(t);
                length += Vector3.Distance(previous, point);
                previous = point;
            }

            return Mathf.Max(0.01f, length);
        }

        public void Clear()
        {
            _p0 = Vector3.zero;
            _p1 = Vector3.zero;
            _p2 = Vector3.zero;
            _p3 = Vector3.zero;
        }
    }
}
