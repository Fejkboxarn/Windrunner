using System;
using System.Collections.Generic;
using UnityEngine;
namespace PortableAnimation
{
    public enum AdditiveStageRole { LeftArm,Torso,Head }
    [Serializable] public sealed class AdditiveStageDefinition
    {
        [SerializeField] private string stageName;
        [SerializeField] private AdditiveStageRole role;
        [SerializeField] private AnimationClip additiveClip,referenceClip;
        [SerializeField] private BoneWeightMask boneMask;
        [SerializeField] private float blendRate=6f;
        [SerializeField] private AdditiveRotationMode rotationMode;
        public string StageName=>stageName;
        public AdditiveStageRole Role=>role;
        public AnimationClip AdditiveClip=>additiveClip;
        public AnimationClip ReferenceClip=>referenceClip;
        public BoneWeightMask BoneMask=>boneMask;
        public float BlendRate=>blendRate;
        public AdditiveRotationMode RotationMode=>rotationMode;
        public AdditiveStageDefinition(string name,AdditiveStageRole role,AnimationClip clip,AnimationClip reference,BoneWeightMask mask,float rate,AdditiveRotationMode mode)
        {stageName=name;this.role=role;additiveClip=clip;referenceClip=reference;boneMask=mask;blendRate=rate;rotationMode=mode;}
        public void AssertValid(){if(!additiveClip||!referenceClip||!boneMask)throw new InvalidOperationException("Incomplete additive stage.");}
    }
    [CreateAssetMenu(menuName="Portable Animation/Phase 4 Additive Pipeline")]
    public sealed class AdditivePipelineProfile:ScriptableObject
    {
        [SerializeField] private PerBonePipelineProfile phase3Profile;
        [SerializeField] private List<AdditiveStageDefinition> stages=new List<AdditiveStageDefinition>();
        public PerBonePipelineProfile Phase3Profile=>phase3Profile;
        public IReadOnlyList<AdditiveStageDefinition> Stages=>stages;
        public void Configure(PerBonePipelineProfile source,params AdditiveStageDefinition[] definitions)
        {phase3Profile=source;stages=new List<AdditiveStageDefinition>(definitions??Array.Empty<AdditiveStageDefinition>());}
        public void AssertValid(){if(!phase3Profile)throw new InvalidOperationException("No Phase3 profile.");phase3Profile.AssertValid();foreach(var stage in stages)stage.AssertValid();}
    }
}
