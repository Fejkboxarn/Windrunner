using System;
using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Lightweight procedural camera-local wind streaks.
    /// Uses LineRenderers instead of authored VFX so the batch stays asset-free.
    /// </summary>
    public sealed class PlayerWindFeedback : MonoBehaviour
    {
        private sealed class Streak
        {
            public LineRenderer line;
            public float x;
            public float y;
            public float z;
            public float speedScale;
        }

        [SerializeField] private PlayerMotor motor;
        [SerializeField] private AirFeelSettings settings;

        private Streak[] _streaks;
        private Material _runtimeMaterial;
        private readonly System.Random _random =
            new System.Random(7919);

        public void Configure(
            PlayerMotor newMotor,
            AirFeelSettings newSettings)
        {
            motor = newMotor;
            settings = newSettings;
        }

        private void Awake()
        {
            if (motor == null)
                motor =
                    FindFirstObjectByType<PlayerMotor>();

            Build();
        }

        private void Update()
        {
            if (motor == null ||
                settings == null)
            {
                SetAllVisible(false);
                return;
            }

            if (_streaks == null ||
                _streaks.Length != settings.maxWindStreaks)
            {
                Rebuild();
            }

            float intensity =
                Mathf.InverseLerp(
                    settings.windStartSpeed,
                    settings.windFullSpeed,
                    motor.HorizontalSpeed);

            int visibleCount =
                Mathf.RoundToInt(
                    intensity *
                    _streaks.Length);

            float length =
                Mathf.Lerp(
                    settings.minWindLength,
                    settings.maxWindLength,
                    intensity);

            float width =
                Mathf.Lerp(
                    settings.minWindWidth,
                    settings.maxWindWidth,
                    intensity);

            for (int i = 0; i < _streaks.Length; i++)
            {
                Streak streak = _streaks[i];

                bool visible =
                    i < visibleCount &&
                    intensity > 0.001f;

                streak.line.enabled = visible;

                if (!visible)
                    continue;

                streak.z -=
                    settings.windTravelSpeed *
                    streak.speedScale *
                    Mathf.Lerp(0.65f, 1.2f, intensity) *
                    Time.deltaTime;

                if (streak.z < 0.45f)
                    ResetStreak(streak, true);

                Vector3 head =
                    new Vector3(
                        streak.x,
                        streak.y,
                        streak.z);

                Vector3 tail =
                    new Vector3(
                        streak.x,
                        streak.y,
                        streak.z + length);

                streak.line.startWidth = width;
                streak.line.endWidth =
                    width * 0.25f;

                streak.line.SetPosition(0, tail);
                streak.line.SetPosition(1, head);
            }
        }

        private void Build()
        {
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

            _runtimeMaterial =
                new Material(shader);

            _runtimeMaterial.color =
                settings.windColor;

            _streaks =
                new Streak[
                    Mathf.Max(
                        1,
                        settings.maxWindStreaks)];

            for (int i = 0; i < _streaks.Length; i++)
            {
                GameObject go =
                    new GameObject(
                        $"Wind Streak {i + 1:00}");

                go.transform.SetParent(
                    transform,
                    false);

                LineRenderer line =
                    go.AddComponent<LineRenderer>();

                line.useWorldSpace = false;
                line.positionCount = 2;
                line.material = _runtimeMaterial;
                line.startColor = settings.windColor;
                line.endColor =
                    new Color(
                        settings.windColor.r,
                        settings.windColor.g,
                        settings.windColor.b,
                        0f);

                line.numCapVertices = 2;
                line.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;

                Streak streak =
                    new Streak
                    {
                        line = line
                    };

                _streaks[i] = streak;

                ResetStreak(
                    streak,
                    false);
            }
        }

        private void Rebuild()
        {
            DestroyStreaks();
            Build();
        }

        private void ResetStreak(
            Streak streak,
            bool farSpawn)
        {
            if (settings == null)
                return;

            streak.x =
                Range(
                    -settings.windSpreadX,
                    settings.windSpreadX);

            streak.y =
                Range(
                    -settings.windSpreadY,
                    settings.windSpreadY);

            streak.z =
                farSpawn
                    ? settings.windSpawnDistance
                    : Range(
                        0.6f,
                        settings.windSpawnDistance);

            streak.speedScale =
                Range(
                    0.82f,
                    1.18f);
        }

        private float Range(
            float min,
            float max)
        {
            return
                Mathf.Lerp(
                    min,
                    max,
                    (float)_random.NextDouble());
        }

        private void SetAllVisible(bool visible)
        {
            if (_streaks == null)
                return;

            for (int i = 0; i < _streaks.Length; i++)
            {
                if (_streaks[i]?.line != null)
                    _streaks[i].line.enabled = visible;
            }
        }

        private void DestroyStreaks()
        {
            if (_streaks != null)
            {
                for (int i = 0; i < _streaks.Length; i++)
                {
                    if (_streaks[i]?.line != null)
                    {
                        Destroy(
                            _streaks[i].line.gameObject);
                    }
                }
            }

            _streaks = null;

            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        private void OnDestroy()
        {
            DestroyStreaks();
        }
    }
}
