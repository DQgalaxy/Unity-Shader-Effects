using System.Collections.Generic;
using UnityEngine;
using static PortfolioFX.FXShowcaseUtility;
namespace PortfolioFX
{
    [DisallowMultipleComponent, DefaultExecutionOrder(110)]
    public sealed class FXShallowWaterInteraction : MonoBehaviour
    {
        public Transform character;
        public Renderer waterRenderer;
        public Material splashMaterial;
        public float surfaceY=.16f;
        public Vector2 halfSize=new Vector2(22,25);
        public float maxWadeDepth=.65f;
        [Tooltip("Optional root-to-feet offset supplied by the cinematic director.")]
        public float footOffsetOverride=float.NaN;
        public bool sequenceDriven=true;
        const int Capacity=24;
        readonly Vector4[] hits=new Vector4[Capacity], strengths=new Vector4[Capacity];
        readonly List<Ripple> ripples=new List<Ripple>();
        MaterialPropertyBlock block;
        Material rippleMaterial;
        ParticleSystem splash;
        Vector3 lastPosition;
        float clock,lastStep;
        int cursor;
        bool tracking;
        float characterFootOffset;
        bool characterFootOffsetReady;
        sealed class Ripple { public GameObject go; public Renderer renderer; public float age; public float life; }
        void Ensure()
        {
            if(block==null)block=new MaterialPropertyBlock();
            if(rippleMaterial==null)
            {
                var shader=Shader.Find("PortfolioFX/WaterRipple");
                if(!shader)shader=Shader.Find("Universal Render Pipeline/Unlit");
                rippleMaterial=new Material(shader){hideFlags=HideFlags.DontSave};
                var ripple=Resources.Load<Texture2D>("FX/Ripple");
                rippleMaterial.SetTexture("_RippleTex",ripple);rippleMaterial.SetTexture("_BaseMap",ripple);
                rippleMaterial.SetColor("_Color",new Color(.34f,.72f,.66f,1));rippleMaterial.SetColor("_BaseColor",new Color(.34f,.72f,.66f,.38f));
                rippleMaterial.SetFloat("_Opacity",.48f);rippleMaterial.SetFloat("_Surface",1);rippleMaterial.SetFloat("_Blend",0);rippleMaterial.SetFloat("_ZWrite",0);rippleMaterial.renderQueue=3005;
            }
            if(!splash && splashMaterial)
            {
                splash=Burst("Footstep_Spray",transform,splashMaterial,70,.5f,2.2f,.045f,new Color(.75f,.95f,1,.72f),.65f);
                var emission=splash.emission;emission.SetBursts(new ParticleSystem.Burst[0]);
                var main=splash.main;main.maxParticles=220;main.simulationSpace=ParticleSystemSimulationSpace.World;
                var shape=splash.shape;shape.angle=32;shape.radius=.07f;
            }
        }
        void LateUpdate(){if(!sequenceDriven)Sample(Time.time,false);}
        void ResolveCharacterFoot()
        {
            if(characterFootOffsetReady || !character)return;
            if(!float.IsNaN(footOffsetOverride)&&!float.IsInfinity(footOffsetOverride))
            {
                characterFootOffset=Mathf.Clamp(Mathf.Min(footOffsetOverride,-.05f),-3f,-.05f);
                characterFootOffsetReady=true;
                return;
            }
            var renderers=character.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0){characterFootOffset=0;characterFootOffsetReady=true;return;}
            var bounds=renderers[0].bounds;
            for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
            characterFootOffset=bounds.min.y-character.position.y;
            if(float.IsNaN(characterFootOffset)||float.IsInfinity(characterFootOffset))characterFootOffset=-.9f;
            characterFootOffset=Mathf.Clamp(Mathf.Min(characterFootOffset,-.05f),-3f,-.05f);
            characterFootOffsetReady=true;
        }
        public void Sample(float time,bool reset)
        {
            if(!character)return;Ensure();ResolveCharacterFoot();Vector3 p=character.position;
            if(reset){System.Array.Clear(hits,0,Capacity);System.Array.Clear(strengths,0,Capacity);cursor=0;tracking=false;lastStep=time;if(splash)splash.Clear();}
            float dt=time-clock;float speed=tracking&&dt>.0001f?Vector3.Distance(p,lastPosition)/dt:0;
            // The moving object pivot is at the hips in the Ellen asset. Test
            // the rendered feet against the water plane so the interaction is
            // still active while the character root remains above the surface.
            Vector3 footPosition=p+Vector3.up*characterFootOffset;
            Vector3 local=transform.InverseTransformPoint(footPosition);
            float depth=surfaceY-footPosition.y;
            bool inWater=Mathf.Abs(local.x)<halfSize.x&&Mathf.Abs(local.z)<halfSize.y&&depth>-0.05f&&depth<maxWadeDepth;
            if(inWater&&speed>.15f&&time-lastStep>Mathf.Lerp(.38f,.18f,Mathf.Clamp01(speed/4)))
            {
                Vector3 foot=footPosition+character.right*((cursor%2==0?-1:1)*.14f);foot.y=surfaceY+.015f;
                EmitStep(foot,time,Mathf.Clamp(speed/3,.25f,1));lastStep=time;
            }
            clock=time;lastPosition=p;tracking=true;
            if(waterRenderer){waterRenderer.GetPropertyBlock(block);block.SetVectorArray("_FootHits",hits);block.SetVectorArray("_FootStrength",strengths);block.SetFloat("_InteractionTime",time);waterRenderer.SetPropertyBlock(block);}
        }
        public void EmitStep(Vector3 foot,float time,float strength)
        {
            Ensure();hits[cursor]=new Vector4(foot.x,foot.z,time,1);strengths[cursor]=new Vector4(strength,0,0,0);cursor=(cursor+1)%Capacity;
            if(splash){splash.Play();var emit=new ParticleSystem.EmitParams{position=foot,applyShapeToPosition=true};splash.Emit(emit,Mathf.RoundToInt(7+strength*11));}
            var go=new GameObject("Footstep_Ripple");go.transform.SetParent(transform,false);go.transform.position=foot+Vector3.up*.008f;go.transform.rotation=Quaternion.Euler(90,0,0);go.transform.localScale=Vector3.one*.16f;
            go.AddComponent<MeshFilter>().sharedMesh=Quad();var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=rippleMaterial;
            rippleMaterial.SetFloat("_Opacity",.30f+.24f*strength);ripples.Add(new Ripple{go=go,renderer=renderer,life=1.15f,age=0});
        }
        void Update()
        {
            for(int i=ripples.Count-1;i>=0;i--){var ripple=ripples[i];if(!ripple.go){ripples.RemoveAt(i);continue;}ripple.age+=Time.deltaTime;float t=Mathf.Clamp01(ripple.age/ripple.life);ripple.go.transform.localScale=Vector3.one*Mathf.Lerp(.16f,1.45f,t);ripple.renderer.GetPropertyBlock(block);block.SetFloat("_Progress",t);block.SetFloat("_Opacity",(1-t)*.6f);ripple.renderer.SetPropertyBlock(block);if(t>=1){Dispose(ripple.go);ripples.RemoveAt(i);}}
        }
        void OnDisable(){if(splash)Dispose(splash.gameObject);foreach(var ripple in ripples)if(ripple.go)Dispose(ripple.go);ripples.Clear();splash=null;tracking=false;characterFootOffsetReady=false;}
    }
}
