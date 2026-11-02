using System;
using UnityEngine;

namespace Windrunner.Production.Feedback
{
    // Project-neutral semantic vocabulary. No tower-defense mechanics here.
    public enum FeedbackKey
    {
        None = 0,
        Footstep = 10, Jump = 11, Land = 12, Flip = 13,
        CastChargeStarted = 20, CastCanceled = 21, CastReleased = 22,
        CastHit = 23, CastMissed = 24, CastPerfect = 25,
        ReturnStarted = 30, RewardDelivered = 31,
        SlingChargeStarted = 40, SlingCommitted = 41, SlingCanceled = 42,
        SlingDashStarted = 43, SlingImpact = 44, SlingLaunched = 45,
        SlingPerfect = 46, SlingInitialBoost = 47,
        AirPowerDepleted = 50, AirPowerRecovered = 51
    }

    public enum FeedbackCategory
    {
        General, Audio, Vfx, CameraShake, Haptics, Flash, HitStop, PostProcessing
    }

    public enum FeedbackPoolOverflow { RejectNewest, StealOldest }

    public readonly struct FeedbackRequest
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Direction;
        public readonly Transform Source;
        public readonly Transform Target;
        public readonly float Intensity;

        public FeedbackRequest(Vector3 position, Quaternion rotation,
            Transform source = null, Transform target = null,
            Vector3 direction = default, float intensity = 1f)
        {
            Position = position;
            Rotation = rotation;
            Source = source;
            Target = target;
            Direction = direction;
            Intensity = Mathf.Max(0f, intensity);
        }

        public static FeedbackRequest At(Transform source, Transform target = null,
            float intensity = 1f)
        {
            return new FeedbackRequest(source != null ? source.position : Vector3.zero,
                source != null ? source.rotation : Quaternion.identity,
                source, target, default, intensity);
        }
    }

    public interface IFeedbackService
    {
        event Action<FeedbackKey, FeedbackRequest> Observed;
        void Publish(FeedbackKey key, FeedbackRequest request);
        int ObservedCount { get; }
        int PlayedCount { get; }
    }

    /// <summary>Implementation may live in an assembly with FEEL installed.</summary>
    public interface IFeedbackRecipeBackend : IDisposable
    {
        void Prewarm(FeedbackRecipeEntry recipe);
        void Play(FeedbackRecipeEntry recipe, FeedbackRequest request, float intensity);
        void Tick();
    }

    /// <summary>
    /// Read-only counters from a FEEL backend. Unlike FeedbackService.PlayedCount,
    /// Started counts actual backend playback calls, not merely accepted events.
    /// </summary>
    public readonly struct FeedbackBackendDiagnostics
    {
        public readonly int Requests;
        public readonly int Started;
        public readonly int Created;
        public readonly int Prewarmed;
        public readonly int Reused;
        public readonly int Rejected;
        public readonly int Stolen;
        public readonly int Released;
        public readonly int Active;
        public readonly int PeakActive;
        public readonly int PlayerPlayCount;
        public readonly float LastIntensity;
        public readonly Vector3 LastPosition;

        public FeedbackBackendDiagnostics(int requests, int started, int created,
            int prewarmed, int reused, int rejected, int stolen, int released,
            int active, int peakActive, int playerPlayCount, float lastIntensity,
            Vector3 lastPosition)
        {
            Requests = requests;
            Started = started;
            Created = created;
            Prewarmed = prewarmed;
            Reused = reused;
            Rejected = rejected;
            Stolen = stolen;
            Released = released;
            Active = active;
            PeakActive = peakActive;
            PlayerPlayCount = playerPlayCount;
            LastIntensity = lastIntensity;
            LastPosition = lastPosition;
        }

        public override string ToString()
        {
            return "requests=" + Requests + ", started=" + Started +
                   ", created=" + Created + ", prewarmed=" + Prewarmed +
                   ", reused=" + Reused + ", rejected=" + Rejected +
                   ", stolen=" + Stolen + ", released=" + Released +
                   ", active=" + Active + ", peak=" + PeakActive +
                   ", FEEL Player.PlayCount=" + PlayerPlayCount +
                   ", last intensity=" + LastIntensity.ToString("F2") +
                   ", last position=" + LastPosition;
        }
    }

    public interface IFeedbackRecipeDiagnostics
    {
        FeedbackBackendDiagnostics Snapshot();
    }
}
