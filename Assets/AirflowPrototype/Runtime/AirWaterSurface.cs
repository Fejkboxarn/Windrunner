using System.Collections.Generic;
using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Runtime-queryable representation of a generated water mesh.
    /// No collider is required: player swimming queries the mesh footprint
    /// and interpolates its Y position directly from its triangles.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    public sealed class AirWaterSurface : MonoBehaviour
    {
        private static readonly List<AirWaterSurface> ActiveSurfaces =
            new List<AirWaterSurface>();

        private MeshFilter _meshFilter;
        private Mesh _cachedMesh;
        private Vector3[] _vertices;
        private int[] _triangles;
        private Bounds _localBounds;

        public static IReadOnlyList<AirWaterSurface> All =>
            ActiveSurfaces;

        private void Awake()
        {
            CacheMesh();
        }

        private void OnEnable()
        {
            if (!ActiveSurfaces.Contains(this))
                ActiveSurfaces.Add(this);

            CacheMesh();
        }

        private void OnDisable()
        {
            ActiveSurfaces.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveSurfaces.Remove(this);
        }

        public bool TryGetSurfaceHeight(
            Vector3 worldPosition,
            float horizontalPadding,
            out float surfaceY)
        {
            surfaceY = 0f;

            if (!CacheMesh())
                return false;

            Vector3 localPoint =
                transform.InverseTransformPoint(
                    worldPosition);

            if (localPoint.x <
                    _localBounds.min.x -
                    horizontalPadding ||
                localPoint.x >
                    _localBounds.max.x +
                    horizontalPadding ||
                localPoint.z <
                    _localBounds.min.z -
                    horizontalPadding ||
                localPoint.z >
                    _localBounds.max.z +
                    horizontalPadding)
            {
                return false;
            }

            Vector2 p =
                new Vector2(
                    localPoint.x,
                    localPoint.z);

            bool found = false;
            float bestLocalY =
                float.NegativeInfinity;

            for (int i = 0;
                 i <= _triangles.Length - 3;
                 i += 3)
            {
                Vector3 a3 =
                    _vertices[
                        _triangles[i]];

                Vector3 b3 =
                    _vertices[
                        _triangles[i + 1]];

                Vector3 c3 =
                    _vertices[
                        _triangles[i + 2]];

                Vector2 a =
                    new Vector2(a3.x, a3.z);

                Vector2 b =
                    new Vector2(b3.x, b3.z);

                Vector2 c =
                    new Vector2(c3.x, c3.z);

                if (!TryGetBarycentric(
                        p,
                        a,
                        b,
                        c,
                        out Vector3 barycentric))
                {
                    continue;
                }

                float localY =
                    a3.y *
                    barycentric.x +
                    b3.y *
                    barycentric.y +
                    c3.y *
                    barycentric.z;

                if (!found ||
                    localY > bestLocalY)
                {
                    found = true;
                    bestLocalY = localY;
                }
            }

            if (!found)
                return false;

            surfaceY =
                transform.TransformPoint(
                    new Vector3(
                        localPoint.x,
                        bestLocalY,
                        localPoint.z)).y;

            return true;
        }

        private bool CacheMesh()
        {
            if (_meshFilter == null)
                _meshFilter = GetComponent<MeshFilter>();

            if (_meshFilter == null ||
                _meshFilter.sharedMesh == null)
            {
                return false;
            }

            Mesh mesh =
                _meshFilter.sharedMesh;

            if (_cachedMesh == mesh &&
                _vertices != null &&
                _triangles != null)
            {
                return true;
            }

            _cachedMesh = mesh;
            _vertices = mesh.vertices;
            _triangles = mesh.triangles;
            _localBounds = mesh.bounds;

            return
                _vertices != null &&
                _vertices.Length > 0 &&
                _triangles != null &&
                _triangles.Length >= 3;
        }

        private static bool TryGetBarycentric(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            out Vector3 barycentric)
        {
            barycentric =
                Vector3.zero;

            Vector2 v0 =
                b - a;

            Vector2 v1 =
                c - a;

            Vector2 v2 =
                point - a;

            float denominator =
                v0.x * v1.y -
                v1.x * v0.y;

            if (Mathf.Abs(denominator) <
                0.000001f)
            {
                return false;
            }

            float inv =
                1f /
                denominator;

            float u =
                (v2.x * v1.y -
                 v1.x * v2.y) *
                inv;

            float v =
                (v0.x * v2.y -
                 v2.x * v0.y) *
                inv;

            float w =
                1f -
                u -
                v;

            const float tolerance =
                -0.0005f;

            if (u < tolerance ||
                v < tolerance ||
                w < tolerance)
            {
                return false;
            }

            barycentric =
                new Vector3(
                    w,
                    u,
                    v);

            return true;
        }
    }
}
