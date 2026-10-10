using UnityEngine;
using static PortfolioFX.FXShowcaseUtility;

namespace PortfolioFX
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FXDepthLabPresentation : MonoBehaviour
    {
        public FXSequenceDriver sequence;
        public Material particleMaterial;
        public bool useSoftFade=true;
        [Range(.02f,1)]public float softness=.3f;
        GameObject generated;
        ParticleSystem particles;
        float sampled=-1;
        public bool Ready => sequence && particleMaterial;
        void LateUpdate(){Refresh();}
        public void Refresh()
        {
            if(!Ready)return;
            if(!generated){sequence.Seek(sequence.CurrentTime);Build();}
            var r=particles.GetComponent<ParticleSystemRenderer>();
            Float(r,"_UseSoftFade",useSoftFade?1:0);Float(r,"_Softness",softness);
            float t=sequence.CurrentTime;
            if(!Mathf.Approximately(t,sampled)){Sample(particles,t+.7f);sampled=t;}
        }
        void Build()
        {
            generated=Child("DepthLabPresentation_Generated",transform);
            particles=Burst("SoftIntersectionMist",generated.transform,particleMaterial,60,1.7f,.12f,.9f,Color.white);
            particles.transform.localPosition=new Vector3(0,.28f,-.25f);
            var main=particles.main;main.duration=8;
            var emission=particles.emission;emission.SetBursts(new ParticleSystem.Burst[0]);emission.rateOverTime=24;
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(4.3f,.25f,1.7f);
            var size=particles.sizeOverLifetime;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.5f,1,1.3f));
        }
        [ContextMenu("Rebuild Presentation")]public void Rebuild(){Dispose(generated);generated=null;sampled=-1;}
        void OnDisable(){Rebuild();}void OnDestroy(){Rebuild();}
    }
}
