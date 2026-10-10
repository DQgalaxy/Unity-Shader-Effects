const fs = require('fs'), path = require('path'), assert = require('assert');
const u = require('./crystal-assets.cjs');
const {ElectricGraph: Graph} = require('./electric-graph-builder.cjs');
process.chdir(path.resolve(__dirname, '..'));
const fx = 'Assets/PortfolioFX';
const main = `${fx}/Shaders/SG_Hologram.shadergraph`;
const scenePath = `${fx}/Scenes/HologramMaterialize.unity`;
const material = `${fx}/MyMaterial/M_HologramBody.mat`;
const beamGraph = `${fx}/Shaders/SG_HologramTransport.shadergraph`;
const beamMat = `${fx}/MyMaterial/M_HologramTransport.mat`;
const beamMesh = `${fx}/Models/HologramTransportTube.asset`;
const clipPath = `${fx}/Animations/HologramMaterialize_DetailedSequence.anim`;
const noisePath = `${fx}/ReferenceHologram/T_HologramSignalNoise.png`;
const volume = `${fx}/Materials/HologramMaterialize_Volume.asset`;
if(fs.existsSync(beamGraph)) throw Error('Revision already applied; preserve subsequent user edits.');
const backup = `RepairBackups/HologramSignal_${new Date().toISOString().replace(/[:.]/g,'-')}`;
fs.mkdirSync(backup,{recursive:true});
for(const p of [main,material,scenePath,clipPath,volume]) fs.copyFileSync(p,`${backup}/${path.basename(p)}`);

