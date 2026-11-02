using System;
using UnityEngine;
namespace PortableAnimation
{
    public static class AimOffsetGrid
    {
        public static void Fill(float yaw,float pitch,float[] output)
        {
            if(output==null||output.Length!=9)throw new ArgumentException("Need nine AO weights.");
            Array.Clear(output,0,9);
            float x=Mathf.Clamp(yaw,-1,1)+1f,y=Mathf.Clamp(pitch,-1,1)+1f;
            int x0=Mathf.Min(1,Mathf.FloorToInt(x)),y0=Mathf.Min(1,Mathf.FloorToInt(y));
            float tx=x-x0,ty=y-y0;
            output[y0*3+x0]+=(1-tx)*(1-ty);
            output[y0*3+x0+1]+=tx*(1-ty);
            output[(y0+1)*3+x0]+=(1-tx)*ty;
            output[(y0+1)*3+x0+1]+=tx*ty;
        }
        public static float NormalizeDegrees(float degrees,float neg,float pos)
        {return degrees>=0?Mathf.Clamp01(degrees/Mathf.Max(0.0001f,pos)):-Mathf.Clamp01(degrees/Mathf.Min(-0.0001f,neg));}
    }
    public static class AimOffsetResolver
    {
        public static AimOffsetSource Resolve(bool enabled,bool montage,bool customIdle,bool weapon,bool[] available)
        {
            if(!enabled||available==null||available.Length!=4)return AimOffsetSource.None;
            if(montage&&available[3])return AimOffsetSource.Montage;
            if(customIdle&&available[2])return AimOffsetSource.CustomIdle;
            if(weapon&&available[1])return AimOffsetSource.PhysicsWeapon;
            return available[0]?AimOffsetSource.Base:AimOffsetSource.None;
        }
    }
    public sealed class AimOffsetSourceBlend
    {
        private readonly float[] current=new float[4],start=new float[4],target=new float[4];
        private readonly float duration,fadeSpeed;
        private float elapsed,strength;
        public AimOffsetSource Target{get;private set;}=AimOffsetSource.None;
        public float Strength=>strength;
        public float Weight(int i)=>current[i];
        public AimOffsetSourceBlend(float seconds,float rate){duration=Mathf.Max(0.001f,seconds);fadeSpeed=Mathf.Max(0.001f,rate);current[0]=1f;target[0]=1f;}
        public void Select(AimOffsetSource next)
        {
            if(next==Target)return;
            Target=next;
            if(next==AimOffsetSource.None)return;
            Array.Copy(current,start,4);Array.Clear(target,0,4);target[(int)next]=1f;elapsed=0;
        }
        public void Advance(float dt)
        {
            dt=Mathf.Max(0,dt);
            strength=Mathf.MoveTowards(strength,Target==AimOffsetSource.None?0f:1f,dt*fadeSpeed);
            if(Target==AimOffsetSource.None)return;
            elapsed=Mathf.Min(duration,elapsed+dt);
            float t=elapsed/duration,smooth=t*t*(3-2*t);
            for(int i=0;i<4;i++)current[i]=Mathf.Lerp(start[i],target[i],smooth);
        }
    }
}
