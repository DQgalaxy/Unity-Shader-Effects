using System;
using UnityEngine;

namespace PortfolioFX
{
    [DisallowMultipleComponent]
    public sealed class FXSequenceDriver : MonoBehaviour
    {
        public AnimationClip sequence;
        [Min(0.1f)] public float duration = 6;
        public bool playOnStart = true;
        public bool loop = true;
        [Range(0.05f, 2)] public float playbackSpeed = 1;
        [Range(0, 1)] public float progress;
        [Range(0, 1)] public float dissolve;
        [Min(0)] public float intensity;
        [Min(0)] public float radius;
        [Range(0, 1)] public float impact;
        public Transform center;
        public Transform hitPoint;
        public FXMaterialBinding[] materials = Array.Empty<FXMaterialBinding>();
        public FXParticleCue[] particles = Array.Empty<FXParticleCue>();
        public FXArcBeam[] arcs = Array.Empty<FXArcBeam>();
        public FXCharacterMotion[] characters = Array.Empty<FXCharacterMotion>();
        public Light effectLight;
        public float maxLightIntensity = 6;
        [SerializeField] float time;
        public float CurrentTime => time;
        public bool IsPlaying { get; private set; }

        void Start() { if (playOnStart) Play(); else Seek(0); }

        void Update()
        {
            if (!IsPlaying) return;
            float next = time + Time.deltaTime * playbackSpeed;
            bool wrapped = next >= duration;
            if (wrapped && !loop) { Seek(duration); Pause(); return; }
            Sample(wrapped ? next % duration : next, wrapped);
        }

        [ContextMenu("Play From Start")]
        public void Play() { IsPlaying = true; Seek(0); }
        [ContextMenu("Pause")]
        public void Pause()
        {
            IsPlaying = false;
            foreach (var cue in particles) if (cue.system != null) cue.system.Pause(true);
        }
        public void Resume()
        {
            IsPlaying = true;
            foreach (var cue in particles)
            {
                if (cue.system == null || time < cue.start) continue;
                cue.system.Play(true);
                if (time >= cue.stop) cue.system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
        [ContextMenu("Reset Sequence")]
        public void ResetSequence() { IsPlaying = false; Seek(0); }
        public void Seek(float seconds)
        {
            Sample(Mathf.Clamp(seconds, 0, duration), true);
            if (!IsPlaying) Pause();
        }

        // AnimationClip is the single source of timing, including shader parameters and transforms.
        void Sample(float seconds, bool resimulate)
        {
            time = seconds;
            if (sequence != null) sequence.SampleAnimation(gameObject, time);
            foreach (var character in characters) if (character != null) character.Sample(time);
            foreach (var binding in materials) binding.Apply(this);
            foreach (var cue in particles) cue.Sample(time, resimulate, playbackSpeed);
            foreach (var arc in arcs) if (arc != null) arc.Sample(time, intensity);
            if (effectLight != null) effectLight.intensity = intensity * maxLightIntensity;
        }

        void OnDisable()
        {
            foreach (var cue in particles)
                if (cue.system != null) cue.system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    public enum FXPreviewMode { Surface, Energy, Dissolve, Reveal, Shield }

    [Serializable]
    public sealed class FXMaterialBinding
    {
        public Renderer target;
        public FXPreviewMode previewMode;
        public Color previewColor = Color.cyan;
        [Tooltip("Disable after replacing the placeholder material with your Shader Graph.")]
        public bool previewPlaceholder = true;
        public bool preserveAlbedo;
        [NonSerialized] MaterialPropertyBlock block;
        static readonly int Progress = Shader.PropertyToID("_Progress");
        static readonly int Dissolve = Shader.PropertyToID("_Dissolve");
        static readonly int Intensity = Shader.PropertyToID("_Intensity");
        static readonly int Radius = Shader.PropertyToID("_Radius");
        static readonly int Impact = Shader.PropertyToID("_Impact");
        static readonly int EffectTime = Shader.PropertyToID("_EffectTime");
        static readonly int CenterWS = Shader.PropertyToID("_CenterWS");
        static readonly int HitWS = Shader.PropertyToID("_HitPositionWS");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public void Apply(FXSequenceDriver driver)
        {
            if (target == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetFloat(Progress, driver.progress);
            block.SetFloat(Dissolve, driver.dissolve);
            block.SetFloat(Intensity, driver.intensity);
            block.SetFloat(Radius, driver.radius);
            block.SetFloat(Impact, driver.impact);
            block.SetFloat(EffectTime, driver.CurrentTime);
            block.SetVector(CenterWS, driver.center != null ? driver.center.position : driver.transform.position);
            block.SetVector(HitWS, driver.hitPoint != null ? driver.hitPoint.position : driver.transform.position);
            if (previewPlaceholder)
            {
                float visibility = previewMode == FXPreviewMode.Dissolve ? 1 - driver.dissolve :
                    previewMode == FXPreviewMode.Reveal ? driver.progress : 1;
                float alpha = previewMode == FXPreviewMode.Shield ? .13f + .23f * driver.impact :
                    previewMode == FXPreviewMode.Energy ? Mathf.Clamp01(driver.intensity) * .65f : 1;
                Color c = Color.Lerp(new Color(.13f, .17f, .19f), previewColor, .25f + .65f * driver.intensity);
                if (preserveAlbedo) c = Color.Lerp(Color.white, previewColor, Mathf.Clamp01(driver.intensity) * .18f);
                c.a = alpha * visibility;
                block.SetColor(BaseColor, c);
                block.SetColor(EmissionColor, previewColor * driver.intensity * (preserveAlbedo ? .08f : .7f));
                target.enabled = visibility > .005f;
            }
            else target.enabled = true;
            target.SetPropertyBlock(block);
        }
    }

    [Serializable]
    public sealed class FXParticleCue
    {
        public ParticleSystem system;
        public float start;
        public float stop = 2;
        [NonSerialized] bool emitting;

        public void Sample(float time, bool resimulate, float speed)
        {
            if (system == null) return;
            var main = system.main;
            main.simulationSpeed = speed;
            if (resimulate)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                emitting = false;
                if (time < start) return;
                float activeTime = Mathf.Max(0, Mathf.Min(time, stop) - start);
                system.Simulate(activeTime, true, true, false);
                if (time >= stop)
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    system.Simulate(time - stop, true, false, false);
                }
                else { system.Play(true); emitting = true; }
                return;
            }
            bool active = time >= start && time < stop;
            if (active && !emitting) system.Play(true);
            if (!active && emitting) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            emitting = active;
        }
    }
}
