using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PortableAnimation
{
    /// <summary>
    /// A direction-selectable four-pose additive stage. Input 0 is the
    /// already composed pose; inputs 1 and 2 are directional reaction
    /// and neutral-reference streams. The actual transform processing
    /// reuses Batch 4's validated AdditivePoseJob.
    /// </summary>
    public sealed class HitReactPoseStage : IDisposable
    {
        private NativeArray<TransformStreamHandle> handles;
        private NativeArray<int> parentIndices;
        private NativeArray<float> positionWeights;
        private NativeArray<float> rotationWeights;
        private NativeArray<float> scaleWeights;
        private NativeArray<Quaternion> reactionComponents;
        private NativeArray<Quaternion> referenceComponents;
        private NativeArray<Quaternion> resultComponents;

        private AnimationMixerPlayable directions;
        private AnimationScriptPlayable additive;
        private bool isConnected;

        public Playable Output => additive;
        public int BoneCount => handles.IsCreated ? handles.Length : 0;

        public HitReactPoseStage(PlayableGraph graph, Animator animator,
            Playable incomingPose, AnimationClip[] directionalClips,
            AnimationClip neutral, BoneWeightMask mask,
            AdditiveRotationMode rotationMode)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            if (mask == null) throw new ArgumentNullException(nameof(mask));
            if (neutral == null) throw new ArgumentNullException(nameof(neutral));
            if (directionalClips == null || directionalClips.Length != 4)
                throw new ArgumentException("A hit stage requires four directional clips.");

            Transform root = animator.transform;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            int count = all.Length - 1;
            if (count <= 0) throw new InvalidOperationException("HitReact rig has no descendants.");

            try
            {
                handles = new NativeArray<TransformStreamHandle>(count, Allocator.Persistent);
                parentIndices = new NativeArray<int>(count, Allocator.Persistent);
                positionWeights = new NativeArray<float>(count, Allocator.Persistent);
                rotationWeights = new NativeArray<float>(count, Allocator.Persistent);
                scaleWeights = new NativeArray<float>(count, Allocator.Persistent);
                reactionComponents = new NativeArray<Quaternion>(count, Allocator.Persistent);
                referenceComponents = new NativeArray<Quaternion>(count, Allocator.Persistent);
                resultComponents = new NativeArray<Quaternion>(count, Allocator.Persistent);

                var indexOf = new Dictionary<Transform, int>(count);
                for (int i = 0; i < count; i++)
                {
                    Transform bone = all[i + 1];
                    handles[i] = animator.BindStreamTransform(bone);
                    parentIndices[i] = bone.parent == root ? -1 : indexOf[bone.parent];
                    indexOf.Add(bone, i);

                    string path = RelativePath(root, bone);
                    BoneChannelWeights weights = mask.Resolve(path);
                    positionWeights[i] = Mathf.Clamp01(weights.position);
                    rotationWeights[i] = Mathf.Clamp01(weights.rotation);
                    scaleWeights[i] = Mathf.Clamp01(weights.scale);
                }

                directions = AnimationMixerPlayable.Create(graph, 4);
                for (int i = 0; i < 4; i++)
                {
                    if (directionalClips[i] == null)
                        throw new ArgumentException("Missing directional clip " + i);

                    var pose = AnimationClipPlayable.Create(graph, directionalClips[i]);
                    pose.SetSpeed(0d);
                    pose.SetTime(0d);
                    if (!graph.Connect(pose, 0, directions, i))
                        throw new InvalidOperationException("Cannot attach hit direction " + i);
                    directions.SetInputWeight(i, i == 0 ? 1f : 0f);
                }

                var reference = AnimationClipPlayable.Create(graph, neutral);
                reference.SetSpeed(0d);
                reference.SetTime(0d);

                var job = new AdditivePoseJob
                {
                    handles = handles,
                    parentIndices = parentIndices,
                    positionWeights = positionWeights,
                    rotationWeights = rotationWeights,
                    scaleWeights = scaleWeights,
                    overlayComponents = reactionComponents,
                    referenceComponents = referenceComponents,
                    resultComponents = resultComponents,
                    rotationMode = (int)rotationMode,
                    strength = 0f
                };

                additive = AnimationScriptPlayable.Create(graph, job, 3);
                additive.SetProcessInputs(false);
                if (!graph.Connect(incomingPose, 0, additive, 0) ||
                    !graph.Connect(directions, 0, additive, 1) ||
                    !graph.Connect(reference, 0, additive, 2))
                    throw new InvalidOperationException("Cannot connect HitReact additive stage.");

                additive.SetInputWeight(0, 1f);
                additive.SetInputWeight(1, 1f);
                additive.SetInputWeight(2, 1f);
                isConnected = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void SetState(HitReactController controller, bool head)
        {
            if (!isConnected) return;
            for (int i = 0; i < 4; i++)
                directions.SetInputWeight(i, controller.DirectionWeight(i));

            var job = additive.GetJobData<AdditivePoseJob>();
            job.strength = head ? controller.HeadWeight : controller.BodyWeight;
            additive.SetJobData(job);
        }

        public void Dispose()
        {
            isConnected = false;
            if (handles.IsCreated) handles.Dispose();
            if (parentIndices.IsCreated) parentIndices.Dispose();
            if (positionWeights.IsCreated) positionWeights.Dispose();
            if (rotationWeights.IsCreated) rotationWeights.Dispose();
            if (scaleWeights.IsCreated) scaleWeights.Dispose();
            if (reactionComponents.IsCreated) reactionComponents.Dispose();
            if (referenceComponents.IsCreated) referenceComponents.Dispose();
            if (resultComponents.IsCreated) resultComponents.Dispose();
        }

        private static string RelativePath(Transform root, Transform target)
        {
            var names = new Stack<string>();
            while (target != null && target != root)
            {
                names.Push(target.name);
                target = target.parent;
            }
            if (target != root) throw new ArgumentException("HitReact bone is outside Animator root.");
            return string.Join("/", names.ToArray());
        }
    }
}
