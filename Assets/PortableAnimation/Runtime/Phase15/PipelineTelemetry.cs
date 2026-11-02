using System;
using System.Collections.Generic;
using UnityEngine;

namespace PortableAnimation
{
    /// <summary>One bounded, allocation-free-on-write sample of the outer evaluation call.</summary>
    [Serializable]
    public struct PipelineFrameSample
    {
        public int frame;
        public int session;
        public double milliseconds;
        public long allocatedBytes;
        public bool graphValid;
        public string scenario;

        public PipelineFrameSample(int frame, int session, double ms,
            long allocation, bool valid, string scenario)
        {
            this.frame = frame;
            this.session = session;
            milliseconds = ms;
            allocatedBytes = allocation;
            graphValid = valid;
            this.scenario = scenario;
        }
    }

    [Serializable]
    public struct PipelineEventSample
    {
        public int frame;
        public string kind;
        public string detail;
        public PipelineEventSample(int frame, string kind, string detail)
        { this.frame = frame; this.kind = kind; this.detail = detail; }
    }

    [Serializable]
    public sealed class PipelineTelemetryReport
    {
        public string capturedAtUtc;
        public string unityVersion;
        public string sceneName;
        public int samples;
        public int validSamples;
        public int sessionsCreated;
        public int sessionsDisposed;
        public int activeSessions;
        public int graphFailures;
        public int longFrames;
        public int gcCollectionDelta0;
        public double averageMs;
        public double p95Ms;
        public double maximumMs;
        public long totalThreadAllocatedBytes;
        public string note;
        public PipelineFrameSample[] frames;
        public PipelineEventSample[] events;
    }

    /// <summary>
    /// Fixed-size ring buffers; snapshot/percentile calculation runs only when requested.
    /// The timing is END-TO-END managed Evaluate wall time (including graph.Evaluate and IK),
    /// not per-job CPU time or a GPU measurement. Allocations count only this thread.
    /// </summary>
    public sealed class PipelineTelemetry
    {
        private readonly PipelineFrameSample[] frames;
        private readonly PipelineEventSample[] events;
        private readonly int warmupFrames;
        private readonly double spikeThresholdMs;
        private int writeFrame, frameCount, writeEvent, eventCount;
        private int lifetimeFrames, failureCount, spikeCount;
        private int firstGc0;
        private long allocatedTotal;
        private double elapsedTotal, maxMs;

        public int Count => frameCount;
        public int FailureCount => failureCount;
        public int SpikeCount => spikeCount;
        public int LifetimeFrames => lifetimeFrames;
        public double ThresholdMs => spikeThresholdMs;
        public int EventCount => eventCount;

        public PipelineTelemetry(int maxFrames, int maxEvents,
            int warmupFrames = 60, double spikeThresholdMs = 8.0)
        {
            frames = new PipelineFrameSample[Mathf.Clamp(maxFrames, 32, 32768)];
            events = new PipelineEventSample[Mathf.Clamp(maxEvents, 8, 2048)];
            this.warmupFrames = Math.Max(0, warmupFrames);
            this.spikeThresholdMs = Math.Max(0.001, spikeThresholdMs);
            firstGc0 = GC.CollectionCount(0);
        }

        public void Reset()
        {
            writeFrame = frameCount = writeEvent = eventCount = 0;
            lifetimeFrames = failureCount = spikeCount = 0;
            allocatedTotal = 0; elapsedTotal = maxMs = 0;
            firstGc0 = GC.CollectionCount(0);
        }

        public void Record(PipelineFrameSample sample)
        {
            frames[writeFrame] = sample;
            writeFrame = (writeFrame + 1) % frames.Length;
            frameCount = Math.Min(frameCount + 1, frames.Length);
            lifetimeFrames++;
            if (!sample.graphValid) failureCount++;
            if (lifetimeFrames <= warmupFrames) return;
            elapsedTotal += sample.milliseconds;
            maxMs = Math.Max(maxMs, sample.milliseconds);
            allocatedTotal += Math.Max(0, sample.allocatedBytes);
            if (sample.milliseconds > spikeThresholdMs) spikeCount++;
        }

        public void Event(int frame, string kind, string detail)
        {
            events[writeEvent] = new PipelineEventSample(frame, kind, detail);
            writeEvent = (writeEvent + 1) % events.Length;
            eventCount = Math.Min(eventCount + 1, events.Length);
        }

        public PipelineFrameSample[] FrameSnapshot()
        {
            var result = new PipelineFrameSample[frameCount];
            int start = (writeFrame - frameCount + frames.Length) % frames.Length;
            for (int i = 0; i < frameCount; i++)
                result[i] = frames[(start + i) % frames.Length];
            return result;
        }

        public PipelineEventSample[] EventSnapshot()
        {
            var result = new PipelineEventSample[eventCount];
            int start = (writeEvent - eventCount + events.Length) % events.Length;
            for (int i = 0; i < eventCount; i++)
                result[i] = events[(start + i) % events.Length];
            return result;
        }

        public PipelineTelemetryReport Snapshot(string sceneName)
        {
            PipelineFrameSample[] samples = FrameSnapshot();
            // Percentiles computed from retained POST-warmup samples only.
            var measurements = new List<double>(samples.Length);
            foreach (var sample in samples)
                if (sample.frame >= 0 && sample.graphValid &&
                    sample.milliseconds >= 0 &&
                    // Frame index in each scenario starts at 0.
                    sample.frame >= warmupFrames)
                    measurements.Add(sample.milliseconds);
            measurements.Sort();
            double sum = 0;
            foreach (var ms in measurements) sum += ms;
            int n = measurements.Count;
            double p95 = n == 0 ? 0 : measurements[Math.Max(0,
                (int)Math.Ceiling(n * 0.95) - 1)];
            return new PipelineTelemetryReport
            {
                capturedAtUtc = DateTime.UtcNow.ToString("o"),
                unityVersion = Application.unityVersion,
                sceneName = sceneName,
                samples = samples.Length,
                validSamples = n,
                graphFailures = failureCount,
                longFrames = spikeCount,
                averageMs = n == 0 ? 0 : sum / n,
                p95Ms = p95,
                maximumMs = n == 0 ? 0 : measurements[n - 1],
                totalThreadAllocatedBytes = allocatedTotal,
                gcCollectionDelta0 = GC.CollectionCount(0) - firstGc0,
                sessionsCreated = PipelineSessionRegistry.Created,
                sessionsDisposed = PipelineSessionRegistry.Disposed,
                activeSessions = PipelineSessionRegistry.Active,
                note = "Wall-clock time of the managed Evaluate call; not total frame time. " +
                    "AllocatedBytes reflects current-thread managed allocations, not NativeArray, " +
                    "job-worker, GPU or other thread memory. See Unity Profiler for stage detail.",
                frames = samples,
                events = EventSnapshot()
            };
        }
    }

    /// <summary>Counts only sessions created by the Phase 15 diagnostic wrapper.</summary>
    public static class PipelineSessionRegistry
    {
        public static int Created { get; private set; }
        public static int Disposed { get; private set; }
        public static int Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        { Created = 0; Disposed = 0; Active = 0; }

        internal static void Register() { Created++; Active++; }
        internal static void Unregister() { Disposed++; Active--; }
    }
}
