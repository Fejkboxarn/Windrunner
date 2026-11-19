Shader "Windrunner/Suntemple/Fountain Flow URP"
{
 Properties
 {
  _WaterNormals("Original water normals",2D)="bump"{} _Foam("Original foam",2D)="white"{}
  _WaterfallSteepnessLineTexture("Original directional flow",2D)="gray"{}
  _ColorShallow("Shallow color",Color)=(0,.42,.5,1) _ColorDeep("Deep color",Color)=(.03,.44,.79,1)
  _WaterOpacity("Opacity",Range(0,1))=.586 _WaterSpeed("Flow speed",Float)=1.47
  _NormalStrength("Normal strength",Range(0,2))=.5 _WaterfallFoam("Foam strength",Range(0,1))=.585
  _WaterfallSteepnessLineSpeed("Directional flow speed",Float)=.9 _WaterEdgeDistance("Intersection fade",Float)=2.82
  _ColorTransparencyDepth("Depth color blend",Float)=.13 _Smoothness("Smoothness",Range(0,1))=1
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass
  {
   Name "FountainForward" Tags {"LightMode"="UniversalForward"}
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   TEXTURE2D(_WaterNormals); SAMPLER(sampler_WaterNormals);
   TEXTURE2D(_Foam); SAMPLER(sampler_Foam);
   TEXTURE2D(_WaterfallSteepnessLineTexture); SAMPLER(sampler_WaterfallSteepnessLineTexture);
   CBUFFER_START(UnityPerMaterial)
    float4 _WaterNormals_ST,_Foam_ST,_WaterfallSteepnessLineTexture_ST,_ColorShallow,_ColorDeep;
    float _WaterOpacity,_WaterSpeed,_NormalStrength,_WaterfallFoam,_WaterfallSteepnessLineSpeed,_WaterEdgeDistance,_ColorTransparencyDepth,_Smoothness;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;};
   struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;float fog:TEXCOORD4;};
   Varyings Vert(Attributes v)
   {
    Varyings o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;
    o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.tangentWS=float4(TransformObjectToWorldDir(v.tangentOS.xyz),v.tangentOS.w*GetOddNegativeScale());o.uv=v.uv;o.fog=ComputeFogFactor(p.positionCS.z);return o;
   }
   half4 Frag(Varyings i):SV_Target
   {
    float2 flow=float2(0,-_Time.y*_WaterSpeed*.05);
    float3 n=UnpackNormalScale(SAMPLE_TEXTURE2D(_WaterNormals,sampler_WaterNormals,i.uv*_WaterNormals_ST.xy+_WaterNormals_ST.zw+flow),_NormalStrength);
    float3 normal=normalize(i.normalWS),tangent=normalize(i.tangentWS.xyz);float3 bitangent=cross(normal,tangent)*i.tangentWS.w;
    float2 screenUV=GetNormalizedScreenSpaceUV(i.positionCS);float depth=LinearEyeDepth(SampleSceneDepth(screenUV),_ZBufferParams)+TransformWorldToView(i.positionWS).z;
    float stripe=SAMPLE_TEXTURE2D(_WaterfallSteepnessLineTexture,sampler_WaterfallSteepnessLineTexture,i.uv*_WaterfallSteepnessLineTexture_ST.xy+_WaterfallSteepnessLineTexture_ST.zw+flow*_WaterfallSteepnessLineSpeed).r;
    float foam=SAMPLE_TEXTURE2D(_Foam,sampler_Foam,i.uv*_Foam_ST.xy+_Foam_ST.zw+flow*.7).r*smoothstep(.4,.85,stripe)*_WaterfallFoam;
    SurfaceData surface=(SurfaceData)0;surface.albedo=lerp(lerp(_ColorShallow.rgb,_ColorDeep.rgb,saturate(depth*_ColorTransparencyDepth)),half3(1,1,1),foam);surface.smoothness=_Smoothness;surface.normalTS=n;surface.occlusion=1;surface.alpha=saturate(_WaterOpacity+foam*.3)*saturate(depth/max(.01,_WaterEdgeDistance));
    InputData input=(InputData)0;input.positionWS=i.positionWS;input.normalWS=normalize(tangent*n.x+bitangent*n.y+normal*n.z);input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);input.bakedGI=SampleSH(input.normalWS);input.normalizedScreenSpaceUV=screenUV;input.shadowMask=half4(1,1,1,1);
    half4 color=UniversalFragmentPBR(input,surface);color.rgb=MixFog(color.rgb,i.fog);return color;
   }
   ENDHLSL
  }
 }
}
