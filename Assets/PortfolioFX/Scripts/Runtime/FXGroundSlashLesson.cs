using UnityEngine;
using UnityEngine.Rendering;

namespace PortfolioFX
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FXGroundSlashLesson : MonoBehaviour
    {
        public FXSequenceDriver sequence;
        public FXCharacterMotion character;
        public Transform hand;
        public Camera viewCamera;
        public Transform hit;
        public Material bladeMaterial, flashMaterial;
        public Material crackMaterial;
        public Material debrisMaterial, dustMaterial, sparkMaterial;
        public Mesh debrisMesh;
        public Transform groundPlane;
        [System.NonSerialized] public ParticleSystem debris, dust, sparks;
        public float releaseTime = .62f;
        public float impactTime = .98f;
        public float bladeSize = 1.65f;
        [Min(.01f)] public float releaseSize = .45f;
        [Min(0)] public float handForwardOffset = .32f;
        public float brightness = 3.5f;
        [Range(0, 1)] public float trailStrength = .32f;
        [Range(.03f, .25f)] public float trailDuration = .13f;

        GameObject generated;
        Mesh quad, scytheMesh;
        Renderer palm, blade, flash, crack;
        const int SweepSegments = 24, ArcSegments = 48;
        Vector3[] scytheVertices;
        MaterialPropertyBlock block;
        Vector3 launch;
        bool hasLaunch;
        float sampledRelease = -1, particleTime = -1;
        float sampledHandOffset = -1;
        Transform sampledHand;
        static readonly int Opacity = Shader.PropertyToID("_Opacity");
        static readonly int ObjectOpacity = Shader.PropertyToID("_ObjectOpacity");
        static readonly int Brightness = Shader.PropertyToID("_Brightness");
        static readonly int EffectTime = Shader.PropertyToID("_EffectTime");
        static readonly int Reveal = Shader.PropertyToID("_Reveal");
        static readonly int Heat = Shader.PropertyToID("_Heat");
        static readonly int WakeStrength = Shader.PropertyToID("_WakeStrength");
        static readonly int EdgeVisibility = Shader.PropertyToID("_EdgeVisibility");

        public string ConfigurationError
        {
            get
            {
                if (sequence == null || character == null || character.animator == null ||
                    character.idle == null || hand == null || viewCamera == null || hit == null)
                    return "Assign sequence, character/animator/idle, hand, camera and hit.";
                if (bladeMaterial == null || sparkMaterial == null || flashMaterial == null ||
                    crackMaterial == null || debrisMaterial == null || dustMaterial == null || debrisMesh == null)
                    return "Assign all six materials and the debris mesh.";
                if (groundPlane == null) return "Assign the collision plane at the plinth surface.";
                if (releaseTime <= .34f || impactTime <= releaseTime + .05f || impactTime + 2.8f > sequence.duration)
                    return "Require release > 0.34, impact > release + 0.05 and duration >= impact + 2.8.";
                return null;
            }
        }

        void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            if (ConfigurationError != null) { if (generated != null) Release(); return; }
            if (generated == null) Build();
            float t = sequence.CurrentTime;
            if (!hasLaunch || sampledRelease != releaseTime || sampledHand != hand ||
                sampledHandOffset != handForwardOffset)
            {
                // Sample both the outer sequence and the hand pose at the release time.
                sequence.Seek(releaseTime);
                launch = transform.InverseTransformPoint(HandPoint());
                sequence.Seek(t);
                sampledRelease = releaseTime;
                sampledHand = hand;
                sampledHandOffset = handForwardOffset;
                hasLaunch = true;
            }
            Quaternion facing = viewCamera.transform.rotation;
            float charge = Ease(.18f, releaseTime, t);
            float palmFade = Ease(.18f, .34f, t) *
                (1 - Ease(releaseTime - .03f, releaseTime, t));
            Quaternion palmRotation = facing * Quaternion.Euler(0, 0, -25);
            float palmSize = Mathf.Lerp(releaseSize * .5f, releaseSize, charge);
            SetCard(palm, HandPoint(), palmRotation, palmSize, palmFade, t, brightness * .7f);

            bool flying = t >= releaseTime && t < impactTime + .15f;
            float flight = Mathf.InverseLerp(releaseTime, impactTime, t);
            float headTime = Mathf.Min(t, impactTime);
            Vector3 direction = viewCamera.transform.InverseTransformDirection(
                Flight(Mathf.Min(headTime + .015f, impactTime)) -
                Flight(Mathf.Max(headTime - .015f, releaseTime)));
            float angle = direction.sqrMagnitude > .00001f
                ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg : -25;
            Quaternion slashRotation = facing * Quaternion.Euler(0, 0, angle);
            // Leave the hand at a compact size, then unfold once the head has travelled away.
            float size = Mathf.Lerp(releaseSize, bladeSize, Ease(.15f, .85f, flight));
            float bladeFade = 1 - Ease(impactTime + .025f, impactTime + .15f, t);
            SetCard(blade, Flight(t), slashRotation,
                size, flying ? bladeFade : 0, t, brightness);
            blade.GetPropertyBlock(block);
            block.SetFloat(EdgeVisibility, 1 - Ease(impactTime, impactTime + .045f, t));
            blade.SetPropertyBlock(block);
            // A single connected surface shares its bright leading arc with the fading wake.
            UpdateScytheMesh(flying ? headTime : releaseTime,
                flying ? slashRotation : palmRotation, flying ? size : palmSize, flying);

            float age = t - impactTime;
            float hitFade = age >= 0 ? 1 - Ease(.025f, .16f, age) : 0;
            SetCard(flash, hit.position + Vector3.up * .10f, facing,
                Mathf.Lerp(.3f, 1.35f, Ease(0, .12f, age)), hitFade, t, 4);
            crack.transform.position = hit.position + Vector3.up * .012f;
            crack.transform.rotation = Quaternion.Euler(90, 25, 0);
            crack.transform.localScale = new Vector3(2.4f, 1.5f, 1);
            float remain = age >= 0 ? 1 - Ease(2.0f, 2.8f, age) : 0;
            SetMaterial(crack, remain, t, 1);
            crack.GetPropertyBlock(block);
            block.SetFloat(Reveal, Ease(0, .20f, age));
            block.SetFloat(Heat, age >= 0 ? 1 - Ease(.04f, .4f, age) : 0);
            crack.SetPropertyBlock(block);
            if (!Mathf.Approximately(t, particleTime))
            {
                SampleParticles(debris, age);
                SampleParticles(dust, age);
                SampleParticles(sparks, age);
                particleTime = t;
            }
        }

        Vector3 HandPoint()
        {
            Vector3 forward = Vector3.ProjectOnPlane(hit.position - hand.position, Vector3.up);
            if (forward.sqrMagnitude < .0001f) forward = character.transform.forward;
            return hand.position + forward.normalized * handForwardOffset + Vector3.up * .04f;
        }
        Vector3 Flight(float seconds)
        {
            float u = Mathf.InverseLerp(releaseTime, impactTime, seconds);
            Vector3 start = transform.TransformPoint(launch);
            Vector3 end = hit.position + Vector3.up * .10f;
            return Vector3.Lerp(start, end, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * .28f;
        }
        static float Ease(float a, float b, float t)
        {
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));
        }
        void BuildScytheMesh()
        {
            scytheMesh = new Mesh { name = "SingleScytheSweep", hideFlags = HideFlags.DontSave };
            scytheMesh.MarkDynamic();
            scytheVertices = new Vector3[(SweepSegments + 1) * (ArcSegments + 1)];
            var uv = new Vector2[scytheVertices.Length];
            var indices = new int[SweepSegments * ArcSegments * 6];
            for (int row = 0; row <= SweepSegments; row++)
                for (int col = 0; col <= ArcSegments; col++)
                    uv[row * (ArcSegments + 1) + col] =
                        new Vector2(row / (float)SweepSegments, col / (float)ArcSegments);
            int index = 0;
            for (int row = 0; row < SweepSegments; row++)
                for (int col = 0; col < ArcSegments; col++)
                {
                    int a = row * (ArcSegments + 1) + col, b = a + ArcSegments + 1;
                    indices[index++] = a; indices[index++] = b; indices[index++] = a + 1;
                    indices[index++] = a + 1; indices[index++] = b; indices[index++] = b + 1;
                }
            scytheMesh.vertices = scytheVertices;
            scytheMesh.uv = uv;
            scytheMesh.triangles = indices;
        }
        void UpdateScytheMesh(float time, Quaternion rotation, float size, bool travelling)
        {
            Vector3 head = Flight(time);
            Quaternion inverse = Quaternion.Inverse(rotation);
            for (int row = 0; row <= SweepSegments; row++)
            {
                float u = row / (float)SweepSegments;
                Vector3 history = travelling
                    ? inverse * (Flight(time - u * trailDuration) - head) / Mathf.Max(.01f, size)
                    : Vector3.zero;
                for (int col = 0; col <= ArcSegments; col++)
                {
                    float v = col / (float)ArcSegments;
                    float theta = Mathf.Lerp(-1.25f, 1.6f, v);
                    float taper = Mathf.Pow(Mathf.Max(0, Mathf.Sin(v * Mathf.PI)), .65f);
                    // x=0 is the forward cutting edge; older samples extend only behind it.
                    Vector3 edge = new Vector3(.52f * (Mathf.Cos(theta) - 1),
                        .58f * Mathf.Sin(theta), 0);
                    Vector3 wake = new Vector3(Mathf.Min(0, history.x) - .18f * u,
                        history.y * .3f - edge.y * .1f * u, 0);
                    scytheVertices[row * (ArcSegments + 1) + col] = edge + wake * taper;
                }
            }
            scytheMesh.vertices = scytheVertices;
            scytheMesh.RecalculateBounds();
        }
        void SampleParticles(ParticleSystem system, float age)
        {
            if (system == null) return;
            system.transform.position = hit.position + Vector3.up * .06f;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed = false;
            system.randomSeed = 613;
            if (age > 0)
            {
                system.Simulate(age, true, true, true);
                system.Pause(true);
            }
        }
        void SetCard(Renderer r, Vector3 p, Quaternion q,
            float size, float opacity, float time, float light)
        {
            r.transform.SetPositionAndRotation(p, q);
            r.transform.localScale = Vector3.one * size;
            SetMaterial(r, opacity, time, light);
        }
        void SetMaterial(Renderer r, float opacity, float time, float light)
        {
            block.Clear();
            block.SetFloat(Opacity, Mathf.Clamp01(opacity));
            // Keep the user's existing main graph reference name intact.
            block.SetFloat(ObjectOpacity, Mathf.Clamp01(opacity));
            block.SetFloat(EffectTime, time);
            block.SetFloat(Brightness, Mathf.Max(0, light));
            block.SetFloat(WakeStrength, trailStrength);
            block.SetFloat(EdgeVisibility, 1);
            r.SetPropertyBlock(block);
            r.enabled = opacity > .001f;
        }
        void Build()
        {
            block = new MaterialPropertyBlock();
            generated = new GameObject("GroundSlashLesson_Generated");
            generated.hideFlags = HideFlags.DontSave;
            generated.transform.SetParent(transform, false);
            quad = new Mesh { name = "SlashLessonQuad", hideFlags = HideFlags.DontSave };
            quad.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0),
                new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.RecalculateNormals(); quad.RecalculateBounds();
            BuildScytheMesh();
            palm = Card("PalmBlade", bladeMaterial, scytheMesh);
            blade = Card("FlyingBlade", bladeMaterial, scytheMesh);
            flash = Card("ImpactFlash", flashMaterial);
            crack = Card("GroundCut", crackMaterial);
            debris = FXGroundSlashParticles.CreateDebris(generated.transform,
                debrisMesh, debrisMaterial, groundPlane);
            dust = FXGroundSlashParticles.CreateDust(generated.transform, dustMaterial);
            sparks = FXGroundSlashParticles.CreateSparks(generated.transform, sparkMaterial, groundPlane);
        }
        GameObject Child(string label)
        {
            var go = new GameObject(label) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(generated.transform, false);
            return go;
        }
        Renderer Card(string label, Material mat, Mesh mesh = null)
        {
            var go = Child(label);
            go.AddComponent<MeshFilter>().sharedMesh = mesh != null ? mesh : quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }
        [ContextMenu("Rebuild Presentation")]
        public void Rebuild()
        {
            Release(); hasLaunch = false; sampledHand = null; particleTime = -1;
        }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }
        void Release()
        {
            if (debris != null)
                debris.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (dust != null)
                dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (sparks != null)
                sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (generated != null)
            {
                generated.SetActive(false);
                if (Application.isPlaying) Destroy(generated); else DestroyImmediate(generated);
            }
            if (quad != null)
            {
                if (Application.isPlaying) Destroy(quad); else DestroyImmediate(quad);
            }
            if (scytheMesh != null)
            {
                if (Application.isPlaying) Destroy(scytheMesh); else DestroyImmediate(scytheMesh);
            }
            scytheMesh = null; scytheVertices = null;
            generated = null; quad = null; hasLaunch = false; particleTime = -1;
            debris = null; dust = null; sparks = null;
        }
    }
}