function clean(g) {
  g.prune();
  const used=new Set(g.docs.filter(o=>o.m_Property).map(o=>o.m_Property.m_Id));
  g.root.m_Properties=g.root.m_Properties.filter(r=>used.has(r.m_Id));
  g.docs=g.docs.filter(o=>!o.m_Type.endsWith('ShaderProperty')||used.has(o.m_ObjectId));
  for(const c of g.docs.filter(o=>o.m_Type.endsWith('CategoryData')))c.m_ChildObjectList=c.m_ChildObjectList.filter(r=>used.has(r.m_Id));
  const groups=new Set(g.root.m_Nodes.map(r=>g.map.get(r.m_Id).m_Group?.m_Id));
  g.root.m_GroupDatas=g.root.m_GroupDatas.filter(r=>groups.has(r.m_Id));
  g.docs=g.docs.filter(o=>!o.m_Type.endsWith('GroupData')||groups.has(o.m_ObjectId));
}
function defaults(g, values) {
  for(const p of g.docs.filter(o=>o.m_Type.endsWith('ShaderProperty'))) {
    const key=p.m_OverrideReferenceName||p.m_DefaultReferenceName;
    if(key in values)p.m_Value=values[key];
  }
}
function saveMaterial(g,p,name,queue) {
  const source=u.records(u.read(material)).docs.find(d=>d.type===21).body;
  const tex=[],floats=[],colors=[];
  for(const ref of g.root.m_Properties) {
    const prop=g.map.get(ref.m_Id),key=prop.m_OverrideReferenceName||prop.m_DefaultReferenceName,v=prop.m_Value;
    if(prop.m_Type.endsWith('Texture2DShaderProperty')){
      const t=JSON.parse(v.m_SerializedTexture).texture;
      tex.push(`    - ${key}:\n        m_Texture: {fileID: ${t.fileID}, guid: ${t.guid}, type: 3}\n        m_Scale: {x: 1, y: 1}\n        m_Offset: {x: 0, y: 0}`);
    }else if(typeof v==='number')floats.push(`    - ${key}: ${v}`);
    else colors.push(`    - ${key}: {r: ${v.r}, g: ${v.g}, b: ${v.b}, a: ${v.a??1}}`);
  }
  floats.push('    - _QueueControl: 1');
  let text=source.replace(/  m_Name: [^\n]+/,`  m_Name: ${name}`)
    .replace(/  m_Shader: \{[^}]+\}/,`  m_Shader: {fileID: -6465566751694194690, guid: ${u.guid(p===material?main:beamGraph)}, type: 3}`)
    .replace(/m_CustomRenderQueue: -?\d+/,`m_CustomRenderQueue: ${queue}`)
    .replace(/  m_SavedProperties:[\s\S]*?  m_BuildTextureStacks:/,`  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs:\n${tex.join('\n')}\n    m_Ints: []\n    m_Floats:\n${floats.join('\n')}\n    m_Colors:\n${colors.join('\n')}\n  m_BuildTextureStacks:`);
  u.write(p,'%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+text);u.meta(p,'NativeFormatImporter',2100000);
}
function transform(g,value,from,to,type=1) {
  const n=g.node('Transform');n.m_Conversion={from,to};n.m_ConversionType=type;n.m_Normalize=false;g.feed(value,n,0);return [n,1];
}
function lod(g,uv) {
  const n=g.node('SampleTexture2DLOD');n.m_TextureType=0;
  g.feed(g.props._SignalNoise,n,1);g.feed(uv,n,2);g.value(n,4,0);return [n,5];
}
const g=new Graph(main);
g.root.m_Path='PortfolioFX/Hologram';
Object.assign(g.target,{m_SurfaceType:0,m_ZWriteControl:1,m_RenderFace:2,m_AlphaClip:true,m_CastShadows:false,m_ReceiveShadows:true,m_AllowMaterialOverride:false});
g.output('AlphaClipThreshold',.5);
const t=g.float('_EffectTime',0),progress=g.math('Saturate',g.float('_Progress',1));
const wp=g.node('Position');wp.m_Space=2;
const ws=g.split([wp,0]);
let edge,core,visible;
g.group('01 - One turbulent scan front with white core and cyan falloff',()=>{
  const h=g.math('Saturate',g.math('Divide',g.math('Subtract',ws[1],g.float('_MinY',.08)),g.math('Maximum',g.float('_Height',2.4),.001)));
  const noiseUV=g.math('Add',g.math('Multiply',g.combine(ws[0],ws[2]),g.float('_EdgeNoiseScale',2.2)),g.scroll(t,.13,-.09));
  const a=g.sample(g.props._SignalNoise,noiseUV);
  const b=g.sample(g.props._SignalNoise,g.math('Add',g.math('Multiply',noiseUV,3.1),[.27,.51]));
  const noise=g.math('Add',g.math('Multiply',[a,4],.7),g.math('Multiply',[b,4],.3));
  const amount=g.math('Multiply',g.math('Saturate',g.math('Divide',g.math('Absolute',g.float('_EdgeNoiseAmount',.18)),.4)),.4);
  const width=g.math('Maximum',g.float('_ScanWidth',.045),.005);
  // Derive overscan from both controls, so stronger noise never breaks the endpoints.
  const margin=g.math('Add',g.math('Add',g.math('Multiply',amount,.5),g.math('Multiply',width,2)),.03);
  const front=g.math('Lerp',g.math('Multiply',margin,-1),g.math('Add',1,margin),progress);
  const d=g.math('Subtract',g.math('Subtract',front,h),g.math('Multiply',g.math('Subtract',noise,.5),amount));
  visible=g.math('Smoothstep',-.004,.004,d);
  const distance=g.math('Absolute',d);
  edge=g.math('OneMinus',g.math('Smoothstep',0,width,distance));
  core=g.math('OneMinus',g.math('Smoothstep',0,g.math('Multiply',width,.3),distance));
  g.output('Alpha',visible);
});
g.group('02 - Reference-style flowing height distortion, no opacity flicker',()=>{
  const tiling=g.float('_GlitchTiling',2.4),speed=g.float('_GlitchSpeed',.65);
  const v=g.math('Subtract',g.math('Multiply',ws[1],tiling),g.math('Multiply',t,speed));
  const n=lod(g,g.combine(g.math('Multiply',t,.21),v));
  const fine=lod(g,g.combine(g.math('Add',.37,g.math('Multiply',t,.16)),g.math('Multiply',v,3.7)));
  const signed=g.math('Subtract',g.math('Multiply',n,2),1);
  const mask=g.math('Smoothstep',.52,.72,n);
  const strength=g.math('Multiply',g.math('Multiply',signed,mask),g.math('Lerp',.55,1,fine));
  const ready=g.math('Smoothstep',.85,1,progress);
  const offset=g.math('Multiply',g.math('Multiply',strength,ready),g.float('_GlitchDistance',.065));
  // View-horizontal world displacement remains visible as the character turns.
  const right=transform(g,g.combine(1,0,0),1,2);
  const deformed=g.math('Add',[wp,0],g.math('Multiply',right,offset));
  const objectPos=transform(g,deformed,2,0,0);
  const block=g.docs.find(o=>o.m_SerializedDescriptor==='VertexDescription.Position');
  g.root.m_Edges=g.root.m_Edges.filter(e=>e.m_InputSlot.m_Node.m_Id!==block.m_ObjectId);g.feed(objectPos,block,0);
});
g.group('03 - UV0 detail maps, opaque depth, steady holographic shading',()=>{
  const base=g.sample(g.props._BaseMap),normal=g.sample(g.props._NormalMap),metal=g.sample(g.props._MetallicSmoothMap),ao=g.sample(g.props._OcclusionMap);
  normal.m_TextureType=1;
  for(const sample of [base,normal,metal,ao])g.slot(sample,2).m_Channel=0;
  const desat=g.math('DotProduct',[base,0],[.2126,.7152,.0722,0]);
  const detail=g.math('Lerp',[base,0],g.math('Multiply',desat,g.color('_Tint',[.12,.8,1])),g.float('_TintStrength',.72));
  g.output('BaseColor',detail);g.output('NormalTS',[normal,0]);
  g.output('Occlusion',[ao,5]);g.output('Metallic',g.math('Multiply',[metal,4],.15));g.output('Smoothness',g.math('Multiply',[metal,7],.5));
  const fr=g.node('Fresnel');g.value(fr,2,3);
  const rim=g.math('Multiply',[fr,3],g.color('_RimColor',[.03,.7,1.1]));
  const colored=g.math('Multiply',edge,g.color('_ScanColor',[.12,1.1,1.8]));
  const white=g.math('Multiply',core,[.7,1,1.1]);
  const scan=g.math('Multiply',g.math('Add',colored,white),g.float('_ScanGain',6));
  g.output('Emission',g.math('Add',g.math('Add',g.math('Multiply',detail,.3),rim),scan));
});
clean(g);
defaults(g,{_EdgeNoiseAmount:.18,_EdgeNoiseScale:2.2,_ScanWidth:.045,_ScanGain:6,_GlitchDistance:.065,_TintStrength:.72,_RimColor:{r:.03,g:.7,b:1.1,a:1},_Tint:{r:.12,g:.8,b:1,a:1},_ScanColor:{r:.12,g:1.1,b:1.8,a:1}});
g.save(main);saveMaterial(g,material,'M_HologramBody',2450);

