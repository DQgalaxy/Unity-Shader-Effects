using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
using static PortfolioFX.FXShowcaseUtility;
namespace PortfolioFX
{
    [DisallowMultipleComponent]
    public sealed class PortfolioHologramProjection : MonoBehaviour
    {
        public Transform player,source;public new Camera camera;public Vector3 position;public Color tint=new Color(.25f,.8f,1);public float width=6,height=3.7f;
        Camera projector;RenderTexture target;Material material;GameObject screen,rig,basePlatform,ghostVillage;Light emitterLight;readonly List<LineRenderer> rings=new List<LineRenderer>();readonly List<Material> ghostMaterials=new List<Material>();bool built;float ringTime;
        public void Build(){if(built)return;rig=new GameObject("Viking_Hologram_Projector");rig.transform.SetParent(transform,false);rig.transform.position=position;
            target=new RenderTexture(960,540,24,RenderTextureFormat.ARGBHalf){name="Viking_Hologram_Render",hideFlags=HideFlags.DontSave};target.Create();
            projector=new GameObject("Viking_Hologram_Camera").AddComponent<Camera>();projector.transform.SetParent(rig.transform,false);projector.transform.position=source.position+new Vector3(9,5,-13);projector.transform.LookAt(source.position+Vector3.up*1.2f);projector.targetTexture=target;projector.enabled=false;projector.cameraType=CameraType.Preview;projector.fieldOfView=42;projector.nearClipPlane=.1f;projector.farClipPlane=180;projector.cullingMask &= ~(1<<31);var data=projector.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;
            var projectionShader=Shader.Find("PortfolioFX/HologramProjection");
            if(!projectionShader||!projectionShader.isSupported)projectionShader=Shader.Find("Universal Render Pipeline/Unlit");
            material=new Material(projectionShader){name="M_Viking_HologramProjection",hideFlags=HideFlags.DontSave};
            var grid=Resources.Load<Texture2D>("FX/HologramRay");
            if(!grid)grid=Resources.Load<Texture2D>("FX/NoiseRG");
            material.SetTexture("_MainTex",target);material.SetTexture("_BaseMap",target);material.SetTexture("_Grid",grid);material.SetColor("_Tint",tint);material.SetColor("_BaseColor",new Color(tint.r,tint.g,tint.b,.42f));material.SetFloat("_Opacity",.48f);material.SetFloat("_Flicker",.08f);material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ZWrite",0);material.renderQueue=3000;
            screen=new GameObject("Viking_Village_Hologram");screen.layer=31;screen.transform.SetParent(rig.transform,false);screen.transform.localPosition=new Vector3(0,1.1f,1.4f);screen.transform.localRotation=Quaternion.Euler(10,0,0);screen.transform.localScale=new Vector3(width,height,1);screen.AddComponent<MeshFilter>().sharedMesh=Quad();screen.AddComponent<MeshRenderer>().sharedMaterial=material;
            var glowShader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var glow=new Material(glowShader){name="M_Hologram_EmitterGlow",hideFlags=HideFlags.DontSave};glow.SetColor("_BaseColor",new Color(tint.r,tint.g,tint.b,.25f));glow.SetTexture("_BaseMap",Resources.Load<Texture2D>("FX/SoftDisc"));glow.SetFloat("_Surface",1);glow.SetFloat("_Blend",0);glow.SetFloat("_ZWrite",0);glow.renderQueue=3000;
            basePlatform=GameObject.CreatePrimitive(PrimitiveType.Cylinder);basePlatform.name="Hologram_Emitter_Base";basePlatform.layer=31;basePlatform.transform.SetParent(rig.transform,false);basePlatform.transform.localPosition=new Vector3(0,.04f,1.4f);basePlatform.transform.localScale=new Vector3(2.25f,.06f,1.25f);var baseRenderer=basePlatform.GetComponent<Renderer>();baseRenderer.sharedMaterial=glow;var baseCollider=basePlatform.GetComponent<Collider>();if(baseCollider)Destroy(baseCollider);emitterLight=rig.AddComponent<Light>();emitterLight.type=LightType.Point;emitterLight.color=tint;emitterLight.range=5.5f;emitterLight.intensity=1.6f;emitterLight.shadows=LightShadows.None;
            for(int i=0;i<3;i++){var ring=CreateRing(glow,i);rings.Add(ring);}
            CreateGhostVillage();
            built=true;SetVisible(false);
        }
        public void SetVisible(bool value){if(!built)return;rig.SetActive(value);if(emitterLight)emitterLight.intensity=value?1.6f:0;if(value)projector.Render();}
        void LateUpdate(){if(!built||!rig.activeSelf)return;projector.transform.position=source.position+new Vector3(9,5,-13);projector.transform.LookAt(source.position+Vector3.up*1.2f);projector.Render();ringTime+=Time.deltaTime;for(int i=0;i<rings.Count;i++){if(rings[i]){rings[i].transform.localRotation=Quaternion.Euler(0,ringTime*(18+i*9)*(i%2==0?1:-1),0);var c=Color.Lerp(tint,Color.white,.25f+.15f*Mathf.Sin(ringTime*3+i));c.a=.25f+.12f*Mathf.Sin(ringTime*4+i);rings[i].startColor=c;rings[i].endColor=c;}}}
        LineRenderer CreateRing(Material glow,int index){var go=new GameObject("Hologram_Emitter_Ring_"+index);go.layer=31;go.transform.SetParent(rig.transform,false);go.transform.localPosition=new Vector3(0,.12f+index*.025f,1.4f);var lr=go.AddComponent<LineRenderer>();lr.useWorldSpace=false;lr.loop=true;lr.positionCount=80;lr.widthMultiplier=.025f+index*.012f;lr.material=glow;float rx=1.8f-index*.25f,rz=.92f-index*.1f;for(int i=0;i<lr.positionCount;i++){float a=i*Mathf.PI*2/(lr.positionCount-1);lr.SetPosition(i,new Vector3(Mathf.Cos(a)*rx,0,Mathf.Sin(a)*rz));}return lr;}
        void CreateGhostVillage(){if(!source)return;var holo=Shader.Find("PortfolioFX/HologramMesh");if(!holo||!holo.isSupported)return;ghostVillage=Instantiate(source.gameObject,rig.transform);ghostVillage.name="Hologram_Viking_Village_Model";ghostVillage.layer=31;ghostVillage.transform.localPosition=new Vector3(0,.18f,1.45f);ghostVillage.transform.localRotation=Quaternion.Euler(0,180,0);ghostVillage.transform.localScale=Vector3.one*.11f;foreach(var child in ghostVillage.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;foreach(var c in ghostVillage.GetComponentsInChildren<Collider>(true))c.enabled=false;foreach(var r in ghostVillage.GetComponentsInChildren<Renderer>(true)){string n=r.name.ToLowerInvariant();if(n.Contains("waterblock")||n.Contains("seavisual")||n=="water"){r.enabled=false;continue;}var m=new Material(holo){name="M_HologramMesh_"+r.name,hideFlags=HideFlags.DontSave};var sourceMaterial=r.sharedMaterial;if(sourceMaterial){var tex=sourceMaterial.GetTexture("_BaseMap");if(!tex)tex=sourceMaterial.GetTexture("_MainTex");if(tex)m.SetTexture("_BaseMap",tex);}m.SetColor("_Tint",tint);m.SetFloat("_Opacity",.38f);m.SetFloat("_ScanSpeed",1.7f);m.SetFloat("_Edge",2.2f);ghostMaterials.Add(m);var slots=r.sharedMaterials;for(int i=0;i<slots.Length;i++)slots[i]=m;r.sharedMaterials=slots;}}
        void OnDestroy(){if(projector)Destroy(projector.gameObject);if(screen)Destroy(screen);if(basePlatform)Destroy(basePlatform);if(ghostVillage)Destroy(ghostVillage);foreach(var m in ghostMaterials)if(m)Destroy(m);if(target){target.Release();Destroy(target);}if(material)Destroy(material);}
    }
}



