using System;
using UnityEngine;

namespace PortableAnimation
{
    [CreateAssetMenu(menuName="Portable Animation/Phase 15 Diagnostics Profile",
        fileName="PipelineStressProfile")]
    public sealed class PipelineStressProfile : ScriptableObject
    {
        [SerializeField] private PostPoseIKProfile previousPipeline;
        [SerializeField, Min(60)] private int stressFrames = 1800;
        [SerializeField, Range(0, 300)] private int warmupFrames = 90;
        [SerializeField, Min(32)] private int sampleCapacity = 4096;
        [SerializeField, Min(8)] private int eventCapacity = 512;
        [SerializeField, Min(0.1f)] private float spikeThresholdMs = 8f;
        [SerializeField] private int deterministicSeed = 1337;
        [SerializeField] private bool enableAimOffset;
        [SerializeField] private bool enablePostPoseIK = true;
        [SerializeField] private bool runAutomatically = true;
        [SerializeField] private bool allowAutomatedLifecycleRestarts;
        [SerializeField, Min(120)] private int framesPerRestart = 450;

        public PostPoseIKProfile PreviousPipeline => previousPipeline;
        public int StressFrames => stressFrames;
        public int WarmupFrames => warmupFrames;
        public int SampleCapacity => sampleCapacity;
        public int EventCapacity => eventCapacity;
        public float SpikeThresholdMs => spikeThresholdMs;
        public int Seed => deterministicSeed;
        public bool EnableAimOffset => enableAimOffset;
        public bool EnableIK => enablePostPoseIK;
        public bool RunAutomatically => runAutomatically;
        public bool AllowRestarts => allowAutomatedLifecycleRestarts;
        public int FramesPerRestart => framesPerRestart;

        public void Configure(PostPoseIKProfile previous) { previousPipeline = previous; }
        public void AssertValid()
        {
            if (previousPipeline == null)
                throw new InvalidOperationException("Phase 15 requires a Phase 14 IK profile.");
            previousPipeline.AssertValid();
            if (stressFrames < 60 || sampleCapacity < 32 || eventCapacity < 8 ||
                warmupFrames >= stressFrames || framesPerRestart < 120 ||
                spikeThresholdMs <= 0f)
                throw new InvalidOperationException("Invalid diagnostics sample settings.");
        }
    }
}
