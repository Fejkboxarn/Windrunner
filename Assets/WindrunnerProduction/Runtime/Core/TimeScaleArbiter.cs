using System;
using System.Collections.Generic;

namespace Windrunner.Production.Core
{
    /// <summary>Engine-independent ownership of overlapping pause/slowdown requests.</summary>
    public sealed class TimeScaleArbiter
    {
        private readonly List<Request> requests = new List<Request>();
        public float BaselineScale { get; }
        public float BaselineFixedStep { get; }
        public int RequestCount => requests.Count;
        public event Action Changed;
        public bool IsPaused
        {
            get { foreach (var request in requests) if (request.IsPause) return true; return false; }
        }
        public float UnpausedScale
        {
            get
            {
                float value = BaselineScale;
                foreach (var request in requests) if (!request.IsPause) value = Math.Min(value, request.Scale);
                return value;
            }
        }
        public float EffectiveScale => IsPaused ? 0f : UnpausedScale;
        // Pause must never set fixedDeltaTime to zero. Preserve the underlying
        // slowdown's ratio so resuming does not change physics integration.
        public float EffectiveFixedStep => BaselineScale > 0.0001f
            ? BaselineFixedStep * (UnpausedScale / BaselineScale) : BaselineFixedStep;

        public TimeScaleArbiter(float scale, float fixedStep)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < 0f) throw new ArgumentOutOfRangeException(nameof(scale));
            if (float.IsNaN(fixedStep) || float.IsInfinity(fixedStep) || fixedStep <= 0f) throw new ArgumentOutOfRangeException(nameof(fixedStep));
            BaselineScale = scale;
            BaselineFixedStep = fixedStep;
        }

        public Request Pause() => Add(true, 0f);
        public Request Slowdown(float scale) => Add(false, scale);
        private Request Add(bool pause, float scale)
        {
            var request = new Request(this, pause, scale);
            requests.Add(request);
            Changed?.Invoke();
            return request;
        }

        public sealed class Request : IDisposable
        {
            private TimeScaleArbiter owner;
            internal bool IsPause { get; }
            internal float Scale { get; private set; }
            internal Request(TimeScaleArbiter owner, bool pause, float scale)
            {
                this.owner = owner;
                IsPause = pause;
                Scale = pause ? 0f : Validate(scale);
            }
            private static float Validate(float scale)
            {
                if (float.IsNaN(scale) || float.IsInfinity(scale)) throw new ArgumentOutOfRangeException(nameof(scale));
                return Math.Max(0.01f, scale);
            }
            public void SetScale(float scale)
            {
                if (owner == null || IsPause) return;
                Scale = Validate(scale);
                owner.Changed?.Invoke();
            }
            public void Dispose()
            {
                if (owner == null) return;
                var previous = owner;
                owner = null;
                previous.requests.Remove(this);
                previous.Changed?.Invoke();
            }
        }
    }
}
