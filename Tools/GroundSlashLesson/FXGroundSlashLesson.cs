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
        public Material bladeMaterial, trailMaterial, flashMaterial;
        public Material crackMaterial;
        public Material debrisMaterial, dustMaterial;
        public Mesh debrisMesh;
        public Transform groundPlane;
        [System.NonSerialized] public ParticleSystem debris, dust;
        public float releaseTime = .62f;
        public float impactTime = .98f;
        public float bladeSize = 1.65f;
        public float brightness = 3.5f;
        public float trailStrength = .32f;

        GameObject generated;
        Mesh quad;
        Renderer palm, blade, flash, crack;
        Renderer[] ghosts;
        LineRenderer ribbon;
        MaterialPropertyBlock block;
        Vector3 launch;
        bool hasLaunch;
        float sampledRelease = -1, particleTime = -1;
        Transform sampledHand;
        static readonly int Opacity = Shader.PropertyToID("_Opacity");
        static readonly int ObjectOpacity = Shader.PropertyToID("_ObjectOpacity");
        static readonly int Brightness = Shader.PropertyToID("_Brightness");
        static readonly int EffectTime = Shader.PropertyToID("_EffectTime");
        static readonly int Reveal = Shader.PropertyToID("_Reveal");
        static readonly int Heat = Shader.PropertyToID("_Heat");

        public string ConfigurationError
        {
            get
            {
                if (sequence == null || character == null || character.animator == null ||
                    character.idle == null || hand == null || viewCamera == null || hit == null)
                    return "Assign sequence, character/animator/idle, hand, camera and hit.";
                if (bladeMaterial == null || trailMaterial == null || flashMaterial == null ||
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
            if (!hasLaunch || sampledRelease != releaseTime || sampledHand != hand)
            {
                // Sample both the outer sequence and the hand pose at the release time.
                sequence.Seek(releaseTime);
                launch = transform.InverseTransformPoint(HandPoint());
                sequence.Seek(t);
                sampledRelease = releaseTime;
                sampledHand = hand;
                hasLaunch = true;
            }
            Quaternion facing = viewCamera.transform.rotation;
            float charge = Ease(.18f, releaseTime, t);
            float palmFade = Ease(.18f, .34f, t) *
                (1 - Ease(releaseTime - .03f, releaseTime, t));
            SetCard(palm, HandPoint(), facing * Quaternion.Euler(0, 0, -25),
                .25f + .6f * charge, palmFade, t, brightness * .7f);

            bool flying = t >= releaseTime && t < impactTime;
            float flight = Mathf.InverseLerp(releaseTime, impactTime, t);
            Quaternion slashRotation = facing * Quaternion.Euler(0, 0, -30 + flight * 70);
            SetCard(blade, Flight(t), slashRotation,
                Mathf.Lerp(.85f, bladeSize, Ease(0, .35f, flight)),
                flying ? 1 : 0, t, brightness);

            // Sample past trajectory positions; no frame history is required for seeking.
            for (int i = 0; i < ghosts.Length; i++)
            {
                float delay = (i + 1) * .035f;
                float past = t - delay;
                float p = Mathf.InverseLerp(releaseTime, impactTime, past);
                float fade = past >= releaseTime && past < impactTime
                    ? trailStrength * (1 - i / (float)ghosts.Length) *
                      (1 - Ease(impactTime, impactTime + .2f, t)) : 0;
                SetCard(ghosts[i], Flight(past),
                    facing * Quaternion.Euler(0, 0, -30 + p * 70),
                    bladeSize * (1 - .055f * i), fade, past, brightness * .65f);
            }
            float tailFade = Ease(releaseTime, releaseTime + .05f, t) *
                (1 - Ease(impactTime, impactTime + .18f, t));
            ribbon.enabled = tailFade > .001f;
            for (int i = 0; i < ribbon.positionCount; i++)
                ribbon.SetPosition(i, Flight(Mathf.Min(t, impactTime) - i * .008f));
            SetMaterial(ribbon, tailFade * trailStrength, t, brightness * .8f);

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
                particleTime = t;
            }
        }

        Vector3 HandPoint()
        {
            return hand.position + viewCamera.transform.right * .10f + Vector3.up * .04f;
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
            palm = Card("PalmBlade", bladeMaterial);
            blade = Card("FlyingBlade", bladeMaterial);
            flash = Card("ImpactFlash", flashMaterial);
            crack = Card("GroundCut", crackMaterial);
            ghosts = new Renderer[5];
            for (int i = 0; i < ghosts.Length; i++)
                ghosts[i] = Card("Afterimage_" + i, bladeMaterial);
            var tail = Child("BladeWake");
            ribbon = tail.AddComponent<LineRenderer>();
            ribbon.sharedMaterial = trailMaterial;
            ribbon.positionCount = 24;
            ribbon.useWorldSpace = true;
            ribbon.alignment = LineAlignment.View;
            ribbon.textureMode = LineTextureMode.Stretch;
            ribbon.widthMultiplier = .24f;
            ribbon.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
            ribbon.shadowCastingMode = ShadowCastingMode.Off;
            ribbon.receiveShadows = false;
            debris = FXGroundSlashParticles.CreateDebris(generated.transform,
                debrisMesh, debrisMaterial, groundPlane);
            dust = FXGroundSlashParticles.CreateDust(generated.transform, dustMaterial);
        }
        GameObject Child(string label)
        {
            var go = new GameObject(label) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(generated.transform, false);
            return go;
        }
        Renderer Card(string label, Material mat)
        {
            var go = Child(label);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
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
            if (generated != null)
            {
                generated.SetActive(false);
                if (Application.isPlaying) Destroy(generated); else DestroyImmediate(generated);
            }
            if (quad != null)
            {
                if (Application.isPlaying) Destroy(quad); else DestroyImmediate(quad);
            }
            generated = null; quad = null; hasLaunch = false; particleTime = -1;
            debris = null; dust = null;
        }
    }
}
