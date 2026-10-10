#ifndef PORTFOLIO_SHIELD_SEQUENCE_WAVES_INCLUDED
#define PORTFOLIO_SHIELD_SEQUENCE_WAVES_INCLUDED

// Adapted from project 1 / Resources/12/PlaneHitWaves.hlsl used by 13Hexagon.
// xyz: world contact; w: normalized remaining lifetime. Data is per renderer.
float4 _ShieldHits[3];
float4 _ShieldRadii;

void ShieldSequenceWaves_float(float3 WorldPos, float Noise, float HitSpread,
    float HitFadeDistance, float HitFadePower, UnityTexture2D RampMap, out float3 Color)
{
    float waveSum = 0;
    for (int i = 0; i < 3; i++)
    {
        float dist = distance(WorldPos, _ShieldHits[i].xyz);
        float width = max(HitSpread, 0.01);
        float offset = dist - _ShieldRadii[i] + (Noise - 0.5) * 0.08;
        float rampUV = saturate(0.06 + 0.94 * abs(offset) / width);
        float ramp = SAMPLE_TEXTURE2D_LOD(RampMap.tex, RampMap.samplerstate, float2(rampUV, 0.5), 0).r;
        // The envelope guarantees zero outside the band even with nonblack ramp borders.
        float envelope = saturate(1 - abs(offset) / width);
        float fade = saturate(1 - dist / max(HitFadeDistance, 0.01));
        float contact = exp2(-dist * dist * 16) * pow(saturate(_ShieldHits[i].w), 6);
        // Keep a broad shoulder; multiplying two narrow masks hid the moving cells.
        float band = (0.35 + 0.65 * ramp) * smoothstep(0, 1, envelope);
        waveSum += (band * pow(fade, max(HitFadePower, 0.01)) * 1.65 + contact)
            * saturate(_ShieldHits[i].w);
    }
    Color = saturate(waveSum).xxx;
}
#endif
