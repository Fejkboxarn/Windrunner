using System;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;

namespace PortableAnimation
{
    /// <summary>
    /// Composition-only diagnostic wrapper. Does not change any earlier animation
    /// graph, job, or input stream. Owns exactly one WeaponPosePipelineRuntime.
    /// </summary>
    public sealed class PipelineDiagnosticSession : IDisposable
    {
        private static readonly ProfilerMarker EvaluationMarker =
            new ProfilerMarker("PortableAnimation.Phase15.FullEvaluate");
        private readonly WeaponPosePipelineRuntime pipeline;
        private readonly PipelineTelemetry telemetry;
        private readonly int generation;
        private bool disposed;
        private int lastFrame = -1;
        private int sessionFrame;

        public WeaponPosePipelineRuntime Runtime => pipeline;
        public bool Valid => !disposed && pipeline != null && pipeline.IsValid;
        public int Generation => generation;

        public PipelineDiagnosticSession(Animator animator, PostPoseIKProfile ikProfile,
            PipelineTelemetry telemetry, int generation)
        {
            this.telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            if (animator == null || ikProfile == null)
                throw new ArgumentNullException("Animator or Phase 14 IK profile missing.");
            ikProfile.AssertValid();
            var extension = ikProfile.PreviousPipeline;
            var aero = extension.PreviousPipeline;
            var ground = aero.PreviousPipeline;
            var weapon = ground.PreviousPipeline;
            pipeline = new WeaponPosePipelineRuntime(
                animator, weapon, ground, aero, extension, ikProfile);
            this.generation = generation;
            PipelineSessionRegistry.Register();
            telemetry.Event(0, "SessionOpened", "generation=" + generation);
        }

        public void Evaluate(in AimOffsetFrame frame,
            in GroundMotionFrame ground, in AeroMotionFrame aero, string scenario)
        {
            if (disposed || pipeline == null)
                throw new ObjectDisposedException(nameof(PipelineDiagnosticSession));
            if (lastFrame == frame.Locomotion.FrameId)
                throw new InvalidOperationException(
                    "Phase 15 attempted to evaluate the same frame twice.");
            if (lastFrame > frame.Locomotion.FrameId)
                throw new InvalidOperationException(
                    "Phase 15 received non-monotonic frame IDs.");
            if (float.IsNaN(frame.Locomotion.DeltaTime) ||
                float.IsInfinity(frame.Locomotion.DeltaTime))
                throw new ArgumentException("Invalid delta time.");
            lastFrame = frame.Locomotion.FrameId;
            bool valid = false;
            long allocationsBefore = GC.GetAllocatedBytesForCurrentThread();
            long began = Stopwatch.GetTimestamp();
            try
            {
                using (EvaluationMarker.Auto())
                    pipeline.Evaluate(in frame, in ground, in aero);
                valid = pipeline.IsValid;
            }
            finally
            {
                long elapsed = Stopwatch.GetTimestamp() - began;
                long allocated = GC.GetAllocatedBytesForCurrentThread() - allocationsBefore;
                double ms = elapsed * 1000.0 / Stopwatch.Frequency;
                telemetry.Record(new PipelineFrameSample(
                    sessionFrame++, generation, ms,
                    Math.Max(0, allocated), valid, scenario));
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { pipeline?.Dispose(); }
            finally
            {
                PipelineSessionRegistry.Unregister();
                telemetry.Event(Math.Max(0, lastFrame),
                    "SessionDisposed", "generation=" + generation);
            }
        }
    }
}
