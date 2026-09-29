using UnityEngine;
using UnityEngine.UI;

namespace AirflowPrototype
{
    public sealed class AirSlingTargetChargeGraphic : MaskableGraphic
    {
        [SerializeField]
        private AirSlingTargetUISettings settings;

        private float _progress01;
        private float _perfectCenter01 = 0.5f;
        private float _perfectHalfWidth01 = 0.15f;
        private bool _charging;
        private float _rotationDegrees;
        private float _perfectFlash;

        public void Configure(
            AirSlingTargetUISettings newSettings)
        {
            settings =
                newSettings;

            SetVerticesDirty();
        }

        public void SetState(
            float progress01,
            float perfectCenter01,
            float perfectHalfWidth01,
            bool charging,
            float rotationDegrees,
            float perfectFlash)
        {
            _progress01 =
                Mathf.Clamp01(
                    progress01);

            _perfectCenter01 =
                Mathf.Clamp01(
                    perfectCenter01);

            _perfectHalfWidth01 =
                Mathf.Clamp(
                    perfectHalfWidth01,
                    0.001f,
                    0.49f);

            _charging =
                charging;

            _rotationDegrees =
                rotationDegrees;

            _perfectFlash =
                Mathf.Clamp01(
                    perfectFlash);

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(
            VertexHelper vh)
        {
            vh.Clear();

            if (settings == null)
                return;

            Rect rect =
                rectTransform.rect;

            float radius =
                Mathf.Min(
                    rect.width,
                    rect.height) *
                0.5f;

            Color outer =
                Color.Lerp(
                    settings.idleColor,
                    settings.chargeColor,
                    _charging ? 1f : 0f);

            outer =
                Color.Lerp(
                    outer,
                    settings.perfectColor,
                    _perfectFlash);

            DrawShape(
                vh,
                radius *
                    settings.outerRadius01,
                settings.outerThickness,
                outer,
                _rotationDegrees);

            if (_charging)
            {
                DrawPerfectBand(
                    vh,
                    radius);

                DrawCompression(
                    vh,
                    radius);
            }

            if (_perfectFlash > 0.001f)
            {
                float perfectRadius =
                    radius *
                    GetPerfectRadius01();

                float flashRadius =
                    Mathf.Lerp(
                        perfectRadius * 0.55f,
                        perfectRadius * 1.5f,
                        1f -
                        _perfectFlash);

                Color flash =
                    settings.perfectColor;

                flash.a *=
                    _perfectFlash;

                DrawShape(
                    vh,
                    flashRadius,
                    Mathf.Lerp(
                        6f,
                        1.25f,
                        1f -
                        _perfectFlash),
                    flash,
                    -_rotationDegrees *
                    0.35f);
            }
        }

        private void DrawPerfectBand(
            VertexHelper vh,
            float radius)
        {
            float perfectRadius =
                radius *
                GetPerfectRadius01();

            Color color =
                Color.Lerp(
                    settings.perfectBandColor,
                    settings.perfectColor,
                    _perfectFlash);

            DrawShape(
                vh,
                perfectRadius,
                settings.perfectBandThickness +
                _perfectFlash *
                3.5f,
                color,
                -_rotationDegrees *
                0.22f);
        }

        private void DrawCompression(
            VertexHelper vh,
            float radius)
        {
            float radius01 =
                Mathf.Lerp(
                    settings.compressionMaxRadius01,
                    settings.compressionMinRadius01,
                    _progress01);

            float currentRadius =
                radius *
                radius01;

            float perfectRadius01 =
                GetPerfectRadius01();

            float perfectWidth01 =
                Mathf.Max(
                    0.01f,
                    _perfectHalfWidth01 *
                    Mathf.Abs(
                        settings.compressionMaxRadius01 -
                        settings.compressionMinRadius01));

            float distance =
                Mathf.Abs(
                    radius01 -
                    perfectRadius01);

            float glow =
                1f -
                Mathf.Clamp01(
                    distance /
                    perfectWidth01);

            Color color =
                Color.Lerp(
                    settings.chargeColor,
                    settings.perfectColor,
                    glow);

            DrawShape(
                vh,
                currentRadius,
                settings.compressionThickness +
                glow *
                2.5f,
                color,
                _rotationDegrees *
                1.25f);
        }

        private float GetPerfectRadius01()
        {
            return
                Mathf.Lerp(
                    settings.compressionMaxRadius01,
                    settings.compressionMinRadius01,
                    _perfectCenter01);
        }

        private void DrawShape(
            VertexHelper vh,
            float radius,
            float thickness,
            Color color,
            float rotation)
        {
            switch (settings.shape)
            {
                case AirSlingReticleShape.Square:
                    DrawPolygonRing(
                        vh,
                        radius,
                        thickness,
                        4,
                        rotation +
                        settings.shapeAngleOffset,
                        color);
                    break;

                case AirSlingReticleShape.Hexagon:
                    DrawPolygonRing(
                        vh,
                        radius,
                        thickness,
                        6,
                        rotation +
                        settings.shapeAngleOffset,
                        color);
                    break;

                case AirSlingReticleShape.Ring:
                    DrawBrokenRing(
                        vh,
                        radius,
                        thickness,
                        color,
                        rotation +
                        settings.shapeAngleOffset);
                    return;

                default:
                    DrawPolygonRing(
                        vh,
                        radius,
                        thickness,
                        4,
                        rotation +
                        45f +
                        settings.shapeAngleOffset,
                        color);
                    break;
            }

        }

        private void DrawBrokenRing(
            VertexHelper vh,
            float radius,
            float thickness,
            Color color,
            float rotation)
        {
            int count =
                Mathf.Max(
                    2,
                    settings.ringArcCount);

            float spacing =
                360f /
                count;

            for (int i = 0;
                 i < count;
                 i++)
            {
                float center =
                    rotation +
                    i *
                    spacing;

                DrawArc(
                    vh,
                    radius,
                    thickness,
                    center -
                    settings.ringArcDegrees *
                    0.5f,
                    center +
                    settings.ringArcDegrees *
                    0.5f,
                    color,
                    10);
            }
        }

        private static void DrawPolygonRing(
            VertexHelper vh,
            float radius,
            float thickness,
            int sides,
            float rotationDegrees,
            Color color)
        {
            sides =
                Mathf.Max(
                    3,
                    sides);

            float innerRadius =
                Mathf.Max(
                    0f,
                    radius -
                    thickness *
                    0.5f);

            float outerRadius =
                radius +
                thickness *
                0.5f;

            int start =
                vh.currentVertCount;

            for (int i = 0;
                 i < sides;
                 i++)
            {
                float angle =
                    (rotationDegrees +
                     i *
                     (360f / sides)) *
                    Mathf.Deg2Rad;

                Vector2 dir =
                    new Vector2(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle));

                UIVertex inner =
                    UIVertex.simpleVert;

                inner.position =
                    dir *
                    innerRadius;

                inner.color =
                    color;

                UIVertex outer =
                    UIVertex.simpleVert;

                outer.position =
                    dir *
                    outerRadius;

                outer.color =
                    color;

                vh.AddVert(inner);
                vh.AddVert(outer);
            }

            for (int i = 0;
                 i < sides;
                 i++)
            {
                int next =
                    (i + 1) %
                    sides;

                int a =
                    start +
                    i *
                    2;

                int b =
                    a +
                    1;

                int c =
                    start +
                    next *
                    2;

                int d =
                    c +
                    1;

                vh.AddTriangle(
                    a,
                    d,
                    b);

                vh.AddTriangle(
                    a,
                    c,
                    d);
            }
        }

