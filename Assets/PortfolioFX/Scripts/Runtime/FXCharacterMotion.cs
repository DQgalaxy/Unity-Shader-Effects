using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PortfolioFX
{
    [Serializable]
    public sealed class FXCharacterCue
    {
        public AnimationClip clip;
        public float start;
        public float end = 1;
        public float sourceStart;
        [Tooltip("Negative means the full source clip length. Equal to Source Start holds a pose.")]
        public float sourceEnd = -1;
        public float blendIn = .12f;
        public float blendOut = .15f;
        public bool loop;
    }

    [DisallowMultipleComponent]
    public sealed class FXCharacterMotion : MonoBehaviour
    {
        public Animator animator;
        public AnimationClip idle;
        public FXCharacterCue[] cues = Array.Empty<FXCharacterCue>();
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] clips;

        public void Sample(float seconds)
        {
            if (animator == null || idle == null) return;
            if (!graph.IsValid()) BuildGraph();
            float total = 0;
            for (int i = 0; i < cues.Length; i++)
            {
                var cue = cues[i];
                float weight = Weight(cue, seconds);
                total += weight;
                mixer.SetInputWeight(i + 1, weight);
                if (cue.clip == null) continue;
                float end = cue.sourceEnd < 0 ? cue.clip.length : cue.sourceEnd;
                float local = cue.loop
                    ? Mathf.Repeat(Mathf.Max(0, seconds - cue.start), Mathf.Max(.001f, end - cue.sourceStart)) + cue.sourceStart
                    : Mathf.Lerp(cue.sourceStart, end, Mathf.InverseLerp(cue.start, cue.end, seconds));
                clips[i + 1].SetTime(Mathf.Clamp(local, 0, Mathf.Max(0, cue.clip.length - .0001f)));
            }
            if (total > 1)
                for (int i = 0; i < cues.Length; i++) mixer.SetInputWeight(i + 1, mixer.GetInputWeight(i + 1) / total);
            mixer.SetInputWeight(0, Mathf.Max(0, 1 - total));
            clips[0].SetTime(Mathf.Repeat(seconds, Mathf.Max(.001f, idle.length)));

            // The outer sequence owns locomotion. Keep imported root curves from moving the showcase.
            Transform t = animator.transform;
            Vector3 position = t.localPosition, scale = t.localScale;
            Quaternion rotation = t.localRotation;
            graph.Evaluate(0);
            t.localPosition = position; t.localRotation = rotation; t.localScale = scale;
        }

        static float Weight(FXCharacterCue cue, float time)
        {
            if (cue.clip == null || time < cue.start || time >= cue.end) return 0;
            float fadeIn = cue.blendIn <= 0 ? 1 : Mathf.Clamp01((time - cue.start) / cue.blendIn);
            float fadeOut = cue.blendOut <= 0 ? 1 : Mathf.Clamp01((cue.end - time) / cue.blendOut);
            return Mathf.SmoothStep(0, 1, Mathf.Min(fadeIn, fadeOut));
        }

        void BuildGraph()
        {
            animator.applyRootMotion = false;
            animator.fireEvents = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create(name + "_CharacterSequence");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, cues.Length + 1);
            clips = new AnimationClipPlayable[cues.Length + 1];
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = AnimationClipPlayable.Create(graph, i == 0 ? idle : cues[i - 1].clip != null ? cues[i - 1].clip : idle);
                clips[i].SetSpeed(0);
                clips[i].SetApplyFootIK(false);
                graph.Connect(clips[i], 0, mixer, i);
            }
            var output = AnimationPlayableOutput.Create(graph, "Ellen Pose", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        public void ReleaseGraph() { if (graph.IsValid()) graph.Destroy(); }
        void OnDisable() { ReleaseGraph(); }
        void OnDestroy() { ReleaseGraph(); }
        void OnValidate() { ReleaseGraph(); }
    }
}
