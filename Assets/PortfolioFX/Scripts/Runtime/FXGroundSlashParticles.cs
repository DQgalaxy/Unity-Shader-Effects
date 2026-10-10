using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PortfolioFX
{
    // ParticleSystem owns gravity, emission and collision; this class only configures modules.
    public static class FXGroundSlashParticles
    {
        static ParticleSystem Create(Transform parent, string name, Material material, int count)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1.6f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count;
            main.startColor = Color.white;
            ps.useAutoRandomSeed = false;
            ps.randomSeed = 613;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.rateOverDistance = 0;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.rotation = new Vector3(-90, 0, 0);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        public static ParticleSystem CreateDebris(Transform parent, Mesh mesh,
            Material material, Transform collisionPlane)
        {
            var ps = Create(parent, "Debris", material, 32);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.75f, 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
            // Normalize against the imported mesh so asset import scale cannot make giant rocks.
            Vector3 bounds = mesh.bounds.size;
            float extent = Mathf.Max(.001f, Mathf.Max(bounds.x, Mathf.Max(bounds.y, bounds.z)));
            main.startSize = new ParticleSystem.MinMaxCurve(.09f / extent, .22f / extent);
            main.gravityModifier = 1;
            main.startRotation3D = true;
            var randomAngle = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startRotationX = randomAngle;
            main.startRotationY = randomAngle;
            main.startRotationZ = randomAngle;
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 14) });
            var shape = ps.shape;
            shape.angle = 55;
            shape.radius = .24f;
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            var spin = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            rotation.x = spin; rotation.y = spin; rotation.z = spin;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1,
                new AnimationCurve(new Keyframe(0, 1), new Keyframe(.75f, 1), new Keyframe(1, 0)));
            // A fixed display plinth only needs a ground plane, not a whole scene physics query.
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.SetPlane(0, collisionPlane);
            collision.dampen = .45f;
            collision.bounce = .15f;
            collision.lifetimeLoss = .25f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return ps;
        }

        public static ParticleSystem CreateSparks(Transform parent, Material material,
            Transform collisionPlane)
        {
            var ps = Create(parent, "ImpactSparks", material, 48);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.22f, .62f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .055f);
            main.gravityModifier = 1.1f;
            var emission = ps.emission;
            emission.SetBursts(new[] {
                new ParticleSystem.Burst(0, 24), new ParticleSystem.Burst(.025f, 8) });
            var shape = ps.shape;
            shape.angle = 65;
            shape.radius = .07f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] {
                new GradientColorKey(new Color(1, .88f, .46f), 0),
                new GradientColorKey(new Color(1, .42f, .075f), .35f),
                new GradientColorKey(new Color(1, .12f, .015f), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.85f, .25f),
                    new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1,
                new AnimationCurve(new Keyframe(0, 1), new Keyframe(.2f, .9f), new Keyframe(1, 0)));
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.SetPlane(0, collisionPlane);
            collision.dampen = .45f;
            collision.bounce = .2f;
            collision.lifetimeLoss = .3f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2;
            renderer.velocityScale = .055f;
            renderer.cameraVelocityScale = 0;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV });
            return ps;
        }

        public static ParticleSystem CreateDust(Transform parent, Material material)
        {
            var ps = Create(parent, "Dust", material, 24);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .65f);
            main.startSize = new ParticleSystem.MinMaxCurve(.4f, .85f);
            main.gravityModifier = 0;
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(.03f, 8) });
            var shape = ps.shape;
            shape.angle = 70;
            shape.radius = .3f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.6f, .15f),
                    new GradientAlphaKey(.3f, .65f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1,
                new AnimationCurve(new Keyframe(0, .35f), new Keyframe(.45f, .8f), new Keyframe(1, 1.4f)));
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 2; sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = 0;
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0, .999f);
            sheet.cycleCount = 1;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
            return ps;
        }
    }
}
