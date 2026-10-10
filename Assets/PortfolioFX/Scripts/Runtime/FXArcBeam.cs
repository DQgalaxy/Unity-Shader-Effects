using UnityEngine;

namespace PortfolioFX
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class FXArcBeam : MonoBehaviour
    {
        public Transform from;
        public Transform to;
        public float startTime = 1;
        public float endTime = 4;
        public float displacement = .2f;
        public int seed;
        [Min(.001f)] public float minWidth = .015f;
        [Min(.001f)] public float maxWidth = .05f;
        public bool driveShader;
        LineRenderer line;
        MaterialPropertyBlock block;
        static readonly int EffectTime = Shader.PropertyToID("_EffectTime");
        static readonly int Intensity = Shader.PropertyToID("_Intensity");

        public void Sample(float time, float intensity)
        {
            if (line == null) line = GetComponent<LineRenderer>();
            line.enabled = from != null && to != null && time >= startTime && time < endTime;
            if (!line.enabled) return;
            const int count = 25;
            line.positionCount = count;
            Vector3 a = from.position, b = to.position;
            Vector3 direction = (b - a).normalized;
            Vector3 side = Vector3.Cross(direction, Vector3.up);
            if (side.sqrMagnitude < .001f) side = Vector3.Cross(direction, Vector3.right);
            side.Normalize();
            Vector3 up = Vector3.Cross(side, direction).normalized;
            float frame = Mathf.Floor(time * 22);
            for (int i = 0; i < count; i++)
            {
                float u = i / (count - 1f);
                float envelope = Mathf.Sin(u * Mathf.PI) * displacement;
                float x = (Mathf.PerlinNoise(i * 1.73f + seed, frame * .67f) - .5f) * 2;
                float y = (Mathf.PerlinNoise(i * 2.13f + seed + 19, frame * .83f) - .5f) * 2;
                line.SetPosition(i, Vector3.Lerp(a, b, u) + (side * x + up * y) * envelope);
            }
            line.widthMultiplier = Mathf.Lerp(Mathf.Max(.001f, minWidth), Mathf.Max(.001f, maxWidth), Mathf.Clamp01(intensity));
            if (driveShader)
            {
                if (block == null) block = new MaterialPropertyBlock();
                line.GetPropertyBlock(block);
                block.SetFloat(EffectTime, time);
                block.SetFloat(Intensity, Mathf.Max(0, intensity));
                line.SetPropertyBlock(block);
            }
        }
    }
}
