const fs=require('fs'),path=require('path'),crypto=require('crypto');
const u=require('./crystal-assets.cjs');
process.chdir(path.resolve(__dirname,'..'));
const {ElectricGraph:G}=require('./electric-graph-builder.cjs');
const fx='Assets/PortfolioFX',graphPath=`${fx}/Shaders/SG_Hologram.shadergraph`,matPath=`${fx}/MyMaterial/M_HologramBody.mat`,scenePath=`${fx}/Scenes/HologramMaterialize.unity`;
const animPath=`${fx}/Animations/HologramMaterialize_DetailedSequence.anim`;
if(fs.existsSync(animPath))throw Error('Detailed sequence already exists. Preserve subsequent edits.');
const backup=`RepairBackups/HologramDetail_${new Date().toISOString().replace(/[:.]/g,'-')}`;
fs.mkdirSync(backup,{recursive:true});
for(const p of [graphPath,matPath,scenePath,`${fx}/Animations/HologramMaterialize_Sequence.anim`])fs.copyFileSync(p,path.join(backup,path.basename(p)));
const protectedPaths=[`${fx}/Shaders/SG_OverloadBody.shadergraph`,`${fx}/Shaders/SG_ArcRibbon.shadergraph`,`${fx}/Scenes/ElectricOverload.unity`,`${fx}/Prefabs/HologramMaterialize_FX.prefab`];
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const hashes=Object.fromEntries(protectedPaths.map(p=>[p,hash(p)]));
const source='F:/UnityProjects/1/Assets/Resources/11/Textures/NoiseClassic01.png';
const dest=`${fx}/ReferenceHologram`;fs.mkdirSync(dest,{recursive:true});
const noisePath=`${dest}/T_HologramSignalNoise.png`;
fs.copyFileSync(source,noisePath);
u.write(noisePath+'.meta',u.read(`${fx}/ReferenceArc/T_ArcFlow.png.meta`).replace(/^guid: \w+/m,`guid: ${u.id()}`).replace('wrapV: 1','wrapV: 0'));
u.write(`${dest}/source.json`,JSON.stringify({source,noisePath,reference:'F:/UnityProjects/1/Assets/MyShaders/11_6Hologram.shadergraph',approach:'Reference height-local vertex disturbance and flicker; no repeated scanline layers copied. Ellen detail maps remain those already imported in project 10.'},null,2));

