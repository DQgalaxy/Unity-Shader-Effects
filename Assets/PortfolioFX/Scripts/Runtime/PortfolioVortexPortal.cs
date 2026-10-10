using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace PortfolioFX
{
    [DisallowMultipleComponent]
    public sealed class PortfolioVortexPortal : MonoBehaviour
    {
        public Transform player,target;public new Camera camera;public Vector3 position;public float triggerDistance=1.8f;
        public Color coreColor=new Color(.15f,.8f,1),rimColor=new Color(.55f,.95f,1);
        public float radius=1.55f;public bool visible=true;
        Material material,particleMaterial;GameObject surface,root;List<LineRenderer> rings=new List<LineRenderer>();ParticleSystem motes;Camera windowCamera;RenderTexture window;Light aura;float openness;
        static readonly int WorldTex=Shader.PropertyToID("_WorldTex");
        public void Build(){
            if(root)return;root=new GameObject("Portal_Vortex_Rig");root.layer=31;root.transform.SetParent(transform,false);root.transform.position=position;
            var vortexShader=Shader.Find("PortfolioFX/PortalVortex");
            if(!vortexShader||!vortexShader.isSupported)vortexShader=Shader.Find("Universal Render Pipeline/Unlit");
            material=new Material(vortexShader);
            var noise=Resources.Load<Texture2D>("FX/PortalPerlin");
            if(!noise)noise=Resources.Load<Texture2D>("FX/NoiseRG");
            material.SetTexture("_NoiseTex",noise);material.SetTexture("_BaseMap",noise);material.SetTexture("_RingTex",Resources.Load<Texture2D>("FX/RingMask"));material.SetColor("_Tint",new Color(.08f,.72f,1));material.SetColor("_Core",new Color(.42f,1f,.92f));material.SetColor("_BaseColor",new Color(coreColor.r,coreColor.g,coreColor.b,.32f));material.SetFloat("_Opacity",0);material.SetFloat("_Rim",2.1f);material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ZWrite",0);material.renderQueue=3000;
            aura=root.AddComponent<Light>();aura.type=LightType.Point;aura.color=new Color(.18f,.72f,1f);aura.range=6.5f;aura.intensity=0;aura.shadows=LightShadows.None;
            if(target && camera){window=new RenderTexture(768,768,24,RenderTextureFormat.ARGBHalf){name="Portal_Destination_Window",hideFlags=HideFlags.DontSave};window.Create();material.SetTexture("_WorldTex",window);var portalCameraObject=new GameObject("Portal_Destination_Camera");portalCameraObject.layer=31;windowCamera=portalCameraObject.AddComponent<Camera>();windowCamera.transform.SetParent(root.transform,false);windowCamera.enabled=false;windowCamera.cameraType=CameraType.Preview;windowCamera.targetTexture=window;windowCamera.nearClipPlane=.1f;windowCamera.farClipPlane=180;windowCamera.cullingMask &= ~(1<<31);windowCamera.fieldOfView=camera.fieldOfView;windowCamera.clearFlags=CameraClearFlags.SolidColor;windowCamera.backgroundColor=new Color(.005f,.015f,.02f,1);var data=windowCamera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;}
            surface=new GameObject("Portal_Vortex_Surface");surface.layer=31;surface.transform.SetParent(root.transform,false);surface.AddComponent<MeshFilter>().sharedMesh=CircleMesh(64);surface.AddComponent<MeshRenderer>().sharedMaterial=material;surface.transform.localScale=new Vector3(radius*1.08f,radius*1.25f,1);
            for(int i=0;i<3;i++)CreateRing(i);
            particleMaterial=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));particleMaterial.SetColor("_BaseColor",new Color(.35f,.85f,1,1));particleMaterial.SetTexture("_BaseMap",Resources.Load<Texture2D>("FX/Smoke"));particleMaterial.SetFloat("_Surface",1);particleMaterial.SetFloat("_Blend",0);particleMaterial.SetFloat("_ZWrite",0);particleMaterial.renderQueue=3000;
            motes=root.AddComponent<ParticleSystem>();motes.name="Portal_Vortex_Dust";var main=motes.main;main.loop=true;main.startLifetime=new ParticleSystem.MinMaxCurve(.8f,1.8f);main.startSpeed=new ParticleSystem.MinMaxCurve(.5f,1.5f);main.startSize=new ParticleSystem.MinMaxCurve(.025f,.09f);main.startColor=new Color(.35f,.85f,1,.8f);main.simulationSpace=ParticleSystemSimulationSpace.Local;main.maxParticles=220;var em=motes.emission;em.rateOverTime=90;var sh=motes.shape;sh.shapeType=ParticleSystemShapeType.Circle;sh.radius=radius*.96f;var vel=motes.velocityOverLifetime;vel.enabled=true;vel.orbitalX=new ParticleSystem.MinMaxCurve(0f);vel.orbitalY=new ParticleSystem.MinMaxCurve(0f);vel.orbitalZ=new ParticleSystem.MinMaxCurve(1.1f);var r=motes.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=particleMaterial;r.renderMode=ParticleSystemRenderMode.Billboard;r.sortMode=ParticleSystemSortMode.Distance;
            var col=root.AddComponent<SphereCollider>();col.isTrigger=true;col.radius=radius*1.25f;col.center=Vector3.zero;
            SetVisible(visible);
        }
        void CreateRing(int index){var go=new GameObject("Portal_Vortex_Ring_"+index);go.layer=31;go.transform.SetParent(root.transform,false);var lr=go.AddComponent<LineRenderer>();lr.useWorldSpace=false;lr.loop=true;lr.positionCount=96;lr.widthCurve=new AnimationCurve(new Keyframe(0,.028f),new Keyframe(.5f,.058f),new Keyframe(1,.016f));var ringTint=Color.Lerp(coreColor,rimColor,.35f+.15f*index);ringTint.a=.22f;lr.startColor=ringTint;lr.endColor=ringTint;lr.material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));lr.material.SetColor("_BaseColor",ringTint);lr.material.SetFloat("_Surface",1);lr.material.SetFloat("_Blend",0);lr.material.SetFloat("_ZWrite",0);lr.material.renderQueue=3000;lr.transform.localScale=new Vector3(1,1.25f,1);float r=radius*(1-index*.12f);for(int i=0;i<lr.positionCount;i++){float a=i*Mathf.PI*2/(lr.positionCount-1);float w=Mathf.Sin(a*(4+index*2))*(.06f+index*.02f);lr.SetPosition(i,new Vector3(Mathf.Cos(a)*(r+w),Mathf.Sin(a)*(r+w),-.02f));}rings.Add(lr);}
        public void Open(float amount){openness=amount;material?.SetFloat("_Opacity",amount);if(motes){var em=motes.emission;em.rateOverTime=90*amount;}}
        public void SetVisible(bool state){visible=state;if(root)root.SetActive(state);}
        public void ResetPortal(){crossed=false;SetVisible(true);Open(0);}
        bool crossed;
        void Update(){if(!root)return;float t=Time.time;if(aura)aura.intensity=Mathf.Lerp(aura.intensity,openness*3.2f,Time.deltaTime*8f);for(int i=0;i<rings.Count;i++){rings[i].transform.localRotation=Quaternion.Euler(0,0,t*(12+i*7)*(i%2==0?1:-1));var ringColor=Color.Lerp(coreColor,rimColor,.5f+.5f*Mathf.Sin(t*2+i));ringColor.a=.24f*openness;rings[i].material.SetColor("_BaseColor",ringColor);}if(material)material.SetFloat("_Rotation",1.1f);if(windowCamera&&target&&camera&&visible){windowCamera.transform.position=target.position+(camera.transform.position-position);windowCamera.transform.rotation=camera.transform.rotation;windowCamera.Render();}
            // The showcase director owns the cinematic crossing and hides the
            // portal on its authored beat. Keep distance-triggered hiding only
            // for standalone/manual use so the approach cannot close it early.
            if(player && visible && !crossed && !GetComponentInParent<PortfolioShowcaseDirector>() && Vector3.Distance(player.position,root.transform.position)<triggerDistance){crossed=true;SetVisible(false);}
        }
        public void Hide(){crossed=true;SetVisible(false);}
        static Mesh CircleMesh(int count){var m=new Mesh();var v=new Vector3[count+1];var uv=new Vector2[v.Length];v[0]=Vector3.zero;uv[0]=Vector2.one*.5f;for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;v[i+1]=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);uv[i+1]=new Vector2(Mathf.Cos(a)*.5f+.5f,Mathf.Sin(a)*.5f+.5f);}var tri=new int[count*3];for(int i=0;i<count;i++){tri[i*3]=0;tri[i*3+1]=i+1;tri[i*3+2]=(i+1)%count+1;}m.vertices=v;m.uv=uv;m.triangles=tri;m.RecalculateBounds();return m;}
        void OnDestroy(){if(root)Destroy(root);if(motes)Destroy(motes);if(window){window.Release();Destroy(window);}if(windowCamera)Destroy(windowCamera.gameObject);}
    }
}









