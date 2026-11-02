using System;
using UnityEngine;
namespace PortableAnimation
{
    [CreateAssetMenu(menuName="Portable Animation/Phase 3 Per-Bone Pipeline")]
    public sealed class PerBonePipelineProfile : ScriptableObject
    {
        [SerializeField] private PosePipelineProfile locomotionProfile;
        [SerializeField] private AnimationClip actionPose,reactionPose;
        [SerializeField] private BoneWeightMask actionMask,reactionMask;
        [SerializeField] private float actionBlendRate=7f,reactionBlendRate=7f;
        public PosePipelineProfile Locomotion=>locomotionProfile;
        public AnimationClip ActionPose=>actionPose;
        public AnimationClip ReactionPose=>reactionPose;
        public BoneWeightMask ActionMask=>actionMask;
        public BoneWeightMask ReactionMask=>reactionMask;
        public float ActionBlendRate=>actionBlendRate;
        public float ReactionBlendRate=>reactionBlendRate;
        public void Configure(PosePipelineProfile locomotion,AnimationClip action,AnimationClip reaction,BoneWeightMask a,BoneWeightMask r)
        {locomotionProfile=locomotion;actionPose=action;reactionPose=reaction;actionMask=a;reactionMask=r;}
        public void AssertValid(){if(!locomotionProfile||!actionPose||!reactionPose||!actionMask||!reactionMask)throw new InvalidOperationException("Phase 3 profile incomplete.");locomotionProfile.AssertValid();}
    }
}
