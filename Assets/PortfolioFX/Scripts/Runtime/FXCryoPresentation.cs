using UnityEngine;
using UnityEngine.Rendering;

namespace PortfolioFX
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FXCryoPresentation : MonoBehaviour
    {
        public FXSequenceDriver sequence;
        public Camera presentationCamera;
        public Mesh[] iceMeshes;
        public Material clearIce, frostedIce;
        public Cubemap studioReflection;
        public Renderer[] legacyCrystals;
        [Range(0, 2)] public float reflectionIntensity = .9f;
        GameObject layers;
        Transform[] roots;
        Renderer[] renderers;
        bool[] previousEnabled;
        Light rim, fill;
        ReflectionProbe probe;
        float lastTime = float.NaN;
        const int GroundCount = 16, FragmentCount = 10;

        void OnEnable()
        {
            lastTime = float.NaN;
            previousEnabled = new bool[legacyCrystals == null ? 0 : legacyCrystals.Length];
            for (int i = 0; i < previousEnabled.Length; i++)
                if (legacyCrystals[i] != null) previousEnabled[i] = legacyCrystals[i].enabled;
        }

        void LateUpdate()
        {
            if (sequence == null || clearIce == null || frostedIce == null || iceMeshes == null || iceMeshes.Length < 4) return;
            foreach (var mesh in iceMeshes) if (mesh == null) return;
            if (layers == null) Build();
            if (legacyCrystals != null) foreach (var r in legacyCrystals) if (r != null) r.enabled = false;
            probe.intensity = reflectionIntensity;
            float t = sequence.CurrentTime;
            if (!Application.isPlaying && Mathf.Approximately(t, lastTime)) return;
            lastTime = t;
            for (int i = 0; i < GroundCount; i++)
            {
                // Grow from planted pivots, then withdraw during the existing thaw interval.
                float start = .72f + (i % 8) * .12f;
                float grow = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, start + .8f, t));
                float thaw = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4.55f + (i % 3) * .08f, 5.85f, t));
                float amount = grow * thaw;
                float height = i < 8 ? .8f + (i % 4) * .21f : .22f + (i % 3) * .12f;
                roots[i].localScale = new Vector3(.7f, 1, .85f) * (height * Mathf.Max(.001f, amount));
                renderers[i].enabled = amount > .003f;
            }
            for (int j = 0; j < FragmentCount; j++)
            {
                int i = GroundCount + j;
                float age = t - (4.55f + j * .035f);
                float angle = j * 2.399963f;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 start = outward * .85f + Vector3.up * (.2f + (j % 4) * .22f);
                roots[i].localPosition = start + (outward * 1.1f + Vector3.up * 1.65f) * age + Vector3.down * (1.8f * age * age);
                roots[i].localRotation = Quaternion.Euler(j * 31 + age * 100, j * 57 + age * 70, j * 19);
                float size = (.09f + (j % 3) * .045f) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.5f, .95f, age)));
                roots[i].localScale = Vector3.one * Mathf.Max(.001f, size);
                renderers[i].enabled = age >= 0 && age < .95f && roots[i].localPosition.y > .025f;
            }
            rim.intensity = 1.6f;
            fill.intensity = .7f;
        }

        void Build()
        {
            layers = new GameObject("CryoPresentationLayers") { hideFlags = HideFlags.DontSave };
            layers.transform.SetParent(transform, false);
            if (presentationCamera != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(presentationCamera.transform.forward, Vector3.up);
                if (forward.sqrMagnitude > .001f) layers.transform.rotation = Quaternion.LookRotation(forward.normalized);
            }
            roots = new Transform[GroundCount + FragmentCount];
            renderers = new Renderer[roots.Length];
            for (int i = 0; i < roots.Length; i++)
            {
                var pivot = Child(i < GroundCount ? $"GroundIce_{i:00}" : $"ThawShard_{i - GroundCount:00}");
                roots[i] = pivot.transform;
                var go = new GameObject("SourceIceMesh") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(pivot.transform, false);
                Mesh mesh = iceMeshes[i < GroundCount ? i % 3 : 3];
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = i % 4 == 0 ? frostedIce : clearIce;
                r.shadowCastingMode = i % 4 == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
                r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                renderers[i] = r;

                // Imported shards have different scales and pivots. Normalize without editing source meshes.
                var b = mesh.bounds;
                Vector3 axis = b.size.x >= b.size.y && b.size.x >= b.size.z ? Vector3.right : b.size.z > b.size.y ? Vector3.forward : Vector3.up;
                float longest = Mathf.Max(.001f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
                Quaternion upright = Quaternion.FromToRotation(axis, Vector3.up);
                go.transform.localRotation = upright;
                go.transform.localScale = Vector3.one / longest;
                go.transform.localPosition = Vector3.up * .5f - upright * b.center / longest;

                if (i < GroundCount)
                {
                    // Tall shards sit behind and beside Ellen; the camera-facing center stays open.
                    float angle = i < 8 ? Mathf.Lerp(-105, 105, i / 7f) : (i - 8) * 45f;
                    float radius = i < 8 ? 1.1f + (i % 3) * .13f : .72f + (i % 3) * .18f;
                    float a = angle * Mathf.Deg2Rad;
                    pivot.transform.localPosition = new Vector3(Mathf.Sin(a) * radius, .005f, Mathf.Cos(a) * radius);
                    pivot.transform.localRotation = Quaternion.Euler(9 + i % 3 * 5, angle + i * 11, i % 2 == 0 ? -12 : 12);
                }
            }
            var reflection = Child("IceStudioReflection");
            reflection.transform.localPosition = Vector3.up * 1.5f;
            probe = reflection.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Custom;
            probe.customBakedTexture = studioReflection;
            probe.size = new Vector3(12, 10, 12);
            probe.boxProjection = false;
            probe.blendDistance = 1;
            probe.importance = 10;
            rim = MakeSpot("IceRimLight", new Vector3(-2.5f, 3.4f, 2.2f), new Color(.68f, .84f, 1), 1.6f);
            fill = MakeSpot("IceSpecularFill", new Vector3(2.8f, 2.4f, -2), new Color(1, .95f, .87f), .7f);
        }

        GameObject Child(string label)
        {
            var go = new GameObject(label) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(layers.transform, false);
            return go;
        }

        Light MakeSpot(string label, Vector3 position, Color color, float intensity)
        {
            var go = Child(label);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.LookRotation(Vector3.up * 1.1f - position);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color; light.intensity = intensity;
            light.range = 9; light.spotAngle = 62; light.innerSpotAngle = 38;
            light.shadows = LightShadows.None;
            return light;
        }

        void OnDisable()
        {
            if (legacyCrystals != null && previousEnabled != null)
                for (int i = 0; i < Mathf.Min(legacyCrystals.Length, previousEnabled.Length); i++)
                    if (legacyCrystals[i] != null) legacyCrystals[i].enabled = previousEnabled[i];
            if (layers != null) { if (Application.isPlaying) Destroy(layers); else DestroyImmediate(layers); }
            layers = null; lastTime = float.NaN;
        }
    }
}
