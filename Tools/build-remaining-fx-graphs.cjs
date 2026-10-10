const fs=require('fs'),assert=require('assert/strict');
const u=require('./crystal-assets.cjs');
const {ShowcaseGraph:G,material}=require('./remaining-fx-graph-builder.cjs');
const root='Assets/PortfolioFX/', tex=root+'Textures/';
const pack='Assets/UnityTechnologies/ParticlePack/EffectExamples/';
const noisePath=tex+'NoiseRG.png';
const names=['MeteorSurface','MeteorWarning','TravelerDissolve','PortalSurface','WaterShockwave','IntersectionShell','SoftParticle','DepthWater','ShowcaseSprite'];
if(!process.argv.includes('--refresh-generated'))for(const n of names)
  assert(!fs.existsSync(root+'Shaders/SG_'+n+'.shadergraph'),'Already exists: '+n);
const finish=(g,n,blend=0,opaque=false)=>{g.finish(n,blend,opaque);material(n,n,g);return g;};
const mul=(g,...v)=>v.reduce((a,b)=>g.math('Multiply',a,b));
const add=(g,...v)=>v.reduce((a,b)=>g.math('Add',a,b));
const band=(g,d,r,w,s)=>g.math('OneMinus',g.math('Smoothstep',w,g.math('Add',w,s),g.math('Absolute',g.math('Subtract',d,r))));
function noise(g,uv){return [g.sample(g.texture('_NoiseTex',noisePath),uv),4];}
function radial(g){const p=g.math('Subtract',g.uv(),[.5,.5,0,0]);return {p,r:g.length(g.combine(...g.split(p).slice(0,2)))};}
function normal(g,path,uv){const n=g.sample(g.texture('_NormalMap',path),uv);n.m_TextureType=1;return [n,0];}

