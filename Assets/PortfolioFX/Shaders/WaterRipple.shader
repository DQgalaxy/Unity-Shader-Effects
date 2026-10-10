Shader "PortfolioFX/WaterRipple"
{
 Properties { _RippleTex("Ripple Mask",2D)="white"{} _Color("Color",Color)=(.42,.9,1,1) _Opacity("Opacity",Range(0,1))=.55 _Progress("Progress",Range(0,1))=0 }
 SubShader
 {
  Tags {"Queue"="Transparent+5" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
  Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};struct V{float4 positionHCS:SV_POSITION;float2 uv:TEXCOORD0;};
   TEXTURE2D(_RippleTex);SAMPLER(sampler_RippleTex);
   CBUFFER_START(UnityPerMaterial)float4 _Color;float _Opacity,_Progress;CBUFFER_END
   V vert(A v){V o;o.positionHCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
   half4 frag(V i):SV_Target{float2 p=i.uv-.5;float r=length(p)*2;float radius=lerp(.08,1.72,_Progress);float width=lerp(.13,.035,_Progress);float band=1-smoothstep(width,width+.045,abs(r-radius));float ring=SAMPLE_TEXTURE2D(_RippleTex,sampler_RippleTex,float2(frac(r*.5+_Progress*.18),.5)).r;float edge=1-smoothstep(.72,1.04,r);float birth=smoothstep(0,.08,_Progress);float fade=pow(1-_Progress,1.15);float a=band*edge*(.24+ring*.76)*birth*fade*_Opacity;return half4(_Color.rgb,a);}
   ENDHLSL
  }
 }
}


