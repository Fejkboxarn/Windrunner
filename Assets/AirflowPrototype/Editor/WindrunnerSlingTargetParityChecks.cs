using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    /// <summary>
    /// Synthetic numeric scorer parity plus an opt-in real-scene LOS comparison.
    /// Never edits or saves a scene/prefab. The temporary settings SO is destroyed.
    /// </summary>
    public static class WindrunnerSlingTargetParityChecks
    {
        private const string MenuRoot = "Tools/Windrunner/Production/Batch 5J/";

        [MenuItem(MenuRoot + "1. Run Scoring Parity Checks")]
        public static void RunScoringChecks()
        {
            AirSlingSettings settings = null;
            try
            {
                settings = ScriptableObject.CreateInstance<AirSlingSettings>();
                var random = new System.Random(5112026);
                int checks = 0;
                int rejected = 0;

                for (int i = 0; i < 20000; i++)
                {
                    Vector3 player = RandomVector(random, -150f, 150f);
                    Vector3 target = RandomVector(random, -150f, 150f);
                    Vector3 camera = RandomVector(random, -150f, 150f);
                    Vector3 forward = RandomVector(random, -1f, 1f).normalized;
                    Vector3 velocity = RandomVector(random, -30f, 30f);
                    Vector3 viewport = RandomVector(random, -1f, 2f);

                    // Explicitly exercise the old distance and camera-point
                    // rejection guards, plus offscreen and no-weight cases.
                    if (i % 13 == 0) target = player;
                    if (i % 17 == 0) camera = target;
                    if (i % 23 == 0) viewport.z = -1f;
                    settings.maxTargetDistance = i % 31 == 0
                        ? 0f : Range(random, 0.1f, 300f);
                    settings.maxCameraAngle = Range(random, 0f, 180f);
                    settings.screenCenterWeight = i % 37 == 0
                        ? 0f : Range(random, 0f, 3f);
                    settings.movementDirectionWeight = i % 37 == 0
                        ? 0f : Range(random, 0f, 3f);
                    settings.distanceWeight = i % 37 == 0
                        ? 0f : Range(random, 0f, 3f);
                    settings.cameraForwardWeight = i % 37 == 0
                        ? 0f : Range(random, 0f, 3f);

                    bool oldPass = LegacyPrepare(player, target, camera, forward,
                        settings.maxTargetDistance, settings.maxCameraAngle,
                        out AirSlingTargetGeometry oldGeometry);
                    bool newPass = AirSlingTargetScoreMath.TryPrepare(
                        player, target, camera, forward,
                        settings.maxTargetDistance, settings.maxCameraAngle,
                        out AirSlingTargetGeometry newGeometry);

                    if (oldPass != newPass)
                        throw new InvalidOperationException(
                            "Geometry rejection drift in case " + i);
                    checks++;
                    if (!oldPass || viewport.z <= 0f)
                    {
                        rejected++;
                        continue;
                    }

                    Equal(oldGeometry.Distance, newGeometry.Distance,
                        "distance " + i, ref checks);
                    Equal(oldGeometry.CameraDot, newGeometry.CameraDot,
                        "camera dot " + i, ref checks);
                    Equal(oldGeometry.MinimumCameraDot, newGeometry.MinimumCameraDot,
                        "minimum camera dot " + i, ref checks);

                    AirSlingTargetScores expected = LegacyComponents(
                        oldGeometry, viewport, velocity, settings.maxTargetDistance);
                    AirSlingTargetScores actual = AirSlingTargetScoreMath.ComputeComponents(
                        newGeometry, viewport, velocity, settings.maxTargetDistance);
                    Equal(expected.Screen, actual.Screen, "screen " + i, ref checks);
                    Equal(expected.Movement, actual.Movement, "movement " + i, ref checks);
                    Equal(expected.Distance, actual.Distance, "distance score " + i, ref checks);
                    Equal(expected.CameraForward, actual.CameraForward,
                        "camera score " + i, ref checks);
                    Equal(LegacyWeightedTotal(expected, settings),
                        AirSlingTargetScoreMath.WeightedTotal(actual, settings),
                        "weighted total " + i, ref checks);
                }

                Debug.Log("[Windrunner 5J] Scoring PASS — 20,000 deterministic " +
                          "candidates, " + checks + " geometry/score checks, " +
                          rejected + " rejected or behind-camera cases. " +
                          "This does not test live collider visibility or target switching.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[Windrunner 5J] Scoring FAIL — do not accept Batch 5J.");
            }
            finally
            {
                if (settings != null)
                    UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [MenuItem(MenuRoot + "2. Audit Live LOS Parity (Play Mode)")]
        public static void AuditLiveLos()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Windrunner 5J] Enter Play Mode to compare " +
                                 "RaycastAll vs the active non-alloc targeting query.");
                return;
            }

            AirSlingTargeter targeter = ResolveTargeter();
            if (targeter == null)
            {
                Debug.LogWarning("[Windrunner 5J] Select the active player with " +
                                 "AirSlingTargeter (or have exactly one active targeter).");
                return;
            }

            var serialized = new SerializedObject(targeter);
            Camera camera = serialized.FindProperty("targetCamera")?.objectReferenceValue as Camera;
            if (camera == null) camera = Camera.main;
            AirSlingSettings settings =
                serialized.FindProperty("settings")?.objectReferenceValue as AirSlingSettings;
            if (camera == null || settings == null)
            {
                Debug.LogError("[Windrunner 5J] Targeter camera or settings not available.", targeter);
                return;
            }

            int checks = 0;
            int mismatches = 0;
            int beforeQueries = targeter.LosQueries;
            int beforeFallbacks = targeter.LosFullBufferFallbacks;
            IReadOnlyList<AirSlingNode> nodes = AirSlingNode.All;

            for (int i = 0; i < nodes.Count; i++)
            {
                AirSlingNode node = nodes[i];
                if (node == null || !node.IsAvailable) continue;
                bool oldVisible = LegacyLineOfSight(camera, settings, node);
                bool newVisible = targeter.DebugHasLineOfSight(node);
                checks++;
                if (oldVisible == newVisible) continue;
                mismatches++;
                Debug.LogError("[Windrunner 5J] LOS MISMATCH " + node.name +
                               " legacy=" + oldVisible + " nonalloc=" + newVisible,
                    node);
            }

            string status = mismatches > 0 ? "FAIL" : checks > 0 ? "PASS" : "INCOMPLETE";
            string message = "[Windrunner 5J] Live LOS " + status +
                " — nodes compared=" + checks +
                ", mismatches=" + mismatches +
                ", nonalloc queries=" + (targeter.LosQueries - beforeQueries) +
                ", full-buffer fallbacks=" +
                (targeter.LosFullBufferFallbacks - beforeFallbacks) +
                ". Manual aim/occlusion behavior still needs testing.";
            if (mismatches > 0) Debug.LogError(message, targeter);
            else Debug.Log(message, targeter);
        }

        private static AirSlingTargeter ResolveTargeter()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                AirSlingTargeter candidate = selected.GetComponent<AirSlingTargeter>() ??
                                             selected.GetComponentInParent<AirSlingTargeter>();
                if (candidate != null && candidate.isActiveAndEnabled &&
                    candidate.gameObject.scene.IsValid())
                    return candidate;
            }

            AirSlingTargeter[] found = UnityEngine.Object.FindObjectsByType<AirSlingTargeter>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            return found.Length == 1 ? found[0] : null;
        }

        // Reference expression from the pre-5J implementation, separate from
        // production AirSlingTargetScoreMath so the comparison has value.
        private static bool LegacyPrepare(Vector3 player, Vector3 target,
            Vector3 camera, Vector3 forward, float maxDistance, float maxAngle,
            out AirSlingTargetGeometry result)
        {
            Vector3 delta = target - player;
            float distance = delta.magnitude;
            if (distance <= 0.001f || distance > maxDistance)
            {
                result = default;
                return false;
            }
            Vector3 cameraDelta = target - camera;
            float cameraDistance = cameraDelta.magnitude;
            if (cameraDistance <= 0.001f)
            {
                result = default;
                return false;
            }
            float dot = Vector3.Dot(forward, cameraDelta / cameraDistance);
            float minDot = Mathf.Cos(maxAngle * Mathf.Deg2Rad);
            if (dot < minDot)
            {
                result = default;
                return false;
            }
            result = new AirSlingTargetGeometry(delta, distance, dot, minDot);
            return true;
        }

        private static AirSlingTargetScores LegacyComponents(
            AirSlingTargetGeometry geometry, Vector3 viewport,
            Vector3 velocity, float maxDistance)
        {
            Vector2 offset = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            float screen = 1f - Mathf.Clamp01(offset.magnitude / 0.70710678f);
            float distance = 1f - Mathf.Clamp01(geometry.Distance / maxDistance);
            float camera = Mathf.InverseLerp(
                geometry.MinimumCameraDot, 1f, geometry.CameraDot);
            Vector3 move = velocity;
            move.y = 0f;
            Vector3 planar = geometry.PlayerToTarget;
            planar.y = 0f;
            float movement = 0.5f;
            if (move.sqrMagnitude > 0.01f && planar.sqrMagnitude > 0.01f)
                movement = Mathf.InverseLerp(-1f, 1f,
                    Vector3.Dot(move.normalized, planar.normalized));
            return new AirSlingTargetScores(screen, movement, distance, camera);
        }

        private static float LegacyWeightedTotal(
            AirSlingTargetScores scores, AirSlingSettings settings)
        {
            float sum = settings.screenCenterWeight + settings.movementDirectionWeight +
                        settings.distanceWeight + settings.cameraForwardWeight;
            if (sum <= 0.0001f) sum = 1f;
            return (scores.Screen * settings.screenCenterWeight +
                    scores.Movement * settings.movementDirectionWeight +
                    scores.Distance * settings.distanceWeight +
                    scores.CameraForward * settings.cameraForwardWeight) / sum;
        }

        private static bool LegacyLineOfSight(
            Camera camera, AirSlingSettings settings, AirSlingNode node)
        {
            Vector3 origin = camera.transform.position;
            Vector3 direction = node.TargetPosition - origin;
            float distance = direction.magnitude;
            if (distance <= 0.001f) return true;
            direction /= distance;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance,
                settings.obstructionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Transform hit = hits[i].transform;
                if (hit == null || hit == node.transform || hit.IsChildOf(node.transform))
                    continue;
                return false;
            }
            return true;
        }

        private static Vector3 RandomVector(System.Random random, float min, float max) =>
            new Vector3(Range(random, min, max), Range(random, min, max),
                Range(random, min, max));

        private static float Range(System.Random random, float min, float max) =>
            min + (float)random.NextDouble() * (max - min);

        private static void Equal(float expected, float actual, string label, ref int checks)
        {
            if (float.IsNaN(expected) != float.IsNaN(actual) ||
                (!float.IsNaN(expected) && Mathf.Abs(expected - actual) > 0.000001f))
                throw new InvalidOperationException(label + " drift: expected=" +
                                                    expected + " actual=" + actual);
            checks++;
        }
    }
}
