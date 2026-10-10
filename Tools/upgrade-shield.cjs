const fs=require('fs'),path=require('path'),crypto=require('crypto');
const u=require('./crystal-assets.cjs');
process.chdir(path.resolve(__dirname,'..'));
const {Graph}=require('./shield-graph-builder.cjs');
const fx='Assets/PortfolioFX',dest=`${fx}/ReferenceShield`;
const backup=`RepairBackups/ShieldUpgrade_${new Date().toISOString().replace(/[:.]/g,'-')}`;
const baseline=process.env.SHIELD_BASELINE;
if(fs.existsSync(`${fx}/Shaders/SG_ReactiveShield_HexSequence.shadergraph`)&&!baseline)throw Error('Set SHIELD_BASELINE to the FIRST ShieldUpgrade backup before rerunning.');
fs.mkdirSync(backup,{recursive:true});fs.mkdirSync(dest,{recursive:true});
const originals=[`${fx}/Scenes/ReactiveShield.unity`,`${fx}/Prefabs/ReactiveShield_FX.prefab`,`${fx}/Shaders/SG_ReactiveShield.shadergraph`,`${fx}/Shaders/SG_ImpactRing.shadergraph`,`${fx}/MyMaterial/M_ReactiveShield.mat`,`${fx}/MyMaterial/M_ShieldImpacting.mat`];
for(const p of originals)fs.copyFileSync(p,`${backup}/${path.basename(p)}`);
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const manifest={backup,originalGraphs:Object.fromEntries(originals.filter(p=>p.endsWith('.shadergraph')).map(p=>[p,hash(p)])),resources:[]};
function copy(src,name){const p=`${dest}/${name}`;if(!fs.existsSync(p)){fs.copyFileSync(src,p);if(fs.existsSync(src+'.meta'))u.write(p+'.meta',u.read(src+'.meta').replace(/^guid: \w+/m,`guid: ${u.id()}`));else u.write(p+'.meta',u.read(`${fx}/Textures/SoftDisc.png.meta`).replace(/^guid: \w+/m,`guid: ${u.id()}`));}manifest.resources.push({source:src,destination:p});return p;}
const src='F:/UnityProjects/1/Assets';
const hex=copy(`${src}/Resources/5/CS06_02/Textures/Hexagon.png`,'HexagonTile.png');
const ramp=copy(`${src}/Resources/12/Textures/Ramps/RampMask01.png`,'HitRamp.png');
const noise=copy(`${src}/Resources/12/Textures/Noises/NoiseCaustic03.png`,'HitNoise.png');
const flow=copy(`${src}/Resources/12/Textures/Noises/CaseTexture01.png`,'FlowLight.png');
const shock=copy('F:/UnityProjects/OthersDemo/VisualEffectGraph-Samples-master/Assets/Samples/Meteorite/Materials/Textures/GroundImpactAlpha.png','GroundImpactAlpha.png');
const flare=copy('F:/UnityProjects/OthersDemo/kenney-particle-pack-master/addons/kenney_particle_pack/flare_01.png','ContactFlare.png');
// Retain the exact source-specific mesh and UV texture for comparison, not as a replacement sphere.
copy(`${src}/Resources/13/hexagon.FBX`,'ReferenceHexagon.fbx');
copy(`${src}/Resources/12/Textures/hexagon_line.png`,'ReferenceHexagonUV.png');
copy(`${src}/Resources/12/Textures/grid2.png`,'FlowGrid.png');
for(const p of [hex,ramp,noise,flow])u.write(p+'.meta',u.read(p+'.meta').replace(/sRGBTexture: 1/,'sRGBTexture: 0'));
u.meta(`${fx}/Shaders/ShieldSequenceWaves.hlsl`,'ShaderIncludeImporter');
const shieldPath=`${fx}/Shaders/SG_ReactiveShield_HexSequence.shadergraph`;
const spritePath=`${fx}/Shaders/SG_ShieldImpactLayers.shadergraph`;
{
  const g=new Graph();g.target.m_AlphaMode=0;g.target.m_RenderFace=2;
  const uv=[g.node('UV'),0],time=g.float('_EffectTime',0);
  const h=g.sample(g.texture('_HexagonMap',`${dest}/ReferenceHexagonUV.png`),uv);
  const hexLine=g.math('Saturate',g.math('Multiply',g.math('Subtract',[h,4],.72),3.6));
  const flowUV=g.math('Add',g.math('Multiply',uv,[2,3,0,0]),g.scroll(time,.07,-.19));
  const flowMap=g.sample(g.texture('_FlowLightMap',flow),flowUV);
  const flowing=g.math('Multiply',hexLine,g.math('Power',[flowMap,4],2));
  const f=g.node('Fresnel');g.value(f,2,3.5);const fresnel=[f,3];
  const n=g.node('SampleTexture2DLOD');g.feed(g.texture('_HitNoiseMap',noise),n,1);g.feed(g.math('Multiply',uv,3),n,2);g.value(n,4,0);
  const wave=g.node('CustomFunction');wave.m_FunctionName='ShieldSequenceWaves';wave.m_Name='ShieldSequenceWaves (Custom Function)';wave.m_FunctionSource=u.guid(`${fx}/Shaders/ShieldSequenceWaves.hlsl`);wave.m_Precision=1;
  const pos=g.node('Position');pos.m_Space=2;
  g.feed([pos,0],wave,0);g.feed([n,5],wave,1);g.feed(g.float('_HitSpread',.22),wave,2);g.feed(g.float('_HitDistance',4),wave,3);g.feed(g.float('_HitFadePower',.7),wave,4);g.feed(g.texture('_HitRampMask01',ramp),wave,5);
  const split=g.node('Split');g.feed([wave,6],split,0);const hit=[split,1];
  const detailedHit=g.math('Multiply',hit,g.math('Add',.45,g.math('Multiply',hexLine,.55)));
  const body=g.math('Add',g.math('Add',g.math('Multiply',hexLine,1.15),g.math('Multiply',fresnel,1.2)),g.math('Multiply',flowing,.7));
  const color=g.math('Add',g.math('Multiply',body,g.color('_ShieldColor',[.045,.65,1.1])),g.math('Multiply',detailedHit,g.color('_HitColor',[3.8,1.25,.18])));
  const alpha=g.math('Multiply',g.math('Saturate',g.math('Add',g.math('Add',g.math('Multiply',hexLine,.24),g.math('Multiply',fresnel,.3)),g.math('Multiply',hit,.65))),g.float('_Visibility',1));
  const normal=g.node('NormalVector');normal.m_Space=2;
  const displaced=g.math('Add',[pos,0],g.math('Multiply',[normal,0],g.math('Multiply',hit,g.float('_CellPush',.065))));
  const toObject=g.node('Transform');toObject.m_Conversion={from:2,to:0};toObject.m_ConversionType=0;g.feed(displaced,toObject,0);
  g.feed([toObject,1],g.docs.find(o=>o.m_SerializedDescriptor==='VertexDescription.Position'),0);
  g.output('BaseColor',color);g.output('Alpha',alpha);g.save(shieldPath);
}
{
  const g=new Graph(),uv=[g.node('UV'),0],t=g.float('_EffectTime',0);
  const tex=g.sample(g.texture('_Mask',shock),uv);
  const noiseSample=g.sample(g.texture('_NoiseTex',noise),g.math('Add',g.math('Multiply',uv,2),g.scroll(t,.11,-.08)));
  const mask=g.math('Multiply',[tex,4],[tex,7]);
  const breakup=g.math('Lerp',1,[noiseSample,4],g.float('_Breakup',.25));
  const split=g.node('Split');g.feed(uv,split,0);
  // Soft UV rectangle boundaries prevent hard cards; no radial math is imposed on trail textures.
  const x=g.math('Saturate',g.math('Multiply',g.math('Multiply',[split,1],g.math('OneMinus',[split,1])),24));
  const y=g.math('Saturate',g.math('Multiply',g.math('Multiply',[split,2],g.math('OneMinus',[split,2])),24));
  const alpha=g.math('Multiply',g.math('Multiply',mask,breakup),g.math('Multiply',g.math('Multiply',x,y),g.float('_Fade',1)));
  const vertex=g.node('Property');delete vertex.m_Property;vertex.m_Type='UnityEditor.ShaderGraph.VertexColorNode';vertex.m_Name='Vertex Color';
  const template=g.slot(tex,0),slot=g.slot(vertex,0);Object.assign(slot,{m_Type:'UnityEditor.ShaderGraph.Vector4MaterialSlot',m_Value:{x:1,y:1,z:1,w:1},m_DefaultValue:{x:1,y:1,z:1,w:1},m_DisplayName:'Out',m_ShaderOutputName:'Out'});
  const vcSplit=g.node('Split');g.feed([vertex,0],vcSplit,0);
  const color=g.math('Multiply',g.math('Multiply',g.color('_Tint',[.15,.7,1.5]),g.float('_Gain',2)),[vertex,0]);
  g.output('BaseColor',color);g.output('Alpha',g.math('Multiply',alpha,[vcSplit,4]));g.save(spritePath);
}
function material(name,shader,overrides){
  const docs=u.graph(shader),map=new Map(docs.map(o=>[o.m_ObjectId,o])),tex=[],floats=[],colors=[];
  for(const ref of docs[0].m_Properties){const p=map.get(ref.m_Id),key=p.m_DefaultReferenceName,v=overrides[key]??p.m_Value;
    if(p.m_Type.endsWith('Texture2DShaderProperty')){const r=typeof v==='string'?{fileID:2800000,guid:u.guid(v),type:3}:JSON.parse(v.m_SerializedTexture).texture;tex.push(`    - ${key}:\n        m_Texture: {fileID: ${r.fileID}, guid: ${r.guid}, type: 3}\n        m_Scale: {x: 1, y: 1}\n        m_Offset: {x: 0, y: 0}`);}
    else if(typeof v==='number')floats.push(`    - ${key}: ${v}`);
    else colors.push(`    - ${key}: {r: ${v.r}, g: ${v.g}, b: ${v.b}, a: ${v.a??1}}`);
  }
  const p=`${fx}/MyMaterial/${name}.mat`;
  u.write(p,`%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_Name: ${name}\n  m_Shader: {fileID: -6465566751694194690, guid: ${u.guid(shader)}, type: 3}\n  m_Parent: {fileID: 0}\n  m_ModifiedSerializedProperties: 0\n  m_ValidKeywords: []\n  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 0\n  m_DoubleSidedGI: 0\n  m_CustomRenderQueue: ${shader===shieldPath?3000:3010}\n  stringTagMap: {}\n  disabledShaderPasses: []\n  m_LockedProperties: \n  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs:\n${tex.join('\n')}\n    m_Ints: []\n    m_Floats:\n${floats.join('\n')}\n    m_Colors:\n${colors.join('\n')}\n  m_BuildTextureStacks: []\n  m_AllowLocking: 1\n`);u.meta(p,'NativeFormatImporter',2100000);return p;
}
material('M_ReactiveShield',shieldPath,{});
const shockMat=material('M_ShieldImpacting',spritePath,{_Mask:shock,_Tint:{r:.09,g:.8,b:1.7},_Gain:2.5,_Breakup:.35});
const trailMat=material('M_ShieldCometTrail',spritePath,{_Mask:'Assets/Trail/Textures/Trail_02.png',_Tint:{r:2.4,g:.55,b:.1},_Gain:2.2,_Breakup:.1});
const flashMat=material('M_ShieldContactFlash',spritePath,{_Mask:flare,_Tint:{r:2.4,g:1.7,b:.8},_Gain:4,_Breakup:0});
const sparkMat=material('M_ShieldSparks',spritePath,{_Mask:`${fx}/Textures/SoftDisc.png`,_Tint:{r:1.3,g:1.3,b:1.3},_Gain:3,_Breakup:0});
const scene=u.records(u.read(`${baseline||backup}/ReactiveShield.unity`)),map=new Map(scene.docs.map(d=>[d.id,d]));
const change=(id,f)=>{const d=map.get(String(id));if(!d)throw Error(id);d.body=f(d.body);};
const root=scene.docs.find(d=>d.type===1&&d.body.includes('m_Name: ReactiveShield_FX\n'));
const componentId='2210000001',script=`${fx}/Scripts/Runtime/FXShieldPresentation.cs`;
u.meta(script,'MonoImporter');u.write(script+'.meta',u.read(script+'.meta').replace('  mainObjectFileID: 0','  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}'));
root.body=root.body.replace('  m_Layer: 0',`  - component: {fileID: ${componentId}}\n  m_Layer: 0`);
const legacy=[];
for(const name of ['ImpactRing_0','ImpactRing_1','ImpactRing_2','IncomingBolt_0','IncomingBolt_1','IncomingBolt_2']){
  const go=scene.docs.find(d=>d.type===1&&d.body.includes(`m_Name: ${name}\n`));
  const r=scene.docs.find(d=>d.type===23&&d.body.includes(`m_GameObject: {fileID: ${go.id}}`));legacy.push(r.id);r.body=r.body.replace('m_Enabled: 1','m_Enabled: 0');
  // Keep old object paths for the existing clip; the new component owns their visible replacement.
}
change(242489122,b=>b.replace(/  materials:[\s\S]*?  arcs:/,`  materials:\n  - target: {fileID: 1618574752}\n    previewMode: 4\n    previewColor: {r: 0.05, g: 0.75, b: 1, a: 1}\n    previewPlaceholder: 0\n    preserveAlbedo: 0\n  particles: []\n  arcs:`).replace(/  time: .*/,'  time: 0'));
for(const name of ['DeflectedSparks_0','DeflectedSparks_1','DeflectedSparks_2']){const go=scene.docs.find(d=>d.type===1&&d.body.includes(`m_Name: ${name}\n`));go.body=go.body.replace('m_IsActive: 1','m_IsActive: 0');}
const baseMat=u.guid(`${fx}/Characters/Ellen/Materials/Ellen_Base.mat`);
change(794387470,b=>b.replace(/m_Materials:\n(?:  - .+\n)+/,`m_Materials:\n  - {fileID: 2100000, guid: ${baseMat}, type: 2}\n`));
const cues=[['EllenRandomIdle_1',.05,1.2,.3,1.45],['Ellen_Idle_Land_Fast',1.4,2.28,.45,1.15],['Ellen_Death',3.49,3.94,0,.18],['Ellen_Idle_Land_Fast',3.92,4.65,.45,1.15],['EllenCombo_1',5.33,6.25,0,.6],['EllenRandomIdle_2',6.35,7.8,.2,1.65]];
change(1854099856,b=>b.replace(/  cues:[\s\S]*/,`  cues:\n`+cues.map(([clip,start,end,sourceStart,sourceEnd])=>`  - clip: {fileID: 7400000, guid: ${u.guid(`${fx}/Characters/Ellen/Animations/${clip}.anim`)}, type: 2}\n    start: ${start}\n    end: ${end}\n    sourceStart: ${sourceStart}\n    sourceEnd: ${sourceEnd}\n    blendIn: 0.12\n    blendOut: 0.2\n    loop: 0\n`).join('')));
change(1854099857,b=>b.replace(/m_LocalRotation: \{[^}]+\}/,'m_LocalRotation: {x: 0, y: 1, z: 0, w: 0}'));
const pitch=Math.atan2(3.1-1.3,9.6)/2;
change(1103090218,b=>b.replace(/m_LocalRotation: \{[^}]+\}/,`m_LocalRotation: {x: ${Math.sin(pitch)}, y: 0, z: 0, w: ${Math.cos(pitch)}}`).replace(/m_LocalPosition: \{[^}]+\}/,'m_LocalPosition: {x: 0, y: 3.1, z: -9.6}'));
change(1103090217,b=>b.replace('field of view: 38','field of view: 42'));
scene.docs.push({id:componentId,type:114,body:`--- !u!114 &${componentId}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: ${root.id}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: ${u.guid(script)}, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n  sequence: {fileID: 242489122}\n  shield: {fileID: 1618574752}\n  shockwaveMaterial: {fileID: 2100000, guid: ${u.guid(shockMat)}, type: 2}\n  trailMaterial: {fileID: 2100000, guid: ${u.guid(trailMat)}, type: 2}\n  flashMaterial: {fileID: 2100000, guid: ${u.guid(flashMat)}, type: 2}\n  sparkMaterial: {fileID: 2100000, guid: ${u.guid(sparkMat)}, type: 2}\n  legacyVisuals:\n${legacy.map(id=>`  - {fileID: ${id}}`).join('\n')}\n  presentationCamera: {fileID: 1103090217}\n`});
scene.docs[scene.docs.length-1].body+=`  honeycombPrefab: {fileID: 919132149155446097, guid: ${u.guid(`${dest}/ReferenceHexagon.fbx`)}, type: 3}\n`;
u.write(`${fx}/Scenes/ReactiveShield.unity`,scene.header+scene.docs.map(d=>d.body).join(''));
const selected=new Set();
function include(tid){const t=map.get(tid);selected.add(tid);const go=map.get(t.body.match(/m_GameObject: \{fileID: (\d+)\}/)[1]);selected.add(go.id);for(const m of go.body.matchAll(/component: \{fileID: (\d+)\}/g))selected.add(m[1]);for(const m of t.body.match(/m_Children:([\s\S]*?)  m_Father:/)[1].matchAll(/fileID: (\d+)/g))include(m[1]);}
include('242489123');
u.write(`${fx}/Prefabs/ReactiveShield_FX.prefab`,scene.header+scene.docs.filter(d=>selected.has(d.id)).map(d=>d.body.replace('presentationCamera: {fileID: 1103090217}','presentationCamera: {fileID: 0}')).join(''));
for(const [p,h] of Object.entries(manifest.originalGraphs))if(hash(p)!==h)throw Error(`Original graph modified: ${p}`);
u.write(`${backup}/manifest.json`,JSON.stringify(manifest,null,2));
console.log(JSON.stringify(manifest,null,2));
