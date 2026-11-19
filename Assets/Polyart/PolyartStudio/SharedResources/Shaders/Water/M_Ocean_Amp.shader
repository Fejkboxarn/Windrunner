// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "M_Ocean"
{
	Properties
	{
		_EdgeLength("Edge Length", Range( 1 , 250)) = 15
		_WavePeakColor("Wave Peak Color", Color) = (0.03933786,0.5660533,0.6415094,0)
		_Smoothness("Smoothness", Range( 0 , 1)) = 1
		_Refraction("Refraction", Range( 0 , 0.03)) = 0
		_WaterDeepColor("Water Deep Color", Color) = (0,0.3077029,0.6132076,0)
		_WaterNormalMap("Water Normal Map", 2D) = "bump" {}
		_NormalTiling("Normal Tiling", Float) = 2
		_NormalsSpeed("Normals Speed", Float) = 0.5
		_TextureNormalsStrength("Texture Normals Strength", Range( 0 , 1)) = 1
		_TerrainPosAndSize("TerrainPosAndSize", Vector) = (0,0,0,0)
		_TerrainHeightMap("TerrainHeightMap", 2D) = "black" {}
		_ShoreDistanceWPODampening("Shore Distance WPO Dampening", Float) = 4
		_TrailFoamAmountHeight("Trail Foam Amount Height", Float) = 0.8
		_TrailFoamGradience("Trail Foam Gradience", Float) = 0.1
		_TrailFoamPowMax("Trail Foam Pow Max", Float) = 1
		_TrailFoamPowMin("Trail Foam Pow Min", Float) = 2
		_FoamTiling("Foam Tiling", Float) = 3
		_FoamTexture("Foam Texture", 2D) = "white" {}
		_EdgeOpacityDistance("Edge Opacity Distance", Float) = 0
		_WaterDepthOpacityGradience("Water Depth Opacity Gradience", Float) = 0
		_CausticsTexture("Caustics Texture", 2D) = "white" {}
		_CausticsIntensity("Caustics Intensity", Float) = 1
		_CausticsScale("Caustics Scale", Float) = 5
		_CausticsDistance("Caustics Distance", Float) = 0
		_CausticsNormalsDistortion("Caustics Normals Distortion", Float) = 0
		_EdgeFoamDistance("Edge Foam Distance", Float) = 0
		_EdgeFoamSpeed("Edge Foam Speed", Float) = 0
		_EdgeFoamWavesNumber("Edge Foam Waves Number", Float) = 8.55
		_EdgeFoamOpacity("Edge Foam Opacity", Range( 0 , 1)) = 0.3
		_Noisetexture("Noise texture", 2D) = "white" {}
		_BackgroundIntensityMultiplier("Background Intensity Multiplier", Range( 0 , 1)) = 0
		_ReflectionProbeTexture("ReflectionProbeTexture", CUBE) = "gray" {}
		_ReflectionAmount("Reflection Amount", Range( 0 , 1)) = 0
		_WindDirection("_WindDirection", Vector) = (0,0,0,0)
		_FlowPivot("_FlowPivot", Vector) = (0,0,0,0)
		_TerrainHeight("TerrainHeight", Vector) = (0,0,0,0)
		[HideInInspector] __dirty( "", Int ) = 1
		[Header(Forward Rendering Options)]
		[ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
		[ToggleOff] _GlossyReflections("Reflections", Float) = 1.0
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True" }
		Cull Off
		ZWrite On
		GrabPass{ }
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#include "UnityStandardUtils.cginc"
		#include "UnityCG.cginc"
		#include "Tessellation.cginc"
		#pragma target 4.6
		#pragma multi_compile_instancing
		#pragma shader_feature _SPECULARHIGHLIGHTS_OFF
		#pragma shader_feature _GLOSSYREFLECTIONS_OFF
		#define ASE_VERSION 19801
		#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex);
		#else
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex)
		#endif
		#include "GerstnerWave.hlsl"
		#pragma surface surf Standard keepalpha exclude_path:deferred vertex:vertexDataFunc tessellate:tessFunction 
		struct Input
		{
			float3 worldPos;
			float3 worldRefl;
			INTERNAL_DATA
			float4 screenPos;
			float3 worldNormal;
		};

		uniform float _ShoreDistanceWPODampening;
		uniform sampler2D _TerrainHeightMap;
		uniform float4 _TerrainPosAndSize;
		uniform float4 _TerrainHeight;
		uniform sampler2D _WaterNormalMap;
		uniform float _NormalTiling;
		uniform float _NormalsSpeed;
		uniform float _TextureNormalsStrength;
		uniform samplerCUBE _ReflectionProbeTexture;
		uniform float4 _WaterDeepColor;
		uniform float4 _WavePeakColor;
		uniform float _ReflectionAmount;
		ASE_DECLARE_SCREENSPACE_TEXTURE( _GrabTexture )
		uniform float _Refraction;
		uniform float _BackgroundIntensityMultiplier;
		uniform float _EdgeOpacityDistance;
		UNITY_DECLARE_DEPTH_TEXTURE( _CameraDepthTexture );
		uniform float4 _CameraDepthTexture_TexelSize;
		uniform float _WaterDepthOpacityGradience;
		uniform sampler2D _FoamTexture;
		uniform float _FoamTiling;
		uniform float _TrailFoamPowMin;
		uniform float _TrailFoamPowMax;
		uniform float _TrailFoamAmountHeight;
		uniform float _TrailFoamGradience;
		uniform float _EdgeFoamDistance;
		uniform float _EdgeFoamWavesNumber;
		uniform float _EdgeFoamSpeed;
		uniform float _EdgeFoamOpacity;
		uniform sampler2D _Noisetexture;
		uniform sampler2D _CausticsTexture;
		uniform float _CausticsNormalsDistortion;
		uniform float _CausticsScale;
		uniform float _CausticsDistance;
		uniform float _CausticsIntensity;
		uniform float _Smoothness;
		uniform float _EdgeLength;


		void GerstnerWaves( inout float WPO, inout float3 Normals, float2 WorldPos, float Time )
		{
			CalculateGerstnerWaves_float(WorldPos, Time,WPO, Normals);
		}


		float2 UnStereo( float2 UV )
		{
			#if UNITY_SINGLE_PASS_STEREO
			float4 scaleOffset = unity_StereoScaleOffset[ unity_StereoEyeIndex ];
			UV.xy = (UV.xy - scaleOffset.zw) / scaleOffset.xy;
			#endif
			return UV;
		}


		float3 InvertDepthDir72_g23( float3 In )
		{
			float3 result = In;
			#if !defined(ASE_SRP_VERSION) || ASE_SRP_VERSION <= 70301
			result *= float3(1,1,-1);
			#endif
			return result;
		}


		float3 InvertDepthDir72_g29( float3 In )
		{
			float3 result = In;
			#if !defined(ASE_SRP_VERSION) || ASE_SRP_VERSION <= 70301
			result *= float3(1,1,-1);
			#endif
			return result;
		}


		float4 tessFunction( appdata_full v0, appdata_full v1, appdata_full v2 )
		{
			return UnityEdgeLengthBasedTessCull( v0.vertex, v1.vertex, v2.vertex, _EdgeLength,0.0);
		}

		void vertexDataFunc( inout appdata_full v )
		{
			float3 ase_objectPosition = UNITY_MATRIX_M._m03_m13_m23;
			float3 ase_positionWS = mul( unity_ObjectToWorld, v.vertex );
			float2 appendResult230 = (float2(ase_positionWS.x , ase_positionWS.z));
			float2 appendResult232 = (float2(_TerrainPosAndSize.x , _TerrainPosAndSize.y));
			float2 appendResult234 = (float2(_TerrainPosAndSize.z , _TerrainPosAndSize.w));
			float Water_Depth_Texture201 = ( ase_objectPosition.y - ( ( tex2Dlod( _TerrainHeightMap, float4( ( ( appendResult230 - appendResult232 ) / appendResult234 ), 0, 0.0) ).r * _TerrainHeight.x ) + _TerrainHeight.y ) );
			float smoothstepResult235 = smoothstep( 0.0 , _ShoreDistanceWPODampening , Water_Depth_Texture201);
			float localGerstnerWaves71 = ( 0.0 );
			float WPO71 = 0.0;
			float3 temp_cast_0 = (0.0).xxx;
			float3 Normals71 = temp_cast_0;
			float2 appendResult76 = (float2(ase_positionWS.x , ase_positionWS.z));
			float2 WorldPos71 = appendResult76;
			float Time71 = _Time.y;
			{
			CalculateGerstnerWaves_float(WorldPos71, Time71,WPO71, Normals71);
			}
			float WPO_Gerstner260 = WPO71;
			float4 appendResult612 = (float4(0.0 , WPO_Gerstner260 , 0.0 , 0.0));
			float4 WPO_Final405 = ( smoothstepResult235 * appendResult612 );
			v.vertex.xyz += WPO_Final405.xyz;
			v.vertex.w = 1;
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float localGerstnerWaves71 = ( 0.0 );
			float WPO71 = 0.0;
			float3 temp_cast_0 = (0.0).xxx;
			float3 Normals71 = temp_cast_0;
			float3 ase_positionWS = i.worldPos;
			float2 appendResult76 = (float2(ase_positionWS.x , ase_positionWS.z));
			float2 WorldPos71 = appendResult76;
			float Time71 = _Time.y;
			{
			CalculateGerstnerWaves_float(WorldPos71, Time71,WPO71, Normals71);
			}
			float3 Normals_Gerstner255 = Normals71;
			float3 ase_objectPosition = UNITY_MATRIX_M._m03_m13_m23;
			float2 appendResult230 = (float2(ase_positionWS.x , ase_positionWS.z));
			float2 appendResult232 = (float2(_TerrainPosAndSize.x , _TerrainPosAndSize.y));
			float2 appendResult234 = (float2(_TerrainPosAndSize.z , _TerrainPosAndSize.w));
			float Water_Depth_Texture201 = ( ase_objectPosition.y - ( ( tex2D( _TerrainHeightMap, ( ( appendResult230 - appendResult232 ) / appendResult234 ) ).r * _TerrainHeight.x ) + _TerrainHeight.y ) );
			float smoothstepResult235 = smoothstep( 0.0 , _ShoreDistanceWPODampening , Water_Depth_Texture201);
			float WPO_Gerstner260 = WPO71;
			float4 appendResult612 = (float4(0.0 , WPO_Gerstner260 , 0.0 , 0.0));
			float4 WPO_Final405 = ( smoothstepResult235 * appendResult612 );
			float4 break351 = ( WPO_Final405 + float4( ase_positionWS , 0.0 ) );
			float2 appendResult349 = (float2(break351.x , break351.z));
			float2 UVs_After_Offset353 = appendResult349;
			float mulTime159 = _Time.y * ( _NormalsSpeed / _NormalTiling );
			float temp_output_194_0 = ( _NormalTiling * 0.9 );
			float mulTime171 = _Time.y * ( ( _NormalsSpeed * 0.85 ) / temp_output_194_0 );
			float2 normalizeResult174 = normalize( float2( -0.1,0.9 ) );
			float temp_output_180_0 = ( _NormalTiling * 1.1 );
			float mulTime184 = _Time.y * ( ( _NormalsSpeed * 0.95 ) / temp_output_180_0 );
			float2 normalizeResult185 = normalize( float2( -1,0.3 ) );
			float3 break344 = ( ( UnpackScaleNormal( tex2D( _WaterNormalMap, ( ( UVs_After_Offset353 / _NormalTiling ) + ( mulTime159 * float2( 1,0 ) ) ) ), _TextureNormalsStrength ) + UnpackScaleNormal( tex2D( _WaterNormalMap, ( ( UVs_After_Offset353 / temp_output_194_0 ) + ( mulTime171 * normalizeResult174 ) ) ), _TextureNormalsStrength ) ) + UnpackScaleNormal( tex2D( _WaterNormalMap, ( ( UVs_After_Offset353 / temp_output_180_0 ) + ( mulTime184 * normalizeResult185 ) ) ), _TextureNormalsStrength ) );
			float3 appendResult345 = (float3(break344.x , break344.y , 1.0));
			float3 Normals_Texture253 = appendResult345;
			float3 Normals_Water479 = BlendNormals( Normals_Gerstner255 , Normals_Texture253 );
			float Edge_Mask444 = saturate( ( Water_Depth_Texture201 / 0.15 ) );
			float3 lerpResult568 = lerp( float3(0,0,1) , Normals_Water479 , Edge_Mask444);
			float3 Normals_Final251 = lerpResult568;
			o.Normal = Normals_Final251;
			float3 _Vector2 = float3(0,0,1);
			float3 lerpResult608 = lerp( _Vector2 , Normals_Texture253 , 0.25);
			float3 lerpResult604 = lerp( _Vector2 , BlendNormals( Normals_Gerstner255 , lerpResult608 ) , Edge_Mask444);
			float3 Reflection595 = texCUBE( _ReflectionProbeTexture, WorldReflectionVector( i , lerpResult604 ) ).rgb;
			float Wave_Peaks275 = ( WPO71 / _FlowPivot.w );
			float smoothstepResult146 = smoothstep( -0.5 , 1.0 , Wave_Peaks275);
			float3 lerpResult110 = lerp( _WaterDeepColor.rgb , _WavePeakColor.rgb , smoothstepResult146);
			float3 blendOpSrc600 = Reflection595;
			float3 blendOpDest600 = lerpResult110;
			float3 lerpBlendMode600 = lerp(blendOpDest600, (( blendOpSrc600 > 0.5 ) ? ( 1.0 - ( 1.0 - 2.0 * ( blendOpSrc600 - 0.5 ) ) * ( 1.0 - blendOpDest600 ) ) : ( 2.0 * blendOpSrc600 * blendOpDest600 ) ),_ReflectionAmount);
			float4 ase_positionSS = float4( i.screenPos.xyz , i.screenPos.w + 1e-7 );
			float4 ase_positionSSNorm = ase_positionSS / ase_positionSS.w;
			ase_positionSSNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_positionSSNorm.z : ase_positionSSNorm.z * 0.5 + 0.5;
			float2 appendResult86 = (float2(ase_positionSSNorm.x , ase_positionSSNorm.y));
			float4 screenColor82 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_GrabTexture,( ( (( Normals_Final251 * _Refraction )).xy * Edge_Mask444 ) + appendResult86 ));
			float4 Scene_Color263 = screenColor82;
			float3 ase_normalWS = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float3 ase_normalOS = mul( unity_WorldToObject, float4( ase_normalWS, 0 ) );
			ase_normalOS = normalize( ase_normalOS );
			float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - ase_positionWS );
			float3 normalizeResult107 = normalize( ase_viewVectorWS );
			float dotResult97 = dot( ase_normalOS , normalizeResult107 );
			float temp_output_418_0 = saturate( ( Water_Depth_Texture201 / _EdgeOpacityDistance ) );
			float2 UV22_g24 = ase_positionSSNorm.xy;
			float2 localUnStereo22_g24 = UnStereo( UV22_g24 );
			float2 break64_g23 = localUnStereo22_g24;
			float depth01_69_g23 = SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_positionSSNorm.xy );
			#ifdef UNITY_REVERSED_Z
				float staticSwitch38_g23 = ( 1.0 - depth01_69_g23 );
			#else
				float staticSwitch38_g23 = depth01_69_g23;
			#endif
			float3 appendResult39_g23 = (float3(break64_g23.x , break64_g23.y , staticSwitch38_g23));
			float4 appendResult42_g23 = (float4((appendResult39_g23*2.0 + -1.0) , 1.0));
			float4 temp_output_43_0_g23 = mul( unity_CameraInvProjection, appendResult42_g23 );
			float3 temp_output_46_0_g23 = ( (temp_output_43_0_g23).xyz / (temp_output_43_0_g23).w );
			float3 In72_g23 = temp_output_46_0_g23;
			float3 localInvertDepthDir72_g23 = InvertDepthDir72_g23( In72_g23 );
			float4 appendResult49_g23 = (float4(localInvertDepthDir72_g23 , 1.0));
			float Water_Depth_Reconstruct_Position415 = abs( ( ase_objectPosition.y - mul( unity_CameraToWorld, appendResult49_g23 ).y ) );
			float4 lerpResult93 = lerp( float4( ( saturate( lerpBlendMode600 )) , 0.0 ) , ( Scene_Color263 * _BackgroundIntensityMultiplier ) , saturate( ( saturate( ( saturate( dotResult97 ) + ( 1.0 - ( (0.5 + (temp_output_418_0 - 0.0) * (1.0 - 0.5) / (1.0 - 0.0)) * Edge_Mask444 ) ) ) ) - saturate( ( Water_Depth_Reconstruct_Position415 / _WaterDepthOpacityGradience ) ) ) ));
			float4 temp_cast_7 = (1.0).xxxx;
			float2 temp_output_360_0 = ( UVs_After_Offset353 / _FoamTiling );
			float smoothstepResult280 = smoothstep( _TrailFoamAmountHeight , ( _TrailFoamAmountHeight + _TrailFoamGradience ) , Wave_Peaks275);
			float lerpResult298 = lerp( _TrailFoamPowMin , _TrailFoamPowMax , smoothstepResult280);
			float Foam_Mask357 = ( saturate( pow( tex2D( _FoamTexture, temp_output_360_0 ).r , lerpResult298 ) ) * saturate( ( ( Water_Depth_Texture201 + -0.4 ) / 1.5 ) ) * temp_output_418_0 );
			float temp_output_492_0 = ( Water_Depth_Reconstruct_Position415 / _EdgeFoamDistance );
			float smoothstepResult515 = smoothstep( 0.7 , 1.0 , temp_output_492_0);
			float mulTime499 = _Time.y * _EdgeFoamSpeed;
			float2 appendResult523 = (float2(ase_positionWS.x , ase_positionWS.z));
			float2 normalizeResult617 = normalize( (_WindDirection).xy );
			float Edge_Foam516 = ( ( 1.0 - smoothstepResult515 ) * saturate( sin( ( ( temp_output_492_0 * _EdgeFoamWavesNumber ) + mulTime499 ) ) ) * _EdgeFoamOpacity * tex2D( _Noisetexture, ( ( appendResult523 / 100.0 ) + ( ( normalizeResult617 * _Time.y ) / 100.0 ) ) ).r * Edge_Mask444 );
			float4 lerpResult278 = lerp( lerpResult93 , temp_cast_7 , max( Foam_Mask357 , Edge_Foam516 ));
			float2 UV22_g30 = ase_positionSSNorm.xy;
			float2 localUnStereo22_g30 = UnStereo( UV22_g30 );
			float2 break64_g29 = localUnStereo22_g30;
			float depth01_69_g29 = SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_positionSSNorm.xy );
			#ifdef UNITY_REVERSED_Z
				float staticSwitch38_g29 = ( 1.0 - depth01_69_g29 );
			#else
				float staticSwitch38_g29 = depth01_69_g29;
			#endif
			float3 appendResult39_g29 = (float3(break64_g29.x , break64_g29.y , staticSwitch38_g29));
			float4 appendResult42_g29 = (float4((appendResult39_g29*2.0 + -1.0) , 1.0));
			float4 temp_output_43_0_g29 = mul( unity_CameraInvProjection, appendResult42_g29 );
			float3 temp_output_46_0_g29 = ( (temp_output_43_0_g29).xyz / (temp_output_43_0_g29).w );
			float3 In72_g29 = temp_output_46_0_g29;
			float3 localInvertDepthDir72_g29 = InvertDepthDir72_g29( In72_g29 );
			float4 appendResult49_g29 = (float4(localInvertDepthDir72_g29 , 1.0));
			float4 break450 = mul( unity_CameraToWorld, appendResult49_g29 );
			float2 appendResult451 = (float2(break450.x , break450.z));
			float3 Caustics468 = ( tex2D( _CausticsTexture, ( ( float3( ( appendResult451 + ( ( sin( _Time.y ) + cos( _Time.y ) ) * 0.15 ) ) ,  0.0 ) + ( Normals_Water479 * _CausticsNormalsDistortion ) ) / _CausticsScale ).xy ).rgb * ( 1.0 - saturate( ( Water_Depth_Reconstruct_Position415 / _CausticsDistance ) ) ) * Edge_Mask444 * _CausticsIntensity );
			float4 Color_Final277 = ( lerpResult278 + float4( Caustics468 , 0.0 ) );
			o.Albedo = Color_Final277.rgb;
			float lerpResult385 = lerp( 0.0 , _Smoothness , Edge_Mask444);
			o.Smoothness = lerpResult385;
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "Polyart.OceanMaterialCustomInspector"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.Vector4Node;227;784,-1232;Inherit;False;Property;_TerrainPosAndSize;TerrainPosAndSize;9;0;Create;False;0;0;0;False;0;False;0,0,0,0;0,0,991.36,1000;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.WorldPosInputsNode;226;816,-1392;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.DynamicAppendNode;232;1040,-1232;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;230;1040,-1360;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;231;1248,-1328;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;234;1040,-1136;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;233;1392,-1232;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;223;1536,-1264;Inherit;True;Property;_TerrainHeightMap;TerrainHeightMap;10;0;Create;True;0;0;0;False;0;False;-1;2c6536772776dd84f872779990273bfc;2c6536772776dd84f872779990273bfc;True;0;False;black;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.Vector4Node;623;1808,-1072;Inherit;False;Property;_TerrainHeight;TerrainHeight;36;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;248;2032,-1216;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WorldPosInputsNode;75;-640,384;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleTimeNode;77;-464,528;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;74;-432,208;Inherit;False;Constant;_Float1;Float 1;17;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ObjectPositionNode;243;2016,-1504;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleAddOpNode;624;2243.188,-1124.727;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;73;-432,144;Inherit;False;Constant;_Float0;Float 0;17;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;76;-432,416;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;272;2240,-1280;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CustomExpressionNode;71;-224,256;Inherit;False;CalculateGerstnerWaves_float(WorldPos, Time,WPO, Normals)@$;7;Create;4;True;WPO;FLOAT;0;InOut;;Inherit;False;True;Normals;FLOAT3;0,0,0;InOut;;Inherit;False;True;WorldPos;FLOAT2;0,0;In;;Inherit;False;True;Time;FLOAT;0;In;;Inherit;False;Gerstner Waves;False;True;0;;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;3;FLOAT2;0,0;False;4;FLOAT;0;False;3;FLOAT;0;FLOAT;2;FLOAT3;3
Node;AmplifyShaderEditor.RegisterLocalVarNode;260;272,240;Inherit;False;WPO Gerstner;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;201;2392,-1280;Inherit;False;Water Depth Texture;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;261;1152,1072;Inherit;False;260;WPO Gerstner;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;245;1072,992;Inherit;False;Property;_ShoreDistanceWPODampening;Shore Distance WPO Dampening;11;0;Create;True;0;0;0;False;0;False;4;11.15;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;259;1136,912;Inherit;False;201;Water Depth Texture;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;612;1408,1040;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SmoothstepOpNode;235;1376,912;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;10;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;250;1632,976;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;405;1792,976;Inherit;False;WPO Final;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.WorldPosInputsNode;348;-1664,144;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.GetLocalVarNode;352;-1696,64;Inherit;False;405;WPO Final;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleAddOpNode;350;-1456,80;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;155;-1152,-2720;Inherit;False;Property;_NormalTiling;Normal Tiling;6;0;Create;True;0;0;0;False;0;False;2;40;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;351;-1312,80;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.RangedFloatNode;157;-816,-2736;Inherit;False;Property;_NormalsSpeed;Normals Speed;7;0;Create;True;0;0;0;False;0;False;0.5;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;165;-976,-2160;Inherit;False;Constant;_Float2;Float 2;31;0;Create;True;0;0;0;False;0;False;0.9;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;169;-576,-2160;Inherit;False;Constant;_Float3;Float 2;31;0;Create;True;0;0;0;False;0;False;0.85;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;349;-1184,80;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;179;-640,-1744;Inherit;False;Constant;_Float5;Float 2;31;0;Create;True;0;0;0;False;0;False;0.95;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;178;-1088,-1808;Inherit;False;Constant;_Float4;Float 2;31;0;Create;True;0;0;0;False;0;False;1.1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;194;-784,-2192;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;168;-368,-2240;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.85;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;170;-160,-2240;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;172;-176,-2096;Inherit;False;Constant;_Vector4;Vector 3;31;0;Create;True;0;0;0;False;0;False;-0.1,0.9;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleDivideOpNode;158;-576,-2736;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;353;-1024,80;Inherit;False;UVs After Offset;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;181;-432,-1840;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.85;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;180;-912,-1840;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;171;-16,-2240;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.NormalizeNode;174;16,-2096;Inherit;False;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleTimeNode;159;-448,-2736;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;161;-416,-2656;Inherit;False;Constant;_Vector3;Vector 3;31;0;Create;True;0;0;0;False;0;False;1,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleDivideOpNode;182;-240,-1856;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;183;-256,-1712;Inherit;False;Constant;_Vector5;Vector 3;31;0;Create;True;0;0;0;False;0;False;-1,0.3;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.GetLocalVarNode;354;-1152,-2848;Inherit;False;353;UVs After Offset;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;173;208,-2240;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;193;-544,-2464;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;154;-608,-2848;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;160;-224,-2736;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleTimeNode;184;-96,-1856;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.NormalizeNode;185;-64,-1712;Inherit;False;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexturePropertyNode;150;-16,-2704;Inherit;True;Property;_WaterNormalMap;Water Normal Map;5;0;Create;True;0;0;0;False;0;False;4ea058df3811fdf439d5bff6fbdb2316;4ea058df3811fdf439d5bff6fbdb2316;True;bump;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.SimpleAddOpNode;166;336,-2464;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;156;32,-2848;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;195;-752,-1952;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;186;128,-1856;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;175;560,-2448;Inherit;True;Property;_TextureSample1;Texture Sample 0;31;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;162;272,-2864;Inherit;True;Property;_TextureSample0;Texture Sample 0;31;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleAddOpNode;187;320,-1936;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;196;480,-2592;Inherit;False;Property;_TextureNormalsStrength;Texture Normals Strength;8;0;Create;True;0;0;0;False;0;False;1;0.118;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;188;608,-2096;Inherit;True;Property;_TextureSample2;Texture Sample 0;31;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.UnpackScaleNormalNode;163;640,-2800;Inherit;False;Tangent;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.UnpackScaleNormalNode;176;896,-2416;Inherit;False;Tangent;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.UnpackScaleNormalNode;189;928,-2096;Inherit;False;Tangent;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleAddOpNode;342;1216,-2672;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;343;1424,-2496;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.BreakToComponentsNode;344;1552,-2496;Inherit;False;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.RangedFloatNode;346;1520,-2368;Inherit;False;Constant;_Float9;Float 9;46;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;345;1696,-2496;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;416;224,-80;Inherit;False;201;Water Depth Texture;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;255;272,304;Inherit;False;Normals Gerstner;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;253;1856,-2496;Inherit;False;Normals Texture;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;441;592,96;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0.15;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;257;3376,2624;Inherit;False;253;Normals Texture;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;256;3376,2544;Inherit;False;255;Normals Gerstner;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SaturateNode;442;720,96;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BlendNormalsNode;197;3600,2576;Inherit;False;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.FunctionNode;420;1904,1936;Inherit;False;Reconstruct World Position From Depth;-1;;23;e7094bcbcc80eb140b2a3dbe6a861de8;0;0;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;444;880,96;Inherit;False;Edge Mask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;479;3840,2576;Inherit;False;Normals Water;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.BreakToComponentsNode;423;2256,1936;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.ObjectPositionNode;421;2064,1792;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.Vector3Node;570;2460.738,3122.663;Inherit;False;Constant;_Vector1;Vector 1;31;0;Create;True;0;0;0;False;0;False;0,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.GetLocalVarNode;569;2464,3344;Inherit;False;444;Edge Mask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;481;2432,3264;Inherit;False;479;Normals Water;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;422;2432,1840;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;568;2688,3200;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.AbsOpNode;577;2640,1840;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;618;-112,496;Inherit;False;Property;_FlowPivot;_FlowPivot;35;0;Fetch;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;251;2848,3200;Inherit;False;Normals Final;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;619;176.4482,479.9236;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;415;2848,1840;Inherit;False;Water Depth Reconstruct Position;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;419;288,0;Inherit;False;Property;_EdgeOpacityDistance;Edge Opacity Distance;19;0;Create;True;0;0;0;False;0;False;0;12.18;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;615;3328,-2016;Inherit;False;Property;_WindDirection;_WindDirection;34;0;Fetch;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;282;80,2240;Inherit;False;Property;_TrailFoamAmountHeight;Trail Foam Amount Height;12;0;Create;True;0;0;0;False;0;False;0.8;-0.31;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;283;80,2320;Inherit;False;Property;_TrailFoamGradience;Trail Foam Gradience;13;0;Create;True;0;0;0;False;0;False;0.1;0.87;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;417;544,-80;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0.15;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;531;3680,-1936;Inherit;False;Constant;_NoiseSpeed;Noise Speed;36;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;490;3520,-2656;Inherit;False;415;Water Depth Reconstruct Position;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;470;2544,304;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;493;3824,-2576;Inherit;False;Property;_EdgeFoamDistance;Edge Foam Distance;26;0;Create;True;0;0;0;False;0;False;0;0.35;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;80;-688,1104;Inherit;False;Property;_Refraction;Refraction;3;0;Create;True;0;0;0;False;0;False;0;0.015;0;0.03;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;276;-624,1040;Inherit;False;251;Normals Final;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;605;2752,-928;Inherit;False;253;Normals Texture;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;609;2784,-848;Inherit;False;Constant;_Float12;Float 12;34;0;Create;True;0;0;0;False;0;False;0.25;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node;601;2784,-1136;Inherit;False;Constant;_Vector2;Vector 1;31;0;Create;True;0;0;0;False;0;False;0,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ComponentMaskNode;616;3504,-2016;Inherit;False;True;True;False;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;275;352,480;Inherit;False;Wave Peaks;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;359;-304,1952;Inherit;False;353;UVs After Offset;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;361;-272,2032;Inherit;False;Property;_FoamTiling;Foam Tiling;16;0;Create;True;0;0;0;False;0;False;3;30;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;284;368,2288;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;418;656,-80;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WorldPosInputsNode;522;3712,-2256;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleTimeNode;530;3872,-1936;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ViewVectorNode;106;592,-272;Inherit;False;World;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.FunctionNode;449;2544,128;Inherit;False;Reconstruct World Position From Depth;-1;;29;e7094bcbcc80eb140b2a3dbe6a861de8;0;0;1;FLOAT4;0
Node;AmplifyShaderEditor.SinOpNode;471;2800,272;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CosOpNode;472;2800,336;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;492;4080,-2656;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;501;3888,-2336;Inherit;False;Property;_EdgeFoamSpeed;Edge Foam Speed;27;0;Create;True;0;0;0;False;0;False;0;1.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;507;3824,-2432;Inherit;False;Property;_EdgeFoamWavesNumber;Edge Foam Waves Number;28;0;Create;True;0;0;0;False;0;False;8.55;8.55;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;84;-400,1056;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;606;3056,-1040;Inherit;False;255;Normals Gerstner;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;608;3120,-960;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.NormalizeNode;617;3712,-2016;Inherit;False;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;281;176,2160;Inherit;False;275;Wave Peaks;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;360;-48,1952;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SmoothstepOpNode;280;496,2224;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;299;464,2064;Inherit;False;Property;_TrailFoamPowMin;Trail Foam Pow Min;15;0;Create;True;0;0;0;False;0;False;2;6;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;300;464,2144;Inherit;False;Property;_TrailFoamPowMax;Trail Foam Pow Max;14;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;397;752,2224;Inherit;False;201;Water Depth Texture;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;523;3904,-2224;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;529;4096,-2016;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;525;3904,-2112;Inherit;False;Constant;_NoiseScale;Noise Scale;37;0;Create;True;0;0;0;False;0;False;100;200;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.NormalVertexDataNode;94;736,-416;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.NormalizeNode;107;768,-272;Inherit;False;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TFHCRemapNode;440;912,-80;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0.5;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;450;2896,128;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.SimpleAddOpNode;473;2928,272;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;475;2928,368;Inherit;False;Constant;_Float10;Float 10;31;0;Create;True;0;0;0;False;0;False;0.15;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;498;4128,-2464;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;9.42477;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;499;4096,-2336;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScreenPosInputsNode;83;-256,1264;Float;False;0;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ComponentMaskNode;87;-256,1056;Inherit;False;True;True;False;True;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;446;-224,1168;Inherit;False;444;Edge Mask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.BlendNormalsNode;607;3280,-1040;Inherit;False;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;603;3312,-928;Inherit;False;444;Edge Mask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode;363;-304,2272;Inherit;True;Property;_FoamTexture;Foam Texture;17;0;Create;True;0;0;0;False;0;False;617b206ec808f7041b7a41b2c1373f91;617b206ec808f7041b7a41b2c1373f91;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.SamplerNode;364;128,1936;Inherit;True;Property;_TextureSample12;Texture Sample 12;26;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.LerpOp;298;656,2112;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;400;928,2224;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;-0.4;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;524;4128,-2224;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;532;4256,-2112;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DotProductOpNode;97;944,-368;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;443;1136,-16;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;451;3072,128;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;474;3072,272;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;497;4304,-2464;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;478;2800,448;Inherit;False;479;Normals Water;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;485;2736,528;Inherit;False;Property;_CausticsNormalsDistortion;Caustics Normals Distortion;25;0;Create;True;0;0;0;False;0;False;0;1.49;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;86;-32,1280;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;445;-16,1104;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;604;3531.107,-1121.005;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.PowerNode;297;832,1984;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;398;1040.44,2225.383;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1.5;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;526;4432,-2224;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SaturateNode;104;1056,-368;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;413;1136,-208;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;434;1360,-16;Inherit;False;415;Water Depth Reconstruct Position;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;439;1360,64;Inherit;False;Property;_WaterDepthOpacityGradience;Water Depth Opacity Gradience;20;0;Create;True;0;0;0;False;0;False;0;40;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;476;3264,128;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;462;3712,384;Inherit;False;415;Water Depth Reconstruct Position;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;465;3808,464;Inherit;False;Property;_CausticsDistance;Caustics Distance;24;0;Create;True;0;0;0;False;0;False;0;10.25;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SinOpNode;496;4448,-2464;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;515;4256,-2656;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0.7;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;484;3024,464;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;89;160,1184;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.WorldReflectionVector;596;3728,-1344;Inherit;False;False;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SaturateNode;304;1008,1984;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;399;1152.44,2225.383;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;376;1248,-368;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;436;1664,-16;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;477;3408,128;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;453;3360,288;Inherit;False;Property;_CausticsScale;Caustics Scale;23;0;Create;True;0;0;0;False;0;False;5;10.13;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;463;4032,384;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;508;4688,-2464;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;510;4560,-2368;Inherit;False;Property;_EdgeFoamOpacity;Edge Foam Opacity;29;0;Create;True;0;0;0;False;0;False;0.3;0.15;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;512;4688,-2656;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;566;4656,-2016;Inherit;False;444;Edge Mask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScreenColorNode;82;288,1184;Inherit;False;Global;_GrabScreen0;Grab Screen 0;20;0;Create;True;0;0;0;False;0;False;Object;-1;False;False;False;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;111;-448,-720;Inherit;False;275;Wave Peaks;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;148;-448,-560;Inherit;False;Constant;_PeakColorMaxHeight;Peak Color Max Height;5;0;Create;True;0;0;0;False;0;False;1;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;147;-448,-640;Inherit;False;Constant;_PeakColorMinHeight;Peak Color Min Height;4;0;Create;True;0;0;0;False;0;False;-0.5;-6.9;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;594;3952,-1360;Inherit;True;Property;_ReflectionProbeTexture;ReflectionProbeTexture;32;0;Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;gray;LockedToCube;False;Object;-1;Auto;Cube;8;0;SAMPLERCUBE;;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;519;4560,-2240;Inherit;True;Property;_Noisetexture;Noise texture;30;0;Create;True;0;0;0;False;0;False;-1;878bec983c34b8c4b9f8fc06d0e7b5af;878bec983c34b8c4b9f8fc06d0e7b5af;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;389;1184,2064;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;377;1376,-368;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;438;1792,-16;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;452;3552,128;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SaturateNode;466;4144,384;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;505;4944,-2432;Inherit;False;5;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;263;496,1184;Inherit;False;Scene Color;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;595;4240,-1344;Inherit;False;Reflection;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SmoothstepOpNode;146;-160,-672;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;57;-416,-912;Inherit;False;Property;_WavePeakColor;Wave Peak Color;1;0;Create;True;0;0;0;False;0;False;0.03933786,0.5660533,0.6415094,0;0,0.5660378,0.5660378,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode;116;-416,-1104;Inherit;False;Property;_WaterDeepColor;Water Deep Color;4;0;Create;True;0;0;0;False;0;False;0,0.3077029,0.6132076,0;0,0.2377969,0.4705881,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.TexturePropertyNode;447;3360,368;Inherit;True;Property;_CausticsTexture;Caustics Texture;21;0;Create;True;0;0;0;False;0;False;0695fb758668c584badb3d37cc51ed53;0695fb758668c584badb3d37cc51ed53;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.RegisterLocalVarNode;357;1328,2064;Inherit;False;Foam Mask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;380;1808,-368;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;455;3712,192;Inherit;True;Property;_TextureSample13;Texture Sample 13;28;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.OneMinusNode;467;4288,384;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;486;4256,464;Inherit;False;444;Edge Mask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;460;4224,544;Inherit;False;Property;_CausticsIntensity;Caustics Intensity;22;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;516;5104,-2432;Inherit;False;Edge Foam;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;587;1504,-608;Inherit;False;Property;_BackgroundIntensityMultiplier;Background Intensity Multiplier;31;0;Create;True;0;0;0;False;0;False;0;0.72;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;110;-64,-880;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;598;496,-720;Inherit;False;595;Reflection;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;599;400,-640;Inherit;False;Property;_ReflectionAmount;Reflection Amount;33;0;Create;True;0;0;0;False;0;False;0;0.299;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;264;1648,-704;Inherit;False;263;Scene Color;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;382;1952,-368;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;461;4464,224;Inherit;False;4;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;358;2496,-368;Inherit;False;357;Foam Mask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;517;2496,-288;Inherit;False;516;Edge Foam;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;586;1840,-704;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.BlendOpsNode;600;736,-784;Inherit;False;HardLight;True;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;1;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;279;2656,-432;Inherit;False;Constant;_Float6;Float 6;34;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;93;2128,-496;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;518;2688,-352;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;468;4624,224;Inherit;False;Caustics;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;278;2832,-496;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;1;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;469;3040,-416;Inherit;False;468;Caustics;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;456;3264,-496;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;66;1664,448;Inherit;False;Property;_Smoothness;Smoothness;2;0;Create;True;0;0;0;False;0;False;1;0.932;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;571;1760,528;Inherit;False;444;Edge Mask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;627;1536,704;Inherit;False;Property;_EdgeLength;Edge Length;0;0;Create;True;0;0;0;False;0;False;15;0;1;250;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;277;3520,-496;Inherit;False;Color Final;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;209;-432,272;Inherit;False;Constant;_Float7;Float 7;33;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;325;-432,336;Inherit;False;Constant;_Float8;Float 8;42;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;591;3504,-1344;Inherit;False;251;Normals Final;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;564;-416,912;Inherit;False;Constant;_Float11;Float 11;31;0;Create;True;0;0;0;False;0;False;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;565;-224,896;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;390;736,2400;Inherit;False;357;Foam Mask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;480;704,2784;Inherit;False;479;Normals Water;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TransformDirectionNode;371;912,2784;Inherit;False;Tangent;World;True;Fast;False;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.FunctionNode;369;912,2400;Inherit;False;PreparePerturbNormalHQ;-1;;31;ce0790c3228f3654b818a19dd51453a4;0;1;1;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT3;4;FLOAT3;6;FLOAT;9
Node;AmplifyShaderEditor.FunctionNode;370;1216,2400;Inherit;False;PerturbNormalHQ;-1;;32;45dff16e78a0685469fed8b5b46e4d96;0;4;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TransformDirectionNode;372;1632,2400;Inherit;False;World;Tangent;False;Fast;False;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;366;1568,2576;Inherit;False;Property;_FoamNormalsStrength;Foam Normals Strength;18;0;Create;True;0;0;0;False;0;False;0.03;0.101;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node;374;1664,2256;Inherit;False;Constant;_Vector0;Vector 0;27;0;Create;True;0;0;0;False;0;False;0,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.LerpOp;373;1920,2384;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;482;2096,2384;Inherit;False;Normals Foam;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.FunctionNode;362;64,2480;Inherit;False;Normal From Texture;-1;;33;9728ee98a55193249b513caf9a0f1676;13,149,0,147,0,143,0,141,0,139,0,151,0,137,0,153,0,159,0,157,0,155,0,135,0,108,0;4;87;SAMPLER2D;0;False;85;FLOAT2;0,0;False;74;SAMPLERSTATE;0;False;91;FLOAT;1.5;False;2;FLOAT3;40;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;365;384,2480;Inherit;False;Foam Normals;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;385;1952,448;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;266;1200,1360;Inherit;False;201;Water Depth Texture;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;394;1488,1360;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0.3;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;396;1600,1360;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;383;1760,1360;Inherit;False;Edge Mask Texture;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;406;1920,640;Inherit;False;405;WPO Final;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.ColorNode;575;2002.221,28.42853;Inherit;False;Constant;_Color0;Color 0;31;0;Create;True;0;0;0;False;0;False;0.5377358,0,0,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.GetLocalVarNode;581;1570.21,279.2276;Inherit;False;595;Reflection;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;252;1888,368;Inherit;False;251;Normals Final;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;292;1920,288;Inherit;False;277;Color Final;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.EdgeLengthCullTessNode;628;1824,720;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;2176,336;Float;False;True;-1;6;Polyart.OceanMaterialCustomInspector;0;0;Standard;M_Ocean;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;False;True;True;False;Off;1;False;;0;False;;False;0;False;;0;False;;False;0;Opaque;0.5;True;False;0;False;Opaque;;Geometry;ForwardOnly;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;True;0;32;50;300;False;0.5;True;0;5;False;;10;False;;0;5;False;;10;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;1;Include;GerstnerWave.hlsl;False;;Custom;False;0;0;;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;232;0;227;1
WireConnection;232;1;227;2
WireConnection;230;0;226;1
WireConnection;230;1;226;3
WireConnection;231;0;230;0
WireConnection;231;1;232;0
WireConnection;234;0;227;3
WireConnection;234;1;227;4
WireConnection;233;0;231;0
WireConnection;233;1;234;0
WireConnection;223;1;233;0
WireConnection;248;0;223;1
WireConnection;248;1;623;1
WireConnection;624;0;248;0
WireConnection;624;1;623;2
WireConnection;76;0;75;1
WireConnection;76;1;75;3
WireConnection;272;0;243;2
WireConnection;272;1;624;0
WireConnection;71;1;73;0
WireConnection;71;2;74;0
WireConnection;71;3;76;0
WireConnection;71;4;77;0
WireConnection;260;0;71;2
WireConnection;201;0;272;0
WireConnection;612;1;261;0
WireConnection;235;0;259;0
WireConnection;235;2;245;0
WireConnection;250;0;235;0
WireConnection;250;1;612;0
WireConnection;405;0;250;0
WireConnection;350;0;352;0
WireConnection;350;1;348;0
WireConnection;351;0;350;0
WireConnection;349;0;351;0
WireConnection;349;1;351;2
WireConnection;194;0;155;0
WireConnection;194;1;165;0
WireConnection;168;0;157;0
WireConnection;168;1;169;0
WireConnection;170;0;168;0
WireConnection;170;1;194;0
WireConnection;158;0;157;0
WireConnection;158;1;155;0
WireConnection;353;0;349;0
WireConnection;181;0;157;0
WireConnection;181;1;179;0
WireConnection;180;0;155;0
WireConnection;180;1;178;0
WireConnection;171;0;170;0
WireConnection;174;0;172;0
WireConnection;159;0;158;0
WireConnection;182;0;181;0
WireConnection;182;1;180;0
WireConnection;173;0;171;0
WireConnection;173;1;174;0
WireConnection;193;0;354;0
WireConnection;193;1;194;0
WireConnection;154;0;354;0
WireConnection;154;1;155;0
WireConnection;160;0;159;0
WireConnection;160;1;161;0
WireConnection;184;0;182;0
WireConnection;185;0;183;0
WireConnection;166;0;193;0
WireConnection;166;1;173;0
WireConnection;156;0;154;0
WireConnection;156;1;160;0
WireConnection;195;0;354;0
WireConnection;195;1;180;0
WireConnection;186;0;184;0
WireConnection;186;1;185;0
WireConnection;175;0;150;0
WireConnection;175;1;166;0
WireConnection;162;0;150;0
WireConnection;162;1;156;0
WireConnection;187;0;195;0
WireConnection;187;1;186;0
WireConnection;188;0;150;0
WireConnection;188;1;187;0
WireConnection;163;0;162;0
WireConnection;163;1;196;0
WireConnection;176;0;175;0
WireConnection;176;1;196;0
WireConnection;189;0;188;0
WireConnection;189;1;196;0
WireConnection;342;0;163;0
WireConnection;342;1;176;0
WireConnection;343;0;342;0
WireConnection;343;1;189;0
WireConnection;344;0;343;0
WireConnection;345;0;344;0
WireConnection;345;1;344;1
WireConnection;345;2;346;0
WireConnection;255;0;71;3
WireConnection;253;0;345;0
WireConnection;441;0;416;0
WireConnection;442;0;441;0
WireConnection;197;0;256;0
WireConnection;197;1;257;0
WireConnection;444;0;442;0
WireConnection;479;0;197;0
WireConnection;423;0;420;0
WireConnection;422;0;421;2
WireConnection;422;1;423;1
WireConnection;568;0;570;0
WireConnection;568;1;481;0
WireConnection;568;2;569;0
WireConnection;577;0;422;0
WireConnection;251;0;568;0
WireConnection;619;0;71;2
WireConnection;619;1;618;4
WireConnection;415;0;577;0
WireConnection;417;0;416;0
WireConnection;417;1;419;0
WireConnection;616;0;615;0
WireConnection;275;0;619;0
WireConnection;284;0;282;0
WireConnection;284;1;283;0
WireConnection;418;0;417;0
WireConnection;530;0;531;0
WireConnection;471;0;470;0
WireConnection;472;0;470;0
WireConnection;492;0;490;0
WireConnection;492;1;493;0
WireConnection;84;0;276;0
WireConnection;84;1;80;0
WireConnection;608;0;601;0
WireConnection;608;1;605;0
WireConnection;608;2;609;0
WireConnection;617;0;616;0
WireConnection;360;0;359;0
WireConnection;360;1;361;0
WireConnection;280;0;281;0
WireConnection;280;1;282;0
WireConnection;280;2;284;0
WireConnection;523;0;522;1
WireConnection;523;1;522;3
WireConnection;529;0;617;0
WireConnection;529;1;530;0
WireConnection;107;0;106;0
WireConnection;440;0;418;0
WireConnection;450;0;449;0
WireConnection;473;0;471;0
WireConnection;473;1;472;0
WireConnection;498;0;492;0
WireConnection;498;1;507;0
WireConnection;499;0;501;0
WireConnection;87;0;84;0
WireConnection;607;0;606;0
WireConnection;607;1;608;0
WireConnection;364;0;363;0
WireConnection;364;1;360;0
WireConnection;298;0;299;0
WireConnection;298;1;300;0
WireConnection;298;2;280;0
WireConnection;400;0;397;0
WireConnection;524;0;523;0
WireConnection;524;1;525;0
WireConnection;532;0;529;0
WireConnection;532;1;525;0
WireConnection;97;0;94;0
WireConnection;97;1;107;0
WireConnection;443;0;440;0
WireConnection;443;1;444;0
WireConnection;451;0;450;0
WireConnection;451;1;450;2
WireConnection;474;0;473;0
WireConnection;474;1;475;0
WireConnection;497;0;498;0
WireConnection;497;1;499;0
WireConnection;86;0;83;1
WireConnection;86;1;83;2
WireConnection;445;0;87;0
WireConnection;445;1;446;0
WireConnection;604;0;601;0
WireConnection;604;1;607;0
WireConnection;604;2;603;0
WireConnection;297;0;364;1
WireConnection;297;1;298;0
WireConnection;398;0;400;0
WireConnection;526;0;524;0
WireConnection;526;1;532;0
WireConnection;104;0;97;0
WireConnection;413;0;443;0
WireConnection;476;0;451;0
WireConnection;476;1;474;0
WireConnection;496;0;497;0
WireConnection;515;0;492;0
WireConnection;484;0;478;0
WireConnection;484;1;485;0
WireConnection;89;0;445;0
WireConnection;89;1;86;0
WireConnection;596;0;604;0
WireConnection;304;0;297;0
WireConnection;399;0;398;0
WireConnection;376;0;104;0
WireConnection;376;1;413;0
WireConnection;436;0;434;0
WireConnection;436;1;439;0
WireConnection;477;0;476;0
WireConnection;477;1;484;0
WireConnection;463;0;462;0
WireConnection;463;1;465;0
WireConnection;508;0;496;0
WireConnection;512;0;515;0
WireConnection;82;0;89;0
WireConnection;594;1;596;0
WireConnection;519;1;526;0
WireConnection;389;0;304;0
WireConnection;389;1;399;0
WireConnection;389;2;418;0
WireConnection;377;0;376;0
WireConnection;438;0;436;0
WireConnection;452;0;477;0
WireConnection;452;1;453;0
WireConnection;466;0;463;0
WireConnection;505;0;512;0
WireConnection;505;1;508;0
WireConnection;505;2;510;0
WireConnection;505;3;519;1
WireConnection;505;4;566;0
WireConnection;263;0;82;0
WireConnection;595;0;594;5
WireConnection;146;0;111;0
WireConnection;146;1;147;0
WireConnection;146;2;148;0
WireConnection;357;0;389;0
WireConnection;380;0;377;0
WireConnection;380;1;438;0
WireConnection;455;0;447;0
WireConnection;455;1;452;0
WireConnection;467;0;466;0
WireConnection;516;0;505;0
WireConnection;110;0;116;5
WireConnection;110;1;57;5
WireConnection;110;2;146;0
WireConnection;382;0;380;0
WireConnection;461;0;455;5
WireConnection;461;1;467;0
WireConnection;461;2;486;0
WireConnection;461;3;460;0
WireConnection;586;0;264;0
WireConnection;586;1;587;0
WireConnection;600;0;598;0
WireConnection;600;1;110;0
WireConnection;600;2;599;0
WireConnection;93;0;600;0
WireConnection;93;1;586;0
WireConnection;93;2;382;0
WireConnection;518;0;358;0
WireConnection;518;1;517;0
WireConnection;468;0;461;0
WireConnection;278;0;93;0
WireConnection;278;1;279;0
WireConnection;278;2;518;0
WireConnection;456;0;278;0
WireConnection;456;1;469;0
WireConnection;277;0;456;0
WireConnection;565;0;418;0
WireConnection;565;1;564;0
WireConnection;371;0;480;0
WireConnection;369;1;390;0
WireConnection;370;1;369;0
WireConnection;370;2;369;4
WireConnection;370;3;369;6
WireConnection;370;4;371;0
WireConnection;372;0;370;0
WireConnection;373;0;374;0
WireConnection;373;1;372;0
WireConnection;373;2;366;0
WireConnection;482;0;373;0
WireConnection;362;87;363;0
WireConnection;362;85;360;0
WireConnection;362;74;363;1
WireConnection;362;91;366;0
WireConnection;365;0;362;40
WireConnection;385;1;66;0
WireConnection;385;2;571;0
WireConnection;394;0;266;0
WireConnection;396;0;394;0
WireConnection;383;0;396;0
WireConnection;628;0;627;0
WireConnection;0;0;292;0
WireConnection;0;1;252;0
WireConnection;0;4;385;0
WireConnection;0;11;406;0
WireConnection;0;14;628;0
ASEEND*/
//CHKSM=EB73CA71F73BF16B539099BE1FC8B5820D7DD0DD