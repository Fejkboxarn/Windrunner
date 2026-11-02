using System;
using System.Collections.Generic;
using UnityEngine;
namespace PortableAnimation
{
    public enum PoseOverlaySignal { Action, Reaction }
    [Serializable] public sealed class PoseOverlayDefinition
    {
        [SerializeField] private string stageName;
        [SerializeField] private PoseOverlaySignal signal;
        [SerializeField] private AnimationClip clip;
        [SerializeField] private float blendRate=5f;
        [SerializeField] private bool enabledByDefault=true;
        public string StageName=>stageName;
        public PoseOverlaySignal Signal=>signal;
        public AnimationClip Clip=>clip;
        public float BlendRate=>blendRate;
        public bool EnabledByDefault=>enabledByDefault;
        public PoseOverlayDefinition(string name,PoseOverlaySignal route,AnimationClip animation,float speed,bool enabled=true)
        {stageName=name;signal=route;clip=animation;blendRate=speed;enabledByDefault=enabled;}
    }
    [CreateAssetMenu(menuName="Portable Animation/Phase 2 Pipeline",fileName="PosePipelineProfile")]
    public sealed class PosePipelineProfile : ScriptableObject
    {
        [SerializeField] private AnimationClip groundIdle,groundMoving,aerialHover,aerialTravel;
        [SerializeField] private float groundBlendRate=4f,aerialBlendRate=4f,locomotionSwitchRate=2f;
        [SerializeField] private List<PoseOverlayDefinition> processors=new List<PoseOverlayDefinition>();
        public AnimationClip GroundIdle=>groundIdle;
        public AnimationClip GroundMoving=>groundMoving;
        public AnimationClip AerialHover=>aerialHover;
        public AnimationClip AerialTravel=>aerialTravel;
        public float GroundBlendRate=>groundBlendRate;
        public float AerialBlendRate=>aerialBlendRate;
        public float LocomotionSwitchRate=>locomotionSwitchRate;
        public IReadOnlyList<PoseOverlayDefinition> Processors=>processors;
        public void Configure(AnimationClip idle,AnimationClip moving,AnimationClip hovering,AnimationClip flying,PoseOverlayDefinition[] ordered)
        {groundIdle=idle;groundMoving=moving;aerialHover=hovering;aerialTravel=flying;processors=new List<PoseOverlayDefinition>(ordered??Array.Empty<PoseOverlayDefinition>());}
        public void AssertValid()
        {
            if(!groundIdle||!groundMoving||!aerialHover||!aerialTravel)throw new InvalidOperationException("Missing Phase 2 locomotion clips.");
            foreach(var p in processors)if(p==null||!p.Clip)throw new InvalidOperationException("Invalid Phase 2 pose processor.");
        }
    }
}
