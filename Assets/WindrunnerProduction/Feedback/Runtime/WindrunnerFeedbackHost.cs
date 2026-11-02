using UnityEngine;
using Windrunner.Production.Unity;

namespace Windrunner.Production.Feedback
{
    /// <summary>Scene composition data and Unity lifecycle only; logic lives in plain classes.</summary>
    [DefaultExecutionOrder(-8500)]
    [DisallowMultipleComponent]
    public sealed class WindrunnerFeedbackHost : MonoBehaviour
    {
        [SerializeField] private FeedbackCatalog catalog;
        [SerializeField] private MonoBehaviour feelBackendComponent;
        [SerializeField, Range(1, 128)] private int maximumAudioVoices = 24;
        [SerializeField] private bool logObservedEvents;

        public static WindrunnerFeedbackHost Active { get; private set; }
        public FeedbackService Service { get; private set; }
        public FeedbackCatalog Catalog => catalog;
        public MonoBehaviour FeelBackendComponent => feelBackendComponent;
        public FeedbackSettingsService Settings { get; private set; }

        public void Configure(FeedbackCatalog newCatalog, MonoBehaviour backend)
        {
            catalog = newCatalog;
            feelBackendComponent = backend;
        }

        [ContextMenu("Report Feedback Counters")]
        public void ReportFeedbackCounters()
        {
            if (Service == null)
            {
                Debug.Log("[Windrunner Feedback] Play Mode host not running yet.", this);
                return;
            }
            Debug.Log("[Windrunner Feedback] Observed=" + Service.ObservedCount +
                      ", production played=" + Service.PlayedCount, this);
            if (feelBackendComponent is IFeedbackRecipeDiagnostics diagnostics)
                Debug.Log("[Windrunner FEEL Pool] " + diagnostics.Snapshot(), this);
        }

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Debug.LogError("[Windrunner Feedback] Duplicate host disabled.", this);
                enabled = false;
                return;
            }

            Active = this;
            Settings = new FeedbackSettingsService();
            IFeedbackRecipeBackend feel = feelBackendComponent as IFeedbackRecipeBackend;
            if (feelBackendComponent != null && feel == null)
                Debug.LogError("[Windrunner Feedback] Assigned FEEL backend does not implement interface.", this);

            Service = new FeedbackService(catalog, Settings,
                new FeedbackAudioPool(transform, maximumAudioVoices),
                new FeedbackVfxPool(transform), feel, logObservedEvents);

            if (ProjectRoot.Active != null && ProjectRoot.Active.Runtime != null)
                ProjectRoot.Active.Runtime.Services.Register<IFeedbackService>(Service);
            else
                Debug.LogWarning("[Windrunner Feedback] No ProjectRoot registry found.", this);
        }

        private void Update() => Service?.Tick();

        private void OnDestroy()
        {
            if (Active != this) return;
            Service?.Dispose();
            Service = null;
            Settings = null;
            Active = null;
        }
    }
}