const original=u.graph(graphPath);
const g=new G(`${fx}/Shaders/SG_OverloadBody.shadergraph`);
g.root.m_ObjectId=u.id();g.root.m_Path='PortfolioFX/Hologram';
Object.assign(g.target,{m_SurfaceType:1,m_AlphaMode:0,m_ZWriteControl:2,m_RenderFace:2,m_AlphaClip:true,m_CastShadows:false,m_ReceiveShadows:true});
function addBlock(name){
  const source=original.find(o=>o.m_SerializedDescriptor==='SurfaceDescription.'+name);
  const b=JSON.parse(JSON.stringify(source));b.m_ObjectId=u.id();b.m_Slots=[];
  for(const ref of source.m_Slots){const s=JSON.parse(JSON.stringify(original.find(o=>o.m_ObjectId===ref.m_Id)));s.m_ObjectId=u.id();g.insert(s);b.m_Slots.push({m_Id:s.m_ObjectId});}
  g.insert(b);g.root.m_Nodes.push({m_Id:b.m_ObjectId});g.root.m_FragmentContext.m_Blocks.push({m_Id:b.m_ObjectId});
}
addBlock('Alpha');addBlock('AlphaClipThreshold');g.output('AlphaClipThreshold',.02);
const t=g.float('_EffectTime',0),progress=g.math('Saturate',g.float('_Progress',1));
const position=g.node('Position');position.m_Space=2;
const objectPosition=g.node('Position');objectPosition.m_Space=0;
const os=g.split([objectPosition,0]),ws=g.split([position,0]);
let visible,edge;
g.group('01 - Single noisy reveal front (no repeated lines)',()=>{
  const height=g.math('Saturate',g.math('Divide',g.math('Subtract',ws[1],g.float('_MinY',.08)),g.math('Maximum',g.float('_Height',2.4),.001)));
  const uv=g.math('Add',g.math('Multiply',g.combine(os[0],os[2]),g.float('_EdgeNoiseScale',3)),g.scroll(t,.055,.09));
  const sample=g.sample(g.texture('_SignalNoise',noisePath),uv);
  const perturb=g.math('Multiply',g.math('Subtract',[sample,4],.5),g.float('_EdgeNoiseAmount',.035));
  const front=g.math('Lerp',-.08,1.08,progress);
  const distance=g.math('Subtract',g.math('Subtract',front,height),perturb);
  visible=g.math('Smoothstep',-.004,.004,distance);
  const width=g.math('Maximum',g.float('_ScanWidth',.018),.005);
  edge=g.math('OneMinus',g.math('Smoothstep',.003,width,g.math('Absolute',distance)));
});
let glitch,offset;
g.group('02 - Short local signal slips, driven by sequence time',()=>{
  function floor(v){const n=g.node('Fraction');n.m_Type='UnityEditor.ShaderGraph.FloorNode';n.m_Name='Floor';g.feed(v,n,0);return [n,1];}
  const frame=floor(g.math('Multiply',t,18));
  const row=floor(g.math('Multiply',os[1],12));
  const noise=g.node('SampleTexture2DLOD');noise.m_TextureType=0;
  g.feed(g.props._SignalNoise,noise,1);g.feed(g.combine(g.math('Multiply',frame,.071),g.math('Multiply',row,.079)),noise,2);g.value(noise,4,0);
  const pulse=g.math('Smoothstep',.9,.985,g.math('Sine',g.math('Add',g.math('Multiply',t,7.4),g.math('Multiply',g.math('Sine',g.math('Multiply',t,2.1)),.5))));
  glitch=g.math('Multiply',g.math('Multiply',g.math('Step',.55,[noise,5]),pulse),g.math('Smoothstep',.9,1,progress));
  offset=g.math('Multiply',g.math('Multiply',g.math('Subtract',g.math('Multiply',[noise,5],2),1),glitch),g.float('_GlitchDistance',.022));
  const shifted=g.math('Add',[objectPosition,0],g.combine(offset,0,0));
  const block=g.docs.find(o=>o.m_SerializedDescriptor==='VertexDescription.Position');
  g.root.m_Edges=g.root.m_Edges.filter(e=>e.m_InputSlot.m_Node.m_Id!==block.m_ObjectId);g.feed(shifted,block,0);
});
g.group('03 - Ellen detail maps and restrained holographic light',()=>{
  const base=g.sample(g.props._BaseMap);
  const tint=g.color('_Tint',[.16,.8,1]);
  const color=g.math('Multiply',[base,0],g.math('Lerp',[1,1,1,1],tint,g.float('_TintStrength',.55)));
  g.output('BaseColor',g.math('Multiply',color,g.math('Subtract',1,g.math('Multiply',glitch,.16))));
  const normal=g.docs.find(o=>o.m_Type.endsWith('SampleTexture2DNode')&&g.root.m_Edges.some(e=>e.m_InputSlot.m_Node.m_Id===o.m_ObjectId&&e.m_InputSlot.m_SlotId===1&&e.m_OutputSlot.m_Node.m_Id===g.props._NormalMap[0].m_ObjectId));
  normal.m_TextureType=1;normal.m_NormalMapSpace=0;
  const metal=g.sample(g.props._MetallicSmoothMap);
  g.output('Metallic',g.math('Multiply',[metal,4],.35));
  g.output('Smoothness',g.math('Multiply',[metal,7],.45));
  const fresnel=g.node('Fresnel');g.value(fresnel,2,3.5);
  const rim=g.math('Multiply',[fresnel,3],g.color('_RimColor',[.06,.5,.8]));
  const scan=g.math('Multiply',edge,g.math('Multiply',g.color('_ScanColor',[.25,1.3,1.8]),g.float('_ScanGain',3)));
  const fill=g.math('Multiply',color,.22);
  const dropout=g.math('Multiply',glitch,g.color('_SignalColor',[.025,.24,.4]));
  const emission=g.math('Add',g.math('Add',fill,rim),g.math('Add',scan,dropout));
  g.output('Emission',g.math('Multiply',emission,g.math('Add',.85,g.math('Multiply',g.math('Saturate',g.float('_Intensity',1)),.15))));
  const opacity=g.math('Saturate',g.math('Subtract',g.float('_BodyOpacity',.92),g.math('Multiply',glitch,.12)));
  g.output('Alpha',g.math('Multiply',visible,opacity));
});
g.prune();
// Remove unused template properties/groups so the Blackboard only contains live controls.
const used=new Set(g.docs.filter(o=>o.m_Property).map(o=>o.m_Property.m_Id));
g.root.m_Properties=g.root.m_Properties.filter(r=>used.has(r.m_Id));
g.docs=g.docs.filter(o=>!o.m_Type.endsWith('ShaderProperty')||used.has(o.m_ObjectId));
for(const c of g.docs.filter(o=>o.m_Type.endsWith('CategoryData')))c.m_ChildObjectList=c.m_ChildObjectList.filter(r=>used.has(r.m_Id));
const groups=new Set(g.root.m_Nodes.map(r=>g.map.get(r.m_Id).m_Group?.m_Id));
g.root.m_GroupDatas=g.root.m_GroupDatas.filter(r=>groups.has(r.m_Id));
g.docs=g.docs.filter(o=>!o.m_Type.endsWith('GroupData')||groups.has(o.m_ObjectId));
const defaults={_RimColor:{r:.06,g:.5,b:.8,a:1},_Intensity:1};
for(const p of g.docs.filter(o=>o.m_Type.endsWith('ShaderProperty')))if(defaults[p.m_DefaultReferenceName]!==undefined)p.m_Value=defaults[p.m_DefaultReferenceName];
g.save(graphPath);
// Preserve the material GUID, replace only the serialized material properties for the new graph.
let mat=u.read(matPath);const tex=[],floats=[],colors=[];
for(const ref of g.root.m_Properties){const p=g.map.get(ref.m_Id),key=p.m_OverrideReferenceName||p.m_DefaultReferenceName,v=p.m_Value;
  if(p.m_Type.endsWith('Texture2DShaderProperty')){const r=JSON.parse(v.m_SerializedTexture).texture;tex.push(`    - ${key}:\n        m_Texture: {fileID: ${r.fileID}, guid: ${r.guid}, type: 3}\n        m_Scale: {x: 1, y: 1}\n        m_Offset: {x: 0, y: 0}`);}
  else if(typeof v==='number')floats.push(`    - ${key}: ${v}`);
  else colors.push(`    - ${key}: {r: ${v.r}, g: ${v.g}, b: ${v.b}, a: ${v.a??1}}`);
}
mat=mat.replace(/  m_SavedProperties:[\s\S]*?  m_BuildTextureStacks:/,`  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs:\n${tex.join('\n')}\n    m_Ints: []\n    m_Floats:\n${floats.join('\n')}\n    m_Colors:\n${colors.join('\n')}\n  m_BuildTextureStacks:`).replace(/m_CustomRenderQueue: -?\d+/,'m_CustomRenderQueue: 3000');
u.write(matPath,mat);

