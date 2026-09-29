using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Lightweight procedural dust for grounded travel and landings.
    /// Uses world-space simulation so emitted dust stays behind the player.
    /// </summary>
    public sealed class PlayerDustFeedback : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerMovementVisuals movementVisuals;
        [SerializeField] private PlayerDustSettings settings;

        private ParticleSystem _particles;
        private ParticleSystemRenderer _renderer;
        private Material _runtimeMaterial;
        private Texture2D _runtimeTexture;

        private float _movementTimer;

        public void Configure(
            PlayerMotor newMotor,
            PlayerMovementVisuals newMovementVisuals,
            PlayerDustSettings newSettings)
        {
            Unsubscribe();

            motor = newMotor;
            movementVisuals = newMovementVisuals;
            settings = newSettings;

            if (Application.isPlaying)
                BuildIfNeeded();

            if (isActiveAndEnabled)
                Subscribe();
        }

        private void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();

            if (movementVisuals == null)
                movementVisuals = GetComponent<PlayerMovementVisuals>();

            BuildIfNeeded();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (movementVisuals == null)
                return;

            movementVisuals.Landed -= OnLanded;
            movementVisuals.Landed += OnLanded;
        }

        private void Unsubscribe()
        {
            if (movementVisuals != null)
                movementVisuals.Landed -= OnLanded;
        }

        private void Update()
        {
            if (motor == null ||
                settings == null ||
                _particles == null)
            {
                return;
            }

            PositionEmitterAtFeet();

            if (!motor.IsGrounded ||
                motor.HorizontalSpeed <
                    settings.movementDustStartSpeed)
            {
                _movementTimer = 0f;
                return;
            }

            _movementTimer -= Time.deltaTime;

            if (_movementTimer > 0f)
                return;

            float speed01 =
                Mathf.InverseLerp(
                    settings.movementDustStartSpeed,
                    Mathf.Max(
                        settings.movementDustStartSpeed + 0.1f,
                        motor.Settings != null
                            ? motor.Settings.dashMaxSpeed
                            : 16f),
                    motor.HorizontalSpeed);

            EmitMovementDust(
                speed01);

            _movementTimer =
                Mathf.Lerp(
                    settings.walkEmissionInterval,
                    settings.sprintEmissionInterval,
                    speed01);
        }

        private void OnLanded(
            float intensity)
        {
            if (_particles == null ||
                settings == null)
            {
                return;
            }

            PositionEmitterAtFeet();

            int count =
                Mathf.RoundToInt(
                    Mathf.Lerp(
                        settings.landingMinCount,
                        settings.landingMaxCount,
                        Mathf.Clamp01(intensity)));

            for (int i = 0;
                 i < count;
                 i++)
            {
                EmitParticle(
                    settings.landingParticleSize *
                    Random.Range(0.75f, 1.35f),
                    settings.landingParticleSpeed *
                    Random.Range(0.65f, 1.2f),
                    true);
            }
        }

        private void EmitMovementDust(
            float speed01)
        {
            int count =
                Mathf.Max(
                    1,
                    settings.movementBurstCount);

            for (int i = 0;
                 i < count;
                 i++)
            {
                EmitParticle(
                    settings.movementParticleSize *
                    Mathf.Lerp(
                        0.75f,
                        1.3f,
                        speed01) *
                    Random.Range(
                        0.8f,
                        1.25f),
                    settings.movementParticleSpeed *
                    Mathf.Lerp(
                        0.7f,
                        1.4f,
                        speed01),
                    false);
            }
        }

        private void EmitParticle(
            float size,
            float speed,
            bool landing)
        {
            Vector2 circle =
                Random.insideUnitCircle *
                settings.spawnRadius;

            Vector3 direction =
                new Vector3(
                    circle.x,
                    landing
                        ? Random.Range(0.2f, 0.65f)
                        : Random.Range(0.08f, 0.28f),
                    circle.y);

            if (direction.sqrMagnitude < 0.001f)
                direction = Vector3.up;

            direction.Normalize();

            Vector3 feetWorldPosition =
                GetFeetWorldPosition();

            ParticleSystem.EmitParams emit =
                new ParticleSystem.EmitParams();

            // IMPORTANT:
            // ParticleSystem simulation is set to WORLD space, so EmitParams.position
            // must also be supplied in world coordinates.
            emit.position =
                feetWorldPosition +
                new Vector3(
                    circle.x,
                    0f,
                    circle.y);

            emit.velocity =
                direction *
                speed;

            emit.startSize =
                size;

            emit.startLifetime =
                settings.lifetime *
                Random.Range(
                    0.8f,
                    1.25f);

            Color color =
                settings.dustColor;

            color.a *=
                Random.Range(
                    0.65f,
                    1f);

            emit.startColor =
                color;

            _particles.Emit(
                emit,
                1);
        }

        private Vector3 GetFeetWorldPosition()
        {
            // The prototype player root is the CharacterController base reference.
            // Keeping this centralized makes it easy to add a dedicated foot/ground
            // probe transform later if the character hierarchy changes.
            return
                transform.position +
                Vector3.up *
                settings.groundOffset;
        }

        private void PositionEmitterAtFeet()
        {
            if (_particles == null)
                return;

            _particles.transform.position =
                GetFeetWorldPosition();
        }

        private void BuildIfNeeded()
        {
            if (_particles != null ||
                settings == null)
            {
                return;
            }

            GameObject go =
                new GameObject(
                    "Ground Dust");

            go.transform.SetParent(
                transform,
                false);

            _particles =
                go.AddComponent<ParticleSystem>();

            _renderer =
                go.GetComponent<ParticleSystemRenderer>();

            ParticleSystem.MainModule main =
                _particles.main;

            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime =
                settings.lifetime;

            main.startSpeed = 0f;
            main.startSize =
                settings.movementParticleSize;

            main.startColor =
                settings.dustColor;

            // World simulation keeps existing dust puffs behind as the player moves.
            main.simulationSpace =
                ParticleSystemSimulationSpace.World;

            main.gravityModifier =
                settings.gravityModifier;

            main.maxParticles = 160;

            ParticleSystem.EmissionModule emission =
                _particles.emission;

            emission.enabled = false;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                _particles.colorOverLifetime;

            colorOverLifetime.enabled = true;

            Gradient gradient =
                new Gradient();

            Color baseColor =
                settings.dustColor;

            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(
                        baseColor,
                        0f),
                    new GradientColorKey(
                        baseColor,
                        1f)
                },
                new[]
                {
                    new GradientAlphaKey(
                        0f,
                        0f),
                    new GradientAlphaKey(
                        baseColor.a,
                        0.12f),
                    new GradientAlphaKey(
                        0f,
                        1f)
                });

            colorOverLifetime.color =
                new ParticleSystem.MinMaxGradient(
                    gradient);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
                _particles.sizeOverLifetime;

            sizeOverLifetime.enabled = true;

            AnimationCurve sizeCurve =
                new AnimationCurve(
                    new Keyframe(0f, 0.55f),
                    new Keyframe(0.22f, 1f),
                    new Keyframe(1f, 1.45f));

            sizeOverLifetime.size =
                new ParticleSystem.MinMaxCurve(
                    1f,
                    sizeCurve);

            ParticleSystem.LimitVelocityOverLifetimeModule limit =
                _particles.limitVelocityOverLifetime;

            limit.enabled = true;
            limit.dampen =
                Mathf.Clamp01(
                    settings.drag *
                    0.35f);

            BuildMaterial();
            PositionEmitterAtFeet();
        }

        private void BuildMaterial()
        {
            if (_renderer == null)
                return;

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Particles/Unlit");

            if (shader == null)
                shader =
                    Shader.Find(
                        "Sprites/Default");

            if (shader == null)
                return;

            _runtimeTexture =
                CreateSoftCircleTexture(
                    32);

            _runtimeMaterial =
                new Material(
                    shader);

            _runtimeMaterial.mainTexture =
                _runtimeTexture;

            _runtimeMaterial.color =
                Color.white;

            _renderer.material =
                _runtimeMaterial;

            _renderer.renderMode =
                ParticleSystemRenderMode.Billboard;

            _renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            _renderer.receiveShadows =
                false;
        }

        private static Texture2D CreateSoftCircleTexture(
            int size)
        {
            Texture2D texture =
                new Texture2D(
                    size,
                    size,
                    TextureFormat.RGBA32,
                    false);

            texture.name =
                "Runtime Dust Soft Circle";

            texture.wrapMode =
                TextureWrapMode.Clamp;

            Color[] pixels =
                new Color[
                    size *
                    size];

            Vector2 center =
                new Vector2(
                    (size - 1) * 0.5f,
                    (size - 1) * 0.5f);

            float radius =
                size * 0.5f;

            for (int y = 0;
                 y < size;
                 y++)
            {
                for (int x = 0;
                     x < size;
                     x++)
                {
                    float distance =
                        Vector2.Distance(
                            new Vector2(x, y),
                            center) /
                        radius;

                    float alpha =
                        Mathf.Clamp01(
                            1f - distance);

                    alpha =
                        alpha *
                        alpha *
                        (3f -
                         2f * alpha);

                    pixels[
                        y * size + x] =
                        new Color(
                            1f,
                            1f,
                            1f,
                            alpha);
                }
            }

            texture.SetPixels(
                pixels);

            texture.Apply();

            return texture;
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);

            if (_runtimeTexture != null)
                Destroy(_runtimeTexture);
        }
    }
}
