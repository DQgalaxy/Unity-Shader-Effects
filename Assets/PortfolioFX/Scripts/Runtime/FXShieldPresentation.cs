using UnityEngine;
using UnityEngine.Rendering;

namespace PortfolioFX
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FXShieldPresentation : MonoBehaviour
    {
        public FXSequenceDriver sequence;
        public Renderer shield;
        public Material shockwaveMaterial, trailMaterial, flashMaterial, sparkMaterial;
        public Renderer[] legacyVisuals;
        public Camera presentationCamera;
        public GameObject honeycombPrefab;
        readonly float[] hits = { 1.5f, 3.6f, 5.7f };
        readonly Vector3[] directions = {
            new Vector3(-.66f, .17f, -.74f), new Vector3(.65f, .38f, -.66f), new Vector3(.12f, .72f, -.68f)
        };
        GameObject visuals;
        LineRenderer[] trails, filaments;
        Renderer[] flashes, waves, coronas;
        ParticleSystem[] sparks;
        MaterialPropertyBlock block;
        Renderer[] honeycombRenderers;
        readonly Vector4[] contacts = new Vector4[3];
        readonly Vector3[] linePoints = new Vector3[28];
        readonly bool[] oldEnabled = new bool[6];
        float lastTime = float.NaN;
        static readonly int HitData = Shader.PropertyToID("_ShieldHits");
        static readonly int Radii = Shader.PropertyToID("_ShieldRadii");
        static readonly int Fade = Shader.PropertyToID("_Fade");
        static readonly int EffectTime = Shader.PropertyToID("_EffectTime");
        static readonly int Visibility = Shader.PropertyToID("_Visibility");
        static readonly int RevealProgress = Shader.PropertyToID("_RevealProgress");
        static readonly int RevealBottom = Shader.PropertyToID("_RevealBottom");
        static readonly int RevealHeight = Shader.PropertyToID("_RevealHeight");

        void OnEnable()
        {
            lastTime = float.NaN;
            if (legacyVisuals != null)
                for (int i = 0; i < Mathf.Min(legacyVisuals.Length, oldEnabled.Length); i++)
                    if (legacyVisuals[i] != null) oldEnabled[i] = legacyVisuals[i].enabled;
        }

        void LateUpdate()
        {
            if (sequence == null || shield == null || shockwaveMaterial == null || trailMaterial == null || flashMaterial == null || sparkMaterial == null) return;
            if (visuals == null) Build();
            if (honeycombRenderers != null && honeycombRenderers.Length > 0) shield.enabled = false;
            if (legacyVisuals != null) foreach (var r in legacyVisuals) if (r != null) r.enabled = false;
            float t = sequence.CurrentTime;
            if (!Application.isPlaying && Mathf.Approximately(lastTime, t)) return;
            lastTime = t;
            var center = shield.transform.position;
            float radius = shield.bounds.extents.x;
            Vector4 radii = Vector4.zero;
            for (int i = 0; i < hits.Length; i++)
            {
                Vector3 normal = shield.transform.TransformDirection(directions[i].normalized);
                Vector3 contact = center + normal * radius;
                float age = t - hits[i];
                float life = age >= 0 ? Mathf.Clamp01(1 - age / 1.6f) : 0;
                contacts[i] = new Vector4(contact.x, contact.y, contact.z, life);
                radii[i] = Mathf.Max(0, age) * 1.9f;
                float flight = Mathf.InverseLerp(hits[i] - .85f, hits[i], t);
                bool flying = t >= hits[i] - .85f && t < hits[i];
                Vector3 start = contact + normal * 4.2f + Vector3.up * .7f;
                Vector3 control = (start + contact) * .5f + Vector3.up * .4f;
                Vector3 head = Bezier(start, control, contact, flight);
                for (int j = 0; j < linePoints.Length; j++)
                {
                    float p = Mathf.Clamp01(flight - .27f + .27f * j / (linePoints.Length - 1));
                    linePoints[j] = Bezier(start, control, contact, p);
                }
                trails[i].SetPositions(linePoints);
                trails[i].enabled = flying;
                Paint(trails[i], flying ? 1 : 0, t);
                for (int j = 0; j < linePoints.Length; j++)
                    linePoints[j] += Vector3.Cross(normal, Vector3.up).normalized * Mathf.Sin(j * .65f + t * 17) * .018f;
                filaments[i].SetPositions(linePoints);
                filaments[i].enabled = flying;
                Paint(filaments[i], flying ? .85f : 0, t);
                var camera = presentationCamera != null ? presentationCamera : Camera.main;
                Quaternion facing = camera != null ? camera.transform.rotation : Quaternion.identity;
                flashes[i].transform.SetPositionAndRotation(flying ? head : contact + normal * .03f, facing);
                float flashFade = flying ? .65f : age >= 0 ? Mathf.Pow(Mathf.Clamp01(1 - age / .32f), 2) : 0;
                flashes[i].transform.localScale = Vector3.one * (flying ? .42f : 1.05f + Mathf.Max(0, age) * 2.4f);
                Paint(flashes[i], flashFade, t);
                waves[i].transform.SetPositionAndRotation(contact + normal * .06f, Quaternion.LookRotation(normal));
                waves[i].transform.localScale = Vector3.one * (.35f + Mathf.Max(0, age) * 3.8f);
                Paint(waves[i], age >= 0 ? Mathf.Pow(Mathf.Clamp01(1 - age / 1.05f), .8f) : 0, t);
                coronas[i].transform.SetPositionAndRotation(contact + normal * .1f, Quaternion.LookRotation(normal) * Quaternion.Euler(0, 0, i * 57 + age * 23));
                coronas[i].transform.localScale = Vector3.one * (.25f + Mathf.Max(0, age - .1f) * 2.8f);
                float aftershockFade = Mathf.SmoothStep(0, 1, (age - .1f) / .08f)
                    * Mathf.Clamp01(1 - Mathf.Max(0, age - .1f) / 1.15f) * .75f;
                Paint(coronas[i], aftershockFade, t);
                SampleSparks(sparks[i], contact, normal, age, i);
            }
            shield.GetPropertyBlock(block);
            block.SetVectorArray(HitData, contacts);
            block.SetVector(Radii, radii);
            block.SetFloat(EffectTime, t);
            // Finish spatial reveal before the first hit; retain the existing ending fade.
            block.SetFloat(RevealProgress, Mathf.InverseLerp(.05f, 1.4f, t));
            block.SetFloat(RevealBottom, shield.bounds.min.y);
            block.SetFloat(RevealHeight, Mathf.Max(.001f, shield.bounds.size.y));
            block.SetFloat(Visibility, Mathf.SmoothStep(0, 1, (8 - t) / .7f));
            shield.SetPropertyBlock(block);
            if (honeycombRenderers != null) foreach (var r in honeycombRenderers) r.SetPropertyBlock(block);
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
            => (1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * c;

        void Paint(Renderer r, float fade, float t)
        {
            r.GetPropertyBlock(block);
            block.SetFloat(Fade, fade);
            block.SetFloat(EffectTime, t);
            r.SetPropertyBlock(block);
            r.enabled = fade > .001f;
        }

        void Build()
        {
            block = new MaterialPropertyBlock();
            visuals = new GameObject("ShieldPresentationLayers") { hideFlags = HideFlags.DontSave };
            visuals.transform.SetParent(transform, false);
            if (honeycombPrefab != null)
            {
                var cells = Instantiate(honeycombPrefab, visuals.transform);
                cells.name = "HexagonArmor_13Hexagon";
                cells.transform.SetPositionAndRotation(shield.transform.position, Quaternion.Euler(90, 0, 0));
                honeycombRenderers = cells.GetComponentsInChildren<Renderer>();
                if (honeycombRenderers.Length > 0)
                {
                    Bounds bounds = honeycombRenderers[0].bounds;
                    foreach (var r in honeycombRenderers) bounds.Encapsulate(r.bounds);
                    cells.transform.localScale *= shield.bounds.size.x / Mathf.Max(.001f, Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z));
                    bounds = honeycombRenderers[0].bounds;
                    foreach (var r in honeycombRenderers)
                    {
                        var materials = r.sharedMaterials;
                        for (int j = 0; j < materials.Length; j++) materials[j] = shield.sharedMaterial;
                        r.sharedMaterials = materials;r.shadowCastingMode = ShadowCastingMode.Off;r.receiveShadows = false;
                        bounds.Encapsulate(r.bounds);
                    }
                    cells.transform.position += shield.transform.position - bounds.center;
                    // Vertex displacement does not automatically enlarge CPU culling bounds.
                    foreach (var r in honeycombRenderers)
                    {
                        var padded = r.localBounds;
                        float scale = Mathf.Min(Mathf.Abs(r.transform.lossyScale.x), Mathf.Abs(r.transform.lossyScale.y), Mathf.Abs(r.transform.lossyScale.z));
                        padded.Expand(1.2f / Mathf.Max(scale, .001f));
                        r.localBounds = padded;
                    }
                    foreach (var t in cells.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
                }
            }
            trails = new LineRenderer[3]; filaments = new LineRenderer[3];
            flashes = new Renderer[3]; waves = new Renderer[3]; coronas = new Renderer[3]; sparks = new ParticleSystem[3];
            for (int i = 0; i < 3; i++)
            {
                trails[i] = MakeTrail($"CometTrail_{i}", .18f, trailMaterial);
                filaments[i] = MakeTrail($"CometCore_{i}", .035f, trailMaterial);
                flashes[i] = Quad($"ContactFlash_{i}", flashMaterial);
                waves[i] = Quad($"FracturedShockwave_{i}", shockwaveMaterial);
                coronas[i] = Quad($"Aftershock_{i}", shockwaveMaterial);
                var go = new GameObject($"ImpactSparks_{i}") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(visuals.transform, false);
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.playOnAwake = false; main.loop = false; main.maxParticles = 64;
                main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = .32f;
                var emission = ps.emission; emission.enabled = false;
                var shape = ps.shape; shape.enabled = false;
                var color = ps.colorOverLifetime; color.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(new Color(1, .78f, .32f), 0), new GradientColorKey(new Color(1, .23f, .04f), 1) },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.9f, .35f), new GradientAlphaKey(0, 1) });
                color.color = gradient;
                var size = ps.sizeOverLifetime; size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 1), new Keyframe(.3f, .85f), new Keyframe(1, 0)));
                var r = ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial = sparkMaterial;
                r.renderMode = ParticleSystemRenderMode.Stretch;r.lengthScale = 3;r.velocityScale = .16f;
                r.shadowCastingMode = ShadowCastingMode.Off;r.receiveShadows = false;
                ps.useAutoRandomSeed = false;ps.randomSeed = (uint)(100 + i); sparks[i] = ps;
            }
        }

        Renderer Quad(string label, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = label; go.hideFlags = HideFlags.DontSave; go.transform.SetParent(visuals.transform, false);
            var collider = go.GetComponent<Collider>(); collider.enabled = false;
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;r.receiveShadows = false;return r;
        }

        LineRenderer MakeTrail(string label, float width, Material material)
        {
            var go = new GameObject(label) { hideFlags = HideFlags.DontSave };go.transform.SetParent(visuals.transform, false);
            var r = go.AddComponent<LineRenderer>();r.sharedMaterial = material;r.useWorldSpace = true;
            r.positionCount = linePoints.Length;r.widthMultiplier = width;
            r.widthCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.65f, .8f), new Keyframe(1, 1));
            r.textureMode = LineTextureMode.Stretch;r.numCapVertices = 3;
            r.shadowCastingMode = ShadowCastingMode.Off;r.receiveShadows = false;return r;
        }

        static void SampleSparks(ParticleSystem ps, Vector3 contact, Vector3 normal, float age, int index)
        {
            // Reconstruct from the same emission data so seeking backward matches playback.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (age < 0 || age > 1.25f) return;
            Quaternion basis = Quaternion.LookRotation(normal);
            for (int j = 0; j < 44; j++)
            {
                float angle = j * 2.399963f + index, radius = .3f + .6f * ((j * 17 % 29) / 29f);
                Vector3 velocity = basis * new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, .25f + (j % 4) * .12f) * (2 + j % 3);
                ps.Emit(new ParticleSystem.EmitParams { position = contact + normal * .06f, velocity = velocity, startLifetime = .65f + (j % 6) * .12f,
                    startSize = .075f + (j % 3) * .022f, startColor = Color.white }, 1);
            }
            ps.Simulate(age, false, false, false);ps.Pause(false);
        }

        void OnDisable()
        {
            if (shield != null)
            {
                shield.enabled = true;
                block ??= new MaterialPropertyBlock();shield.GetPropertyBlock(block);
                block.SetVectorArray(HitData, new Vector4[3]);block.SetVector(Radii, Vector4.zero);
                block.SetFloat(RevealProgress, 1);
                block.SetFloat(Visibility, 1);shield.SetPropertyBlock(block);
            }
            if (legacyVisuals != null)
                for (int i = 0; i < Mathf.Min(legacyVisuals.Length, oldEnabled.Length); i++)
                    if (legacyVisuals[i] != null) legacyVisuals[i].enabled = oldEnabled[i];
            if (visuals != null) { if (Application.isPlaying) Destroy(visuals); else DestroyImmediate(visuals); }
            visuals = null;honeycombRenderers = null;lastTime = float.NaN;
        }
    }
}
