using UnityEngine;
using UnityEngine.UI;

namespace AirflowPrototype
{
    /// <summary>
    /// Air-themed target/timing reticle.
    ///
    /// Important readability rule:
    /// the timing landmarks are visible BEFORE charging begins.
    /// Charging only moves the compression ring through those landmarks.
    /// </summary>
    public sealed class AirTargetChargeGraphic : MaskableGraphic
    {
        [SerializeField] private AirTargetUISettings settings;

        private float _lock01;
        private float _perfectCenter01 = 0.5f;
        private float _perfectHalfWidth01 = 0.12f;
        private bool _charging;
        private float _rotationDegrees;
        private float _perfectFlash;

        public void Configure(
            AirTargetUISettings newSettings)
        {
            settings = newSettings;
            SetVerticesDirty();
        }

        public void SetState(
            float lock01,
            float perfectCenter01,
            float perfectHalfWidth01,
            bool charging,
            float rotationDegrees,
            float perfectFlash)
        {
            _lock01 =
                Mathf.Clamp01(lock01);

            _perfectCenter01 =
                Mathf.Clamp01(perfectCenter01);

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

            Color outerColor =
                Color.Lerp(
                    settings.idleColor,
                    settings.chargeColor,
                    _charging
                        ? 1f
                        : 0f);

            outerColor =
                Color.Lerp(
                    outerColor,
                    settings.perfectColor,
                    _perfectFlash);

            // 1) Persistent target silhouette.
            DrawBrokenOuterRing(
                vh,
                radius * 0.82f,
                settings.outerArcThickness,
                outerColor);

            // 2) Persistent timing information.
            // These stay visible before the player starts charging.
            DrawTimingTrack(
                vh,
                radius);

            DrawPerfectWindow(
                vh,
                radius);

            DrawFullLockCore(
                vh,
                radius);

            // 3) Moving charge/compression indicator.
            if (_charging)
            {
                DrawCompressionRing(
                    vh,
                    radius);
            }
            else
            {
                DrawStartGhost(
                    vh,
                    radius);
            }

            // 4) Perfect confirmation flash.
            if (_perfectFlash > 0.001f)
            {
                float sweetRadius01 =
                    GetSweetSpotRadius01();

                float flashRadius =
                    radius *
                    Mathf.Lerp(
                        sweetRadius01 * 0.45f,
                        sweetRadius01 * 1.55f,
                        1f - _perfectFlash);

                Color flash =
                    settings.perfectColor;

                flash.a *=
                    _perfectFlash;

                DrawFullRing(
                    vh,
                    flashRadius,
                    Mathf.Lerp(
                        6f,
                        1.2f,
                        1f - _perfectFlash),
                    flash,
                    40);
            }
        }

        private void DrawTimingTrack(
            VertexHelper vh,
            float radius)
        {
            float startRadius =
                radius *
                settings.compressionMaxRadius01;

            float endRadius =
                radius *
                settings.compressionMinRadius01;

            Color track =
                settings.idleColor;

            track.a *=
                _charging
                    ? 0.17f
                    : 0.12f;

            // Four subtle "air rails" make the direction of compression readable
            // without turning the reticle into a conventional progress bar.
            for (int i = 0;
                 i < 4;
                 i++)
            {
                float angle =
                    45f +
                    i * 90f;

                DrawRadialSpoke(
                    vh,
                    angle,
                    endRadius,
                    startRadius,
                    1.0f,
                    track);
            }
        }

        private void DrawPerfectWindow(
            VertexHelper vh,
            float radius)
        {
            float sweetRadius01 =
                GetSweetSpotRadius01();

            float sweetRadius =
                radius *
                sweetRadius01;

            float radiusRange =
                Mathf.Abs(
                    settings.compressionMaxRadius01 -
                    settings.compressionMinRadius01);

            // Since charge radius maps linearly to lock01, the visible radial band
            // represents the real perfect timing half-width.
            float halfBandPixels =
                radius *
                radiusRange *
                _perfectHalfWidth01;

            float bandThickness =
                Mathf.Max(
                    settings.sweetSpotThickness * 2.2f,
                    halfBandPixels * 2f);

            Color band =
                settings.sweetSpotColor;

            band.a *=
                _charging
                    ? 0.34f
                    : 0.24f;

            band =
                Color.Lerp(
                    band,
                    settings.perfectColor,
                    _perfectFlash);

            DrawFullRing(
                vh,
                sweetRadius,
                bandThickness,
                band,
                48);

            // Crisp seam at the exact ideal timing point.
            Color seam =
                settings.sweetSpotColor;

            seam.a =
                Mathf.Lerp(
                    0.72f,
                    1f,
                    _perfectFlash);

            seam =
                Color.Lerp(
                    seam,
                    settings.perfectColor,
                    _perfectFlash);

            DrawFullRing(
                vh,
                sweetRadius,
                settings.sweetSpotThickness +
                _perfectFlash * 4f,
                seam,
                48);

            // Four small marks keep the timing point readable at speed.
            float markerLength =
                Mathf.Max(
                    5f,
                    sweetRadius * 0.17f);

            for (int i = 0;
                 i < 4;
                 i++)
            {
                float angle =
                    i * 90f +
                    _rotationDegrees * -0.16f;

                DrawRadialSpoke(
                    vh,
                    angle,
                    sweetRadius - markerLength * 0.45f,
                    sweetRadius + markerLength,
                    Mathf.Max(
                        1f,
                        settings.sweetSpotThickness * 0.75f),
                    seam);
            }
        }

