using System.Collections.Generic;
using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Reward-arrival shine using the exact brightAirColor used by the return stream.
    /// Presentation-only: never modifies movement/collision transforms.
    /// </summary>
    public sealed class AirRewardGlowFeedback : MonoBehaviour
    {
        private sealed class RendererSlot
        {
            public Renderer renderer;
            public int materialIndex;
            public Material material;

            public string baseColorProperty;
            public Color baseColor;
            public bool hasBaseColor;

            public Color baseEmission;
            public bool hasEmission;

            public MaterialPropertyBlock block;
        }

        [SerializeField] private AirFlowHitSystem hitSystem;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private AirPresentationSettings presentationSettings;
        [SerializeField] private AirRewardGlowSettings glowSettings;

        private readonly List<RendererSlot> _slots =
            new List<RendererSlot>();

        private readonly HashSet<Material> _emissionKeywordEnabledByUs =
            new HashSet<Material>();

        private Light _pulseLight;
        private float _age = 999f;
        private float _strength = 1f;

        public void Configure(
            AirFlowHitSystem newHitSystem,
            Transform newVisualRoot,
            AirPresentationSettings newPresentationSettings,
            AirRewardGlowSettings newGlowSettings)
        {
            Unsubscribe();

            hitSystem = newHitSystem;
            visualRoot = newVisualRoot;
            presentationSettings = newPresentationSettings;
            glowSettings = newGlowSettings;

            RebuildRendererCache();
            BuildLight();

            if (isActiveAndEnabled)
                Subscribe();
        }

        private void Awake()
        {
            if (hitSystem == null)
                hitSystem = GetComponent<AirFlowHitSystem>();

            if (visualRoot == null)
            {
                Transform found =
                    transform.Find("Capsule Animation Root");

                visualRoot =
                    found != null
                        ? found
                        : transform;
            }

            RebuildRendererCache();
            BuildLight();
            ApplyGlow(0f);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ApplyGlow(0f);
        }

        private void Subscribe()
        {
            if (hitSystem == null)
                return;

            hitSystem.RewardDelivered -= OnRewardDelivered;
            hitSystem.RewardDelivered += OnRewardDelivered;
        }

        private void Unsubscribe()
        {
            if (hitSystem != null)
                hitSystem.RewardDelivered -= OnRewardDelivered;
        }

        private void Update()
        {
            if (glowSettings == null)
                return;

            float duration =
                Mathf.Max(
                    0.03f,
                    glowSettings.duration);

            if (_age >= duration)
                return;

            _age +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    _age /
                    duration);

            float attack =
                Mathf.Clamp(
                    glowSettings.attackFraction,
                    0.01f,
                    0.45f);

            float envelope;

            if (t < attack)
            {
                envelope =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        t / attack);
            }
            else
            {
                float releaseT =
                    Mathf.InverseLerp(
                        attack,
                        1f,
                        t);

                envelope =
                    1f -
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        releaseT);
            }

            ApplyGlow(
                envelope *
                _strength);

            if (t >= 1f)
                ApplyGlow(0f);
        }

        private void OnRewardDelivered(
            AirNode node,
            float intensity)
        {
            _age = 0f;

            _strength =
                Mathf.Clamp(
                    intensity,
                    0.65f,
                    1.35f);

            if (_slots.Count == 0)
                RebuildRendererCache();

            BuildLight();
        }

        private Color GetRewardColor()
        {
            Color color =
                presentationSettings != null
                    ? presentationSettings.brightAirColor
                    : new Color(
                        0.90f,
                        0.99f,
                        1f,
                        1f);

            color.a = 1f;
            return color;
        }

        private void ApplyGlow(
            float amount)
        {
            if (glowSettings == null)
                return;

            Color rewardColor =
                GetRewardColor();

            for (int i = 0;
                 i < _slots.Count;
                 i++)
            {
                RendererSlot slot =
                    _slots[i];

                if (slot == null ||
                    slot.renderer == null ||
                    slot.material == null)
                {
                    continue;
                }

                MaterialPropertyBlock block =
                    slot.block;

                if (block == null)
                {
                    block =
                        new MaterialPropertyBlock();

                    slot.block = block;
                }

                slot.renderer.GetPropertyBlock(
                    block,
                    slot.materialIndex);

                if (slot.hasBaseColor)
                {
                    Color tinted =
                        Color.Lerp(
                            slot.baseColor,
                            rewardColor,
                            Mathf.Clamp01(
                                amount *
                                glowSettings.surfaceTint));

                    block.SetColor(
                        slot.baseColorProperty,
                        tinted);
                }

                if (slot.hasEmission)
                {
                    Color emission =
                        slot.baseEmission +
                        rewardColor *
                        glowSettings.emissionIntensity *
                        Mathf.Max(
                            0f,
                            amount);

                    emission.a = 1f;

                    block.SetColor(
                        "_EmissionColor",
                        emission);
                }

                slot.renderer.SetPropertyBlock(
                    block,
                    slot.materialIndex);
            }

            if (_pulseLight != null)
            {
                _pulseLight.enabled =
                    glowSettings.usePointLight &&
                    amount > 0.001f;

                _pulseLight.color =
                    rewardColor;

                _pulseLight.intensity =
                    glowSettings.lightIntensity *
                    Mathf.Max(
                        0f,
                        amount);

                _pulseLight.range =
                    glowSettings.lightRange;
            }
        }

        private void RebuildRendererCache()
        {
            RestoreEmissionKeywords();
            _slots.Clear();

            if (visualRoot == null)
                return;

            Renderer[] renderers =
                visualRoot.GetComponentsInChildren<Renderer>(
                    true);

            for (int r = 0;
                 r < renderers.Length;
                 r++)
            {
                Renderer renderer =
                    renderers[r];

                if (!(renderer is MeshRenderer) &&
                    !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }

                Material[] materials =
                    renderer.sharedMaterials;

                for (int m = 0;
                     m < materials.Length;
                     m++)
                {
                    Material material =
                        materials[m];

                    if (material == null)
                        continue;

                    RendererSlot slot =
                        new RendererSlot
                        {
                            renderer = renderer,
                            materialIndex = m,
                            material = material,
                            block =
                                new MaterialPropertyBlock()
                        };

                    if (material.HasProperty(
                            "_BaseColor"))
                    {
                        slot.baseColorProperty =
                            "_BaseColor";

                        slot.baseColor =
                            material.GetColor(
                                "_BaseColor");

                        slot.hasBaseColor = true;
                    }
                    else if (material.HasProperty(
                                 "_Color"))
                    {
                        slot.baseColorProperty =
                            "_Color";

                        slot.baseColor =
                            material.GetColor(
                                "_Color");

                        slot.hasBaseColor = true;
                    }

                    if (material.HasProperty(
                            "_EmissionColor"))
                    {
                        slot.baseEmission =
                            material.GetColor(
                                "_EmissionColor");

                        slot.hasEmission = true;

                        if (!material.IsKeywordEnabled(
                                "_EMISSION"))
                        {
                            material.EnableKeyword(
                                "_EMISSION");

                            _emissionKeywordEnabledByUs.Add(
                                material);
                        }
                    }

                    _slots.Add(slot);
                }
            }
        }

        private void BuildLight()
        {
            if (glowSettings == null)
                return;

            if (_pulseLight == null)
            {
                Transform existing =
                    transform.Find(
                        "Reward Glow Light");

                GameObject lightObject;

                if (existing != null)
                {
                    lightObject =
                        existing.gameObject;
                }
                else
                {
                    lightObject =
                        new GameObject(
                            "Reward Glow Light");

                    lightObject.transform.SetParent(
                        transform,
                        false);
                }

                _pulseLight =
                    lightObject.GetComponent<Light>();

                if (_pulseLight == null)
                    _pulseLight = lightObject.AddComponent<Light>();

                _pulseLight.type =
                    LightType.Point;

                _pulseLight.shadows =
                    LightShadows.None;
            }

            CharacterController controller =
                GetComponent<CharacterController>();

            Vector3 localPosition =
                controller != null
                    ? controller.center
                    : Vector3.up *
                      glowSettings.lightHeight;

            _pulseLight.transform.localPosition =
                localPosition;

            _pulseLight.range =
                glowSettings.lightRange;

            _pulseLight.enabled = false;
        }

        private void RestoreEmissionKeywords()
        {
            foreach (Material material in
                     _emissionKeywordEnabledByUs)
            {
                if (material != null)
                    material.DisableKeyword("_EMISSION");
            }

            _emissionKeywordEnabledByUs.Clear();
        }

        private void OnDestroy()
        {
            ApplyGlow(0f);
            RestoreEmissionKeywords();
        }
    }
}
