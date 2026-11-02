using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
namespace PortableAnimation
{
    public struct PerBoneMixJob : IAnimationJob
    {
        public NativeArray<TransformStreamHandle> handles;
        public NativeArray<float> positionWeights,rotationWeights,scaleWeights;
        public float strength;
        public void ProcessRootMotion(AnimationStream stream)
        {
            if(stream.inputStreamCount<2)return;
            var source=stream.GetInputStream(0);
            if(source.isValid){stream.velocity=source.velocity;stream.angularVelocity=source.angularVelocity;}
        }
        public void ProcessAnimation(AnimationStream stream)
        {
            if(stream.inputStreamCount<2)return;
            var source=stream.GetInputStream(0);
            var overlay=stream.GetInputStream(1);
            if(!source.isValid||!overlay.isValid)return;
            float alpha=Mathf.Clamp01(strength);
            for(int i=0;i<handles.Length;i++)
            {
                var bone=handles[i];
                float p=Mathf.Clamp01(positionWeights[i]*alpha);
                float r=Mathf.Clamp01(rotationWeights[i]*alpha);
                float s=Mathf.Clamp01(scaleWeights[i]*alpha);
                Vector3 baseP=bone.GetLocalPosition(source);
                Quaternion baseR=bone.GetLocalRotation(source);
                Vector3 baseS=bone.GetLocalScale(source);
                bone.SetLocalPosition(stream,p>0f?Vector3.Lerp(baseP,bone.GetLocalPosition(overlay),p):baseP);
                bone.SetLocalRotation(stream,r>0f?Quaternion.Slerp(baseR,bone.GetLocalRotation(overlay),r):baseR);
                bone.SetLocalScale(stream,s>0f?Vector3.Lerp(baseS,bone.GetLocalScale(overlay),s):baseS);
            }
        }
    }
}
