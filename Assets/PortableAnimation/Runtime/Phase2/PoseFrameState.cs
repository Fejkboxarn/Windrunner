using UnityEngine;
namespace PortableAnimation
{
    public readonly struct PoseFrameState
    {
        public readonly AnimationFrameState Locomotion;
        public readonly float ActionWeight,ReactionWeight;
        public readonly bool AllowAction,AllowReaction;
        public PoseFrameState(AnimationFrameState locomotion,float actionWeight,float reactionWeight,bool allowAction,bool allowReaction)
        {Locomotion=locomotion;ActionWeight=Mathf.Clamp01(actionWeight);ReactionWeight=Mathf.Clamp01(reactionWeight);AllowAction=allowAction;AllowReaction=allowReaction;}
    }
}
