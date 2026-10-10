using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PortfolioFX
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FXElectricPresentation : MonoBehaviour
    {
        public FXSequenceDriver sequence;
        public Camera presentationCamera;
        public SkinnedMeshRenderer body;
        public Material orbMaterial, orbitMaterial, goldOrbitMaterial, contactMaterial, branchMaterial;
        public Transform[] terminals, contacts;
        public Renderer[] legacyRenderers;

        GameObject layers;
        Transform[] orbs;
        Renderer[] cores, flashes, branches, beads;
        LineRenderer[] rings;
        Light[] lights;
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        MaterialPropertyBlock block;
        static readonly int TimeId = Shader.PropertyToID("_EffectTime");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int FadeId = Shader.PropertyToID("_Fade");

        void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            if (sequence == null || orbMaterial == null || orbitMaterial == null || goldOrbitMaterial == null ||
                contactMaterial == null || branchMaterial == null || terminals == null || terminals.Length != 3 ||
                contacts == null || contacts.Length < 6) return;
            foreach (var terminal in terminals) if (terminal == null) return;
            foreach (var contact in contacts) if (contact == null) return;
            if (layers == null) Build();
            if (legacyRenderers != null) foreach (var r in legacyRenderers) if (r != null) r.enabled = false;

            float t = sequence.CurrentTime, strength = Mathf.Max(0, sequence.intensity);
            float envelope = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.5f, 1.2f, t)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4.4f, 5.4f, t)));
            var camera = presentationCamera != null ? presentationCamera : Camera.main;
            Vector3 towardCamera = camera != null ? -camera.transform.forward : Vector3.back;
            Quaternion facing = camera != null ? camera.transform.rotation : Quaternion.identity;
            for (int i = 0; i < orbs.Length; i++)
            {
                orbs[i].position = terminals[i].position + Vector3.up * (.055f * Mathf.Sin(t * 1.8f + i * 2));
                orbs[i].rotation = Quaternion.Euler(14 + i * 37, t * (14 + i * 3), 8);
                cores[i].transform.localScale = Vector3.one * (.5f + .022f * Mathf.Sin(t * 4 + i));
                Set(cores[i], t + i * .73f, strength, 1);
                for (int j = 0; j < 2; j++)
                {
                    int k = i * 2 + j;
                    rings[k].transform.localRotation = Quaternion.Euler(35 + j * 75, t * (j == 0 ? 36 : -27) + i * 51, j * 65);
                    Set(rings[k], t, 1, .5f + .35f * Mathf.Clamp01(strength));
                    float angle = t * (j == 0 ? 1.8f : -1.2f) + i;
                    beads[k].transform.position = rings[k].transform.TransformPoint(new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (.39f + j * .09f));
                    beads[k].transform.rotation = facing;
                    Set(beads[k], t, 1, .5f + .4f * envelope);
                }
                lights[i].intensity = .2f + strength * .65f;
            }

            // Attach card flashes to animated bones; never freeze them in world-space at setup time.
            for (int i = 0; i < contacts.Length; i++)
            {
                float phase = Mathf.Repeat(t * 5.5f + i * .381f, 1);
                float flash = Mathf.Pow(1 - phase, 3) * envelope * Mathf.Clamp01(strength * 1.6f);
                flashes[i].transform.position = contacts[i].position + towardCamera * .11f;
                flashes[i].transform.rotation = facing * Quaternion.Euler(0, 0, i * 39);
                flashes[i].transform.localScale = Vector3.one * (.14f + .12f * flash);
                flashes[i].enabled = flash > .012f;
                Set(flashes[i], t, 1, flash);
                float branchFade = Mathf.Clamp01((.6f - phase) * 2.4f) * envelope * Mathf.Clamp01(strength * 1.8f);
                branches[i].transform.position = contacts[i].position + towardCamera * .12f;
                branches[i].transform.rotation = facing * Quaternion.Euler(0, 0, i * 61 + Mathf.Floor(t * 5.5f + i * .381f) * 47);
                branches[i].transform.localScale = Vector3.one * (.48f + (i % 3) * .1f);
                branches[i].enabled = branchFade > .015f;
                Set(branches[i], t, 1, branchFade);
            }
        }

        void Build()
        {
            block = new MaterialPropertyBlock();
            layers = new GameObject("ElectricPresentationLayers") { hideFlags = HideFlags.DontSave };
            layers.transform.SetParent(transform, false);
            orbs = new Transform[3]; cores = new Renderer[3]; rings = new LineRenderer[6]; beads = new Renderer[6]; lights = new Light[3];
            for (int i = 0; i < 3; i++)
            {
                orbs[i] = Child($"AetherOrb_{i}").transform;
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "PlasmaCore"; sphere.hideFlags = HideFlags.DontSave;
                sphere.transform.SetParent(orbs[i], false);
                var collider = sphere.GetComponent<Collider>(); collider.enabled = false; Dispose(collider);
                cores[i] = sphere.GetComponent<Renderer>(); Configure(cores[i], orbMaterial);
                for (int j = 0; j < 2; j++)
                {
                    int k = i * 2 + j;
                    var go = Child(j == 0 ? "CyanOrbit" : "GoldOrbit", orbs[i]);
                    var line = go.AddComponent<LineRenderer>(); rings[k] = line;
                    Configure(line, j == 0 ? orbitMaterial : goldOrbitMaterial);
                    line.useWorldSpace = false; line.loop = true; line.positionCount = 97;
                    line.widthMultiplier = j == 0 ? .045f : .032f;
                    line.textureMode = LineTextureMode.Stretch; line.alignment = LineAlignment.View;
                    line.numCornerVertices = 2; line.numCapVertices = 2;
                    line.startColor = line.endColor = Color.white;
                    for (int n = 0; n < 97; n++)
                    {
                        float a = n / 96f * Mathf.PI * 2;
                        line.SetPosition(n, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (.39f + j * .09f));
                    }
                    beads[k] = Card("OrbitGlint", contactMaterial);
                    beads[k].transform.localScale = Vector3.one * .16f;
                }
                var lightObject = Child("LocalPlasmaLight", orbs[i]);
                lights[i] = lightObject.AddComponent<Light>();
                lights[i].type = LightType.Point; lights[i].range = 2.4f;
                lights[i].color = new Color(.62f, .76f, 1); lights[i].shadows = LightShadows.None;
            }
            flashes = new Renderer[contacts.Length]; branches = new Renderer[contacts.Length];
            for (int i = 0; i < contacts.Length; i++)
            {
                flashes[i] = Card($"BodyContact_{i}", contactMaterial);
                branches[i] = Card($"BodyBranch_{i}", branchMaterial, i % 4);
            }
            sequence.Seek(sequence.CurrentTime);
        }

        GameObject Child(string label, Transform parent = null)
        {
            var go = new GameObject(label) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent != null ? parent : layers.transform, false);
            return go;
        }

        Renderer Card(string label, Material material, int tile = -1)
        {
            var go = Child(label); var mesh = new Mesh { name = label + "Mesh", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            float scale = tile < 0 ? 1 : .5f;
            Vector2 offset = tile < 0 ? Vector2.zero : new Vector2(tile % 2, tile / 2) * .5f;
            mesh.uv = new[] { offset, offset + Vector2.right * scale, offset + Vector2.one * scale, offset + Vector2.up * scale };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0,2,1,0,3,2 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            ownedMeshes.Add(mesh); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); Configure(r, material); return r;
        }

        static void Configure(Renderer r, Material material)
        {
            r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        }

        void Set(Renderer r, float time, float intensity, float fade)
        {
            block.Clear(); block.SetFloat(TimeId,time); block.SetFloat(IntensityId,intensity); block.SetFloat(FadeId,fade);
            r.SetPropertyBlock(block);
        }

        void OnDisable()
        {
            if (layers != null) Dispose(layers);
            layers = null;
            foreach (var mesh in ownedMeshes) if (mesh != null) Dispose(mesh);
            ownedMeshes.Clear();
        }

        static void Dispose(Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
