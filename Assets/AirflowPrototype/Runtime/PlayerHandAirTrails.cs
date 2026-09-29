using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirflowPrototype
{
    [DisallowMultipleComponent]
    public sealed class PlayerHandAirTrails : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private AirFlowHitSystem airFlowHitSystem;
        [SerializeField] private AirSlingController slingController;
        [SerializeField] private Animator humanoidAnimator;
        [SerializeField] private Transform leftTrailOrigin;
        [SerializeField] private Transform rightTrailOrigin;

        [Header("Air Node Boost")]
        [Tooltip(
            "How long both hand trails keep emitting after an airborne normal Air Node reward.")]
        [Min(0f)]
        [SerializeField] private float airNodeEmitDuration = 0.28f;

        [Tooltip(
            "How long the Air Node hand trails remain visible after emission stops.")]
        [Min(0.01f)]
        [SerializeField] private float airNodeFadeDuration = 0.32f;

        [Header("Sling Impact")]
        [Tooltip(
            "How long both hand trails keep emitting after contacting a Sling Node.")]
        [Min(0f)]
        [SerializeField] private float slingImpactEmitDuration = 0.36f;

        [Tooltip(
            "How long the Sling Impact hand trails remain visible after emission stops.")]
        [Min(0.01f)]
        [SerializeField] private float slingImpactFadeDuration = 0.44f;

        [Header("Trail Look")]
        [SerializeField] private Material trailMaterial;

        [Min(0.001f)]
        [SerializeField] private float startWidth = 0.085f;

        [Min(0f)]
        [SerializeField] private float endWidth = 0.005f;

        [Min(0.001f)]
        [SerializeField] private float minimumVertexDistance = 0.025f;

        [Range(0, 8)]
        [SerializeField] private int cornerVertices = 3;

        [Range(0, 8)]
        [SerializeField] private int capVertices = 2;

        [SerializeField] private Color trailColor =
            new Color(
                0.78f,
                0.95f,
                1f,
                0.92f);

        [Header("Optional Intensity")]
        [Tooltip(
            "Scales trail width by the normal Air Node reward intensity.")]
        [SerializeField] private bool scaleAirNodeWidthByRewardIntensity = true;

        [Range(0.25f, 2f)]
        [SerializeField] private float minimumRewardWidthMultiplier = 0.70f;

        private TrailRenderer _leftTrail;
        private TrailRenderer _rightTrail;

        private Material _runtimeMaterial;

        private float _emissionRemaining;
        private float _activeFadeDuration;
        private float _activeWidthMultiplier = 1f;

        private bool _subscribed;

        public event Action AirNodeTrailsStarted;
        public event Action SlingImpactTrailsStarted;

        public void Configure(
            PlayerMotor newMotor,
            AirFlowHitSystem newAirFlowHitSystem,
            AirSlingController newSlingController,
            Animator newAnimator,
            Transform newLeftTrailOrigin = null,
            Transform newRightTrailOrigin = null)
        {
            Unsubscribe();

            motor = newMotor;
            airFlowHitSystem = newAirFlowHitSystem;
            slingController = newSlingController;
            humanoidAnimator = newAnimator;

            if (newLeftTrailOrigin != null)
                leftTrailOrigin = newLeftTrailOrigin;

            if (newRightTrailOrigin != null)
                rightTrailOrigin = newRightTrailOrigin;

            ResolveReferences();
            EnsureTrails();

            if (isActiveAndEnabled)
                Subscribe();
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureTrails();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureTrails();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopEmissionImmediate();
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(
                    _runtimeMaterial);
            }
        }

        private void Update()
        {
            if (_emissionRemaining <= 0f)
                return;

            _emissionRemaining -=
                Time.unscaledDeltaTime;

            if (_emissionRemaining > 0f)
                return;

            _emissionRemaining = 0f;

            SetEmission(
                false);
        }

        private void ResolveReferences()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();

            if (airFlowHitSystem == null)
            {
                airFlowHitSystem =
                    GetComponent<AirFlowHitSystem>();
            }

            if (slingController == null)
            {
                slingController =
                    GetComponent<AirSlingController>();
            }

            if (humanoidAnimator == null)
            {
                Animator[] animators =
                    GetComponentsInChildren<Animator>(
                        true);

                for (int i = 0;
                     i < animators.Length;
                     i++)
                {
                    Animator candidate =
                        animators[i];

                    if (candidate != null &&
                        candidate.isHuman)
                    {
                        humanoidAnimator =
                            candidate;

                        break;
                    }
                }
            }

            ResolveHandOrigins();
        }

        private void ResolveHandOrigins()
        {
            if (humanoidAnimator == null ||
                !humanoidAnimator.isHuman)
            {
                return;
            }

            if (leftTrailOrigin == null)
            {
                Transform leftHand =
                    humanoidAnimator.GetBoneTransform(
                        HumanBodyBones.LeftHand);

                if (leftHand != null)
                {
                    Transform throwOrigin =
                        leftHand.Find(
                            "Left Throw Origin");

                    leftTrailOrigin =
                        throwOrigin != null
                            ? throwOrigin
                            : leftHand;
                }
            }

            if (rightTrailOrigin == null)
            {
                Transform rightHand =
                    humanoidAnimator.GetBoneTransform(
                        HumanBodyBones.RightHand);

                if (rightHand != null)
                {
                    Transform throwOrigin =
                        rightHand.Find(
                            "Right Throw Origin");

                    rightTrailOrigin =
                        throwOrigin != null
                            ? throwOrigin
                            : rightHand;
                }
            }
        }

        private void EnsureTrails()
        {
            if (leftTrailOrigin != null &&
                _leftTrail == null)
            {
                _leftTrail =
                    EnsureTrailRenderer(
                        leftTrailOrigin,
                        "Left Hand Air Trail");
            }

            if (rightTrailOrigin != null &&
                _rightTrail == null)
            {
                _rightTrail =
                    EnsureTrailRenderer(
                        rightTrailOrigin,
                        "Right Hand Air Trail");
            }

            ApplyTrailAppearance(
                _activeFadeDuration > 0f
                    ? _activeFadeDuration
                    : airNodeFadeDuration,
                _activeWidthMultiplier);
        }

        private TrailRenderer EnsureTrailRenderer(
            Transform origin,
            string objectName)
        {
            Transform existing =
                origin.Find(
                    objectName);

            GameObject trailObject;

            if (existing != null)
            {
                trailObject =
                    existing.gameObject;
            }
            else
            {
                trailObject =
                    new GameObject(
                        objectName);

                trailObject.transform.SetParent(
                    origin,
                    false);

                trailObject.transform.localPosition =
                    Vector3.zero;

                trailObject.transform.localRotation =
                    Quaternion.identity;

                trailObject.transform.localScale =
                    Vector3.one;
            }

            TrailRenderer renderer =
                trailObject.GetComponent<TrailRenderer>();

            if (renderer == null)
            {
                renderer =
                    trailObject.AddComponent<TrailRenderer>();
            }

            renderer.emitting = false;
            renderer.autodestruct = false;
            renderer.shadowCastingMode =
                ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.alignment =
                LineAlignment.View;
            renderer.textureMode =
                LineTextureMode.Stretch;
            renderer.minVertexDistance =
                minimumVertexDistance;
            renderer.numCornerVertices =
                cornerVertices;
            renderer.numCapVertices =
                capVertices;

            renderer.Clear();

            return renderer;
        }

        private void Subscribe()
        {
            if (_subscribed)
                return;

            if (airFlowHitSystem != null)
            {
                airFlowHitSystem.RewardDelivered +=
                    OnAirNodeRewardDelivered;
            }

            if (slingController != null)
            {
                slingController.NodeImpact +=
                    OnSlingNodeImpact;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;

            if (airFlowHitSystem != null)
            {
                airFlowHitSystem.RewardDelivered -=
                    OnAirNodeRewardDelivered;
            }

            if (slingController != null)
            {
                slingController.NodeImpact -=
                    OnSlingNodeImpact;
            }

            _subscribed = false;
        }

        private void OnAirNodeRewardDelivered(
            AirNode node,
            float intensity)
        {
            if (motor == null ||
                motor.IsGrounded)
            {
                return;
            }

            float widthMultiplier = 1f;

            if (scaleAirNodeWidthByRewardIntensity)
            {
                widthMultiplier =
                    Mathf.Lerp(
                        minimumRewardWidthMultiplier,
                        1f,
                        Mathf.Clamp01(
                            intensity));
            }

            BeginTrailBurst(
                airNodeEmitDuration,
                airNodeFadeDuration,
                widthMultiplier);

            AirNodeTrailsStarted?.Invoke();
        }

        private void OnSlingNodeImpact(
            AirSlingNode node)
        {
            BeginTrailBurst(
                slingImpactEmitDuration,
                slingImpactFadeDuration,
                1f);

            SlingImpactTrailsStarted?.Invoke();
        }

        private void BeginTrailBurst(
            float emissionDuration,
            float fadeDuration,
            float widthMultiplier)
        {
            ResolveReferences();
            EnsureTrails();

            _activeFadeDuration =
                Mathf.Max(
                    0.01f,
                    fadeDuration);

            _activeWidthMultiplier =
                Mathf.Max(
                    0.01f,
                    widthMultiplier);

            ApplyTrailAppearance(
                _activeFadeDuration,
                _activeWidthMultiplier);

            ClearTrails();

            _emissionRemaining =
                Mathf.Max(
                    0f,
                    emissionDuration);

            SetEmission(
                _emissionRemaining > 0f);

            if (_emissionRemaining <= 0f)
            {
                // A zero emission time still leaves a short point trail once
                // the hands move on the following frames.
                SetEmission(
                    true);

                _emissionRemaining =
                    Time.unscaledDeltaTime;
            }
        }

        private void ApplyTrailAppearance(
            float fadeDuration,
            float widthMultiplier)
        {
            Material material =
                GetTrailMaterial();

            ConfigureTrail(
                _leftTrail,
                material,
                fadeDuration,
                widthMultiplier);

            ConfigureTrail(
                _rightTrail,
                material,
                fadeDuration,
                widthMultiplier);
        }

        private void ConfigureTrail(
            TrailRenderer renderer,
            Material material,
            float fadeDuration,
            float widthMultiplier)
        {
            if (renderer == null)
                return;

            renderer.time =
                Mathf.Max(
                    0.01f,
                    fadeDuration);

            renderer.startWidth =
                Mathf.Max(
                    0.001f,
                    startWidth *
                    widthMultiplier);

            renderer.endWidth =
                Mathf.Max(
                    0f,
                    endWidth *
                    widthMultiplier);

            renderer.minVertexDistance =
                Mathf.Max(
                    0.001f,
                    minimumVertexDistance);

            renderer.numCornerVertices =
                cornerVertices;

            renderer.numCapVertices =
                capVertices;

            renderer.startColor =
                trailColor;

            Color end =
                trailColor;

            end.a = 0f;

            renderer.endColor =
                end;

            if (material != null)
            {
                renderer.sharedMaterial =
                    material;
            }
        }

        private Material GetTrailMaterial()
        {
            if (trailMaterial != null)
                return trailMaterial;

            if (_runtimeMaterial != null)
                return _runtimeMaterial;

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit");

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Sprites/Default");
            }

            if (shader == null)
                return null;

            _runtimeMaterial =
                new Material(
                    shader)
                {
                    name =
                        "Runtime Hand Air Trail Material"
                };

            _runtimeMaterial.color =
                trailColor;

            return _runtimeMaterial;
        }

        private void SetEmission(
            bool emitting)
        {
            if (_leftTrail != null)
                _leftTrail.emitting = emitting;

            if (_rightTrail != null)
                _rightTrail.emitting = emitting;
        }

        private void ClearTrails()
        {
            if (_leftTrail != null)
                _leftTrail.Clear();

            if (_rightTrail != null)
                _rightTrail.Clear();
        }

        private void StopEmissionImmediate()
        {
            _emissionRemaining = 0f;

            SetEmission(
                false);
        }

        private void OnValidate()
        {
            airNodeEmitDuration =
                Mathf.Max(
                    0f,
                    airNodeEmitDuration);

            airNodeFadeDuration =
                Mathf.Max(
                    0.01f,
                    airNodeFadeDuration);

            slingImpactEmitDuration =
                Mathf.Max(
                    0f,
                    slingImpactEmitDuration);

            slingImpactFadeDuration =
                Mathf.Max(
                    0.01f,
                    slingImpactFadeDuration);

            startWidth =
                Mathf.Max(
                    0.001f,
                    startWidth);

            endWidth =
                Mathf.Max(
                    0f,
                    endWidth);

            minimumVertexDistance =
                Mathf.Max(
                    0.001f,
                    minimumVertexDistance);

            minimumRewardWidthMultiplier =
                Mathf.Clamp(
                    minimumRewardWidthMultiplier,
                    0.25f,
                    2f);

            if (Application.isPlaying)
            {
                ApplyTrailAppearance(
                    _activeFadeDuration > 0f
                        ? _activeFadeDuration
                        : airNodeFadeDuration,
                    _activeWidthMultiplier);
            }
        }
    }
}
