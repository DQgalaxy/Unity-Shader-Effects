using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PortfolioFX
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FXFireballPresentation : MonoBehaviour
    {
        public FXSequenceDriver sequence;
        public FXCharacterMotion character;
        public Transform hand;
        public Camera presentationCamera;
        public Mesh targetMesh;
        public Material fireMaterial, flameMaterial, ribbonMaterial, haloMaterial;
        public Material explosionMaterial, smokeMaterial, targetMaterial, fractureMaterial;
        public Renderer[] legacyRenderers;
        public GameObject weapon;
        public Vector3 targetBase = new Vector3(2.6f, .08f, 0);
        public float targetHeight = 2.35f;
        public float releaseTime = 1.8f, hitTime = 2.45f;

        GameObject layers;
        Renderer fire, corona, halo, impact, shock, dust;
        Renderer[] tongues, wake, sparks;
        LineRenderer tail;
        LineRenderer[] orbits;
        Renderer stone;
        FXFireballFragments.Piece[] pieces;
        Light fireLight;
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        MaterialPropertyBlock block;
        Vector3 launchLocal;
        Mesh cardMesh;
        static readonly int FrameId = Shader.PropertyToID("_Frame");
        static readonly int TimeId = Shader.PropertyToID("_EffectTime");
        static readonly int FadeId = Shader.PropertyToID("_Fade");

        public int FragmentCount => pieces == null ? 0 : pieces.Length;
        public Vector3 LaunchPosition => transform.TransformPoint(launchLocal);
        public Vector3 HitPosition => transform.TransformPoint(targetBase + new Vector3(-.3f, 1.25f, 0));
        public Renderer FireRenderer => fire;
        public Renderer TargetRenderer => stone;

        void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            if (sequence == null || character == null || hand == null || targetMesh == null ||
                fireMaterial == null || flameMaterial == null || ribbonMaterial == null || haloMaterial == null ||
                explosionMaterial == null || smokeMaterial == null || targetMaterial == null || fractureMaterial == null) return;
            if (layers == null) Build();
            if (weapon != null && weapon.activeSelf) weapon.SetActive(false);
            if (legacyRenderers != null) foreach (var r in legacyRenderers) if (r != null) r.enabled = false;
            float t = sequence.CurrentTime;
            Camera camera = presentationCamera != null ? presentationCamera : Camera.main;
            Quaternion facing = camera != null ? camera.transform.rotation : Quaternion.identity;
            Vector3 view = camera != null ? -camera.transform.forward : Vector3.back;
            float charge = Ease(.25f, releaseTime, t);
            float exist = Ease(.2f,.48f,t) * (1 - Ease(hitTime-.025f,hitTime+.025f,t));
            Vector3 center = t < releaseTime ? HandPoint() : Flight(t);
            float diameter = Mathf.Lerp(.28f,.85f,charge);
            CardState(fire,center,facing,diameter,exist,16+Mathf.Repeat(t*20,16),t);
            CardState(corona,center+view*.012f,facing*Quaternion.Euler(0,0,67),diameter*1.15f,exist*.28f,19+Mathf.Repeat(t*17,12),t);
            fireLight.transform.position=center; fireLight.intensity=exist*(1+charge*2.4f); fireLight.range=3;

            float charging=exist*(1-Ease(releaseTime-.12f,releaseTime+.04f,t));
            CardState(halo,center-view*.08f,facing*Quaternion.Euler(0,0,t*31),.7f+charge*.9f,charging*.45f,0,t);
            for(int j=0;j<orbits.Length;j++)
            {
                var line=orbits[j];line.enabled=charging>.001f;line.widthMultiplier=.045f+.045f*charge;
                Quaternion q=Quaternion.Euler(25+j*60,t*(j==0?60:-48),j*65);
                for(int i=0;i<line.positionCount;i++)
                {
                    float u=i/(line.positionCount-1f),a=u*Mathf.PI*1.65f+t*(j==0?2.4f:-2.1f)+j*2;
                    Vector3 p=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*(.3f+.27f*charge);
                    line.SetPosition(i,center+q*p);
                }
                Set(line,t,0,charging*.75f);
            }
            for(int i=0;i<tongues.Length;i++)
            {
                float a=t*(1.8f+i*.16f)+i*2.094f;
                Vector3 shift=new Vector3(Mathf.Cos(a),.2f,Mathf.Sin(a))*(diameter*.22f);
                CardState(tongues[i],center+shift,facing*Quaternion.Euler(0,0,-18+i*18),diameter*.82f,exist*.55f,Mathf.Repeat(t*24+i*8,36),t);
            }

            // Reconstruct the wake from prior trajectory samples, not a fixed line to the caster.
            float tailFade=Ease(releaseTime,releaseTime+.1f,t)*(1-Ease(hitTime,hitTime+.28f,t));
            tail.enabled=tailFade>.001f;tail.widthMultiplier=.48f;
            for(int i=0;i<tail.positionCount;i++)
            {
                float u=i/(tail.positionCount-1f);
                tail.SetPosition(i,Flight(Mathf.Min(t,hitTime)-u*.32f));
            }
            Set(tail,t,0,tailFade);
            for(int i=0;i<wake.Length;i++)
            {
                float age=(i+1)*.034f,at=t-age;
                float alpha=at>=releaseTime&&at<=hitTime ? (1-Ease(.1f,.48f,age))*(1-Ease(hitTime,hitTime+.45f,t))*.42f:0;
                Vector3 p=Flight(at)+Vector3.up*age*.45f;
                CardState(wake[i],p,facing*Quaternion.Euler(0,0,i*37),Mathf.Lerp(.65f,.18f,i/(wake.Length-1f)),alpha,20+Mathf.Repeat(at*21+i*3,13),t);
            }

            float ageHit=t-hitTime;
            if(ageHit>=0)fireLight.intensity=5*Mathf.Exp(-ageHit*12);
            float flash=ageHit>=0 ? 1-Ease(.12f,.65f,ageHit):0;
            CardState(impact,HitPosition+view*.08f,facing,Mathf.Lerp(.4f,2.8f,Ease(0,.55f,ageHit)),flash,Mathf.Clamp(ageHit*38,0,35),t);
            CardState(shock,transform.TransformPoint(targetBase+Vector3.up*.04f),Quaternion.Euler(90,0,0),Mathf.Lerp(.4f,4.4f,Ease(0,.55f,ageHit)),ageHit>=0?(1-Ease(.1f,.7f,ageHit))*.65f:0,0,t);
            CardState(dust,HitPosition+Vector3.up*Mathf.Max(0,ageHit)*.22f,facing,Mathf.Lerp(.8f,3.3f,Ease(0,1.7f,ageHit)),ageHit>=0?Ease(0,.15f,ageHit)*(1-Ease(.5f,1.8f,ageHit))*.42f:0,Mathf.Clamp(ageHit*20,0,35),t);
            for(int i=0;i<sparks.Length;i++)
            {
                float seed=Hash(i+19),delay=(i%4)*.024f,age=ageHit-delay;
                Vector3 dir=new Vector3(.4f+Hash(i+51),.5f+Hash(i+71)*1.6f,(seed-.5f)*2);
                Vector3 p=HitPosition+dir*age*2+Vector3.down*(age*age*2.8f);
                CardState(sparks[i],p,facing*Quaternion.Euler(0,0,i*27),.07f+Hash(i+9)*.09f,age>=0?1-Ease(.14f,.65f+seed*.3f,age):0,22,t);
            }
            bool broken=ageHit>=.035f;
            stone.enabled=!broken;
            for(int i=0;i<pieces.Length;i++)
            {
                var piece=pieces[i];piece.renderer.enabled=broken;
                if(!broken){piece.transform.localPosition=piece.home;piece.transform.localRotation=Quaternion.identity;continue;}
                float q=Mathf.Clamp01((ageHit-.035f)/(1.0f+Hash(i)*.6f));
                float angle=i*2.399963f;
                Vector3 destination=new Vector3(targetBase.x+Mathf.Cos(angle)*(.5f+Hash(i+30)),.1f+piece.floorOffset,targetBase.z+Mathf.Sin(angle)*(1.0f+Hash(i+40)));
                Vector3 p=Vector3.Lerp(piece.home,destination,1-Mathf.Pow(1-q,2));
                p.y+=Mathf.Sin(q*Mathf.PI)*(.7f+Hash(i+80));
                piece.transform.localPosition=p;
                piece.transform.localRotation=Quaternion.Euler(new Vector3(75+i*13,41+i*31,63+i*19)*q);
            }
        }

        Vector3 HandPoint()
        {
            Vector3 toward=(HitPosition-hand.position).normalized;
            return hand.position+toward*.22f+Vector3.up*.12f;
        }
        public Vector3 Flight(float seconds)
        {
            float u=Mathf.InverseLerp(releaseTime,hitTime,seconds);
            return Vector3.Lerp(LaunchPosition,HitPosition,u)+transform.up*(Mathf.Sin(u*Mathf.PI)*.2f);
        }
        static float Ease(float a,float b,float t){return Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));}
        static float Hash(int i){return Mathf.Repeat(Mathf.Sin(i*127.1f+17.7f)*43758.5453f,1);}

        void Build()
        {
            block=new MaterialPropertyBlock();
            layers=new GameObject("FireballPresentationLayers"){hideFlags=HideFlags.DontSave};layers.transform.SetParent(transform,false);
            cardMesh=new Mesh{name="FireballCard",hideFlags=HideFlags.DontSave};
            cardMesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
            cardMesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};cardMesh.triangles=new[]{0,2,1,0,3,2};cardMesh.RecalculateNormals();cardMesh.RecalculateBounds();ownedMeshes.Add(cardMesh);
            fire=Card("PalmFireball",fireMaterial);corona=Card("FireballCorona",fireMaterial);halo=Card("TexturedChargeHalo",haloMaterial);
            impact=Card("ImpactExplosion",explosionMaterial);shock=Card("GroundShockwave",haloMaterial);dust=Card("ImpactDust",smokeMaterial);
            tongues=new Renderer[3];for(int i=0;i<tongues.Length;i++)tongues[i]=Card("FlameTongue_"+i,flameMaterial);
            wake=new Renderer[10];for(int i=0;i<wake.Length;i++)wake[i]=Card("CoolingWake_"+i,fireMaterial);
            sparks=new Renderer[18];for(int i=0;i<sparks.Length;i++)sparks[i]=Card("ImpactEmber_"+i,fireMaterial);
            tail=Line("TaperedFireWake",32);
            orbits=new[]{Line("ChargeFlameArc_A",48),Line("ChargeFlameArc_B",48)};
            fireLight=Child("FireballLight").AddComponent<Light>();fireLight.type=LightType.Point;fireLight.color=new Color(1,.3f,.055f);fireLight.shadows=LightShadows.None;
            var target=Child("RuneStone_Intact");
            float scale=targetHeight/Mathf.Max(.001f,targetMesh.bounds.size.y);
            Quaternion rotation=Quaternion.Euler(0,35,0);
            Vector3 offset=targetBase-rotation*(new Vector3(targetMesh.bounds.center.x,targetMesh.bounds.min.y,targetMesh.bounds.center.z)*scale);
            target.transform.localPosition=offset;target.transform.localRotation=rotation;target.transform.localScale=Vector3.one*scale;
            target.AddComponent<MeshFilter>().sharedMesh=targetMesh;stone=target.AddComponent<MeshRenderer>();stone.sharedMaterial=targetMaterial;
            pieces=FXFireballFragments.Build(targetMesh,scale,rotation,offset,layers.transform,targetMaterial,fractureMaterial,ownedMeshes);
            // Sample the launch pose once so seeking and replay use the same launch position.
            character.Sample(releaseTime);launchLocal=transform.InverseTransformPoint(HandPoint());character.Sample(sequence.CurrentTime);
        }
        GameObject Child(string label){var o=new GameObject(label){hideFlags=HideFlags.DontSave};o.transform.SetParent(layers.transform,false);return o;}
        Renderer Card(string name,Material mat){var o=Child(name);o.AddComponent<MeshFilter>().sharedMesh=cardMesh;var r=o.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return r;}
        LineRenderer Line(string name,int count)
        {
            var l=Child(name).AddComponent<LineRenderer>();l.sharedMaterial=ribbonMaterial;l.useWorldSpace=true;l.positionCount=count;l.alignment=LineAlignment.View;l.textureMode=LineTextureMode.Stretch;
            l.widthCurve=new AnimationCurve(new Keyframe(0,1),new Keyframe(.3f,.7f),new Keyframe(1,0));
            l.numCapVertices=2;l.numCornerVertices=2;l.shadowCastingMode=ShadowCastingMode.Off;l.receiveShadows=false;return l;
        }
        void CardState(Renderer r,Vector3 p,Quaternion q,float size,float fade,float frame,float time)
        {
            r.enabled=fade>.001f;if(!r.enabled)return;
            r.transform.SetPositionAndRotation(p,q);r.transform.localScale=Vector3.one*Mathf.Max(.001f,size);Set(r,time,frame,fade);
        }
        void Set(Renderer r,float t,float frame,float fade){block.Clear();block.SetFloat(TimeId,t);block.SetFloat(FrameId,frame);block.SetFloat(FadeId,Mathf.Clamp01(fade));r.SetPropertyBlock(block);}
        void OnDisable()
        {
            if(layers!=null){layers.SetActive(false);Dispose(layers);}layers=null;
            foreach(var mesh in ownedMeshes)if(mesh!=null)Dispose(mesh);ownedMeshes.Clear();pieces=null;
        }
        static void Dispose(Object o){if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
    }
}
