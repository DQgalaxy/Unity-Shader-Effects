Shader "PortfolioFX/HologramMesh"
{
    Properties
    {
        [MainTexture] _BaseMap("Surface",2D)="white"{}
        _Tint("Hologram Tint",Color)=(.18,.9,.78,1)
        _Opacity("Opacity",Range(0,1))=.48
        _ScanSpeed("Scan Speed",Float)=1.4
        _ScanWidth("Scan Width",Range(.01,.5))=.12
        _Edge("Edge Glow",Range(0,4))=1.8
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct V{float4 positionHCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;};
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)float4 _BaseMap_ST;float4 _Tint;float _Opacity,_ScanSpeed,_ScanWidth,_Edge;CBUFFER_END
            V vert(A v){V o;o.positionWS=TransformObjectToWorld(v.positionOS.xyz);o.positionHCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o;}
            half4 frag(V i):SV_Target{float3 n=normalize(i.normalWS);float3 view=SafeNormalize(GetWorldSpaceViewDir(i.positionWS));float fres=pow(saturate(1-dot(n,view)),2.2);float scan=1-smoothstep(0,_ScanWidth,abs(frac(i.positionWS.y*.7-_Time.y*_ScanSpeed)-.5));half4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);float alpha=_Opacity*(.22+scan*.68+fres*.55)*saturate(.55+tex.a*.8);float3 col=_Tint.rgb*(.72+scan*1.25+fres*_Edge);return half4(col,alpha);}
            ENDHLSL
        }
    }
}
