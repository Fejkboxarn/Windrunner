using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Code-driven airbending presentation.
    ///
    /// Important: this component may live on the player root, so it must NEVER
    /// move or rotate its own transform. All charge visuals are authored directly
    /// in world space from the Air Cast Origin.
    /// </summary>
    public sealed class AirPresentationController : MonoBehaviour
    {
        private sealed class ChargeStreak
        {
            public LineRenderer line;
            public float baseAngle;
            public float orbitOffset;
            public float radiusScale;
        }

        private const int RingSegments = 36;

        [SerializeField] private AirCaster caster;
        [SerializeField] private AirFlowHitSystem hitSystem;
        [SerializeField] private Transform castOrigin;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private AirPresentationSettings settings;

        private ChargeStreak[] _chargeStreaks;
        private LineRenderer _chargeRing;
        private Material _material;

        public void Configure(
            AirCaster newCaster,
            AirFlowHitSystem newHitSystem,
            Transform newCastOrigin,
            Camera newCamera,
            AirPresentationSettings newSettings)
        {
            Unsubscribe();

            caster = newCaster;
            hitSystem = newHitSystem;
            castOrigin = newCastOrigin;
            targetCamera = newCamera;
            settings = newSettings;

            Rebuild();

            if (isActiveAndEnabled)
                Subscribe();
        }

        private void Awake()
        {
            if (caster == null)
                caster = GetComponent<AirCaster>();

            if (hitSystem == null)
                hitSystem = GetComponent<AirFlowHitSystem>();

            if (targetCamera == null)
                targetCamera = Camera.main;

            ResolveCastOrigin();
            Rebuild();
        }

        private void OnEnable()
        {
            ResolveCastOrigin();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            SetChargeVisible(false);
        }

        private void ResolveCastOrigin()
        {
            if (castOrigin != null && castOrigin != transform)
                return;

            Transform found = transform.Find("Air Cast Origin");

            if (found != null)
                castOrigin = found;
        }

        private void Subscribe()
        {
            if (caster != null)
            {
                caster.PulseReleased -= OnPulseReleased;
                caster.PulseReleased += OnPulseReleased;

                caster.PulseHit -= OnPulseHit;
                caster.PulseHit += OnPulseHit;
            }

            if (hitSystem != null)
            {
                hitSystem.RewardDelivered -= OnRewardDelivered;
                hitSystem.RewardDelivered += OnRewardDelivered;
            }
        }

        private void Unsubscribe()
        {
            if (caster != null)
            {
                caster.PulseReleased -= OnPulseReleased;
                caster.PulseHit -= OnPulseHit;
            }

            if (hitSystem != null)
                hitSystem.RewardDelivered -= OnRewardDelivered;
        }

        private void Update()
        {
            ResolveCastOrigin();

            if (caster == null ||
                castOrigin == null ||
                settings == null)
            {
                SetChargeVisible(false);
                return;
            }

            if (_chargeStreaks == null ||
                _chargeStreaks.Length != settings.chargeStreakCount)
            {
                Rebuild();
            }

            bool charging = caster.IsCharging;

            SetChargeVisible(charging);

            if (charging)
                UpdateChargeVisuals();
        }

        private void UpdateChargeVisuals()
        {
            float charge = caster.Charge01;
            float readyBoost = caster.IsReady ? 1f : 0f;

            Vector3 center = castOrigin.position;

            Vector3 screenRight =
                targetCamera != null
                    ? targetCamera.transform.right
                    : castOrigin.right;

            Vector3 screenUp =
                targetCamera != null
                    ? targetCamera.transform.up
                    : castOrigin.up;

            float orbit =
                Time.time *
                settings.chargeOrbitSpeed;

            for (int i = 0; i < _chargeStreaks.Length; i++)
            {
                ChargeStreak streak = _chargeStreaks[i];

                float angleDegrees =
                    streak.baseAngle +
                    orbit +
                    streak.orbitOffset;

                float angle =
                    angleDegrees *
                    Mathf.Deg2Rad;

                Vector3 outwardDirection =
                    (screenRight * Mathf.Cos(angle) +
                     screenUp * Mathf.Sin(angle)).normalized;

                float radius =
                    settings.chargeStreakRadius *
                    streak.radiusScale *
                    Mathf.Lerp(
                        1f,
                        0.38f,
                        charge);

                Vector3 outer =
                    center +
                    outwardDirection *
                    radius;

                Vector3 inner =
                    center +
                    outwardDirection *
                    Mathf.Max(
                        0.03f,
                        radius -
                        settings.chargeStreakLength);

                streak.line.SetPosition(0, outer);
                streak.line.SetPosition(1, inner);

                Color c =
                    Color.Lerp(
                        settings.softAirColor,
                        settings.brightAirColor,
                        charge);

                c.a =
                    Mathf.Lerp(
                        0.22f,
                        0.82f,
                        charge);

                streak.line.startColor = c;
                streak.line.endColor =
                    new Color(
                        c.r,
                        c.g,
                        c.b,
                        0f);
            }

            if (_chargeRing != null)
            {
                float radius =
                    Mathf.Lerp(
                        settings.chargeRingStartRadius,
                        settings.chargeRingReadyRadius,
                        charge);

                DrawWorldRing(
                    _chargeRing,
                    center,
                    screenRight,
                    screenUp,
                    radius);

                Color ringColor =
                    Color.Lerp(
                        settings.softAirColor,
                        settings.brightAirColor,
                        charge);

                ringColor.a =
                    Mathf.Lerp(
                        0.28f,
                        0.95f,
                        Mathf.Max(
                            charge,
                            readyBoost));

                _chargeRing.startColor = ringColor;
                _chargeRing.endColor = ringColor;
            }
        }

        private void OnPulseReleased(
            float charge01,
            bool ready)
        {
            if (settings == null ||
                castOrigin == null)
            {
                return;
            }

            Vector3 forward =
                targetCamera != null
                    ? targetCamera.transform.forward
                    : castOrigin.forward;

            AirPressureBurstVisual.Spawn(
                castOrigin.position,
                forward,
                AirPressureBurstVisual.BurstKind.Release,
                settings);
        }

        private void OnPulseHit(
            AirNode node,
            float charge01)
        {
            if (node == null ||
                settings == null)
            {
                return;
            }

            Vector3 source =
                castOrigin != null
                    ? castOrigin.position
                    : transform.position + Vector3.up;

            Vector3 forward =
                node.TargetPosition -
                source;

            AirPressureBurstVisual.Spawn(
                node.TargetPosition,
                forward,
                AirPressureBurstVisual.BurstKind.NodeImpact,
                settings);
        }

        private void OnRewardDelivered(
            AirNode node,
            float intensity)
        {
            if (settings == null)
                return;

            Vector3 playerCenter =
                transform.position +
                Vector3.up;

            Vector3 source =
                node != null
                    ? node.TargetPosition
                    : playerCenter -
                      transform.forward;

            Vector3 forward =
                playerCenter -
                source;

            AirPressureBurstVisual.Spawn(
                playerCenter,
                forward,
                AirPressureBurstVisual.BurstKind.RewardArrival,
                settings);
        }

        private void Rebuild()
        {
            DestroyRuntimeObjects();

            if (settings == null)
                return;

            Shader shader =
                Shader.Find("Sprites/Default");

            if (shader == null)
                shader =
                    Shader.Find(
                        "Universal Render Pipeline/Unlit");

            if (shader == null)
                return;

            _material =
                new Material(shader);

            _material.color =
                settings.softAirColor;

            int count =
                Mathf.Max(
                    1,
                    settings.chargeStreakCount);

            _chargeStreaks =
                new ChargeStreak[count];

            for (int i = 0; i < count; i++)
            {
                GameObject go =
                    new GameObject(
                        $"Charge Intake {i + 1:00}");

                go.transform.SetParent(
                    transform,
                    false);

                LineRenderer line =
                    go.AddComponent<LineRenderer>();

                // World space is intentional: this component can be attached to
                // the player root without using/moving that root as the VFX anchor.
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.material = _material;
                line.startWidth =
                    settings.chargeStreakWidth;
                line.endWidth =
                    settings.chargeStreakWidth * 0.15f;
                line.numCapVertices = 2;
                line.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;

                _chargeStreaks[i] =
                    new ChargeStreak
                    {
                        line = line,
                        baseAngle =
                            (i / (float)count) *
                            360f,
                        orbitOffset =
                            i * 31.7f,
                        radiusScale =
                            Mathf.Lerp(
                                0.78f,
                                1.16f,
                                (i % 4) / 3f)
                    };
            }

            GameObject ringObject =
                new GameObject(
                    "Compressed Air Ring");

            ringObject.transform.SetParent(
                transform,
                false);

            _chargeRing =
                ringObject.AddComponent<LineRenderer>();

            _chargeRing.useWorldSpace = true;
            _chargeRing.loop = true;
            _chargeRing.positionCount = RingSegments;
            _chargeRing.material = _material;
            _chargeRing.startWidth =
                settings.chargeRingWidth;
            _chargeRing.endWidth =
                settings.chargeRingWidth;
            _chargeRing.numCornerVertices = 2;
            _chargeRing.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            _chargeRing.receiveShadows = false;
            _chargeRing.enabled = false;

            SetChargeVisible(false);
        }

        private void SetChargeVisible(bool visible)
        {
            if (_chargeStreaks != null)
            {
                for (int i = 0;
                     i < _chargeStreaks.Length;
                     i++)
                {
                    if (_chargeStreaks[i]?.line != null)
                    {
                        _chargeStreaks[i].line.enabled =
                            visible;
                    }
                }
            }

            if (_chargeRing != null)
                _chargeRing.enabled = visible;
        }

        private static void DrawWorldRing(
            LineRenderer line,
            Vector3 center,
            Vector3 right,
            Vector3 up,
            float radius)
        {
            for (int i = 0;
                 i < RingSegments;
                 i++)
            {
                float angle =
                    (i /
                     (float)RingSegments) *
                    Mathf.PI *
                    2f;

                Vector3 point =
                    center +
                    right *
                    (Mathf.Cos(angle) * radius) +
                    up *
                    (Mathf.Sin(angle) * radius);

                line.SetPosition(
                    i,
                    point);
            }
        }

        private void DestroyRuntimeObjects()
        {
            if (_chargeStreaks != null)
            {
                for (int i = 0;
                     i < _chargeStreaks.Length;
                     i++)
                {
                    if (_chargeStreaks[i]?.line != null)
                    {
                        Destroy(
                            _chargeStreaks[i]
                                .line
                                .gameObject);
                    }
                }
            }

            _chargeStreaks = null;

            if (_chargeRing != null)
            {
                Destroy(
                    _chargeRing.gameObject);

                _chargeRing = null;
            }

            if (_material != null)
            {
                Destroy(_material);
                _material = null;
            }
        }

        private void OnDestroy()
        {
            DestroyRuntimeObjects();
        }
    }
}
