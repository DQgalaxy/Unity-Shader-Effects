using UnityEngine;
using static PortfolioFX.FXShowcaseUtility;

namespace PortfolioFX
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FXMeteorPresentation : MonoBehaviour
    {
        public FXSequenceDriver sequence;
        public Camera viewCamera;
        public Renderer body, warning, shockwave;
        public Mesh rockMesh;
        public Material flameMaterial, emberMaterial, smokeMaterial, rockMaterial, flashMaterial, craterMaterial;
        public float impactTime = 2.4f;
        public Vector3 start = new Vector3(-2.4f, 5.8f, .2f);
        public Vector3 landing = new Vector3(0,.42f,0);
        GameObject generated;
        Mesh ribbonMesh, quad;
        Renderer trail, flash, crater;
        ParticleSystem embers, dust, debris;
        Vector3[] vertices;
        float sampled = -1;
        public bool Ready => sequence && viewCamera && body && warning && shockwave && rockMesh &&
            flameMaterial && emberMaterial && smokeMaterial && rockMaterial && flashMaterial && craterMaterial;
        public int LiveParticles => generated ? embers.particleCount + dust.particleCount + debris.particleCount : 0;
        void LateUpdate() { Refresh(); }
        Vector3 Flight(float time)
        {
            float u = Mathf.InverseLerp(1.6f, impactTime, time);
            return transform.TransformPoint(Vector3.Lerp(start, landing, u * u));
        }
        public void Refresh()
        {
            if (!Ready) return;
            if (!generated) { sequence.Seek(sequence.CurrentTime); Build(); }
            float t = sequence.CurrentTime, age = t - impactTime;
            body.enabled = t >= 1.6f && age < .09f;
            body.transform.position = Flight(t);
            body.transform.localRotation = Quaternion.Euler(t*91,t*53,t*38);
            float extent = Mathf.Max(rockMesh.bounds.size.x, Mathf.Max(rockMesh.bounds.size.y,rockMesh.bounds.size.z));
            body.transform.localScale = Vector3.one * (.92f / Mathf.Max(.001f,extent));
            Float(body,"_Intensity", age < 0 ? Mathf.Lerp(.8f,1.6f,Ease(1.6f,impactTime,t)) : 1.6f);
            warning.enabled = t >= .25f && age < 0;
            warning.transform.localScale = Vector3.one * (2.0f * Ease(.25f,.7f,t));
            shockwave.enabled = age >= 0 && age < 1.15f;
            shockwave.transform.localScale = Vector3.one * Mathf.Lerp(.15f,3.3f,Ease(0,1.15f,age));
            Float(shockwave,"_Intensity",1.8f*(1-Ease(.12f,1.15f,age)));
            float tailFade = Ease(1.6f,1.72f,t) * (1-Ease(impactTime,impactTime+.25f,t));
            Set(trail,t,tailFade);
            for (int i=0;i<=32;i++)
            {
                float u=i/32f,at=Mathf.Min(t,impactTime)-u*.34f;
                Vector3 p=Flight(at), tangent=Flight(at+.01f)-Flight(at-.01f);
                Vector3 right=Vector3.Cross(tangent,viewCamera.transform.forward).normalized;
                float width=.48f*(1-u);
                vertices[i*2]=transform.InverseTransformPoint(p-right*width);
                vertices[i*2+1]=transform.InverseTransformPoint(p+right*width);
            }
            ribbonMesh.vertices=vertices; ribbonMesh.RecalculateBounds();
            Vector3 hit=transform.TransformPoint(new Vector3(landing.x,.06f,landing.z));
            flash.transform.SetPositionAndRotation(hit+Vector3.up*.25f,viewCamera.transform.rotation);
            flash.transform.localScale=Vector3.one*Mathf.Lerp(.7f,3.2f,Ease(0,.22f,age));
            Set(flash,t,age>=0?1-Ease(.025f,.24f,age):0);
            crater.transform.SetPositionAndRotation(hit,Quaternion.Euler(90,18,0));
            crater.transform.localScale=Vector3.one*3;
            Set(crater,t,age>=0?1-Ease(2.6f,4.2f,age):0);
            Float(crater,"_Reveal",Ease(0,.28f,age));Float(crater,"_Heat",1-Ease(.2f,1.7f,age));
            if (!Mathf.Approximately(sampled,t))
            {
                embers.transform.position=dust.transform.position=debris.transform.position=hit+Vector3.up*.12f;
                Sample(embers,age);Sample(debris,age);Sample(dust,age-.08f);sampled=t;
            }
        }
        void Build()
        {
            generated=Child("MeteorPresentation_Generated",transform);quad=Quad();
            ribbonMesh=new Mesh {name="MeteorHistoryRibbon",hideFlags=HideFlags.DontSave};ribbonMesh.MarkDynamic();
            vertices=new Vector3[66];var uv=new Vector2[66];var indices=new int[192];
            for(int i=0;i<=32;i++){uv[i*2]=new Vector2(0,i/32f);uv[i*2+1]=new Vector2(1,i/32f);}
            for(int i=0;i<32;i++){int a=i*2,k=i*6;indices[k]=a;indices[k+1]=a+2;indices[k+2]=a+1;indices[k+3]=a+1;indices[k+4]=a+2;indices[k+5]=a+3;}
            ribbonMesh.vertices=vertices;ribbonMesh.uv=uv;ribbonMesh.triangles=indices;
            trail=MeshObject("FlameWake",generated.transform,ribbonMesh,flameMaterial);
            flash=MeshObject("ImpactFlash",generated.transform,quad,flashMaterial);
            crater=MeshObject("CoolingCrater",generated.transform,quad,craterMaterial);
            embers=Burst("ImpactEmbers",generated.transform,emberMaterial,64,1.05f,6,.075f,Color.white,1);
            var er=embers.GetComponent<ParticleSystemRenderer>();er.renderMode=ParticleSystemRenderMode.Stretch;er.lengthScale=2;er.velocityScale=.07f;
            dust=Burst("RollingDust",generated.transform,smokeMaterial,20,2.4f,1.1f,1.4f,Color.white);
            var ds=dust.shape;ds.radius=.7f;ds.angle=80;
            var size=dust.sizeOverLifetime;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.4f,1,1.8f));
            var plane=Child("GroundCollision",generated.transform).transform;plane.position=transform.TransformPoint(new Vector3(0,.01f,0));
            debris=FXGroundSlashParticles.CreateDebris(generated.transform,rockMesh,rockMaterial,plane);
            var dm=debris.main;dm.startSpeed=new ParticleSystem.MinMaxCurve(2,4.8f);dm.startLifetime=new ParticleSystem.MinMaxCurve(1.1f,2);
            var de=debris.emission;de.SetBursts(new[]{new ParticleSystem.Burst(0,28)});
            var ec=embers.collision;ec.enabled=true;ec.type=ParticleSystemCollisionType.Planes;ec.SetPlane(0,plane);ec.bounce=.15f;ec.dampen=.5f;
        }
        [ContextMenu("Rebuild Presentation")] public void Rebuild(){Dispose(generated);Dispose(ribbonMesh);Dispose(quad);generated=null;sampled=-1;}
        void OnDisable(){Rebuild();}
        void OnDestroy(){Rebuild();}
    }
}
