Shader "PortfolioFX/HologramProjection"
{
 Properties { _MainTex("Projected Scene",2D)="white"{} _Grid("Grid",2D)="white"{} _Tint("Tint",Color)=(.2,.8,1,1) _Opacity("Opacity",Range(0,1))=.65 _ScanSpeed("Scan Speed",Float)=1.2 _Flicker("Flicker",Range(0,1))=.12 }
 SubShader
 {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
  Blend SrcAlpha One ZWrite Off Cull Off
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};struct V{float4 positionHCS:SV_POSITION;float2 uv:TEXCOORD0;};
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);TEXTURE2D(_Grid);SAMPLER(sampler_Grid);
   CBUFFER_START(UnityPerMaterial)float4 _Tint;float _Opacity,_ScanSpeed,_Flicker;CBUFFER_END
   V vert(A v){V o;o.positionHCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
   half4 frag(V i):SV_Target{float2 uv=i.uv;float scan=smoothstep(.35,.5,frac(uv.y-_Time.y*_ScanSpeed));float grid=SAMPLE_TEXTURE2D(_Grid,sampler_Grid,uv*float2(8,12)+_Time.y*.03).r;float flick=1-_Flicker+sin(_Time.y*18+uv.y*30)*_Flicker;half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);float2 q=abs(uv-.5)*2;float frame=1-smoothstep(.86,1,max(q.x,q.y));float a=_Opacity*frame*(.65+scan*.35)*(0.78+grid*.22)*flick;return half4(c.rgb*_Tint.rgb*1.25,a);}
   ENDHLSL
  }
 }
}