const beam=new Graph(`${fx}/Shaders/SG_ImpactRing.shadergraph`);
beam.root.m_Path='PortfolioFX/Hologram';
Object.assign(beam.target,{m_SurfaceType:1,m_AlphaMode:2,m_ZWriteControl:2,m_RenderFace:0,m_AlphaClip:false,m_CastShadows:false,m_ReceiveShadows:false});
const bt=beam.float('_EffectTime',0),bp=beam.math('Saturate',beam.float('_Progress',0));
beam.group('Transport - scrolling noise filaments, soft endpoints, transition envelope',()=>{
  const uv=beam.node('UV');uv.m_OutputChannel=0;
  const channels=beam.split([uv,0]);
  const uvn=beam.math('Add',beam.combine(beam.math('Multiply',channels[0],6),beam.math('Multiply',channels[1],.8)),beam.scroll(bt,.045,-.7));
  const n=beam.sample(beam.texture('_SignalNoise',noisePath),uvn);
  const filaments=beam.math('Power',beam.math('Smoothstep',.35,.85,[n,4]),2);
  const ends=beam.math('Multiply',beam.math('Smoothstep',0,.08,channels[1]),beam.math('OneMinus',beam.math('Smoothstep',.55,1,channels[1])));
  const active=beam.math('Multiply',beam.math('Smoothstep',0,.08,bp),beam.math('OneMinus',beam.math('Smoothstep',.88,1,bp)));
  const mask=beam.math('Multiply',beam.math('Multiply',filaments,ends),active);
  beam.output('BaseColor',beam.math('Multiply',beam.color('_BeamColor',[.1,1.4,2.4]),beam.float('_BeamGain',2)));
  beam.output('Alpha',beam.math('Multiply',mask,beam.float('_BeamOpacity',.28)));
  const pos=beam.node('Position');pos.m_Space=0;
  const block=beam.docs.find(o=>o.m_SerializedDescriptor==='VertexDescription.Position');
  beam.root.m_Edges=beam.root.m_Edges.filter(e=>e.m_InputSlot.m_Node.m_Id!==block.m_ObjectId);beam.feed([pos,0],block,0);
});
clean(beam);beam.save(beamGraph);saveMaterial(beam,beamMat,'M_HologramTransport',3000);

// Reuse the project's ring topology as an open tube; no caps across Ellen's face.
let mesh=u.read(`${fx}/Models/RingStrip.asset`);
const data=Buffer.from(mesh.match(/_typelessdata: ([a-f0-9]+)/i)[1],'hex');
assert.equal(data.length,258*32);
for(let i=0;i<258;i++){
  const at=i*32,angle=data.readFloatLE(at+24)*Math.PI*2,v=data.readFloatLE(at+28);
  const x=Math.cos(angle),z=Math.sin(angle);
  [x*.88,v*3.5,z*.88,x,0,z].forEach((n,j)=>data.writeFloatLE(n,at+j*4));
}
mesh=mesh.replace('m_Name: RingStrip','m_Name: HologramTransportTube').replace(/_typelessdata: [a-f0-9]+/i,`_typelessdata: ${data.toString('hex')}`)
  .replace(/m_Center: \{x: 0, y: 0, z: 0\}/g,'m_Center: {x: 0, y: 1.75, z: 0}')
  .replace(/m_Extent: \{x: 1.06, y: 0, z: 1.06\}/g,'m_Extent: {x: 0.88, y: 1.75, z: 0.88}');
