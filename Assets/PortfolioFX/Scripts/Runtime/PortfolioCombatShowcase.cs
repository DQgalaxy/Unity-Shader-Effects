using System.Collections.Generic;
using UnityEngine;
using static PortfolioFX.FXShowcaseUtility;
namespace PortfolioFX
{
    [DisallowMultipleComponent]
    public sealed class PortfolioCombatShowcase : MonoBehaviour
    {
        public enum Combo{ArcaneVolley,GroundRift,MeteorShield,SpectralBurst}
        public Transform player;public new Camera camera;public PortfolioSpectralSentinel enemy;public Color accent=new Color(.15f,.8f,1);public Material softMaterial;Material pulseMaterial;Mesh pulseMesh;
        readonly List<Transient> live=new List<Transient>();GameObject root;float cooldown;bool built;
        sealed class Transient{public GameObject go;public float life;public float max;public Vector3 start,end,targetScale;public LineRenderer line;public bool billboard;}
        public void Build(){if(built)return;root=new GameObject("Combat_Composite_FX");root.transform.SetParent(transform,false);softMaterial=MakeSoft();pulseMaterial=MakePulse();pulseMesh=Quad();built=true;}
        Material MakeSoft(){var m=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));m.SetColor("_BaseColor",new Color(.18f,.75f,1,.72f));m.SetTexture("_BaseMap",Resources.Load<Texture2D>("FX/Smoke"));m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_ZWrite",0);m.renderQueue=3000;return m;}
        Material MakePulse(){var m=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));m.SetColor("_BaseColor",new Color(.25f,.95f,1,.7f));m.SetTexture("_BaseMap",Resources.Load<Texture2D>("FX/SoftDisc"));m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_ZWrite",0);m.renderQueue=3000;return m;}
        void Update(){cooldown-=Time.deltaTime;for(int i=live.Count-1;i>=0;i--){var x=live[i];x.life+=Time.deltaTime;float k=x.life/x.max;if(x.go==null){live.RemoveAt(i);continue;}if(x.line!=null){x.line.SetPosition(0,x.start);x.line.SetPosition(1,Vector3.Lerp(x.start,x.end,k));x.line.widthMultiplier=Mathf.Lerp(.12f,.01f,k);}else{x.go.transform.localScale=Vector3.Lerp(Vector3.one*.04f,x.targetScale.sqrMagnitude>.001f?x.targetScale:Vector3.one*.65f,k);if(x.billboard&&camera)x.go.transform.rotation=Quaternion.LookRotation(x.go.transform.position-camera.transform.position);var r=x.go.GetComponent<Renderer>();if(r){var mpb=new MaterialPropertyBlock();r.GetPropertyBlock(mpb);mpb.SetColor("_BaseColor",new Color(.18f,.8f,1,.46f*(1-k)));r.SetPropertyBlock(mpb);}}if(k>=1){Destroy(x.go);live.RemoveAt(i);}}}
        public void Trigger(Combo combo){if(!built||cooldown>0)return;cooldown=.2f;switch(combo){case Combo.ArcaneVolley:ArcaneVolley();break;case Combo.GroundRift:GroundRift();break;case Combo.MeteorShield:MeteorShield();break;default:SpectralBurst();break;}}
        public void PlayCinematic(float t){if(t<.15f)Trigger(Combo.ArcaneVolley);else if(t>.85f&&t<1)Trigger(Combo.GroundRift);else if(t>1.7f&&t<1.9f)Trigger(Combo.MeteorShield);else if(t>2.7f&&t<2.9f)Trigger(Combo.SpectralBurst);}
        Vector3 TargetPoint => enemy != null && enemy.AimPoint != null ? enemy.AimPoint.position : player.position + player.forward * 5f + Vector3.up * 1.2f;
        void MarkHit(float strength){if(enemy)enemy.Hit(strength);}
        void ArcaneVolley(){Vector3 origin=player.position+Vector3.up*1.25f+player.forward*.4f;Vector3 target=TargetPoint;for(int i=0;i<5;i++){float spread=(i-2)*.7f;Vector3 end=Vector3.Lerp(target,target+player.right*spread+Vector3.up*Mathf.Sin(i)*.25f,.92f);Beam(origin,end,.08f,.2f+i*.04f);Pulse(end,.18f,1.3f);}MarkHit(.42f);}
        void GroundRift(){Vector3 origin=player.position+player.forward*.3f;Vector3 target=TargetPoint;for(int i=0;i<3;i++){var go=new GameObject("Rift_Line_"+i);go.transform.SetParent(root.transform,false);var lr=go.AddComponent<LineRenderer>();lr.positionCount=8;lr.widthMultiplier=.12f;lr.material=softMaterial;lr.useWorldSpace=true;Vector3 side=player.right*(i-1)*.32f;for(int n=0;n<8;n++){float u=n/7f;Vector3 p=Vector3.Lerp(origin,target,u)+side*(1-u);p+=Vector3.up*(.015f+Mathf.Sin(n*2+i)*.04f);lr.SetPosition(n,p);}live.Add(new Transient{go=go,line=lr,life=0,max=1.2f,start=origin,end=target});}Pulse(Vector3.Lerp(origin,target,.58f),.65f,2);MarkHit(.66f);}
        void MeteorShield(){Vector3 center=player.position+Vector3.up*1.1f;Vector3 target=TargetPoint;for(int i=0;i<9;i++){float a=i*Mathf.PI*2/9;Vector3 start=target+new Vector3(Mathf.Cos(a)*4,4,Mathf.Sin(a)*4);Vector3 end=target+new Vector3(Mathf.Cos(a)*.8f,0,Mathf.Sin(a)*.8f);Beam(start,end,.14f,.6f);Pulse(end,.12f,1.8f);}Pulse(center,.8f,2.8f);Pulse(target,.56f,2.2f);MarkHit(.9f);}
        void SpectralBurst(){Vector3 c=TargetPoint;Vector3 origin=player.position+Vector3.up*1.2f;for(int i=0;i<16;i++){float a=i*Mathf.PI*2/16;Beam(c, c+new Vector3(Mathf.Cos(a),.15f,Mathf.Sin(a))*4,.07f,.35f);}Beam(origin,c,.2f,.5f);Pulse(c,.9f,3.2f);MarkHit(1f);}
        void Beam(Vector3 a,Vector3 b,float width,float life){var go=new GameObject("Composite_Energy_Ribbon");go.transform.SetParent(root.transform,false);var lr=go.AddComponent<LineRenderer>();lr.positionCount=2;lr.useWorldSpace=true;lr.widthMultiplier=width;lr.material=softMaterial;lr.SetPosition(0,a);lr.SetPosition(1,a);live.Add(new Transient{go=go,line=lr,life=0,max=life,start=a,end=b});}
        void Pulse(Vector3 p,float size,float life){var go=new GameObject("Impact_Radial_Disc");go.transform.SetParent(root.transform,false);go.transform.position=p;go.AddComponent<MeshFilter>().sharedMesh=pulseMesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=pulseMaterial;go.transform.localScale=Vector3.one*.04f;if(camera)go.transform.rotation=Quaternion.LookRotation(go.transform.position-camera.transform.position);live.Add(new Transient{go=go,life=0,max=life,targetScale=Vector3.one*(size*2.1f),billboard=true});}
        public void ResetShowcase(){foreach(var x in live)if(x.go)Destroy(x.go);live.Clear();}
    }
}



