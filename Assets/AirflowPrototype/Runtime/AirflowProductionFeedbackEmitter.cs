using UnityEngine;
using Windrunner.Production.Feedback;

namespace AirflowPrototype
{
    /// <summary>Serialized legacy component references; regular C# adapter owns subscriptions.</summary>
    [DefaultExecutionOrder(-8000)]
    [DisallowMultipleComponent]
    public sealed class AirflowProductionFeedbackEmitter : MonoBehaviour
    {
        [SerializeField] private PlayerMovementVisuals movement;
        [SerializeField] private AirCaster caster;
        [SerializeField] private AirSlingController sling;
        [SerializeField] private AirFlowHitSystem hits;
        [SerializeField] private AirPower airPower;

        private AirflowProductionFeedbackAdapter adapter;
        private IFeedbackService boundFeedback;
        private bool hasFeedbackBinding;

        public void BindFeedbackService(IFeedbackService service)
        {
            StopObserving();
            hasFeedbackBinding = true;
            boundFeedback = service;
            if (isActiveAndEnabled && Application.isPlaying) StartObserving();
        }

        public void Configure(PlayerMovementVisuals newMovement, AirCaster newCaster,
            AirSlingController newSling, AirFlowHitSystem newHits, AirPower newPower)
        {
            StopObserving();
            movement = newMovement;
            caster = newCaster;
            sling = newSling;
            hits = newHits;
            airPower = newPower;
            if (isActiveAndEnabled && Application.isPlaying) StartObserving();
        }

        private void Awake() => ResolveReferences();
        private void OnEnable() => StartObserving();
        private void Start()
        {
            // Covers unusual scene load ordering where ProjectRoot became active late.
            if (adapter == null) StartObserving();
        }
        private void OnDisable() => StopObserving();
        private void OnDestroy() => StopObserving();

        private void ResolveReferences()
        {
            if (movement == null) movement = GetComponent<PlayerMovementVisuals>();
            if (caster == null) caster = GetComponent<AirCaster>();
            if (sling == null) sling = GetComponent<AirSlingController>();
            if (hits == null) hits = GetComponent<AirFlowHitSystem>();
            if (airPower == null) airPower = GetComponent<AirPower>();
        }

        private void StartObserving()
        {
            if (adapter != null) return;
            ResolveReferences();
            IFeedbackService service = boundFeedback;
            if (!hasFeedbackBinding)
            {
                WindrunnerFeedbackHost host = WindrunnerFeedbackHost.Active;
                service = host != null ? host.Service : null;
            }
            if (service == null) return;
            adapter = new AirflowProductionFeedbackAdapter(transform, service,
                movement, caster, sling, hits, airPower, GetComponent<AirDash>());
        }

        private void StopObserving()
        {
            adapter?.Dispose();
            adapter = null;
        }
    }
}