u.write(beamMesh,mesh);u.meta(beamMesh,'NativeFormatImporter',4300000);

const scene=u.records(u.read(scenePath));
const byId=id=>scene.docs.find(d=>d.id===String(id));
const scanGo=byId(70143866),scanTr=byId(70143869),scanRenderer=byId(70143867);
const filter=scene.docs.find(d=>d.type===33&&d.body.includes('m_GameObject: {fileID: 70143866}'));
const ids=new Map([[scanGo.id,'1909202600'],[scanTr.id,'1909202601'],[scanRenderer.id,'1909202602'],[filter.id,'1909202603']]);
assert([...ids.values()].every(id=>!byId(id)));
for(const source of [scanGo,scanTr,scanRenderer,filter]){
  let body=source.body.replace(/(&|fileID: )(\d+)/g,(m,p,n)=>p+(ids.get(n)||n));
  if(source===scanGo)body=body.replace('m_Name: ScanRing','m_Name: TransportBeam');
  if(source===scanTr)body=body.replace(/m_LocalPosition: \{[^}]+\}/,'m_LocalPosition: {x: 0, y: 0.1, z: 0}').replace(/m_LocalRotation: \{[^}]+\}/,'m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}').replace(/m_LocalScale: \{[^}]+\}/,'m_LocalScale: {x: 1, y: 1, z: 1}');
  if(source===scanRenderer)body=body.replace('m_Enabled: 0','m_Enabled: 1').replace('m_CastShadows: 1','m_CastShadows: 0').replace('m_ReceiveShadows: 1','m_ReceiveShadows: 0').replace(/(m_Materials:\n  - )[^\n]+/,`$1{fileID: 2100000, guid: ${u.guid(beamMat)}, type: 2}`);
  if(source===filter)body=body.replace(/m_Mesh: \{[^}]+\}/,`m_Mesh: {fileID: 4300000, guid: ${u.guid(beamMesh)}, type: 2}`);
  scene.docs.push({type:source.type,id:ids.get(source.id),body});
}
byId(666129676).body=byId(666129676).body.replace('  m_Children:\n','  m_Children:\n  - {fileID: 1909202601}\n');
const driver=byId(666129675);
driver.body=driver.body.replace('  particles:\n','  - target: {fileID: 1909202602}\n    previewMode: 1\n    previewColor: {r: 0.1, g: 0.8, b: 1, a: 1}\n    previewPlaceholder: 0\n    preserveAlbedo: 0\n  particles:\n');
driver.body=driver.body.replace(/  time: [^\n]+/,'  time: 0').replace(/  progress: [^\n]+/,'  progress: 0');
// Clone the existing particle system for a deterministic second cue on departure.
const particle=byId(909881699),pgo=scene.docs.find(d=>d.id===particle.body.match(/m_GameObject: \{fileID: (\d+)\}/)[1]);
const parts=scene.docs.filter(d=>d===pgo||d.body.includes(`m_GameObject: {fileID: ${pgo.id}}`));
const pids=new Map(parts.map((d,i)=>[d.id,String(1909202620+i)]));
assert([...pids.values()].every(id=>!byId(id)));
for(const source of parts){let body=source.body.replace(/(&|fileID: )(\d+)/g,(m,p,n)=>p+(pids.get(n)||n));body=body.replace('m_Name: AssemblyPixels','m_Name: DeparturePixels');scene.docs.push({type:source.type,id:pids.get(source.id),body});}
const ptr=parts.find(d=>d.type===4);
byId(666129676).body=byId(666129676).body.replace('  m_Children:\n',`  m_Children:\n  - {fileID: ${pids.get(ptr.id)}}\n`);
driver.body=driver.body.replace('  arcs: []',`  - system: {fileID: ${pids.get(particle.id)}}\n    start: 8.1\n    stop: 9.4\n  arcs: []`);
u.write(scenePath,scene.header+scene.docs.map(d=>d.body).join(''));
// Slightly stronger bloom, local to this scene's volume.
u.write(volume,u.read(volume).replace(/(  intensity:\n    m_OverrideState: 1\n    m_Value:) 0.25/,'$1 0.38'));
u.write(`${backup}/manifest.json`,JSON.stringify({backup,changed:[main,material,scenePath,volume],created:[beamGraph,beamMat,beamMesh],reference:'F:/UnityProjects/1/Assets/MyShaders/11_6Hologram.shadergraph',beamReference:'F:/UnityProjects/1/Assets/MyShaders/11_2LightBean.shadergraph',notes:'Opaque cutout fixes self-overlap; continuous height noise modifies vertex positions only; beam uses reference noise texture with project ring topology.'},null,2));
console.log({backup,bodyNodes:g.root.m_Nodes.length,beamNodes:beam.root.m_Nodes.length});
