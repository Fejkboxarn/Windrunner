using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PortableAnimation
{
    /// <summary>
    /// One animation job processes multiple overlay clips without nesting
    /// another AnimationScriptPlayable for each clip. This is a Phase 10
    /// stabilization component, replacing deeply recursive job chains.
    /// Each channel uses the SAME ordered Lerp/Slerp math as the old chain.
    /// The testbed has no root-motion animation; root motion is deliberately
    /// unsupported by this job until a separate shallow pass is implemented.
    /// </summary>
    public struct FlattenedPoseStackJob : IAnimationJob
    {
        public NativeArray<TransformStreamHandle> handles;
        public NativeArray<float> positionWeights;
        public NativeArray<float> rotationWeights;
        public NativeArray<float> scaleWeights;
        public NativeArray<Vector3> workingPositions;
        public NativeArray<Quaternion> workingRotations;
        public NativeArray<Vector3> workingScales;
        public int overlayCount;

        public void ProcessRootMotion(AnimationStream stream)
        {
            // Important: Do NOT call GetInputStream here. In the original
            // Batch 10 graph, nested PerBoneMixJob.ProcessRootMotion calls
            // re-entered each other deeply and crashed Unity on Play.
            // The testbed uses in-place clips and Animator.applyRootMotion=false.
            stream.velocity = Vector3.zero;
            stream.angularVelocity = Vector3.zero;
        }

        public void ProcessAnimation(AnimationStream stream)
        {
            int count = handles.Length;
            if (stream.inputStreamCount < overlayCount + 1)
                return;

            // Process the incoming composed pose once.
            var input = stream.GetInputStream(0);
            if (!input.isValid)
                return;

            for (int bone = 0; bone < count; bone++)
            {
                var handle = handles[bone];
                workingPositions[bone] = handle.GetLocalPosition(input);
                workingRotations[bone] = handle.GetLocalRotation(input);
                workingScales[bone] = handle.GetLocalScale(input);
            }

            // Overlay clips are all direct input children of this one job.
            // They never add another nested custom script-playable level.
            for (int overlay = 0; overlay < overlayCount; overlay++)
            {
                int offset = overlay * count;
                bool anyWeight = false;
                for (int bone = 0; bone < count; bone++)
                {
                    int index = offset + bone;
                    if (positionWeights[index] > 0f ||
                        rotationWeights[index] > 0f ||
                        scaleWeights[index] > 0f)
                    {
                        anyWeight = true;
                        break;
                    }
                }

                if (!anyWeight)
                    continue;

                var pose = stream.GetInputStream(overlay + 1);
                if (!pose.isValid)
                    continue;

                for (int bone = 0; bone < count; bone++)
                {
                    int index = offset + bone;
                    var handle = handles[bone];
                    float p = Mathf.Clamp01(positionWeights[index]);
                    float q = Mathf.Clamp01(rotationWeights[index]);
                    float s = Mathf.Clamp01(scaleWeights[index]);
                    if (p > 0f)
                    {
                        workingPositions[bone] = Vector3.Lerp(
                            workingPositions[bone],
                            handle.GetLocalPosition(pose), p);
                    }
                    if (q > 0f)
                    {
                        workingRotations[bone] = Quaternion.Slerp(
                            workingRotations[bone],
                            handle.GetLocalRotation(pose), q);
                    }
                    if (s > 0f)
                    {
                        workingScales[bone] = Vector3.Lerp(
                            workingScales[bone],
                            handle.GetLocalScale(pose), s);
                    }
                }
            }

            for (int bone = 0; bone < count; bone++)
            {
                var handle = handles[bone];
                handle.SetLocalPosition(stream, workingPositions[bone]);
                handle.SetLocalRotation(stream, workingRotations[bone]);
                handle.SetLocalScale(stream, workingScales[bone]);
            }
        }
    }

    /// <summary>
    /// Owns a single multi-input AnimationScriptPlayable and its native data.
    /// The overlay pose clips can be set to manual time for montages/HeroState.
    /// </summary>
    public sealed class FlattenedPoseStack : IDisposable
    {
        private readonly Dictionary<string, int> boneIndices =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private NativeArray<TransformStreamHandle> handles;
        private NativeArray<float> positions;
        private NativeArray<float> rotations;
        private NativeArray<float> scales;
        private NativeArray<Vector3> scratchPositions;
        private NativeArray<Quaternion> scratchRotations;
        private NativeArray<Vector3> scratchScales;
        private AnimationScriptPlayable playable;
        private AnimationClipPlayable[] clips;
        private double[] clipLengths;
        private bool alive;

        public Playable Output => playable;
        public int OverlayCount => clips == null ? 0 : clips.Length;
        public int BoneCount => boneIndices.Count;

        public FlattenedPoseStack(
            PlayableGraph graph,
            Animator animator,
            Playable incoming,
            AnimationClip[] overlayClips,
            bool externallyTimed)
        {
            if (animator == null)
                throw new ArgumentNullException(nameof(animator));
            if (overlayClips == null || overlayClips.Length == 0)
                throw new ArgumentException("At least one pose clip is required.");

            try
            {
                Transform root = animator.transform;
                Transform[] hierarchy = root.GetComponentsInChildren<Transform>(true);
                int boneCount = hierarchy.Length - 1;
                if (boneCount < 1)
                    throw new InvalidOperationException("Animator has no child bones.");

                handles = new NativeArray<TransformStreamHandle>(
                    boneCount, Allocator.Persistent);
                positions = new NativeArray<float>(
                    boneCount * overlayClips.Length, Allocator.Persistent);
                rotations = new NativeArray<float>(
                    boneCount * overlayClips.Length, Allocator.Persistent);
                scales = new NativeArray<float>(
                    boneCount * overlayClips.Length, Allocator.Persistent);
                scratchPositions = new NativeArray<Vector3>(
                    boneCount, Allocator.Persistent);
                scratchRotations = new NativeArray<Quaternion>(
                    boneCount, Allocator.Persistent);
                scratchScales = new NativeArray<Vector3>(
                    boneCount, Allocator.Persistent);

                for (int i = 0; i < boneCount; i++)
                {
                    Transform bone = hierarchy[i + 1];
                    boneIndices.Add(PathOf(root, bone), i);
                    handles[i] = animator.BindStreamTransform(bone);
                }

                var job = new FlattenedPoseStackJob
                {
                    handles = handles,
                    positionWeights = positions,
                    rotationWeights = rotations,
                    scaleWeights = scales,
                    workingPositions = scratchPositions,
                    workingRotations = scratchRotations,
                    workingScales = scratchScales,
                    overlayCount = overlayClips.Length
                };

                playable = AnimationScriptPlayable.Create(
                    graph, job, 1 + overlayClips.Length);
                playable.SetProcessInputs(false);

                if (!graph.Connect(incoming, 0, playable, 0))
                    throw new InvalidOperationException("Unable to connect source pose.");
                playable.SetInputWeight(0, 1f);

                clips = new AnimationClipPlayable[overlayClips.Length];
                clipLengths = new double[overlayClips.Length];
                for (int i = 0; i < overlayClips.Length; i++)
                {
                    AnimationClip asset = overlayClips[i];
                    if (asset == null || asset.length <= 0f)
                        throw new InvalidOperationException("Missing/empty overlay clip at " + i);
                    clips[i] = AnimationClipPlayable.Create(graph, asset);
                    clipLengths[i] = asset.length;
                    if (externallyTimed)
                    {
                        clips[i].SetSpeed(0d);
                        clips[i].SetTime(0d);
                    }
                    if (!graph.Connect(clips[i], 0, playable, i + 1))
                        throw new InvalidOperationException("Unable to connect overlay clip " + i);
                    playable.SetInputWeight(i + 1, 1f);
                }

                alive = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool SetBoneWeights(int overlay, string path, BoneChannelWeights weight)
        {
            if (!alive || overlay < 0 || overlay >= OverlayCount ||
                !boneIndices.TryGetValue(path, out int bone))
                return false;
            int flat = overlay * boneIndices.Count + bone;
            positions[flat] = Mathf.Clamp01(weight.position);
            rotations[flat] = Mathf.Clamp01(weight.rotation);
            scales[flat] = Mathf.Clamp01(weight.scale);
            return true;
        }

        public void SetSampleTime(int overlay, double seconds)
        {
            if (!alive || overlay < 0 || overlay >= OverlayCount)
                return;
            // The generated test clips loop: never sample the wrapped end.
            double max = Math.Max(0d, clipLengths[overlay] - 0.0001d);
            clips[overlay].SetTime(Math.Max(0d, Math.Min(seconds, max)));
        }

        public void Dispose()
        {
            alive = false;
            if (handles.IsCreated) handles.Dispose();
            if (positions.IsCreated) positions.Dispose();
            if (rotations.IsCreated) rotations.Dispose();
            if (scales.IsCreated) scales.Dispose();
            if (scratchPositions.IsCreated) scratchPositions.Dispose();
            if (scratchRotations.IsCreated) scratchRotations.Dispose();
            if (scratchScales.IsCreated) scratchScales.Dispose();
            boneIndices.Clear();
        }

        private static string PathOf(Transform root, Transform bone)
        {
            var pieces = new Stack<string>();
            while (bone != null && bone != root)
            {
                pieces.Push(bone.name);
                bone = bone.parent;
            }
            if (bone != root)
                throw new InvalidOperationException("Bone outside Animator hierarchy.");
            return string.Join("/", pieces.ToArray());
        }
    }
}
