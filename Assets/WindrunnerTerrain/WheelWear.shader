Shader "Windrunner/Terrain/Wheel Wear"
{
 Properties { _Color("Compacted soil tint",Color)=(0.19,0.12,0.075,0.72) }
 SubShader
 {
  Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   Tags { "LightMode"="SRPDefaultUnlit" }
   ZWrite Off ZTest LEqual Cull Off Offset -1,-1 Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _Color;
   CBUFFER_END
   struct A { float4 pos:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
   struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; half fog:TEXCOORD1; };
   V vert(A a){ V o; o.pos=TransformObjectToHClip(a.pos.xyz);o.uv=a.uv;o.color=a.color;o.fog=ComputeFogFactor(o.pos.z);return o; }
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p)
   {
    float2 cell=floor(p),f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),
                lerp(hash(cell+float2(0,1)),hash(cell+1),f.x),f.y);
   }
   half4 frag(V i):SV_Target
   {
    float across=abs(2*i.uv.x-1);
    float grain=noise(i.uv*float2(3.7,14.5));
    float scuff=noise(float2(i.uv.y*1.7,5));
    float breakup=i.color.r;
    float edge=1-smoothstep(.16,1,across+(grain-.5)*breakup*.65);
    float fade=rcp(1+.45*fwidth(i.uv.x));
    float pressure=lerp(1,smoothstep(.12,.85,scuff)*(.5+.5*grain),breakup);
    return half4(MixFog(_Color.rgb*(.9+.18*grain),i.fog),_Color.a*i.color.a*edge*fade*pressure);
   }
   ENDHLSL
  }
 }
}
