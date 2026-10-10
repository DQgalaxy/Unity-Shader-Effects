Shader "PortfolioFX/PortalVortex"
{
    Properties
    {
        _WorldTex("Destination",2D)="black"{}
        _NoiseTex("Vortex Noise",2D)="white"{}
        _RingTex("Ring Mask",2D)="white"{}
        _Tint("Tint",Color)=(0.12,0.75,1,1)
        _Core("Core",Color)=(0.55,0.95,1,1)
        _Opacity("Opacity",Range(0,2))=1
        _Rotation("Rotation",Float)=1
        _Distortion("Distortion",Range(0,1))=.22
        _Rim("Rim",Range(0,4))=1.5
    }
    SubShader
    {
        Tags {"RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Blend SrcAlpha OneMinusSrcAlpha
ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION; float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0;};
            TEXTURE2D(_WorldTex);SAMPLER(sampler_WorldTex);TEXTURE2D(_NoiseTex);SAMPLER(sampler_NoiseTex);TEXTURE2D(_RingTex);SAMPLER(sampler_RingTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint,_Core;float _Opacity,_Rotation,_Distortion,_Rim;
            CBUFFER_END
            Varyings vert(Attributes v){Varyings o;o.positionHCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.uv-.5;float r=length(p)*2;float a=atan2(p.y,p.x);
                float t=_Time.y*_Rotation;float2 uv=float2(a/6.2831853+.5+t*.06,r*.7);
                float n=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,uv+float2(t*.035,0)).r;
                float n2=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,uv*1.7-float2(t*.02,0)).g;
                float spiral=saturate(1-abs(frac(uv.x+n*_Distortion)-.5)*2);
                float ring=SAMPLE_TEXTURE2D(_RingTex,sampler_RingTex,float2(r*.5,.5)).r;
                // Read the grayscale maps as coverage: a soft filled aperture
                // makes this read as a door, while the outer band carries the
                // bright vortex rim. There is no separate white frame.
                float disc=1-smoothstep(.86,1.01,r);
                float rim=smoothstep(.68,.91,r)*(1-smoothstep(.91,1.015,r));
                float textureAlpha=saturate(.22+n*.86)*saturate(.18+ring*.9);
                float swirl=saturate(.18+spiral*.82);
                float alpha=saturate((disc*(.10+swirl*.26)*textureAlpha+rim*(.26+textureAlpha*.62))*_Opacity);
                float core=smoothstep(.86,.10,r);
                float3 destination=SAMPLE_TEXTURE2D(_WorldTex,sampler_WorldTex,i.uv).rgb;
                float3 vortex=lerp(_Tint.rgb,_Core.rgb,saturate(core*.72+spiral*.24));
                float3 col=lerp(destination*(_Core.rgb*1.22),vortex*(_Rim*.72+core*1.35),saturate(.38+spiral*.38));
                return half4(col,alpha);
            }
            ENDHLSL
        }
    }
}



