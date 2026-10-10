using UnityEngine;
using UnityEngine.Rendering.Universal;
using static PortfolioFX.FXShowcaseUtility;
namespace PortfolioFX
{
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class FXPortalPresentation : MonoBehaviour
    {
        public FXSequenceDriver sequence;
        public Camera viewCamera;
        public Renderer traveler;
        public Renderer[] surfaces, frames;
        public Material moteMaterial, portalMaterial;
        public Transform travelerRoot, destination;
        public GameObject sourceEnvironment;
        public FXShallowWaterInteraction water;
        public bool cinematic=true;
        Camera windowCamera;
        RenderTexture window;
        Material instance;
        Vector3 originalPosition;
        Quaternion originalRotation;
        float previous=-1;
        bool initialized,crossed;
        public bool Ready => sequence && viewCamera && traveler && surfaces!=null && surfaces.Length>0 && surfaces[0];
        void LateUpdate(){Refresh();}
        public void Refresh()
        {
            if(!Ready)return;
            if(!initialized){if(travelerRoot==null)travelerRoot=traveler.transform;if(destination==null)destination=transform;if(portalMaterial==null)portalMaterial=surfaces[0].sharedMaterial;Build();}
            float t=sequence.CurrentTime;
            if(cinematic)
            {
                crossed=t>=5;
                travelerRoot.position=crossed ? destination.position+Vector3.forward*Mathf.Clamp(t-6,0,8)*1.65f : new Vector3(0,0,Mathf.Lerp(-4.5f,0,Ease(1,5,t)));
                travelerRoot.rotation=Quaternion.identity;
            }
            float dissolve=cinematic ? (t<5 ? Ease(4.35f,4.95f,t) : 1-Ease(5.05f,5.65f,t)) : 0;
            if(cinematic && t>19)dissolve=Ease(19,21,t);
            Float(traveler,"_Dissolve",dissolve);traveler.enabled=dissolve<.999f;
            float open=Ease(0,1,t)*(1-Ease(5,6.2f,t));
            var gate=surfaces[0];gate.transform.SetPositionAndRotation(new Vector3(0,1.55f,0),Quaternion.identity);
            gate.transform.localScale=new Vector3(3,3.3f,1)*Mathf.Max(.001f,open);
            gate.enabled=open>.001f && !crossed;Float(gate,"_EffectTime",t);Float(gate,"_Opacity",open);
            for(int i=1;i<surfaces.Length;i++)if(surfaces[i])surfaces[i].gameObject.SetActive(false);
            if(frames!=null)foreach(var frame in frames)if(frame)frame.gameObject.SetActive(false);
            if(sourceEnvironment)sourceEnvironment.SetActive(!crossed);
            if(cinematic)
            {
                if(crossed){Vector3 focus=travelerRoot.position+Vector3.up*1.1f;viewCamera.transform.position=focus+new Vector3(5.8f,3,-8);viewCamera.transform.LookAt(focus+Vector3.forward*2);}
                else {viewCamera.transform.position=new Vector3(6,3.8f,-9);viewCamera.transform.LookAt(new Vector3(0,1.3f,-.5f));}
            }
            if(!crossed && open>.001f)
            {
                windowCamera.transform.position=destination.position+viewCamera.transform.position;
                windowCamera.transform.rotation=viewCamera.transform.rotation;windowCamera.fieldOfView=viewCamera.fieldOfView;windowCamera.Render();
            }
            if(water && cinematic)water.Sample(t,previous<0 || t<previous || Mathf.Abs(t-previous)>.2f);
            previous=t;
        }
        void Build()
        {
            originalPosition=viewCamera.transform.position;originalRotation=viewCamera.transform.rotation;
            window=new RenderTexture(1024,1024,24,RenderTextureFormat.ARGBHalf){name="Foldstep_DestinationView",hideFlags=HideFlags.DontSave};window.Create();
            instance=new Material(portalMaterial){hideFlags=HideFlags.DontSave};instance.SetTexture("_WorldTex",window);surfaces[0].sharedMaterial=instance;
            windowCamera=Child("Destination_WindowCamera",transform).AddComponent<Camera>();windowCamera.enabled=false;windowCamera.targetTexture=window;
            windowCamera.nearClipPlane=.1f;windowCamera.farClipPlane=180;windowCamera.clearFlags=CameraClearFlags.Skybox;
            var data=windowCamera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.requiresColorOption=CameraOverrideOption.On;data.requiresDepthOption=CameraOverrideOption.On;
            initialized=true;
        }
        public void Traverse()
        {
            if(!Ready || crossed)return;cinematic=false;crossed=true;
            var controller=travelerRoot.GetComponent<CharacterController>();bool active=controller && controller.enabled;if(active)controller.enabled=false;
            travelerRoot.SetPositionAndRotation(destination.position,destination.rotation);if(active)controller.enabled=true;
            var body=travelerRoot.GetComponent<Rigidbody>();if(body){body.position=destination.position;body.linearVelocity=Vector3.zero;}
            if(water)water.sequenceDriven=false;
        }
        [ContextMenu("Rebuild Presentation")]public void Rebuild()
        {
            if(initialized && viewCamera)viewCamera.transform.SetPositionAndRotation(originalPosition,originalRotation);
            if(surfaces!=null && surfaces.Length>0 && surfaces[0] && portalMaterial)surfaces[0].sharedMaterial=portalMaterial;
            if(windowCamera)Dispose(windowCamera.gameObject);if(window){window.Release();Dispose(window);}Dispose(instance);initialized=false;previous=-1;
        }
        void OnDisable(){Rebuild();}void OnDestroy(){Rebuild();}
    }
}