        private void DrawFullLockCore(
            VertexHelper vh,
            float radius)
        {
            float fullRadius =
                radius *
                settings.compressionMinRadius01;

            Color core =
                settings.idleColor;

            core.a =
                _charging
                    ? 0.52f
                    : 0.33f;

            DrawFullRing(
                vh,
                fullRadius,
                Mathf.Max(
                    1.2f,
                    settings.sweetSpotThickness * 0.75f),
                core,
                36);

            // Small inner "eye" makes full-lock clearly different from skill timing.
            Color eye =
                settings.idleColor;

            eye.a *=
                _charging
                    ? 0.40f
                    : 0.22f;

            DrawFullRing(
                vh,
                fullRadius * 0.48f,
                1.2f,
                eye,
                28);
        }

        private void DrawStartGhost(
            VertexHelper vh,
            float radius)
        {
            float startRadius =
                radius *
                settings.compressionMaxRadius01;

            Color ghost =
                settings.idleColor;

            ghost.a *=
                0.20f;

            DrawFullRing(
                vh,
                startRadius,
                Mathf.Max(
                    1f,
                    settings.compressionThickness * 0.55f),
                ghost,
                44);
        }

        private void DrawBrokenOuterRing(
            VertexHelper vh,
            float radius,
            float thickness,
            Color color)
        {
            int count =
                Mathf.Max(
                    2,
                    settings.outerArcCount);

            float spacing =
                360f /
                count;

            for (int i = 0;
                 i < count;
                 i++)
            {
                float center =
                    _rotationDegrees +
                    i * spacing;

                float start =
                    center -
                    settings.outerArcDegrees *
                    0.5f;

                float end =
                    center +
                    settings.outerArcDegrees *
                    0.5f;

                DrawArc(
                    vh,
                    radius,
                    thickness,
                    start,
                    end,
                    color,
                    14);
            }
        }

        private void DrawCompressionRing(
            VertexHelper vh,
            float radius)
        {
            float currentRadius01 =
                Mathf.Lerp(
                    settings.compressionMaxRadius01,
                    settings.compressionMinRadius01,
                    _lock01);

            float currentRadius =
                radius *
                currentRadius01;

            float distanceToSweet =
                Mathf.Abs(
                    _lock01 -
                    _perfectCenter01);

            float sweetGlow =
                1f -
                Mathf.Clamp01(
                    distanceToSweet /
                    Mathf.Max(
                        0.001f,
                        _perfectHalfWidth01));

            Color color =
                Color.Lerp(
                    settings.chargeColor,
                    settings.perfectColor,
                    sweetGlow);

            DrawFullRing(
                vh,
                currentRadius,
                settings.compressionThickness +
                sweetGlow * 3.0f,
                color,
                48);

            for (int i = 0;
                 i < 3;
                 i++)
            {
                float angle =
                    -_rotationDegrees * 1.25f +
                    i * 120f;

                DrawArc(
                    vh,
                    currentRadius,
                    settings.compressionThickness * 1.55f,
                    angle - 12f,
                    angle + 12f,
                    color,
                    5);
            }
        }

        private float GetSweetSpotRadius01()
        {
            return
                Mathf.Lerp(
                    settings.compressionMaxRadius01,
                    settings.compressionMinRadius01,
                    _perfectCenter01);
        }

        private static void DrawFullRing(
            VertexHelper vh,
            float radius,
            float thickness,
            Color color,
            int segments)
        {
            DrawArc(
                vh,
                radius,
                thickness,
                0f,
                360f,
                color,
                segments);
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
                    thickness * 0.5f);

            float outer =
                radius +
                thickness * 0.5f;

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
                    i * 2;

                int b = a + 1;
                int c = a + 2;
                int d = a + 3;

                vh.AddTriangle(a, d, b);
                vh.AddTriangle(a, c, d);
            }
        }

        private static void DrawRadialSpoke(
            VertexHelper vh,
            float angleDegrees,
            float innerRadius,
            float outerRadius,
            float thickness,
            Color color)
        {
            float angle =
                angleDegrees *
                Mathf.Deg2Rad;

            Vector2 dir =
                new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle));

            Vector2 perpendicular =
                new Vector2(
                    -dir.y,
                    dir.x) *
                thickness *
                0.5f;

            Vector2 a =
                dir * innerRadius -
                perpendicular;

            Vector2 b =
                dir * innerRadius +
                perpendicular;

            Vector2 c =
                dir * outerRadius -
                perpendicular;

            Vector2 d =
                dir * outerRadius +
                perpendicular;

            int start =
                vh.currentVertCount;

            AddVertex(vh, a, color);
            AddVertex(vh, b, color);
            AddVertex(vh, c, color);
            AddVertex(vh, d, color);

            vh.AddTriangle(
                start,
                start + 3,
                start + 1);

            vh.AddTriangle(
                start,
                start + 2,
                start + 3);
        }

        private static void AddVertex(
            VertexHelper vh,
            Vector2 position,
            Color color)
        {
            UIVertex vertex =
                UIVertex.simpleVert;

            vertex.position =
                position;

            vertex.color =
                color;

            vh.AddVert(vertex);
        }
    }
}
