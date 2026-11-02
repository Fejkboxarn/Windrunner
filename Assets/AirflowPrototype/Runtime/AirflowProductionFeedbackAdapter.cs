using System;
using UnityEngine;
using Windrunner.Production.Feedback;

namespace AirflowPrototype
{
    /// <summary>
    /// Zero gameplay changes: subscribes to existing Windrunner events and
    /// publishes semantic requests. Legacy systems continue their own playback.
    /// </summary>
    public sealed class AirflowProductionFeedbackAdapter : IDisposable
    {
        private readonly Transform source;
        private readonly IFeedbackService feedback;
        private readonly PlayerMovementVisuals movement;
        private readonly AirCaster caster;
        private readonly AirSlingController sling;
        private readonly AirFlowHitSystem hits;
        private readonly AirPower airPower;
        private bool disposed;

        public AirflowProductionFeedbackAdapter(Transform source, IFeedbackService feedback,
            PlayerMovementVisuals movement, AirCaster caster, AirSlingController sling,
            AirFlowHitSystem hits, AirPower airPower)
        {
            this.source = source;
            this.feedback = feedback;
            this.movement = movement;
            this.caster = caster;
            this.sling = sling;
            this.hits = hits;
            this.airPower = airPower;
            Subscribe();
        }

        private void Emit(FeedbackKey key, Transform target = null, float intensity = 1f,
            bool atTarget = false)
        {
            if (disposed || feedback == null) return;
            Transform origin = source != null ? source : target;
            Vector3 position = atTarget && target != null ? target.position :
                (origin != null ? origin.position : Vector3.zero);
            Quaternion rotation = origin != null ? origin.rotation : Quaternion.identity;
            feedback.Publish(key, new FeedbackRequest(position, rotation,
                source, target, default, intensity));
        }

        private void Subscribe()
        {
            if (movement != null)
            {
                movement.Footstep += OnFootstep;
                movement.JumpStarted += OnJump;
                movement.Landed += OnLand;
                movement.FlipStarted += OnFlip;
            }
            if (caster != null)
            {
                caster.ChargeStarted += OnCastCharge;
                caster.ChargeCanceled += OnCastCanceled;
                caster.PulseReleased += OnCastReleased;
                caster.PulseHit += OnCastHit;
                caster.PulseMissed += OnCastMissed;
                caster.PerfectReleased += OnCastPerfect;
            }
            if (sling != null)
            {
                sling.SlingStarted += OnSlingStarted;
                sling.SlingCommitted += OnSlingCommitted;
                sling.InitialBoostDelivered += OnSlingBoost;
                sling.NodeDashStarted += OnSlingDash;
                sling.NodeImpact += OnSlingImpact;
                sling.SlingLaunched += OnSlingLaunched;
                sling.PerfectSlingLaunched += OnSlingPerfect;
                sling.SlingCanceled += OnSlingCanceled;
            }
            if (hits != null)
            {
                hits.ReturnStarted += OnReturn;
                hits.RewardDelivered += OnReward;
            }
            if (airPower != null)
            {
                airPower.Depleted += OnAirDepleted;
                airPower.RecoveredFromDepletion += OnAirRecovered;
            }
        }

        private void OnFootstep(float intensity) => Emit(FeedbackKey.Footstep, null, intensity);
        private void OnJump() => Emit(FeedbackKey.Jump);
        private void OnLand(float intensity) => Emit(FeedbackKey.Land, null, intensity);
        private void OnFlip(int variant, float intensity) => Emit(FeedbackKey.Flip, null, intensity);
        private void OnCastCharge() => Emit(FeedbackKey.CastChargeStarted);
        private void OnCastCanceled() => Emit(FeedbackKey.CastCanceled);
        private void OnCastReleased(float charge, bool perfect) => Emit(FeedbackKey.CastReleased, null, charge);
        private void OnCastHit(AirNode node, float intensity) =>
            Emit(FeedbackKey.CastHit, node != null ? node.transform : null, intensity, true);
        private void OnCastMissed() => Emit(FeedbackKey.CastMissed);
        private void OnCastPerfect(AirNode node, float intensity) =>
            Emit(FeedbackKey.CastPerfect, node != null ? node.transform : null, intensity, true);
        private void OnSlingStarted(AirSlingNode node) =>
            Emit(FeedbackKey.SlingChargeStarted, node != null ? node.transform : null);
        private void OnSlingCommitted(AirSlingNode node, float quality, bool perfect) =>
            Emit(FeedbackKey.SlingCommitted, node != null ? node.transform : null, Mathf.Max(0.2f, quality));
        private void OnSlingBoost(AirSlingNode node, float intensity) =>
            Emit(FeedbackKey.SlingInitialBoost, node != null ? node.transform : null, intensity);
        private void OnSlingDash(AirSlingNode node) =>
            Emit(FeedbackKey.SlingDashStarted, node != null ? node.transform : null);
        private void OnSlingImpact(AirSlingNode node) =>
            Emit(FeedbackKey.SlingImpact, node != null ? node.transform : null, 1f, true);
        private void OnSlingLaunched(AirSlingNode node, float quality, bool perfect) =>
            Emit(FeedbackKey.SlingLaunched, node != null ? node.transform : null, quality);
        private void OnSlingPerfect(AirSlingNode node, float intensity) =>
            Emit(FeedbackKey.SlingPerfect, node != null ? node.transform : null, intensity);
        private void OnSlingCanceled() => Emit(FeedbackKey.SlingCanceled);
        private void OnReturn(AirNode node, float intensity) =>
            Emit(FeedbackKey.ReturnStarted, node != null ? node.transform : null, intensity, true);
        private void OnReward(AirNode node, float intensity) =>
            Emit(FeedbackKey.RewardDelivered, node != null ? node.transform : null, intensity);
        private void OnAirDepleted() => Emit(FeedbackKey.AirPowerDepleted);
        private void OnAirRecovered() => Emit(FeedbackKey.AirPowerRecovered);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (movement != null)
            {
                movement.Footstep -= OnFootstep;
                movement.JumpStarted -= OnJump;
                movement.Landed -= OnLand;
                movement.FlipStarted -= OnFlip;
            }
            if (caster != null)
            {
                caster.ChargeStarted -= OnCastCharge;
                caster.ChargeCanceled -= OnCastCanceled;
                caster.PulseReleased -= OnCastReleased;
                caster.PulseHit -= OnCastHit;
                caster.PulseMissed -= OnCastMissed;
                caster.PerfectReleased -= OnCastPerfect;
            }
            if (sling != null)
            {
                sling.SlingStarted -= OnSlingStarted;
                sling.SlingCommitted -= OnSlingCommitted;
                sling.InitialBoostDelivered -= OnSlingBoost;
                sling.NodeDashStarted -= OnSlingDash;
                sling.NodeImpact -= OnSlingImpact;
                sling.SlingLaunched -= OnSlingLaunched;
                sling.PerfectSlingLaunched -= OnSlingPerfect;
                sling.SlingCanceled -= OnSlingCanceled;
            }
            if (hits != null)
            {
                hits.ReturnStarted -= OnReturn;
                hits.RewardDelivered -= OnReward;
            }
            if (airPower != null)
            {
                airPower.Depleted -= OnAirDepleted;
                airPower.RecoveredFromDepletion -= OnAirRecovered;
            }
        }
    }
}
