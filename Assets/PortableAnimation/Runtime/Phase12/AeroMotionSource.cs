using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PortableAnimation
{
    /// <summary>
    /// 17 direct AnimationClipPlayable inputs into ONE ordinary mixer.
    /// No custom animation jobs are created here. Runtime clip playheads are
    /// controlled explicitly, as in GroundMotionSource.
    /// </summary>
    public sealed class AeroMotionSource
    {
        private readonly AeroMotionProfile profile;
        private readonly AeroMotionController controller;
        private readonly AnimationMixerPlayable mixer;
        private readonly AnimationClipPlayable[] clips;
        private readonly double[] playbackTimes;

        public Playable Output => mixer;
        public AeroMotionState State => controller.State;
        public AeroPose PrimaryPose => controller.PrimaryPose;
        public float StateTime => controller.StateTime;
        public float AirBlend => controller.AirBlend;
        public float Weight(AeroPose pose) => controller.Weight(pose);
        public event Action<AeroMotionSignal> StateChanged
        {
            add { controller.StateChanged += value; }
            remove { controller.StateChanged -= value; }
        }

        public AeroMotionSource(PlayableGraph graph, AeroMotionProfile profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            profile.AssertValid();
            controller = new AeroMotionController(profile);
            int count = (int)AeroPose.Count;
            clips = new AnimationClipPlayable[count];
            playbackTimes = new double[count];
            mixer = AnimationMixerPlayable.Create(graph, count);
            for (int i = 0; i < count; i++)
            {
                clips[i] = AnimationClipPlayable.Create(graph, profile.Clip((AeroPose)i));
                clips[i].SetSpeed(0d);
                clips[i].SetTime(0d);
                if (!graph.Connect(clips[i], 0, mixer, i))
                    throw new InvalidOperationException("Failed to connect AeroMotion sample " + (AeroPose)i);
                mixer.SetInputWeight(i, i == (int)AeroPose.Hover ? 1f : 0f);
            }
        }

        public void Tick(in AeroMotionFrame frame)
        {
            controller.Advance(in frame);
            float dt = Mathf.Max(0f, frame.Ground.Locomotion.DeltaTime);
            for (int i = 0; i < clips.Length; i++)
            {
                AeroPose pose = (AeroPose)i;
                float alpha = controller.Weight(pose);
                mixer.SetInputWeight(i, alpha);
                if (alpha <= 0.0001f) continue;

                AnimationClip clip = profile.Clip(pose);
                double seconds;
                if (IsLooping(pose))
                {
                    playbackTimes[i] += dt * profile.LoopPlaybackRate *
                        (pose == AeroPose.Hover ? 0.7f : 1f);
                    seconds = playbackTimes[i] % clip.length;
                }
                else if (pose == controller.PrimaryPose)
                {
                    float normalized = Mathf.Clamp01(controller.StateTime /
                        Mathf.Max(0.01f, Duration(pose)));
                    seconds = normalized * Math.Max(0.0001d, clip.length - 0.0001d);
                    playbackTimes[i] = seconds;
                }
                else
                    seconds = playbackTimes[i];
                clips[i].SetTime(seconds);
            }
        }

        private bool IsLooping(AeroPose pose)
        {
            switch (pose)
            {
                case AeroPose.Takeoff:
                case AeroPose.Boost:
                case AeroPose.Brake:
                case AeroPose.LandingApproach:
                case AeroPose.Recover:
                    return false;
                default: return true;
            }
        }

        private float Duration(AeroPose pose)
        {
            switch (pose)
            {
                case AeroPose.Takeoff: return profile.TakeoffSeconds;
                case AeroPose.Boost: return profile.BoostSeconds;
                case AeroPose.Brake: return profile.BrakeSeconds;
                case AeroPose.LandingApproach: return profile.LandingApproachSeconds;
                case AeroPose.Recover: return profile.BrakeSeconds;
                default: return 1f;
            }
        }
    }
}
