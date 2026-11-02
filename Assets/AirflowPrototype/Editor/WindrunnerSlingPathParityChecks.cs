using System;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class WindrunnerSlingPathParityChecks
    {
        [MenuItem("Tools/Windrunner/Production/Batch 5E/1. Run Sling Path Parity Checks")]
        public static void Run()
        {
            AirSlingSettings settings = ScriptableObject.CreateInstance<AirSlingSettings>();
            try
            {
                var random = new System.Random(551905);
                var extracted = new AirSlingBezierPath();
                var legacy = new LegacyPathReference();
                int comparisons = 0;

                for (int i = 0; i < 1024; i++)
                {
                    Vector3 start = RandomVector(random, 100f);
                    Vector3 end = start + RandomVector(random, 35f);
                    if (i % 13 == 0) end = start;
                    if (i % 17 == 0) end = start + Vector3.up * 12f;

                    Vector3 forward = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f) * Vector3.forward;
                    Vector3 incoming = i % 11 == 0 ? Vector3.zero : RandomVector(random, 20f);
                    bool hasCamera = i % 3 != 0;
                    Vector3 right = i % 19 == 0 ? Vector3.up :
                        Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f) * Vector3.right;

                    settings.startMomentumInfluence = (float)random.NextDouble() * 2f;
                    settings.startTangentDistance = (float)random.NextDouble() * 10f;
                    settings.approachTangentDistance = (float)random.NextDouble() * 10f;
                    settings.bezierVerticalLift = (float)random.NextDouble() * 5f;
                    settings.bezierLateralOffset = (float)random.NextDouble() * 3f;

                    extracted.Build(start, end, forward, incoming, hasCamera, right, settings);
                    legacy.Build(start, end, forward, incoming, hasCamera, right, settings);

                    Compare("End", i, legacy.End, extracted.End);
                    comparisons++;
                    for (int sample = -1; sample <= 9; sample++)
                    {
                        float t = sample / 8f;
                        Compare("Position", i, legacy.Evaluate(t), extracted.Evaluate(t));
                        Compare("Tangent", i, legacy.Tangent(t), extracted.EvaluateTangent(t));
                        comparisons += 2;
                    }

                    int lengthSamples = 8 + i % 57;
                    float lengthExpected = legacy.Length(lengthSamples);
                    float lengthActual = extracted.EstimateLength(lengthSamples);
                    if (Mathf.Abs(lengthExpected - lengthActual) > 0.0001f)
                        throw new InvalidOperationException("Length mismatch case " + i +
                            ": legacy=" + lengthExpected + " extracted=" + lengthActual);
                    comparisons++;

                    extracted.Clear();
                    Compare("Clear", i, Vector3.zero, extracted.Evaluate(0.5f));
                    Compare("Clear end", i, Vector3.zero, extracted.End);
                    comparisons += 2;
                }

                Debug.Log("[Windrunner 5E] PASS — " + comparisons +
                    " sling path comparisons matched (control point endpoint, cubic positions, " +
                    "tangents, sampled length, clamping and clear). This is geometry parity, " +
                    "NOT the complete live sling or visual-feel test.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[Windrunner 5E] FAIL — Do not retune curves to conceal a parity failure.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private static Vector3 RandomVector(System.Random random, float scale)
        {
            return new Vector3(
                ((float)random.NextDouble() * 2f - 1f) * scale,
                ((float)random.NextDouble() * 2f - 1f) * scale,
                ((float)random.NextDouble() * 2f - 1f) * scale);
        }

        private static void Compare(string label, int caseNumber, Vector3 expected, Vector3 actual)
        {
            if (float.IsNaN(actual.x) || float.IsNaN(actual.y) || float.IsNaN(actual.z) ||
                Vector3.Distance(expected, actual) > 0.0001f)
            {
                throw new InvalidOperationException(label + " mismatch at case " + caseNumber +
                    ": legacy=" + expected.ToString("F6") + ", extracted=" + actual.ToString("F6"));
            }
        }

        // Editor-only independent reference of the original Batch 5E path formulas.
        // Intentionally kept separate from the production path implementation.
        private sealed class LegacyPathReference
        {
            private Vector3 p0, p1, p2, p3;
            public Vector3 End => p3;

            public void Build(Vector3 start, Vector3 end, Vector3 forward,
                Vector3 incomingVelocity, bool hasCamera, Vector3 cameraRight,
                AirSlingSettings settings)
            {
                p0 = start;
                p3 = end;
                Vector3 direct = p3 - p0;
                Vector3 directDirection = direct.sqrMagnitude > 0.0001f ? direct.normalized : forward;
                Vector3 planar = directDirection;
                planar.y = 0f;
                if (planar.sqrMagnitude < 0.0001f)
                {
                    planar = forward;
                    planar.y = 0f;
                }
                planar.Normalize();

                Vector3 incoming = incomingVelocity;
                incoming.y = 0f;
                if (incoming.sqrMagnitude < 0.25f) incoming = planar;
                else incoming.Normalize();

                Vector3 startDirection = planar + incoming * settings.startMomentumInfluence;
                if (startDirection.sqrMagnitude < 0.0001f) startDirection = planar;
                startDirection.Normalize();

                Vector3 side = hasCamera ? cameraRight : Vector3.Cross(Vector3.up, planar);
                side.y = 0f;
                if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(Vector3.up, planar);
                side.Normalize();

                float dot = Vector3.Dot(incoming, side);
                float sign = Mathf.Abs(dot) > 0.08f ? Mathf.Sign(dot) : 1f;
                Vector3 lateral = side * sign * settings.bezierLateralOffset;
                p1 = p0 + startDirection * settings.startTangentDistance +
                     Vector3.up * settings.bezierVerticalLift + lateral;
                p2 = p3 - planar * settings.approachTangentDistance +
                     Vector3.up * (settings.bezierVerticalLift * 0.55f) + lateral * 0.55f;
            }

            public Vector3 Evaluate(float t)
            {
                t = Mathf.Clamp01(t);
                float u = 1f - t;
                return u * u * u * p0 + 3f * u * u * t * p1 +
                       3f * u * t * t * p2 + t * t * t * p3;
            }

            public Vector3 Tangent(float t)
            {
                t = Mathf.Clamp01(t);
                float u = 1f - t;
                Vector3 tangent = 3f * u * u * (p1 - p0) +
                                  6f * u * t * (p2 - p1) +
                                  3f * t * t * (p3 - p2);
                return tangent;
            }

            public float Length(int samples)
            {
                samples = Mathf.Max(2, samples);
                float length = 0f;
                Vector3 previous = Evaluate(0f);
                for (int i = 1; i <= samples; i++)
                {
                    float t = i / (float)samples;
                    Vector3 point = Evaluate(t);
                    length += Vector3.Distance(previous, point);
                    previous = point;
                }
                return Mathf.Max(0.01f, length);
            }
        }
    }
}
