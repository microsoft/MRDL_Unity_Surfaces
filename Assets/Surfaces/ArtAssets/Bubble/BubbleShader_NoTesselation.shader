Shader "MRDL/Bubble (No Tesselation)" {
	Properties{
		_GazeTex("Gaze Texture", 2D) = "white" {}
		_GazeTexScale("Gaze Texture Scale", Range(0.1, 10)) = 2
		_FlowTex("Flow Texture", 2D) = "white" {}
		_FreezeTex("Freeze Texture", 2D) = "white" {}

		_TexScale("Texture Scale", Range(0.0,100.0)) = 1
		_TexScrollSpeed("Texture Scroll Speed", Vector) = (0,1,0)
		_EdgeLength("Edge length", Range(2,50)) = 5
		_Phong("Phong Strengh", Range(0,1)) = 0.5
		_Color("Main Color", Color) = (1,1,1,1)
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
		_RimColor("Rim Color",  Color) = (1,1,1,1)
		_RimPower("Rim Power", Range(0.5,8.0)) = 3.0
		_Cube("Cubemap", CUBE) = "" {}
		_CubeColor("Cube Color", Color) = (1,1,1,1)
		_InnerTransparency("InnerTransparency", Range(0,1)) = 1
		_Transparency("Transparency", Range(0,1)) = 1
		_Freeze("Freeze", Range(0,1)) = 1
		_Highlight("Highlight", Range(0,1)) = 0
		_Gaze("Gaze", Range(0,1)) = 0
	}

		SubShader{

		Tags{ "Queue" = "Transparent" "RenderType" = "Transparent" }
		LOD 100

		ZWrite On
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM
			#pragma target 5.0
			#pragma surface surf Standard alpha:blend nolightmap// noinstancing
			#include "UnityCG.cginc"
			//#include "Tessellation.cginc"			

			float _Phong;
			float _EdgeLength;

			struct appdata {
				float4 vertex : POSITION;
				float4 tangent : TANGENT;
				float3 normal : NORMAL;
				float2 texcoord : TEXCOORD0;
				float2 texcoord1 : TEXCOORD1;
				float2 texcoord2 : TEXCOORD2;
			};

			struct Input {
				float3 viewDir;
				float3 worldRefl;
				float2 uv_MainTex;
				float3 worldPos;
				float3 worldNormal;
				float4 screenPos;
			};

			float _TexScale;
			float _GazeTexScale;
			float3 _TexScrollSpeed;
			float4 _Color;
			float4 _RimColor;
			float4 _CubeColor;
			float _RimPower;
			half _Glossiness;
			half _Metallic;
			float _InnerTransparency;
			float _Transparency;
			float _Freeze;
			samplerCUBE _Cube;
			sampler2D _GazeTex;
			sampler2D _FlowTex;
			sampler2D _FreezeTex;
			float _Highlight;
			float _Gaze;

			void surf(Input IN, inout SurfaceOutputStandard o) {

				float3 projNormal = saturate(pow(IN.worldNormal * 1.4, 4));
				float3 worldPos = IN.worldPos;
				float3 flowPos = worldPos;
				float freezeLerp = 1 - _Freeze;
				float4 color = 1;

				flowPos.x += _Time * _TexScrollSpeed.x;
				flowPos.y += _Time * _TexScrollSpeed.y;
				flowPos.z += _Time * _TexScrollSpeed.z;

				color = lerp(color, _Color, freezeLerp);

				// SIDE X
				float3 XTex = lerp(tex2D(_FreezeTex, frac(flowPos.zy * _TexScale)), tex2D(_FlowTex, frac(flowPos.zy * _TexScale)), freezeLerp) * abs(IN.worldNormal.x);

				// TOP / BOTTOM
				float3 YTex = 0;
				if (IN.worldNormal.y > 0) {
					YTex = lerp(tex2D(_FreezeTex, frac(flowPos.zx * _TexScale)), tex2D(_FlowTex, frac(flowPos.zx * _TexScale)), freezeLerp) * abs(IN.worldNormal.y);
				}
 else {
  YTex = lerp(tex2D(_FreezeTex, frac(flowPos.zx * _TexScale)), tex2D(_FlowTex, frac(flowPos.zx * _TexScale)), freezeLerp) * abs(IN.worldNormal.y);
}

				// SIDE Z
				float3 ZTex = lerp(tex2D(_FreezeTex, frac(flowPos.xy * _TexScale)), tex2D(_FlowTex, frac(flowPos.xy * _TexScale)), freezeLerp) * abs(IN.worldNormal.z);

				o.Albedo = ZTex;
				o.Albedo = lerp(o.Albedo, XTex, projNormal.x);
				o.Albedo = lerp(o.Albedo, YTex, projNormal.y);

				color.a *= _Transparency;

				o.Albedo *= color;
				o.Metallic = _Metallic;
				o.Smoothness = _Glossiness;

				half lum = Luminance(o.Albedo.rgb);
				half freezeAlpha = saturate(_Freeze * lum * 2);

				// Screen space gaze UV
				float2 screenUV = (IN.screenPos.xy / IN.screenPos.w) * _GazeTexScale;
				fixed4 gazeTex = tex2D(_GazeTex, screenUV) * _RimColor;

				half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
				o.Emission = ((pow(rim, _RimPower) * _RimColor) * _Transparency)
							+ (gazeTex.rgb * _Color.rgb * _Gaze)
							+ ((texCUBE(_Cube, WorldReflectionVector(IN, o.Normal)).rgb * _CubeColor) * _Transparency);

				half alpha = pow(saturate(rim + (1 - _InnerTransparency)), 2) + (_Gaze * 0.1);
				rim = saturate((rim * _RimColor.a * _Transparency) - freezeAlpha);
				rim += (_Highlight * alpha * 2);

				o.Alpha = saturate((rim + lum * alpha) + freezeAlpha + (_Highlight * 0.1));
			}
			ENDCG
	}
		Fallback "Diffuse"
}