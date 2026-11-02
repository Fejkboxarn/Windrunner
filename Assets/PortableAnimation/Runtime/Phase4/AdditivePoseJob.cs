using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
namespace PortableAnimation
{
    public enum AdditiveRotationMode { LocalSpace=0, ComponentSpace=1 }
    public struct AdditivePoseJob : IAnimationJob
    {
        public NativeArray<TransformStreamHandle> handles;
        public NativeArray<int> parentIndices;
        public NativeArray<float> positionWeights,rotationWeights,scaleWeights;
        public NativeArray<Quaternion> overlayComponents,referenceComponents,resultComponents;
        public float strength;
        public int rotationMode;
        public void ProcessRootMotion(AnimationStream stream)
        {
            if(stream.inputStreamCount<3)return;
            var src=stream.GetInputStream(0);
            if(src.isValid){stream.velocity=src.velocity;stream.angularVelocity=src.angularVelocity;}
        }
        public void ProcessAnimation(AnimationStream stream)
        {
            if(stream.inputStreamCount<3)return;
            var source=stream.GetInputStream(0);
            var overlay=stream.GetInputStream(1);
            var reference=stream.GetInputStream(2);
            if(!source.isValid||!overlay.isValid||!reference.isValid)return;
            float global=Mathf.Clamp01(strength);
            bool component=rotationMode==(int)AdditiveRotationMode.ComponentSpace;
            for(int i=0;i<handles.Length;i++)
            {
                var handle=handles[i];int parent=parentIndices[i];
                Quaternion oParent=parent<0?Quaternion.identity:overlayComponents[parent];
                Quaternion rParent=parent<0?Quaternion.identity:referenceComponents[parent];
                Quaternion outParent=parent<0?Quaternion.identity:resultComponents[parent];
                Quaternion overlayLocal=handle.GetLocalRotation(overlay);
                Quaternion refLocal=handle.GetLocalRotation(reference);
                Quaternion sourceLocal=handle.GetLocalRotation(source);
                overlayComponents[i]=oParent*overlayLocal;
                referenceComponents[i]=rParent*refLocal;
                float pw=Mathf.Clamp01(global*positionWeights[i]);
                float rw=Mathf.Clamp01(global*rotationWeights[i]);
                float sw=Mathf.Clamp01(global*scaleWeights[i]);
                Vector3 outPosition=handle.GetLocalPosition(source);
                if(pw>0)outPosition+=pw*(handle.GetLocalPosition(overlay)-handle.GetLocalPosition(reference));
                Quaternion outRotation=sourceLocal;
                if(rw>0)
                {
                    if(component)
                    {
                        Quaternion delta=overlayComponents[i]*Quaternion.Inverse(referenceComponents[i]);
                        Quaternion weighted=Quaternion.Slerp(Quaternion.identity,delta,rw);
                        outRotation=Quaternion.Inverse(outParent)*(weighted*(outParent*sourceLocal));
                    }
                    else
                    {
                        Quaternion delta=Quaternion.Inverse(refLocal)*overlayLocal;
                        outRotation=sourceLocal*Quaternion.Slerp(Quaternion.identity,delta,rw);
                    }
                }
                resultComponents[i]=outParent*outRotation;
                Vector3 outScale=handle.GetLocalScale(source);
                if(sw>0)
                {
                    Vector3 o=handle.GetLocalScale(overlay),r=handle.GetLocalScale(reference);
                    Vector3 ratio=new Vector3(Safe(o.x,r.x),Safe(o.y,r.y),Safe(o.z,r.z));
                    outScale=Vector3.Scale(outScale,Vector3.Lerp(Vector3.one,ratio,sw));
                }
                handle.SetLocalPosition(stream,outPosition);
                handle.SetLocalRotation(stream,outRotation);
                handle.SetLocalScale(stream,outScale);
            }
        }
        private static float Safe(float a,float b){return Mathf.Abs(b)<0.00001f?1f:a/b;}
    }
}
