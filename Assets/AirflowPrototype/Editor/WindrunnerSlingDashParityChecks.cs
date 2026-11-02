using System;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class WindrunnerSlingDashParityChecks
    {
        [MenuItem("Tools/Windrunner/Production/Batch 5G/1. Run Dash Progression Parity Checks")]
        public static void Run()
        {
            AirSlingSettings settings = ScriptableObject.CreateInstance<AirSlingSettings>();
            try
            {
                var random = new System.Random(551907);
                AnimationCurve defaultCurve = settings.dashSpeedCurve;
                AnimationCurve unusualCurve = new AnimationCurve(
                    new Keyframe(0f, -0.2f), new Keyframe(0.4f, 0.35f),
                    new Keyframe(1f, 1.5f));
                var extracted = new AirSlingDashProgression();
                var legacy = new LegacyDashReference();
                int comparisons = 0;

                for (int i = 0; i < 2048; i++)
                {
                    settings.dashSpeedCurve = i % 13 == 0 ? null :
                        i % 17 == 0 ? unusualCurve : defaultCurve;
                    settings.dashStartSpeed = 1f + Next(random) * 40f;
                    settings.dashInitialKickSpeed = Next(random) * 20f;
                    settings.dashMaximumSpeed = 1f + Next(random) * 80f;
                    settings.dashAcceleration = 1f + Next(random) * 250f;
                    settings.dashArrivalAccelerationStart = 0.35f + Next(random) * 0.6f;
                    settings.dashArrivalAccelerationMultiplier = 1f + Next(random) * 3f;
                    settings.maximumDashDuration = 0.1f + Next(random) * 2.5f;

                    float pathDistance = i % 23 == 0 ? 0f : 0.01f + Next(random) * 100f;
                    extracted.Reset();
                    legacy.Reset();
                    extracted.SetPathDistance(pathDistance);
                    legacy.SetPathDistance(pathDistance);
                    extracted.Begin(settings);
                    legacy.Begin(settings);
                    CompareState(i, legacy, extracted);
                    comparisons += 4;

                    for (int frame = 0; frame < 10; frame++)
                    {
                        float dt = 0.002f + Next(random) * 0.08f;
                        extracted.AdvanceElapsed(dt);
                        legacy.AdvanceElapsed(dt);
                        Compare("elapsed", i, frame, legacy.Elapsed, extracted.Elapsed);
                        comparisons++;

                        bool timeoutBeforeMove = legacy.Elapsed >= settings.maximumDashDuration;
                        bool newTimeout = extracted.HasTimedOut(settings.maximumDashDuration);
                        if (timeoutBeforeMove != newTimeout)
                            throw new InvalidOperationException("Timeout mismatch at " + i + ":" + frame);
                        comparisons++;
                        if (newTimeout)
                            break;

                        float oldNext = legacy.PreviewNextProgress(dt, settings);
                        float newNext = extracted.PreviewNextProgress(dt, settings);
                        Compare("next progress", i, frame, oldNext, newNext);
                        Compare("speed before movement", i, frame, legacy.Speed, extracted.Speed);
                        Compare("uncommitted progress", i, frame, legacy.Progress, extracted.Progress);
                        comparisons += 3;

                        // Mirror CharacterController.Move occurring here, before CommitProgress.
                        legacy.CommitProgress(oldNext);
                        extracted.CommitProgress(newNext);
                        Compare("committed progress", i, frame, legacy.Progress, extracted.Progress);
                        comparisons++;
                    }

                    float oldDistance = legacy.PathDistance;
                    legacy.ClearAfterLaunch();
                    extracted.ClearAfterLaunch();
                    CompareState(i + 2048, legacy, extracted);
                    Compare("launch retained distance", i, 0, oldDistance, extracted.PathDistance);
                    comparisons += 5;

                    extracted.Reset();
                    legacy.Reset();
                    CompareState(i + 4096, legacy, extracted);
                    comparisons += 4;
                }

                Debug.Log("[Windrunner 5G] PASS — " + comparisons +
                    " old-vs-new dash progression comparisons matched. Covers start speed, " +
                    "speed curves (including null/negative), arrival acceleration, " +
                    "preview-before-commit ordering, timeout, launch cleanup and full reset. " +
                    "Live sling physics/feel still requires Play Mode.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[Windrunner 5G] FAIL — Stop. Do not retune dash values to mask a regression.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private static float Next(System.Random random) => (float)random.NextDouble();

        private static void CompareState(int i, LegacyDashReference oldState, AirSlingDashProgression newState)
        {
            Compare("speed", i, -1, oldState.Speed, newState.Speed);
            Compare("elapsed", i, -1, oldState.Elapsed, newState.Elapsed);
            Compare("progress", i, -1, oldState.Progress, newState.Progress);
            Compare("path distance", i, -1, oldState.PathDistance, newState.PathDistance);
        }

        private static void Compare(string label, int scenario, int frame, float expected, float actual)
        {
            if (float.IsNaN(actual) || Mathf.Abs(expected - actual) > 0.00001f)
                throw new InvalidOperationException(label + " mismatch " + scenario + ":" + frame +
                    " legacy=" + expected + " extracted=" + actual);
        }

        // Frozen Batch 5F calculations, used only as an Editor reference.
        private sealed class LegacyDashReference
        {
            private float speed;
            private float elapsed;
            private float progress;
            private float pathDistance;

            public float Speed => speed;
            public float Elapsed => elapsed;
            public float Progress => progress;
            public float PathDistance => pathDistance;

            public void SetPathDistance(float value) => pathDistance = value;

            public void Begin(AirSlingSettings settings)
            {
                progress = 0f;
                float initialCurveSpeed = EvaluateDashSpeed(0f, settings);
                speed = Mathf.Max(settings.dashStartSpeed + settings.dashInitialKickSpeed,
                    initialCurveSpeed);
                elapsed = 0f;
            }

            public void AdvanceElapsed(float dt) => elapsed += dt;

            public float PreviewNextProgress(float dt, AirSlingSettings settings)
            {
                float targetSpeed = EvaluateDashSpeed(progress, settings);
                float accelerationMultiplier = progress >= settings.dashArrivalAccelerationStart
                    ? settings.dashArrivalAccelerationMultiplier : 1f;
                speed = Mathf.MoveTowards(speed, targetSpeed,
                    settings.dashAcceleration * accelerationMultiplier * dt);
                float progressStep = (speed * dt) / Mathf.Max(0.01f, pathDistance);
                float nextProgress = Mathf.Clamp01(progress + progressStep);
                return nextProgress;
            }

            public void CommitProgress(float next) => progress = next;

            private static float EvaluateDashSpeed(float progress01, AirSlingSettings settings)
            {
                float multiplier = settings.dashSpeedCurve != null
                    ? settings.dashSpeedCurve.Evaluate(Mathf.Clamp01(progress01))
                    : 1f;
                multiplier = Mathf.Max(0.05f, multiplier);
                return settings.dashMaximumSpeed * multiplier;
            }

            public void ClearAfterLaunch()
            {
                elapsed = 0f;
                speed = 0f;
                progress = 0f;
            }

            public void Reset()
            {
                speed = 0f;
                elapsed = 0f;
                progress = 0f;
                pathDistance = 0f;
            }
        }
    }
}