const scene=u.records(u.read(scenePath));
const go=n=>scene.docs.find(d=>d.type===1&&d.body.includes(`  m_Name: ${n}\n`));
const component=(n,type)=>scene.docs.find(d=>d.type===type&&d.body.includes(`m_GameObject: {fileID: ${go(n).id}}`));
const driver=component('HologramMaterialize_FX',114),scan=component('ScanRing',23),body=component('Ellen_Body',137);
driver.body=driver.body.replace(new RegExp(`  - target: \\{fileID: ${scan.id}\\}\\n(?:    [^\\n]*\\n)+`),'');
driver.body=driver.body.replace(new RegExp(`(- target: \\{fileID: ${body.id}\\}[\\s\\S]*?previewPlaceholder:) 1`),'$1 0');
scan.body=scan.body.replace('m_Enabled: 1','m_Enabled: 0');
body.body=body.body.replace('m_CastShadows: 1','m_CastShadows: 0');
const motion=component('HologramBody',114);motion.body=motion.body.replace(/  cues:[\s\S]*$/,'  cues: []\n');

const oldClip=u.read(`${fx}/Animations/HologramMaterialize_Sequence.anim`);
const vec=v=>`{x: ${v[0]}, y: ${v[1]}, z: ${v[2]}}`;
function scalar(attribute,times,values){return `  - serializedVersion: 2\n    curve:\n      serializedVersion: 2\n      m_Curve:\n`+times.map((t,i)=>`      - serializedVersion: 3\n        time: ${t}\n        value: ${values[i]}\n        inSlope: 0\n        outSlope: 0\n        tangentMode: 0\n        weightedMode: 0\n        inWeight: 0.33333334\n        outWeight: 0.33333334\n`).join('')+`      m_PreInfinity: 2\n      m_PostInfinity: 2\n      m_RotationOrder: 4\n    attribute: ${attribute}\n    path: \n    classID: 114\n    script: {fileID: 11500000, guid: ${u.guid(`${fx}/Scripts/Runtime/FXSequenceDriver.cs`)}, type: 3}\n    flags: 0\n`;}
const tr=component('HologramBody',4);
const q=tr.body.match(/m_LocalRotation: \{x: [^,]+, y: ([^,]+), z: [^,]+, w: ([^}]+)\}/);
const yaw=2*Math.atan2(+q[1],+q[2])*180/Math.PI;
const times=[0,2.8,7.8,10],values=[yaw,yaw,yaw+360,yaw+360];
const rotation=`  m_EulerCurves:\n  - curve:\n      serializedVersion: 2\n      m_Curve:\n`+times.map((t,i)=>`      - serializedVersion: 3\n        time: ${t}\n        value: ${vec([0,values[i],0])}\n        inSlope: ${vec([0,0,0])}\n        outSlope: ${vec([0,0,0])}\n        tangentMode: 0\n        weightedMode: 0\n        inWeight: ${vec([.33333334,.33333334,.33333334])}\n        outWeight: ${vec([.33333334,.33333334,.33333334])}\n`).join('')+`      m_PreInfinity: 2\n      m_PostInfinity: 2\n      m_RotationOrder: 4\n    path: HologramBody\n`;
let clip=oldClip.replace('m_Name: HologramMaterialize_Sequence','m_Name: HologramMaterialize_DetailedSequence').replace('  m_EulerCurves: []\n',rotation);
clip=clip.replace(/  m_PositionCurves:[\s\S]*?(?=  m_ScaleCurves:)/,'  m_PositionCurves: []\n');
const curves=scalar('progress',[0,.4,2.5,8.1,9.5,10],[0,0,1,1,0,0])+scalar('intensity',[0,.4,2.5,8.1,9.6,10],[0,.3,1,1,0,0])+scalar('radius',[0,1.2,10],[.1,1.4,1.4])+scalar('impact',[0,10],[0,0])+scalar('dissolve',[0,10],[0,0]);
clip=clip.replace(/  m_FloatCurves:[\s\S]*?(?=  m_PPtrCurves:)/,'  m_FloatCurves:\n'+curves).replace('m_StopTime: 6','m_StopTime: 10');
u.write(animPath,clip);u.meta(animPath,'NativeFormatImporter',7400000);
driver.body=driver.body.replace(/  sequence: \{[^}]+\}/,`  sequence: {fileID: 7400000, guid: ${u.guid(animPath)}, type: 2}`).replace('  duration: 6','  duration: 10').replace(/  time: [^\n]+/,'  time: 0').replace(/  progress: [^\n]+/,'  progress: 0').replace(/  intensity: [^\n]+/,'  intensity: 0');
u.write(scenePath,scene.header+scene.docs.map(d=>d.body).join(''));
for(const [p,h]of Object.entries(hashes))if(hash(p)!==h)throw Error('Protected asset changed '+p);
u.write(`${backup}/manifest.json`,JSON.stringify({backup,protectedAssets:hashes,graphPath,matPath,scenePath,animPath,noisePath,source,referenceGraph:'F:/UnityProjects/1/Assets/MyShaders/11_6Hologram.shadergraph'},null,2));
console.log(JSON.stringify({backup,animPath,graphNodes:g.root.m_Nodes.length,graphEdges:g.root.m_Edges.length,yaw},null,2));
