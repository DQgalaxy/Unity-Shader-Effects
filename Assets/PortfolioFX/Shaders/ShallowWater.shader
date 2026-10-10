Shader "PortfolioFX/ShallowWater"
{
    Properties
    {
        [MainTexture] _BaseMap("Water Albedo", 2D) = "white" {}
        _NormalTex("Wave Normal", 2D) = "bump" {}
        _WaveTex("Wave Noise", 2D) = "gray" {}
        _FoamTex("Foam / Ripple", 2D) = "white" {}
        [MainColor] _ShallowColor("Shallow Color", Color) = (0.09, 0.27, 0.25, 1)
        _DeepColor("Deep Color", Color) = (0.012, 0.065, 0.075, 1)
        _FoamColor("Foam Color", Color) = (0.48, 0.78, 0.70, 1)
        _SpecColor("Cold Grey Green Specular", Color) = (0.58, 0.88, 0.79, 1)
        _Alpha("Water Opacity", Range(0,1)) = 0.78
        _NormalStrength("Normal Strength", Range(0,2)) = 0.72
        _NormalTiling("Normal Tiling", Float) = 0.38
        _NormalScroll("Normal Scroll", Vector) = (0.018, -0.012, 0, 0)
        _WaveTexScale("Wave Noise Scale", Float) = 0.075
        _WaveTexScroll("Wave Noise Scroll", Vector) = (0.008, -0.006, 0, 0)
        _WaveAmplitude("Wave Amplitude", Range(0,0.6)) = 0.095
        _WaveFrequency("Wave Frequency", Float) = 1.35
        _WaveSpeed("Wave Speed", Float) = 0.78
        _FootAmplitude("Foot Wave Amplitude", Range(0,0.5)) = 0.13
        _FootRadius("Foot Core Radius", Range(0.01,1)) = 0.26
        _FootWaveLength("Foot Wave Length", Range(0.2,4)) = 1.35
        _FootWaveSpeed("Foot Wave Speed", Float) = 1.55
        _FootLifetime("Foot Wave Lifetime", Range(0.5,8)) = 2.8
        _FootFalloff("Foot Wave Falloff", Float) = 1.6
        _FootFoamFalloff("Foot Foam Falloff", Float) = 2.8
        _FoamStrength("Foam Strength", Range(0,2)) = 0.8
        _DepthFade("Shore Depth Fade", Range(0.1,8)) = 2.4
        _FresnelPower("Fresnel Power", Range(0.5,8)) = 4.0
        _SpecularPower("Specular Power", Range(8,256)) = 96
        _InteractionTime("Interaction Time", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ShallowWaterForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define FOOT_CAPACITY 24

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);
            TEXTURE2D(_WaveTex); SAMPLER(sampler_WaveTex);
            TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NormalTex_ST;
                float4 _WaveTex_ST;
                float4 _FoamTex_ST;
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float4 _SpecColor;
                float4 _NormalScroll;
                float4 _WaveTexScroll;
                float _Alpha;
                float _NormalStrength;
                float _NormalTiling;
                float _WaveTexScale;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
                float _FootAmplitude;
                float _FootRadius;
                float _FootWaveLength;
                float _FootWaveSpeed;
                float _FootLifetime;
                float _FootFalloff;
                float _FootFoamFalloff;
                float _FoamStrength;
                float _DepthFade;
                float _FresnelPower;
                float _SpecularPower;
                float _InteractionTime;
                float4 _FootHits[FOOT_CAPACITY];
                float4 _FootStrength[FOOT_CAPACITY];
            CBUFFER_END

            struct WaveState
            {
                float height;
                float2 gradient;
                float foam;
            };

            void AddDirectionalWave(inout WaveState state, float2 position, float2 direction, float amplitude, float frequency, float speed, float time)
            {
                float phase = dot(position, direction) * frequency + time * speed;
                float wave = sin(phase);
                state.height += wave * amplitude;
                state.gradient += cos(phase) * amplitude * frequency * direction;
                state.foam += smoothstep(0.70, 0.98, wave) * amplitude * 2.5;
            }

            WaveState EvaluateSurface(float2 position, float time)
            {
                WaveState state = (WaveState)0;

                float2 d0 = normalize(float2(0.86, 0.51));
                float2 d1 = normalize(float2(-0.42, 0.91));
                float2 d2 = normalize(float2(0.18, -0.98));
                AddDirectionalWave(state, position, d0, _WaveAmplitude, _WaveFrequency, _WaveSpeed, time);
                AddDirectionalWave(state, position, d1, _WaveAmplitude * 0.62, _WaveFrequency * 1.63, _WaveSpeed * 1.21, time + 1.73);
                AddDirectionalWave(state, position, d2, _WaveAmplitude * 0.36, _WaveFrequency * 2.25, _WaveSpeed * 0.73, time + 3.11);

                float2 noiseUv = position * _WaveTexScale + time * _WaveTexScroll.xy;
                float2 noise = SAMPLE_TEXTURE2D_LOD(_WaveTex, sampler_WaveTex, noiseUv, 0).rg * 2.0 - 1.0;
                state.height += dot(noise, float2(0.055, 0.035));
                state.gradient += noise * 0.025;

                float footFrequency = 6.2831853 / max(_FootWaveLength, 0.05);
                [unroll]
                for (int index = 0; index < FOOT_CAPACITY; index++)
                {
                    float strength = max(_FootStrength[index].x, 0.0);
                    // CPU stores the hit as (worldX, worldZ, timestamp, 1).
                    // Keep the timestamp out of the spatial lookup; using the
                    // old y/z mix made the rings appear at unrelated positions.
                    float age = time - _FootHits[index].z;
                    float alive = step(0.001, strength) * step(0.0, age) * saturate(1.0 - age / max(_FootLifetime, 0.01));
                    float2 delta = position - _FootHits[index].xy;
                    float radius = max(length(delta), 0.0001);
                    float2 radial = delta / radius;

                    float distanceFromRing = radius - age * _FootWaveSpeed;
                    float phase = distanceFromRing * footFrequency;
                    float envelope = exp(-abs(distanceFromRing) * _FootFalloff) * alive;
                    float crest = sin(phase) * envelope;
                    float ringSlope = cos(phase) * footFrequency - sin(phase) * _FootFalloff * sign(distanceFromRing);
                    float core = 1.0 - smoothstep(0.0, max(_FootRadius, 0.01), radius);

                    state.height += strength * _FootAmplitude * (crest + core * alive * 0.24);
                    state.gradient += radial * strength * _FootAmplitude * (ringSlope * envelope - core * alive * 0.9);
                    state.foam += strength * alive * (exp(-radius * _FootFoamFalloff) * 0.72 + smoothstep(0.86, 1.0, sin(phase)) * envelope);
                }

                return state;
            }

            float3 MakeSurfaceNormal(float2 gradient)
            {
                return normalize(float3(-gradient.x, 1.0, -gradient.y));
            }

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float foam : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
                half fogFactor : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                WaveState state = EvaluateSurface(positionWS.xz, _InteractionTime);
                positionWS.y += state.height;
                output.positionWS = positionWS;
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.normalWS = MakeSurfaceNormal(state.gradient);
                output.uv = input.uv;
                output.foam = state.foam;
                output.screenPos = ComputeScreenPos(output.positionHCS);
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                return output;
            }

            float3 ApplyNormalTexture(float3 surfaceNormal, float2 positionWS, float time)
            {
                float2 uv0 = positionWS * _NormalTiling + time * _NormalScroll.xy;
                float2 uv1 = positionWS * (_NormalTiling * 1.73) - time * _NormalScroll.yx * 0.73;
                float3 sample0 = SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv0).xyz * 2.0 - 1.0;
                float3 sample1 = SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv1).xyz * 2.0 - 1.0;
                float3 tangentNormal = normalize(float3((sample0.xy + sample1.yx * float2(0.72, -0.72)) * _NormalStrength, 1.0));

                float3 tangent = normalize(float3(1.0, 0.0, 0.0) - surfaceNormal * surfaceNormal.x);
                float3 bitangent = normalize(cross(surfaceNormal, tangent));
                return normalize(tangent * tangentNormal.x + bitangent * tangentNormal.y + surfaceNormal * tangentNormal.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUv = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                float rawSceneDepth = SampleSceneDepth(screenUv);
                float sceneEyeDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                float waterEyeDepth = LinearEyeDepth(input.screenPos.z / max(input.screenPos.w, 0.0001), _ZBufferParams);
                float shoreDepth = saturate((sceneEyeDepth - waterEyeDepth) / max(_DepthFade, 0.01));

                float3 normalWS = ApplyNormalTexture(normalize(input.normalWS), input.positionWS.xz, _InteractionTime);
                float3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight();
                float3 lightDirection = SafeNormalize(mainLight.direction);
                float3 halfDirection = SafeNormalize(lightDirection + viewDirection);
                float ndl = saturate(dot(normalWS, lightDirection));
                float ndh = saturate(dot(normalWS, halfDirection));
                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDirection)), _FresnelPower);

                float2 surfaceUv = input.positionWS.xz * 0.05 + _InteractionTime * float2(0.006, -0.004);
                float3 baseTexture = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, surfaceUv).rgb;
                float foamTexture = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, surfaceUv * 1.85 + float2(_InteractionTime * 0.012, 0)).r;
                float foamMask = saturate(input.foam * _FoamStrength + foamTexture * 0.16 + fresnel * 0.08);

                float3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, shoreDepth) * baseTexture;
                float3 diffuse = waterColor * (0.20 + ndl * mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation);
                float3 specular = _SpecColor.rgb * pow(ndh, _SpecularPower) * (0.18 + fresnel * 1.7) * mainLight.color;
                float3 foamColor = _FoamColor.rgb * foamMask * (0.28 + 0.72 * ndl);
                float3 color = diffuse + specular + foamColor + _SpecColor.rgb * fresnel * 0.08;
                color = MixFog(color, input.fogFactor);

                float alpha = saturate(_Alpha * (0.58 + 0.34 * shoreDepth + 0.24 * foamMask));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
