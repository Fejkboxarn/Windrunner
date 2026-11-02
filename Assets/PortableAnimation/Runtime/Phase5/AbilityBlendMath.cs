using System;
using UnityEngine;
namespace PortableAnimation
{
    public sealed class AbilityWeightController
    {
        private readonly float[] current,start,target;
        private float elapsed,duration;
        private int targetIndex=-1;
        public int TargetIndex=>targetIndex;
        public bool IsTransitioning=>duration>0f&&elapsed<duration;
        public AbilityWeightController(int count)
        {if(count<1)throw new ArgumentOutOfRangeException(nameof(count));current=new float[count];start=new float[count];target=new float[count];}
        public float GetWeight(int index)=>current[index];
        public void Request(int index,float seconds)
        {
            if(index< -1||index>=current.Length)throw new ArgumentOutOfRangeException(nameof(index));
            if(index==targetIndex)return;
            targetIndex=index;Array.Copy(current,start,current.Length);Array.Clear(target,0,target.Length);
            if(index>=0)target[index]=1f;
            duration=Mathf.Max(0f,seconds);elapsed=0f;
            if(duration==0f)Array.Copy(target,current,current.Length);
        }
        public void Advance(float dt)
        {
            if(duration<=0f||elapsed>=duration)return;
            elapsed=Mathf.Min(duration,elapsed+Mathf.Max(0f,dt));
            float t=elapsed/duration;float eased=t*t*(3f-2f*t);
            for(int i=0;i<current.Length;i++)current[i]=Mathf.Lerp(start[i],target[i],eased);
        }
        public int GetDominantIndex()
        {int result=-1;float strongest=0.0001f;for(int i=0;i<current.Length;i++)if(current[i]>strongest){strongest=current[i];result=i;}return result;}
    }
    public static class AbilityBlendMath
    {
        public static void NormalizeSequential(float[] contributions,float[] mixerWeights)
        {
            if(contributions==null||mixerWeights==null||contributions.Length!=mixerWeights.Length)throw new ArgumentException("Bad blend arrays.");
            float sum=0f;
            for(int i=0;i<contributions.Length;i++){contributions[i]=Mathf.Clamp01(contributions[i]);sum+=contributions[i];}
            if(sum>1f)for(int i=0;i<contributions.Length;i++)contributions[i]/=sum;
            float remaining=1f;
            for(int i=contributions.Length-1;i>=0;i--)
            {mixerWeights[i]=remaining<=0.000001f?0f:Mathf.Clamp01(contributions[i]/remaining);remaining=Mathf.Max(0f,remaining-contributions[i]);}
        }
    }
}
