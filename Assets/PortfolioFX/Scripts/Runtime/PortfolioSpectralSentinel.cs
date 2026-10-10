using System.Collections.Generic;
using UnityEngine;
using static PortfolioFX.FXShowcaseUtility;

namespace PortfolioFX
{
    /// A lightweight portfolio target assembled from primitive meshes so the
    /// combat beats still read when the optional enemy model is unavailable.
    /// It is intentionally a spectral silhouette: the readable shape comes
    /// from the rim light, orbiting runes, and hit flash rather than a cartoon
    /// texture.
    [DisallowMultipleComponent]
    public sealed class PortfolioSpectralSentinel : MonoBehaviour
    {
        public Transform player;
        public Color bodyColor = new Color(.035f, .055f, .075f, 1f);
        public Color energyColor = new Color(1f, .18f, .07f, 1f);
        public Vector3 position = new Vector3(0, 0, 5.2f);

        readonly List<Transform> orbiters = new List<Transform>();
        GameObject rig;
        Material bodyMaterial;
        Material energyMaterial;
        Light rim;
        ParticleSystem embers;
        float hitPulse;
        float time;
        bool built;

        public Transform AimPoint { get; private set; }

        public void Build()
        {
            if (built) return;
            rig = new GameObject("Spectral_Sentinel_Rig");
            rig.transform.SetParent(transform, false);
            rig.transform.localPosition = position;
            rig.transform.localRotation = Quaternion.Euler(0, 180, 0);

            bodyMaterial = MakeLit("M_Sentinel_Body", bodyColor, .78f, .22f);
            energyMaterial = MakeEmissive("M_Sentinel_Runes", energyColor, 3.5f);

            Primitive("Sentinel_Core", PrimitiveType.Capsule, new Vector3(0, 1.45f, 0), new Vector3(.72f, 1.15f, .48f), bodyMaterial);
            Primitive("Sentinel_Chest_Rune", PrimitiveType.Sphere, new Vector3(0, 1.55f, -.42f), new Vector3(.28f, .42f, .07f), energyMaterial);
            Primitive("Sentinel_Head", PrimitiveType.Sphere, new Vector3(0, 2.75f, 0), new Vector3(.46f, .42f, .42f), bodyMaterial);
            Primitive("Sentinel_Eye", PrimitiveType.Sphere, new Vector3(0, 2.77f, -.39f), new Vector3(.22f, .10f, .05f), energyMaterial);
            Primitive("Sentinel_LeftShoulder", PrimitiveType.Sphere, new Vector3(-.68f, 1.9f, 0), new Vector3(.3f, .28f, .3f), bodyMaterial);
            Primitive("Sentinel_RightShoulder", PrimitiveType.Sphere, new Vector3(.68f, 1.9f, 0), new Vector3(.3f, .28f, .3f), bodyMaterial);
            Primitive("Sentinel_LeftHorn", PrimitiveType.Capsule, new Vector3(-.29f, 3.18f, .02f), new Vector3(.12f, .45f, .12f), energyMaterial).transform.localRotation = Quaternion.Euler(0, 0, -28);
            Primitive("Sentinel_RightHorn", PrimitiveType.Capsule, new Vector3(.29f, 3.18f, .02f), new Vector3(.12f, .45f, .12f), energyMaterial).transform.localRotation = Quaternion.Euler(0, 0, 28);

            for (int i = 0; i < 3; i++)
            {
                var rune = new GameObject("Sentinel_Orbit_Rune_" + i).transform;
                rune.SetParent(rig.transform, false);
                rune.localPosition = new Vector3(0, 1.65f + i * .42f, 0);
                rune.localRotation = Quaternion.Euler(90, 0, i * 23f);
                var line = rune.gameObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = 48;
                line.widthMultiplier = .018f + i * .009f;
                line.sharedMaterial = energyMaterial;
                float radius = .9f + i * .26f;
                for (int p = 0; p < line.positionCount; p++)
                {
                    float a = p * Mathf.PI * 2f / (line.positionCount - 1);
                    float wobble = Mathf.Sin(a * (3 + i) + i) * .06f;
                    line.SetPosition(p, new Vector3(Mathf.Cos(a) * (radius + wobble), Mathf.Sin(a) * (radius + wobble), 0));
                }
                orbiters.Add(rune);
            }

            AimPoint = new GameObject("Sentinel_AimPoint").transform;
            AimPoint.SetParent(rig.transform, false);
            AimPoint.localPosition = new Vector3(0, 1.65f, -.45f);

            rim = rig.AddComponent<Light>();
            rim.type = LightType.Point;
            rim.color = energyColor;
            rim.range = 5.5f;
            rim.intensity = 2.1f;
            rim.shadows = LightShadows.None;

            var particleMat = MakeEmissive("M_Sentinel_Embers", new Color(1f, .32f, .08f, 1f), 2.4f);
            embers = Burst("Sentinel_Embers", rig.transform, particleMat, 50, 1.8f, .7f, .045f, new Color(1f, .3f, .08f, .8f), .15f);
            var emission = embers.emission;
            emission.rateOverTime = 18;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            var emberMain = embers.main;
            emberMain.loop = true;
            emberMain.duration = 3f;
            embers.Play();
            built = true;
        }

        GameObject Primitive(string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(rig.transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<Renderer>();
            if (renderer) renderer.sharedMaterial = material;
            var collider = go.GetComponent<Collider>();
            if (collider) Destroy(collider);
            return go;
        }

        Material MakeLit(string name, Color color, float smoothness, float metallic)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            return mat;
        }

        Material MakeEmissive(string name, Color color, float strength)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            mat.SetColor("_BaseColor", color * strength);
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetFloat("_ZWrite", 0);
            mat.renderQueue = 3000;
            return mat;
        }

        void Update()
        {
            if (!built || rig == null) return;
            time += Time.deltaTime;
            rig.transform.localPosition = position + Vector3.up * (Mathf.Sin(time * 1.7f) * .06f);
            rig.transform.localRotation = Quaternion.Euler(0, 180 + Mathf.Sin(time * .75f) * 5f, 0);
            for (int i = 0; i < orbiters.Count; i++)
            {
                if (orbiters[i] == null) continue;
                orbiters[i].localRotation = Quaternion.Euler(90, 0, time * (24 + i * 13) * (i % 2 == 0 ? 1 : -1));
                float scale = 1f + hitPulse * (.1f + i * .04f);
                orbiters[i].localScale = Vector3.one * scale;
            }
            hitPulse = Mathf.MoveTowards(hitPulse, 0, Time.deltaTime * 2.4f);
            if (rim) rim.intensity = 2.1f + hitPulse * 6f;
            if (embers)
            {
                var main = embers.main;
                main.startColor = Color.Lerp(new Color(1f, .22f, .06f, .72f), Color.white, hitPulse);
            }
        }

        public void Hit(float strength)
        {
            hitPulse = Mathf.Clamp01(hitPulse + strength);
            if (rig) rig.transform.localScale = Vector3.one * (1f + hitPulse * .08f);
        }

        void OnDestroy()
        {
            if (bodyMaterial) Destroy(bodyMaterial);
            if (energyMaterial) Destroy(energyMaterial);
        }
    }
}