// 17: Rock albedo and normal stay legible beneath narrow molten veins.
{
  const g=new G(true),uv=g.uv(),t=g.float('_EffectTime',0),heat=g.float('_Intensity',.5);
  const rock=g.sample(g.texture('_BaseMap',pack+'Magic Effects/Textures/RocksAlbedo.tga'),uv);
  g.group('01 Rock texture and tangent-space normal',()=>{
    g.output('BaseColor',mul(g,[rock,0],g.color('_RockColor',[.32,.3,.28])));
    g.output('NormalTS',normal(g,pack+'Magic Effects/Textures/RockNormal.tga',uv));
    g.output('Smoothness',.22);g.output('Metallic',.08);
  });
  g.group('02 Molten veins - two flowing noise scales',()=>{
    const a=noise(g,mul(g,uv,g.float('_NoiseScale',3)));
    const b=noise(g,add(g,mul(g,uv,7),g.scroll(t,.035,-.023)));
    const vein=band(g,add(g,mul(g,a,.7),mul(g,b,.3)),.48,.025,.055);
    const hot=g.math('Lerp',g.color('_LavaColor',[1,.12,.01]),[1,.65,.12],g.math('Power',vein,4));
    g.output('Emission',mul(g,hot,vein,heat,g.float('_LavaGain',3.5)));
  });
  g.output('Alpha',1);finish(g,'MeteorSurface',0,true);
}
// 18: Low-brightness warning with a fine inner ring and broken angular marks.
{
  const g=new G(),uv=g.uv(),[u0,v]=g.split(uv),t=g.float('_EffectTime',0);
  const across=g.math('Absolute',g.math('Subtract',mul(g,v,2),1));
  const rim=band(g,across,.72,.09,.08);
  const dash=g.math('Smoothstep',.3,.6,g.math('Sine',mul(g,u0,Math.PI*2*24)));
  const middle=mul(g,band(g,across,.35,.11,.07),dash);
  const pulse=add(g,.65,mul(g,g.math('Sine',mul(g,t,7)),.15));
  g.output('BaseColor',mul(g,g.color('_WarningColor',[1,.2,.035]),1.5));
  g.output('Alpha',mul(g,g.math('Saturate',add(g,rim,middle)),pulse));
  finish(g,'MeteorWarning',2);
}
// 19: Opaque Ellen with clipped dissolve, not a transparent duplicate body.
{
  const g=new G(true),uv=g.uv();g.target.m_AlphaClip=true;
  const ellen=root+'Characters/Ellen/Textures/';
  const base=g.sample(g.texture('_BaseMap',ellen+'Ellen_Combined_Albedo.tif'),uv);
  const metal=g.sample(g.texture('_MetallicSmoothMap',ellen+'Ellen_Combined_MetallicSmooth.tif'),uv);
  const ao=g.sample(g.texture('_OcclusionMap',ellen+'Ellen_Combined_Occlusion.tif'),uv);
  g.group('01 Preserve all Ellen surface detail',()=>{
    g.output('BaseColor',[base,0]);g.output('NormalTS',normal(g,ellen+'Ellen_Combined_Normal.tif',uv));
    g.output('Metallic',[metal,4]);g.output('Smoothness',mul(g,[metal,7],g.float('_SmoothnessScale',.45)));
    g.output('Occlusion',[ao,5]);
  });
  g.group('02 Height-noise dissolve with endpoint overscan',()=>{
    const h=g.math('Saturate',g.math('Divide',g.math('Subtract',g.split(g.position(2))[1],g.float('_MinY',0)),g.math('Maximum',g.float('_Height',2.4),.001)));
    const n=noise(g,mul(g,uv,g.float('_NoiseScale',4)));
    const mask=g.math('Lerp',h,n,.32);
    const threshold=g.math('Lerp',-.12,1.12,g.math('Saturate',g.float('_Dissolve',0)));
    const d=g.math('Subtract',mask,threshold);
    const keep=g.math('Smoothstep',-.001,.001,d);
    const edge=mul(g,keep,g.math('OneMinus',g.math('Smoothstep',0,g.float('_EdgeWidth',.065),d)));
    g.output('Alpha',keep);g.output('AlphaClipThreshold',.5);
    g.output('Emission',mul(g,edge,g.color('_EdgeColor',[.18,.85,1]),g.float('_EdgeGain',4)));
  });
  finish(g,'TravelerDissolve',0,true);
}
// 20: Cartesian vortex sampling avoids atan2's visible seam.
{
  const g=new G(),{p,r}=radial(g),t=g.float('_EffectTime',0),intensity=g.float('_Intensity',.7);
  let flow,inside,rim;
  g.group('01 Continuous vortex noise coordinates',()=>{
    const [x,y]=g.split(p),angle=add(g,mul(g,r,15),mul(g,t,-1.8));
    const s=g.math('Sine',angle),c=g.math('Sine',add(g,angle,Math.PI/2));
    const rotated=g.combine(g.math('Subtract',mul(g,x,c),mul(g,y,s)),add(g,mul(g,x,s),mul(g,y,c)));
    flow=noise(g,add(g,mul(g,rotated,3),g.scroll(t,.07,-.11)));
    inside=g.math('OneMinus',g.math('Smoothstep',.40,.49,r));
    rim=band(g,add(g,r,mul(g,g.math('Subtract',flow,.5),.018)),.438,.012,.022);
  });
  g.group('02 Refracted interior and luminous textured rim',()=>{
    const offset=mul(g,g.math('Subtract',flow,.5),g.float('_Distortion',.012),inside);
    const bg=g.sceneColor(g.math('Saturate',add(g,g.screen(),g.combine(offset,mul(g,offset,-.7)))));
    const color=g.color('_PortalColor',[.035,.42,.65]);
    const thread=mul(g,g.math('Power',flow,3),inside,.6);
    g.output('BaseColor',add(g,g.math('Lerp',mul(g,bg,.45),color,.55),mul(g,add(g,rim,thread),[.35,1.1,1.5],add(g,1,intensity))));
    g.output('Alpha',g.math('Saturate',add(g,mul(g,inside,.7),mul(g,rim,.65))));
  });
  finish(g,'PortalSurface');
}

