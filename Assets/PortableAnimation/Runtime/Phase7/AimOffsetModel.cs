using System;
using System.Collections.Generic;
using UnityEngine;
namespace PortableAnimation
{
    public enum AimOffsetSource { None=-1, Base=0, PhysicsWeapon=1, CustomIdle=2, Montage=3 }
    [Serializable] public sealed class AimPoseSet
    {
        [SerializeField] private AimOffsetSource source;
        [SerializeField] private AnimationClip[] samples=new AnimationClip[9];
        public AimOffsetSource Source=>source;
        public int SampleCount=>samples==null?0:samples.Length;
        public AimPoseSet(AimOffsetSource source,AnimationClip[] clips){this.source=source;samples=clips;}
        public AnimationClip Sample(int i)=>samples[i];
        public void AssertValid(){if(source==AimOffsetSource.None||samples==null||samples.Length!=9)throw new InvalidOperationException("Aim set must contain 9 samples.");foreach(var c in samples)if(!c)throw new InvalidOperationException("Aim pose sample missing.");}
    }
    [CreateAssetMenu(menuName="Portable Animation/Phase 7 AimOffset")]
    public sealed class AimOffsetProfile:ScriptableObject
    {
        [SerializeField] private ActionMontageProfile montageProfile;
        [SerializeField] private AnimationClip referencePose;
        [SerializeField] private AimPoseSet[] sourceSets;
        [SerializeField] private BoneWeightMask upperBodyMask,fullBodyMask;
        [SerializeField] private float minYaw=-90f,maxYaw=90f,minPitch=-60f,maxPitch=60f;
        [SerializeField] private float sourceTransition=0.2f,aimDegreesPerSecond=320f,bodyMaskBlendPerSecond=4f,enableFadePerSecond=7f;
        public ActionMontageProfile MontageProfile=>montageProfile;
        public AnimationClip ReferencePose=>referencePose;
        public BoneWeightMask UpperMask=>upperBodyMask;
        public BoneWeightMask FullMask=>fullBodyMask;
        public float MinYaw=>minYaw;
        public float MaxYaw=>maxYaw;
        public float MinPitch=>minPitch;
        public float MaxPitch=>maxPitch;
        public float SourceTransition=>sourceTransition;
        public float AimRate=>aimDegreesPerSecond;
        public float MaskRate=>bodyMaskBlendPerSecond;
        public float EnableFadeRate=>enableFadePerSecond;
        public void Configure(ActionMontageProfile montage,AnimationClip reference,BoneWeightMask upper,BoneWeightMask full,AimPoseSet[] sets)
        {montageProfile=montage;referencePose=reference;upperBodyMask=upper;fullBodyMask=full;sourceSets=sets;}
        public AimPoseSet GetSet(AimOffsetSource source){if(sourceSets==null)return null;foreach(var set in sourceSets)if(set!=null&&set.Source==source)return set;return null;}
        public bool[] BuildAvailability(){var b=new bool[4];for(int i=0;i<4;i++)b[i]=GetSet((AimOffsetSource)i)!=null;return b;}
        public void AssertValid()
        {
            if(!montageProfile||!referencePose||!upperBodyMask||!fullBodyMask)throw new InvalidOperationException("AO requires montage profile, reference and both masks.");
            montageProfile.AssertValid();
            if(sourceSets==null||sourceSets.Length!=4)throw new InvalidOperationException("Expected four AO sets.");
            for(int i=0;i<4;i++){var set=GetSet((AimOffsetSource)i);if(set==null)throw new InvalidOperationException("Missing AO source "+i);set.AssertValid();}
        }
    }
    public readonly struct AimOffsetFrame
    {
        public readonly AnimationFrameState Locomotion;
        public readonly float Yaw,Pitch;
        public readonly bool Enabled,CustomIdleActive,PhysicsWeaponActive,UpperBodyOnly;
        public AimOffsetFrame(AnimationFrameState locomotion,float yaw,float pitch,bool enabled,bool customIdle,bool physicsWeapon,bool upper)
        {Locomotion=locomotion;Yaw=yaw;Pitch=pitch;Enabled=enabled;CustomIdleActive=customIdle;PhysicsWeaponActive=physicsWeapon;UpperBodyOnly=upper;}
    }
}
