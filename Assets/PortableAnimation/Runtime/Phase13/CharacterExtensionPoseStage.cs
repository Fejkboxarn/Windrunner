using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PortableAnimation
{
    /// <summary>
    /// One native AnimationMixerPlayable: existing locomotion is input 0,
    /// hero-specific pose clips are inputs 1..N. No custom animation job,
    /// no additional Animator-stream bindings or rig transforms.
    /// </summary>
    public sealed class CharacterExtensionPoseStage : IDisposable
    {
        private readonly AnimationMixerPlayable mixer;
        private readonly AnimationClipPlayable[] clips;
        private readonly CharacterExtensionController controller;
        private readonly CharacterExtensionProfile profile;

        public Playable Output => mixer;
        public CharacterExtensionController Controller => controller;
        public int EntryCount => clips.Length;

        public CharacterExtensionPoseStage(PlayableGraph graph,
            Playable incoming, CharacterExtensionProfile profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            profile.AssertValid();
            controller = new CharacterExtensionController(profile);
            clips = new AnimationClipPlayable[profile.Entries.Count];
            mixer = AnimationMixerPlayable.Create(graph, clips.Length + 1);
            if (!graph.Connect(incoming, 0, mixer, 0))
                throw new InvalidOperationException("Character extension base connection failed.");
            mixer.SetInputWeight(0, 1f);
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = AnimationClipPlayable.Create(graph, profile.Entries[i].Clip);
                clips[i].SetSpeed(0d);
                clips[i].SetTime(0d);
                if (!graph.Connect(clips[i], 0, mixer, i + 1))
                    throw new InvalidOperationException(
                        "Character extension connection failed: " + profile.Entries[i].Id);
                mixer.SetInputWeight(i + 1, 0f);
            }
        }

        public void Tick(float dt, bool grounded, bool flight, bool blocked)
        {
            controller.Advance(dt, grounded, flight, blocked);
            mixer.SetInputWeight(0, controller.BaseWeight);
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].SetTime(controller.ClipTime(i));
                mixer.SetInputWeight(i + 1, controller.Weight(i));
            }
        }

        public void Dispose()
        {
            // PlayableGraph owns the mixer and clip playables. No NativeArrays.
        }
    }
}