function waterGraph(dynamic){
  const g=new G(true),uv=g.uv(),t=g.float('_EffectTime',0),wp=g.position(2),xyz=g.split(wp);
  let crest=0,waveNormal=g.combine(0,1,0);
  if(dynamic)g.group('01 Gaussian wave - world-space displacement and analytic slope',()=>{
    const center=g.property('_CenterWS','Vector3',{x:0,y:.18,z:0});
    const delta=g.split(g.math('Subtract',wp,center)),xz=g.combine(delta[0],0,delta[2]);
    const distance=g.length(xz),q=g.math('Subtract',distance,g.float('_Radius',0));
    const width=g.math('Maximum',g.float('_BandWidth',.45),.01),amp=g.float('_Amplitude',.12),impact=g.float('_Impact',0),k=g.float('_WaveFrequency',12);
    const envelope=g.math('Power',Math.E,g.math('Negate',g.math('Power',g.math('Divide',q,width),2)));
    const phase=mul(g,q,k),sin=g.math('Sine',phase),cos=g.math('Sine',add(g,phase,Math.PI/2));
    const height=mul(g,sin,envelope,amp,impact);
    const slope=mul(g,envelope,amp,impact,g.math('Subtract',mul(g,k,cos),g.math('Divide',mul(g,2,q,sin),mul(g,width,width))));
    const direction=g.math('Divide',xz,g.math('Maximum',distance,.001));
    const n=g.split(mul(g,direction,g.math('Negate',slope)));
    waveNormal=g.math('Normalize',g.combine(n[0],1,n[2]));
    g.vertex('Position',g.transform(add(g,wp,g.combine(0,height,0)),2,0));
    g.vertex('Normal',g.transform(waveNormal,2,0,2));
    crest=mul(g,band(g,distance,g.props._Radius,.045,.16),impact);
  });
  g.group('02 Eye-depth shoreline and refraction',()=>{
    const d=g.depth(g.float('_ContactWidth',.075),.2);
    const n=normal(g,pack+'Water Effects/Textures/WaterNormal.tif',add(g,mul(g,uv,3),g.scroll(t,.025,.017)));
    const nn=g.split(n);
    const sceneUV=g.math('Saturate',add(g,g.screen(),mul(g,g.combine(nn[0],nn[1]),g.float('_Distortion',.009),g.math('Saturate',mul(g,d.gap,4)))));
    const bg=g.sceneColor(sceneUV),deep=g.math('Saturate',g.math('Divide',d.gap,1.2));
    const tint=g.math('Lerp',g.color('_ShallowColor',[.13,.52,.49]),g.color('_WaterColor',[.018,.16,.22]),deep);
    const shoreNoise=noise(g,add(g,mul(g,uv,10),g.scroll(t,.06,.04)));
    const foam=g.math('Saturate',add(g,mul(g,d.contact,add(g,.45,mul(g,shoreNoise,.55))),crest));
    g.output('BaseColor',g.math('Lerp',g.math('Lerp',bg,tint,.55),[.72,.88,.84],foam));
    g.output('Emission',mul(g,foam,[.07,.12,.12]));g.output('Smoothness',g.math('Lerp',.9,.55,foam));
    g.output('Alpha',g.math('Saturate',add(g,.48,mul(g,deep,.2),mul(g,foam,.25))));
    // WaterGrid has no tangents. Supply a world normal instead of relying on a tangent basis.
    const sub=g.docs.find(x=>x.m_Type.endsWith('.UniversalLitSubTarget'));sub.m_NormalDropOffSpace=2;
    const block=g.docs.find(x=>x.m_SerializedDescriptor==='SurfaceDescription.NormalTS');
    block.m_Name=block.m_SerializedDescriptor='SurfaceDescription.NormalWS';
    const slot=g.slot(block,0);slot.m_Space=2;slot.m_DisplayName='Normal (World Space)';
    g.output('NormalWS',g.math('Normalize',add(g,waveNormal,mul(g,g.combine(nn[0],0,nn[1]),.22))));
    const centered=g.math('Subtract',uv,[.5,.5,0,0]);
    const xy=g.split(centered),radius=g.length(g.combine(xy[0],xy[1]));
    const edge=g.math('OneMinus',g.math('Smoothstep',.47,.5,radius));
    const alpha=g.root.m_Edges.find(e=>e.m_InputSlot.m_Node.m_Id===g.docs.find(x=>x.m_SerializedDescriptor==='SurfaceDescription.Alpha').m_ObjectId);
    const a=[g.map.get(alpha.m_OutputSlot.m_Node.m_Id),alpha.m_OutputSlot.m_SlotId];
    g.output('Alpha',mul(g,a,edge));
  });
  return finish(g,dynamic?'WaterShockwave':'DepthWater');
}
waterGraph(true);waterGraph(false);
// 22: Contact and Fresnel are separate masks; do not erase contact with soft fade.
{
  const g=new G(),d=g.depth(g.float('_ContactWidth',.12),.2);
  const f=g.node('Fresnel');g.value(f,2,3.5);
  g.output('BaseColor',add(g,mul(g,[f,3],g.color('_ShellColor',[.04,.55,.28])),mul(g,d.contact,g.color('_ContactColor',[.5,1,.75]),2)));
  g.output('Alpha',g.math('Saturate',add(g,.02,mul(g,[f,3],.3),mul(g,d.contact,.8))));
  finish(g,'IntersectionShell');
}
// 23: One graph supports a same-frame hard/soft comparison without material mutation.
{
  const g=new G(),uv=g.uv(),v=g.node('VertexColor'),vc=g.split([v,0]);
  const s=g.sample(g.texture('_MainTex',tex+'Smoke.png'),uv);
  const tint=g.color('_Tint',[.58,.7,.68,.42]),tc=g.split(tint);
  const depth=g.depth(.1,g.float('_Softness',.3));
  const soft=g.math('Lerp',1,depth.soft,g.float('_UseSoftFade',1));
  g.output('BaseColor',mul(g,[v,0],tint));
  g.output('Alpha',mul(g,[s,7],vc[3],tc[3],soft));
  const graph=finish(g,'SoftParticle');material('DepthSoftParticle','SoftParticle',graph);
  material('DepthHardParticle','SoftParticle',graph,{_UseSoftFade:0});
  material('MeteorSmoke','SoftParticle',graph,{_Tint:[.32,.29,.27,.48],_Softness:.4});
}
// Shared textured additive layer for embers, portal motes, water droplets and flame ribbons.
{
  const g=new G(),uv=g.uv(),vc=g.node('VertexColor'),v=g.split([vc,0]);
  const s=g.sample(g.texture('_MaskTex',tex+'SoftDisc.png'),uv);
  const tint=g.color('_Tint',[1,1,1,1]),tc=g.split(tint);
  const mask=g.math('Lerp',[s,7],[s,4],g.float('_MaskChannel',0));
  const fade=g.math('Lerp',1,g.math('OneMinus',g.split(uv)[1]),g.float('_TailFade',0));
  g.output('BaseColor',mul(g,[vc,0],tint,g.float('_Gain',2)));
  g.output('Alpha',g.math('Saturate',mul(g,mask,v[3],tc[3],fade,g.float('_Opacity',1))));
  g.finish('ShowcaseSprite',2);
  material('MeteorFlame','ShowcaseSprite',g,{_MaskTex:tex+'FlameMask.png',_Tint:[1,.27,.025,1],_Gain:3.2,_TailFade:1});
  material('MeteorEmbers','ShowcaseSprite',g,{_Tint:[1,.55,.09,1],_Gain:4});
  material('PortalMotes','ShowcaseSprite',g,{_Tint:[.16,.75,1,1],_Gain:2.5});
  material('WaterDroplets','ShowcaseSprite',g,{_MaskTex:pack+'Water Effects/Textures/Splash04Albedo.tif',_Tint:[.45,.8,1,1],_Gain:1.2});
  material('WaterCrown','ShowcaseSprite',g,{_MaskTex:pack+'Water Effects/Textures/Splash01.tif',_Tint:[.42,.8,.9,1],_Gain:1.3});
}
console.log('Created eight chapter graphs plus one shared sprite graph and their materials.');
