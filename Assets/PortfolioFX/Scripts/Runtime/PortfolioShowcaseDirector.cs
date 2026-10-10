using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using static PortfolioFX.FXShowcaseUtility;
namespace PortfolioFX
{
    /// Portfolio-grade runtime orchestrator: one route, one portal, one readable combat beat.
    [DisallowMultipleComponent, DefaultExecutionOrder(-200)]
    public sealed class PortfolioShowcaseDirector : MonoBehaviour
    {
        public enum Phase { Approach, PortalOpen, Projection, Traversal, Combat, WaterRun, Finale }
        public Phase phase=Phase.Approach;
        public bool autoplay=true;
        public bool useVikingPrefabs=true;
        public Transform player;
        public Camera playerCamera;
        public Transform waterArea;
        public FXShallowWaterInteraction water;
        public float routeSpeed=1.75f;
        [Header("Portfolio controls")]
        public bool allowManualInput=true;
        public bool lightingCool;
        PortfolioVortexPortal portal;
        PortfolioHologramProjection projection;
        PortfolioPlayerController controller;
        PortfolioCombatShowcase combat;
        PortfolioSpectralSentinel sentinel;
        FXCharacterMotion motion;
        Vector3 routeStart,routeEnd;
        float groundY,footOffset;
        float timeline;
        GameObject vikingRoot;
        Transform villageAnchor;
        GameObject sourcePlinth,sourceWorkingSurface;
        void Awake(){foreach(var legacy in FindObjectsByType<FXPortalPresentation>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(legacy)legacy.enabled=false;foreach(var legacy in FindObjectsByType<FXSequenceDriver>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(legacy)legacy.enabled=false;ResolvePlayer();BuildShowcase();}
        void Start(){if(autoplay)PlayRoute();}
        public float Timeline => timeline;
        public bool IsManual => !autoplay;
        public PortfolioCombatShowcase Combat => combat;
        public Transform Player => player;
        void Update(){HandleShowcaseInput();if(autoplay)TickRoute();}
        void ResolvePlayer(){
            // Traveler is the visible character root in the Foldstep scene;
            // Ellen_Root is only the skeleton hierarchy and moving it leaves
            // the skinned EllenVisual mesh behind.
            if(!player){var go=GameObject.Find("Traveler");if(go)player=go.transform;}
            if(!player){var go=GameObject.Find("Ellen_Root");if(go)player=go.transform;}
            if(!player)player=transform;
            if(!playerCamera)playerCamera=Camera.main;
            sourcePlinth=GameObject.Find("Plinth");sourceWorkingSurface=GameObject.Find("WorkingSurface");
            routeStart=player.position;groundY=routeStart.y;footOffset=GetFootOffset();routeEnd=routeStart+new Vector3(0,0,8);
            controller=player.GetComponent<PortfolioPlayerController>()??player.gameObject.AddComponent<PortfolioPlayerController>();controller.targetCamera=playerCamera;motion=player.GetComponent<FXCharacterMotion>();
        }
        void BuildShowcase(){
            // TA_Showcase_Demo is based on FoldstepPortal, which still contains
            // the legacy FXPortalPresentation. Disable that looping presentation
            // so it cannot overwrite this demo's camera or portal visuals.
            var legacyPortals=FindObjectsByType<FXPortalPresentation>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(var legacy in legacyPortals)if(legacy)legacy.enabled=false;
            var root=new GameObject("TA_Showcase_Runtime");root.transform.SetParent(transform,false);
            var anchor=new GameObject("Viking_Projection_Anchor");anchor.transform.SetParent(root.transform,false);anchor.transform.position=routeEnd+new Vector3(0,0,2.5f);villageAnchor=anchor.transform;
            if(useVikingPrefabs)BuildVillage(villageAnchor);
            portal=root.AddComponent<PortfolioVortexPortal>();portal.player=player;portal.camera=playerCamera;portal.target=villageAnchor;portal.triggerDistance=1.8f;portal.position=routeStart+new Vector3(0,1.85f,2.3f);portal.Build();
            projection=root.AddComponent<PortfolioHologramProjection>();projection.player=player;projection.camera=playerCamera;projection.source=villageAnchor;projection.position=routeEnd+new Vector3(0,1.2f,3.5f);projection.Build();projection.SetVisible(false);
            waterArea=anchor.transform;water=root.AddComponent<FXShallowWaterInteraction>();water.character=player;water.footOffsetOverride=footOffset;water.waterRenderer=ConfigureWater(villageAnchor);water.splashMaterial=CreateParticleMaterial(Resources.Load<Texture2D>("FX/Ripple"));water.surfaceY=groundY-.02f;
            // The director is the single clock for the authored camera route.
            // Sampling here avoids a second Time.time-driven pass fighting the
            // timeline and makes each footfall deterministic on every loop.
            water.sequenceDriven=true;
            sentinel=root.AddComponent<PortfolioSpectralSentinel>();sentinel.player=player;sentinel.position=routeEnd+new Vector3(0,0,5.3f);sentinel.Build();
            combat=root.AddComponent<PortfolioCombatShowcase>();combat.player=player;combat.camera=playerCamera;combat.enemy=sentinel;combat.Build();
        }
        void BuildVillage(Transform parent){
            // The original Boat Attack Water component owns a planar-reflection
            // command buffer and is not needed here. The showcase uses the
            // SeaVisual mesh plus PortfolioFX/ShallowWater, which gives us
            // deterministic waves and foot interaction without its RT lifetime
            // bug during preview-camera renders.
            GameObject sea=Resources.Load<GameObject>("FX/VikingVillage/SeaVisual");if(sea){var go=Instantiate(sea,parent);go.name="Viking_SeaVisual";go.transform.localPosition=new Vector3(0,-.02f,2);go.transform.localScale=new Vector3(2,1,2);}
            GameObject house=Resources.Load<GameObject>("FX/VikingVillage/pf_build_bighouse_01");if(house){var go=Instantiate(house,parent);go.name="Viking_Longhouse";go.transform.localPosition=new Vector3(0,0,9);go.transform.localRotation=Quaternion.Euler(0,180,0);go.transform.localScale=Vector3.one*1.2f;}
            GameObject boat=Resources.Load<GameObject>("FX/VikingVillage/pf_build_boat_01");if(boat){var go=Instantiate(boat,parent);go.name="Viking_Drakkar";go.transform.localPosition=new Vector3(-5,.05f,4);go.transform.localRotation=Quaternion.Euler(0,35,0);go.transform.localScale=Vector3.one*1.3f;}
            GameObject tower=Resources.Load<GameObject>("FX/VikingVillage/pf_build_tower_01");if(tower){var go=Instantiate(tower,parent);go.name="Viking_Watchtower";go.transform.localPosition=new Vector3(7,0,11);go.transform.localRotation=Quaternion.Euler(0,-25,0);go.transform.localScale=Vector3.one*1.15f;}
            GameObject gate=Resources.Load<GameObject>("FX/VikingVillage/pf_build_gate_01");if(gate){var go=Instantiate(gate,parent);go.name="Viking_Gate";go.transform.localPosition=new Vector3(-7,0,10);go.transform.localRotation=Quaternion.Euler(0,35,0);}
            GameObject barrel=Resources.Load<GameObject>("FX/VikingVillage/pf_barrel_01");if(barrel){for(int i=0;i<3;i++){var go=Instantiate(barrel,parent);go.name="Viking_Barrel_"+i;go.transform.localPosition=new Vector3(-2+i*.6f,0,6+i*.2f);go.transform.localRotation=Quaternion.Euler(0,i*37,0);}}
            PlaceVillagePrefab(parent,"pf_build_small_house_01",new Vector3(-9,0,15),new Vector3(1.1f,1.1f,1.1f),25);
            PlaceVillagePrefab(parent,"pf_build_blacksmith_01",new Vector3(8,0,5),Vector3.one,205);
            PlaceVillagePrefab(parent,"pf_build_storage_01",new Vector3(4,0,16),Vector3.one,160);
            PlaceVillagePrefab(parent,"pf_build_crane_01",new Vector3(-8,0,5),Vector3.one,20);
            PlaceVillagePrefab(parent,"pf_build_boat_02",new Vector3(4,.05f,3),Vector3.one*1.15f,110);
            PlaceVillagePrefab(parent,"pf_build_wall_panel_01",new Vector3(-3,0,17),Vector3.one*1.4f,180);
        }
        void PlaceVillagePrefab(Transform parent,string resource,Vector3 localPosition,Vector3 scale,float yaw){var prefab=Resources.Load<GameObject>("FX/VikingVillage/"+resource);if(!prefab)return;var go=Instantiate(prefab,parent);go.name="Viking_"+resource;go.transform.localPosition=localPosition;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=scale;}
        Renderer FindWaterRenderer(Transform root){foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(r.name.ToLowerInvariant().Contains("water"))return r;return root.GetComponentInChildren<Renderer>();}
        Renderer ConfigureWater(Transform root){
            var shader=Shader.Find("PortfolioFX/ShallowWater");
            var noise=Resources.Load<Texture2D>("FX/NoiseRG");var ripple=Resources.Load<Texture2D>("FX/Ripple");
            Renderer first=null;
            foreach(var r in root.GetComponentsInChildren<Renderer>(true)){
                string n=r.name.ToLowerInvariant();if(!n.Contains("waterblock")&&!n.Contains("sea"))continue;
                if(first==null)first=r;
                if(!shader)continue;
                var m=new Material(shader){name="M_Showcase_ShallowWater"};
                m.SetTexture("_BaseMap",noise);m.SetTexture("_NormalTex",noise);m.SetTexture("_WaveTex",noise);m.SetTexture("_FoamTex",ripple);
                m.SetColor("_ShallowColor",new Color(.11f,.27f,.25f,1));m.SetColor("_DeepColor",new Color(.018f,.065f,.072f,1));m.SetColor("_FoamColor",new Color(.48f,.78f,.7f,1));m.SetColor("_SpecColor",new Color(.58f,.88f,.79f,1));m.SetFloat("_Alpha",.86f);m.SetFloat("_WaveAmplitude",.08f);m.SetFloat("_FootAmplitude",.16f);m.SetFloat("_FoamStrength",1.15f);m.renderQueue=3000;
                var slots=r.sharedMaterials;if(slots==null||slots.Length==0)r.sharedMaterial=m;else{for(int i=0;i<slots.Length;i++)slots[i]=m;r.sharedMaterials=slots;}
            }
            return first??FindWaterRenderer(root);
        }
        Material CreateParticleMaterial(Texture2D tex){var sh=Shader.Find("Universal Render Pipeline/Particles/Unlit");var m=new Material(sh);m.SetColor("_BaseColor",new Color(.25f,.8f,1,.75f));if(tex)m.SetTexture("_BaseMap",tex);m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_ZWrite",0);m.renderQueue=3000;return m;}
        public void PlayRoute(){autoplay=true;timeline=0;phase=Phase.Approach;controller.enabled=false;controller.enabledInput=false;player.position=routeStart;SetSourceStage(true);portal.ResetPortal();projection.SetVisible(false);combat.ResetShowcase();if(water){water.sequenceDriven=true;water.Sample(0,true);}}
        void SetManual(){autoplay=false;phase=Phase.Combat;timeline=26f;portal.SetVisible(false);projection.SetVisible(false);SetSourceStage(false);player.position=routeEnd;FaceRoute();controller.enabled=true;controller.enabledInput=true;if(water)water.sequenceDriven=false;}
        public void EnterManual(){SetManual();}
        public void ToggleAutoplay(){if(autoplay)SetManual();else PlayRoute();}
        void HandleShowcaseInput(){
            if(!allowManualInput)return;
            if(Input.GetKeyDown(KeyCode.F1)){ToggleAutoplay();return;}
            if(Input.GetKeyDown(KeyCode.F2)){PlayRoute();return;}
            if(Input.GetKeyDown(KeyCode.F3)||Input.GetKeyDown(KeyCode.L)){ToggleLighting();}
            if(autoplay)return;
            if(Input.GetKeyDown(KeyCode.Alpha1)||Input.GetKeyDown(KeyCode.Q))combat?.Trigger(PortfolioCombatShowcase.Combo.ArcaneVolley);
            if(Input.GetKeyDown(KeyCode.Alpha2)||Input.GetKeyDown(KeyCode.E))combat?.Trigger(PortfolioCombatShowcase.Combo.GroundRift);
            if(Input.GetKeyDown(KeyCode.Alpha3)||Input.GetKeyDown(KeyCode.R))combat?.Trigger(PortfolioCombatShowcase.Combo.MeteorShield);
            if(Input.GetKeyDown(KeyCode.Alpha4)||Input.GetKeyDown(KeyCode.T)||Input.GetMouseButtonDown(2))combat?.Trigger(PortfolioCombatShowcase.Combo.SpectralBurst);
        }
        void TickRoute(){
            timeline+=Time.deltaTime;
            // The authored route visibly crosses the portal plane. Ellen never
            // pauses beside it and never teleports through a hidden trigger.
            if(timeline<6f){phase=Phase.Approach;portal.Open(Mathf.Lerp(.12f,.3f,timeline/6f));player.position=Vector3.Lerp(routeStart,PortalGround(-.65f),timeline/6f);FacePortal();if(motion)motion.Sample(1.1f+Mathf.Repeat(timeline,1.05f));}
            else if(timeline<12f){phase=Phase.PortalOpen;float u=Mathf.InverseLerp(6f,12f,timeline);portal.Open(Mathf.Lerp(.08f,.9f,u));player.position=Vector3.Lerp(PortalGround(-.65f),PortalGround(.72f),u);FaceForward();if(motion)motion.Sample(1.1f+Mathf.Repeat(timeline,1.05f));}
            else if(timeline<20f){phase=Phase.Projection;portal.SetVisible(false);projection.SetVisible(true);SetSourceStage(false);player.position=PortalGround(1.25f);FaceRoute();if(motion)motion.Sample(4.2f);}
            else if(timeline<26f){phase=Phase.Traversal;projection.SetVisible(false);player.position=Vector3.Lerp(PortalGround(1.25f),routeEnd,Mathf.InverseLerp(20f,26f,timeline));FaceRoute();if(motion)motion.Sample(1.1f+Mathf.Repeat(timeline,1.05f));}
            else if(timeline<34f){phase=Phase.Combat;player.position=routeEnd;if(motion)motion.Sample(4.2f);combat.PlayCinematic(timeline-26f);}
            else if(timeline<44f){phase=Phase.WaterRun;player.position=Vector3.Lerp(routeEnd,routeEnd+new Vector3(0,0,7),Mathf.InverseLerp(34f,44f,timeline));player.position=new Vector3(player.position.x,water.surfaceY+.008f-footOffset,player.position.z);FaceRoute();if(motion)motion.Sample(1.1f+Mathf.Repeat(timeline,1.05f));water.Sample(timeline,false);}
            else {PlayRoute();return;}
            if(autoplay)UpdateCinematicCamera();
        }
        void FacePortal(){Vector3 d=portal.position-player.position;d.y=0;if(d.sqrMagnitude>.001f)player.rotation=Quaternion.LookRotation(d);}
        Vector3 PortalGround(float offset){Vector3 p=portal.position+portal.transform.forward*offset;p.y=groundY;return p;}
        void FaceForward(){Vector3 d=portal.transform.forward;d.y=0;if(d.sqrMagnitude>.001f)player.rotation=Quaternion.LookRotation(d);}
        void SetSourceStage(bool state){if(sourcePlinth)sourcePlinth.SetActive(state);if(sourceWorkingSurface)sourceWorkingSurface.SetActive(state);}
        float GetFootOffset(){var renderers=player.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)return -.9f;var bounds=renderers[0].bounds;for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);float offset=bounds.min.y-player.position.y;if(float.IsNaN(offset)||float.IsInfinity(offset))return -.9f;return Mathf.Clamp(Mathf.Min(offset,-.05f),-3f,-.05f);}
        void FaceRoute(){Vector3 d=routeEnd+Vector3.forward-player.position;d.y=0;if(d.sqrMagnitude>.001f)player.rotation=Quaternion.LookRotation(d);}
        void UpdateCinematicCamera(){if(!playerCamera)return;Vector3 focus=player.position+Vector3.up*1.1f;Vector3 offset;
            if(phase==Phase.Approach||phase==Phase.PortalOpen){focus=Vector3.Lerp(player.position+Vector3.up*.9f,portal.position+Vector3.up*1.05f,.56f);offset=new Vector3(4.3f,2.4f,-6.6f);}
            else if(phase==Phase.Projection){focus=Vector3.Lerp(player.position+Vector3.up*1.1f,projection.position+Vector3.up*1.2f,.5f);offset=new Vector3(5.2f,2.9f,-9.8f);}
            else offset=-player.forward*6f+Vector3.up*2.4f+player.right*3.2f;
            playerCamera.transform.position=Vector3.Lerp(playerCamera.transform.position,focus+offset,Time.deltaTime*3.5f);playerCamera.transform.LookAt(focus);
        }
        public void ToggleLighting(){lightingCool=!lightingCool;var lights=FindObjectsByType<Light>(FindObjectsSortMode.None);foreach(var l in lights)if(l.type==LightType.Directional){l.color=lightingCool?new Color(.38f,.55f,1f):new Color(1f,.76f,.58f);l.intensity=lightingCool?.78f:1.15f;}}
        void OnDestroy(){if(vikingRoot)Destroy(vikingRoot);}
    }
}