        private static void DrawArc(
            VertexHelper vh,
            float radius,
            float thickness,
            float startDegrees,
            float endDegrees,
            Color color,
            int segments)
        {
            segments =
                Mathf.Max(
                    1,
                    segments);

            float inner =
                Mathf.Max(
                    0f,
                    radius -
                    thickness *
                    0.5f);

            float outer =
                radius +
                thickness *
                0.5f;

            int startIndex =
                vh.currentVertCount;

            for (int i = 0;
                 i <= segments;
                 i++)
            {
                float t =
                    i /
                    (float)segments;

                float angle =
                    Mathf.Lerp(
                        startDegrees,
                        endDegrees,
                        t) *
                    Mathf.Deg2Rad;

                Vector2 direction =
                    new Vector2(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle));

                UIVertex innerVertex =
                    UIVertex.simpleVert;

                innerVertex.position =
                    direction *
                    inner;

                innerVertex.color =
                    color;

                UIVertex outerVertex =
                    UIVertex.simpleVert;

                outerVertex.position =
                    direction *
                    outer;

                outerVertex.color =
                    color;

                vh.AddVert(innerVertex);
                vh.AddVert(outerVertex);
            }

            for (int i = 0;
                 i < segments;
                 i++)
            {
                int a =
                    startIndex +
                    i *
                    2;

                int b = a + 1;
                int c = a + 2;
                int d = a + 3;

                vh.AddTriangle(a, d, b);
                vh.AddTriangle(a, c, d);
            }
        }
    }
}
