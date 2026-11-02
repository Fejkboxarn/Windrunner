using System;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    /// <summary>
    /// Editor-only subscriptions to existing AirSlingController events. No new
    /// MonoBehaviour, prefab change, scene write or runtime gameplay patch.
    /// </summary>
    [InitializeOnLoad]
    public static class WindrunnerSlingEventTraceMenu
    {
        private const string Root = "Tools/Windrunner/Production/Batch 5H/";
        private static AirSlingController _controller;
        private static WindrunnerSlingEventTraceSession _session;

        static WindrunnerSlingEventTraceMenu()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(Root + "1. Run Synthetic Trace Checks")]
        public static void SyntheticChecks()
        {
            try
            {
                var trace = new WindrunnerSlingEventTraceSession();
                int frame = 1;
                trace.Observe(SlingTraceEvent.Started, frame++, 0f, "Node A");
                trace.Observe(SlingTraceEvent.Committed, frame++, 0.1f, "Node A", 0.9f, true);
                trace.Observe(SlingTraceEvent.InitialBoost, frame++, 0.11f, "Node A");
                trace.Observe(SlingTraceEvent.DashStarted, frame++, 0.2f, "Node A");
                trace.Observe(SlingTraceEvent.Impact, frame++, 0.3f, "Node A");
                trace.Observe(SlingTraceEvent.Launched, frame++, 0.4f, "Node A", 0.9f, true);
                trace.Observe(SlingTraceEvent.PerfectLaunched, frame++, 0.41f, "Node A");

                trace.Observe(SlingTraceEvent.Started, frame++, 0.5f, "Node B");
                trace.Observe(SlingTraceEvent.Committed, frame++, 0.6f, "Node B", 0f, false);
                trace.Observe(SlingTraceEvent.DashStarted, frame++, 0.7f, "Node B");
                trace.Observe(SlingTraceEvent.Impact, frame++, 0.8f, "Node B");
                trace.Observe(SlingTraceEvent.Launched, frame++, 0.9f, "Node B", 0f, false);

                trace.Observe(SlingTraceEvent.Started, frame++, 1f, "Node C");
                trace.Observe(SlingTraceEvent.Canceled, frame++, 1.1f);
                trace.Observe(SlingTraceEvent.Canceled, frame++, 1.2f); // Allowed idle cancellation.

                if (trace.ViolationCount != 0 || trace.PerfectCompleted != 1 ||
                    trace.NormalCompleted != 1 || trace.CanceledCount != 1 ||
                    trace.OrphanCancelCount != 1 || trace.HasActiveAttempt)
                    throw new InvalidOperationException("Valid synthetic event sequences were misclassified.");

                var bad = new WindrunnerSlingEventTraceSession();
                bad.Observe(SlingTraceEvent.Started, 1, 0f, "Node X");
                bad.Observe(SlingTraceEvent.Impact, 2, 0.1f, "Node X");
                if (bad.ViolationCount == 0)
                    throw new InvalidOperationException("Invalid impact-before-dash sequence was not detected.");

                Debug.Log("[Windrunner 5H] Synthetic PASS — perfect success, normal success, " +
                          "cancel, expected idle cancel and out-of-order detection verified. " +
                          "Now record REAL gameplay via menu item 2.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[Windrunner 5H] Synthetic FAIL — Do not rely on a live trace yet.");
            }
        }

        [MenuItem(Root + "2. Start Live Event Trace (Play Mode)")]
        public static void StartLive()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Windrunner 5H] Enter Play Mode, leave the sling idle, then start tracing.");
                return;
            }

            AirSlingController target = ResolveController();
            if (target == null)
            {
                Debug.LogError("[Windrunner 5H] Select a player with AirSlingController. " +
                               "If multiple controllers exist, explicit selection is required.");
                return;
            }

            if (target.IsSlinging)
            {
                Debug.LogWarning("[Windrunner 5H] Wait for the current sling to finish or cancel, " +
                                 "then start recording from Idle.", target);
                return;
            }

            StopLive(false);
            _controller = target;
            _session = new WindrunnerSlingEventTraceSession();
            _controller.SlingStarted += OnStarted;
            _controller.SlingCommitted += OnCommitted;
            _controller.InitialBoostDelivered += OnInitialBoost;
            _controller.NodeDashStarted += OnDashStarted;
            _controller.NodeImpact += OnImpact;
            _controller.SlingLaunched += OnLaunched;
            _controller.PerfectSlingLaunched += OnPerfectLaunched;
            _controller.SlingCanceled += OnCanceled;
            Debug.Log("[Windrunner 5H] Live trace STARTED. Perform normal/perfect/failed/canceled " +
                      "slings, then use menu item 3 to report. No scene changes were made.", target);
        }

        [MenuItem(Root + "3. Report Live Event Trace")]
        public static void ReportLive()
        {
            if (_session == null)
            {
                Debug.LogWarning("[Windrunner 5H] No live trace is active. Use menu item 2 in Play Mode.");
                return;
            }

            string report = _session.Report(40);
            if (_session.ViolationCount > 0)
                Debug.LogError(report);
            else
                Debug.Log(report);
        }

        [MenuItem(Root + "4. Stop Live Event Trace")]
        public static void StopLiveFromMenu() => StopLive(true);

        private static void StopLive(bool report)
        {
            if (report && _session != null)
                ReportLive();

            if (_controller != null)
            {
                _controller.SlingStarted -= OnStarted;
                _controller.SlingCommitted -= OnCommitted;
                _controller.InitialBoostDelivered -= OnInitialBoost;
                _controller.NodeDashStarted -= OnDashStarted;
                _controller.NodeImpact -= OnImpact;
                _controller.SlingLaunched -= OnLaunched;
                _controller.PerfectSlingLaunched -= OnPerfectLaunched;
                _controller.SlingCanceled -= OnCanceled;
            }

            _controller = null;
            _session = null;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                StopLive(true);
        }

        private static AirSlingController ResolveController()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                AirSlingController selectedController = selected.GetComponent<AirSlingController>() ??
                                                         selected.GetComponentInParent<AirSlingController>();
                // Project-window prefab assets are not live scene controllers.
                if (selectedController != null && selectedController.isActiveAndEnabled &&
                    selectedController.gameObject.scene.IsValid())
                    return selectedController;
            }

            AirSlingController[] controllers = UnityEngine.Object.FindObjectsByType<AirSlingController>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            return controllers.Length == 1 ? controllers[0] : null;
        }

        // Instance ID distinguishes separate nodes that share a display name.
        private static string NameOf(AirSlingNode node) => node != null
            ? node.name + " [" + node.GetEntityId() + "]"
            : null;

        private static void Record(SlingTraceEvent kind, AirSlingNode node = null,
            float quality = 0f, bool perfect = false)
        {
            if (_session == null) return;
            try
            {
                _session.Observe(kind, Time.frameCount, Time.time, NameOf(node), quality, perfect);
            }
            catch (Exception exception)
            {
                // A diagnostic subscriber must never interrupt a gameplay event.
                Debug.LogException(exception);
                Debug.LogError("[Windrunner 5H] Trace disabled after unexpected observer error.");
                StopLive(false);
            }
        }

        private static void OnStarted(AirSlingNode node) => Record(SlingTraceEvent.Started, node);
        private static void OnCommitted(AirSlingNode node, float quality, bool perfect) =>
            Record(SlingTraceEvent.Committed, node, quality, perfect);
        private static void OnInitialBoost(AirSlingNode node, float quality) =>
            Record(SlingTraceEvent.InitialBoost, node, quality);
        private static void OnDashStarted(AirSlingNode node) => Record(SlingTraceEvent.DashStarted, node);
        private static void OnImpact(AirSlingNode node) => Record(SlingTraceEvent.Impact, node);
        private static void OnLaunched(AirSlingNode node, float quality, bool perfect) =>
            Record(SlingTraceEvent.Launched, node, quality, perfect);
        private static void OnPerfectLaunched(AirSlingNode node, float quality) =>
            Record(SlingTraceEvent.PerfectLaunched, node, quality);
        private static void OnCanceled() => Record(SlingTraceEvent.Canceled);
    }
}
