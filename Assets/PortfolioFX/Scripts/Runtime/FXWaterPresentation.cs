using UnityEngine;
using static PortfolioFX.FXShowcaseUtility;

namespace PortfolioFX
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FXWaterPresentation : MonoBehaviour
    {
        public FXSequenceDriver sequence;
        public Renderer water;
        public Renderer drop;
        public Renderer[] shoreRocks;
        public Mesh rockMesh;
        public Mesh crownMesh;
        public Material crownMaterial, dropletMaterial;
        public float impactTime=1.3f;
        GameObject generated;
        Renderer crown;
        ParticleSystem droplets;
        float sampled=-1;
        public bool Ready => sequence && sequence.center && water && drop && rockMesh && crownMesh && crownMaterial && dropletMaterial;
        void LateUpdate(){Refresh();}
        public void Refresh()
        {
            if(!Ready)return;
            if(!generated){sequence.Seek(sequence.CurrentTime);Build();}
            float t=sequence.CurrentTime,age=t-impactTime;
            Vector3 rockSize=rockMesh.bounds.size;
            float rockExtent=Mathf.Max(.001f,Mathf.Max(rockSize.x,Mathf.Max(rockSize.y,rockSize.z)));
            drop.enabled=age<0;
            drop.transform.localScale=Vector3.one*(.4f/rockExtent);
            drop.transform.localRotation=Quaternion.Euler(t*100,t*60,25);
            for(int i=0;shoreRocks!=null&&i<shoreRocks.Length;i++)
                if(shoreRocks[i])shoreRocks[i].transform.localScale=new Vector3(.95f,.75f+i*.07f,.85f)/rockExtent;
            float life=age>=0?1-Ease(.25f,.75f,age):0;
            Vector3 size=crownMesh.bounds.size;
            float width=Mathf.Max(.001f,Mathf.Max(size.x,size.z));
            float diameter=Mathf.Lerp(.3f,1.8f,Ease(0,.7f,age));
            float height=Mathf.Lerp(.15f,.85f,Ease(0,.12f,age))*(1-Ease(.22f,.75f,age));
            crown.transform.localScale=new Vector3(diameter/width,height/Mathf.Max(size.y,.001f),diameter/width);
            Vector3 center=sequence.center.position;center.y=water.transform.position.y+.008f;
            crown.transform.position=center-Vector3.up*crownMesh.bounds.min.y*crown.transform.localScale.y;
            Set(crown,t,life*.7f);
            if(!Mathf.Approximately(t,sampled))
            {
                droplets.transform.position=center+Vector3.up*.05f;Sample(droplets,age);sampled=t;
            }
        }
        void Build()
        {
            generated=Child("WaterPresentation_Generated",transform);
            crown=MeshObject("TexturedSplashCrown",generated.transform,crownMesh,crownMaterial);
            droplets=Burst("WaterDroplets",generated.transform,dropletMaterial,36,.95f,2.8f,.14f,Color.white,.65f);
            var shape=droplets.shape;shape.angle=42;shape.radius=.2f;
            var r=droplets.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Stretch;r.lengthScale=1.4f;r.velocityScale=.035f;
            water.localBounds=new Bounds(Vector3.zero,new Vector3(2,1,2));
        }
        [ContextMenu("Rebuild Presentation")]public void Rebuild(){Dispose(generated);generated=null;sampled=-1;}
        void OnDisable(){Rebuild();}void OnDestroy(){Rebuild();}
    }
}
