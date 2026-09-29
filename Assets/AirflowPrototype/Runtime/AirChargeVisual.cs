using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Temporary procedural "compressed air" feedback.
    /// Three thin rotating rings contract toward the cast origin while charging.
    /// </summary>
    public sealed class AirChargeVisual : MonoBehaviour
    {
        [SerializeField] private AirCaster caster;
        [SerializeField] private Transform castOrigin;
        [SerializeField] private AirCastSettings settings;

        private readonly LineRenderer[] _rings = new LineRenderer[3];
        private readonly Material[] _materials = new Material[3];
        private float _releaseBurst;
        private const int Segments = 28;

        public void Configure(
            AirCaster newCaster,
            Transform newCastOrigin,
            AirCastSettings newSettings)
        {
            caster = newCaster;
            castOrigin = newCastOrigin;
            settings = newSettings;

            BuildIfNeeded();
        }

        private void Awake()
        {
            BuildIfNeeded();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (caster == null || castOrigin == null || settings == null)
                return;

            BuildIfNeeded();

            bool visible = caster.IsCharging || _releaseBurst > 0.001f;
            SetVisible(visible);

            if (!visible)
                return;

            float charge01 = caster.IsCharging ? caster.Charge01 : 1f;
            float eased = charge01 * charge01 * (3f - 2f * charge01);

            float radius = Mathf.Lerp(
                settings.unchargedRingRadius,
                settings.chargedRingRadius,
                eased);

            if (_releaseBurst > 0f)
            {
                radius += _releaseBurst * 0.38f;
                _releaseBurst =
                    Mathf.MoveTowards(
                        _releaseBurst,
                        0f,
                        Time.deltaTime * 5.5f);
            }

            for (int i = 0; i < _rings.Length; i++)
            {
                LineRenderer ring = _rings[i];
                if (ring == null)
                    continue;

                Transform ringTransform = ring.transform;
                ringTransform.position = castOrigin.position;

                float spin =
                    Time.time *
                    settings.ringRotationSpeed *
                    (i % 2 == 0 ? 1f : -1f);

                ringTransform.rotation =
                    Quaternion.Euler(
                        i == 0 ? 0f : 62f,
                        spin + i * 42f,
                        i == 2 ? 58f : 0f);

                DrawRing(ring, radius * (1f + i * 0.08f));

                Color c = caster.IsReady
                    ? new Color(0.88f, 0.98f, 1f, 0.95f)
                    : new Color(0.55f, 0.82f, 0.94f, 0.65f + charge01 * 0.25f);

                ring.startColor = c;
                ring.endColor = c;
            }
        }

        private void Subscribe()
        {
            if (caster == null)
                return;

            caster.PulseReleased -= OnPulseReleased;
            caster.PulseReleased += OnPulseReleased;
        }

        private void Unsubscribe()
        {
            if (caster != null)
                caster.PulseReleased -= OnPulseReleased;
        }

        private void OnPulseReleased(float charge01, bool ready)
        {
            _releaseBurst = ready ? 1f : 0.55f;
        }

        private void BuildIfNeeded()
        {
            if (settings == null || castOrigin == null)
                return;

            if (_rings[0] != null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            for (int i = 0; i < _rings.Length; i++)
            {
                GameObject child =
                    new GameObject($"Air Compression Ring {i + 1}");

                child.transform.SetParent(transform, false);

                LineRenderer line = child.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = Segments;
                line.startWidth = settings.ringWidth;
                line.endWidth = settings.ringWidth;
                line.numCornerVertices = 2;
                line.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;

                Material material = new Material(shader);
                material.color =
                    new Color(0.55f, 0.82f, 0.94f, 0.8f);

                line.material = material;

                _rings[i] = line;
                _materials[i] = material;
            }

            SetVisible(false);
        }

        private static void DrawRing(LineRenderer line, float radius)
        {
            for (int i = 0; i < Segments; i++)
            {
                float angle =
                    (i / (float)Segments) * Mathf.PI * 2f;

                line.SetPosition(
                    i,
                    new Vector3(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius,
                        0f));
            }
        }

        private void SetVisible(bool visible)
        {
            for (int i = 0; i < _rings.Length; i++)
            {
                if (_rings[i] != null)
                    _rings[i].enabled = visible;
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _materials.Length; i++)
            {
                if (_materials[i] != null)
                    Destroy(_materials[i]);
            }
        }
    }
}
