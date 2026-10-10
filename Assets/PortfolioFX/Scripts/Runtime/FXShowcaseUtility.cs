using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PortfolioFX
{
    // Shared construction and deterministic preview, not a second animation clock.
    public static class FXShowcaseUtility
    {
        static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        public static float Ease(float a, float b, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));
        public static GameObject Child(string name, Transform parent)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false); return go;
        }
        public static void Dispose(Object obj)
        {
            if (obj == null) return;
            if (obj is GameObject go) go.SetActive(false);
            if (Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj);
        }
        public static Mesh Quad()
        {
            var mesh = new Mesh { name = "ShowcaseQuad", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0),
                new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0,2,1,0,3,2 }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        public static Renderer MeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = Child(name, parent); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; return r;
        }
        public static void Set(Renderer r, float time, float alpha)
        {
            if (r == null) return;
            Block.Clear(); Block.SetFloat("_EffectTime", time);
            Block.SetFloat("_Opacity", alpha); Block.SetFloat("_ObjectOpacity", alpha);
            r.SetPropertyBlock(Block); r.enabled = alpha > .001f;
        }
        public static void Float(Renderer r, string name, float value)
        {
            r.GetPropertyBlock(Block); Block.SetFloat(name, value); r.SetPropertyBlock(Block);
        }
        public static ParticleSystem Burst(string name, Transform parent, Material material, int count,
            float lifetime, float speed, float size, Color color, float gravity = 0)
        {
            var ps = Child(name, parent).AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.duration = 3; main.loop = false; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = count * 2;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .65f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .6f, size);
            main.startColor = color; main.gravityModifier = gravity;
            ps.useAutoRandomSeed = false; ps.randomSeed = 1724;
            var emission = ps.emission; emission.rateOverTime = 0; emission.rateOverDistance = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 60; shape.radius = .15f; shape.rotation = new Vector3(-90,0,0);
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(
                new[] {new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[] {new GradientAlphaKey(1,0),new GradientAlphaKey(.8f,.35f),new GradientAlphaKey(0,1)});
            fade.color = gradient;
            var sizeModule = ps.sizeOverLifetime; sizeModule.enabled = true;
            sizeModule.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0,1,1,.1f));
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
            return ps;
        }
        public static void Sample(ParticleSystem ps, float age)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (age <= 0) return;
            ps.Simulate(age, true, true, true); ps.Pause(true);
        }
    }
}
