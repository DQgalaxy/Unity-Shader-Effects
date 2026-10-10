const fs=require('fs'),path=require('path');
const u=require('./crystal-assets.cjs');
process.chdir(path.resolve(__dirname,'..'));
const {ElectricGraph:G}=require('./electric-graph-builder.cjs');
const fx='Assets/PortfolioFX',sp=`${fx}/Scenes/ElectricOverload.unity`;
if(u.read(sp).includes('  orbMaterial:'))throw Error('Electric presentation already exists. Do not overwrite subsequent edits.');
const backup=`RepairBackups/ElectricUpgrade_${new Date().toISOString().replace(/[:.]/g,'-')}`;
fs.mkdirSync(backup,{recursive:true});
const originals=[sp,`${fx}/Shaders/SG_OverloadBody.shadergraph`,`${fx}/Shaders/SG_ArcRibbon.shadergraph`,`${fx}/MyMaterial/M_OverloadBody.mat`,`${fx}/MyMaterial/M_ElectricArc.mat`,`${fx}/Materials/ElectricOverload_Volume.asset`,`${fx}/Scripts/Runtime/FXArcBeam.cs`];
for(const p of originals)fs.copyFileSync(p,`${backup}/${path.basename(p)}`);
u.write(`${backup}/manifest.json`,JSON.stringify({originals,scope:'ElectricOverload scene; keep existing graph and material GUIDs; no other scene, prefab or animation edited.'},null,2));
const bodyPath=`${fx}/Shaders/SG_OverloadBody.shadergraph`,arcPath=`${fx}/Shaders/SG_ArcRibbon.shadergraph`,orbPath=`${fx}/Shaders/SG_AetherOrb.shadergraph`,spritePath=`${fx}/Shaders/SG_ElectricAccent.shadergraph`;
const lightning=`${fx}/ReferenceArc/T_BodyLightning.png`,noise=`${fx}/ReferenceShield/HitNoise.png`;

{
  const g=new G(bodyPath),t=g.float('_EffectTime',0),intensity=g.float('_Intensity',0);
  const body=g.group('01 - Surface lightning / object-space projection',()=>g.triplanar(g.texture('_LightningTex',lightning),g.float('_LightningScale',1.65),g.scroll(t,.017,.025)));
  const emission=g.group('02 - Bright cores, blue penumbra, moving charge',()=>{
    const mask=g.math('Saturate',g.math('Multiply',body,2.2));
    const core=g.math('Smoothstep',.12,.65,mask),halo=g.math('Power',mask,.6);
    const position=g.node('Position');position.m_Space=0;
    const y=g.split([position,0])[1];
    const pulse=g.math('Add',.68,g.math('Multiply',g.math('Sine',g.math('Subtract',g.math('Multiply',t,19),g.math('Multiply',y,13))),.32));
    const tint=g.math('Add',g.math('Multiply',halo,g.color('_CurrentColor',[.025,.38,1])),g.math('Multiply',core,g.color('_CoreColor',[.72,.95,1])));
    const f=g.node('Fresnel');g.value(f,2,4);
    const rim=g.math('Multiply',[f,3],g.color('_RimColor',[.015,.085,.16]));
    return g.math('Multiply',g.math('Add',g.math('Multiply',tint,g.math('Multiply',pulse,g.float('_LightningGain',9))),rim),g.math('Maximum',intensity,0));
  });
  g.output('Emission',emission);g.prune();g.save(bodyPath);
}
{
  const g=new G(),uv=[g.node('UV'),0],t=g.float('_EffectTime',0),intensity=g.float('_Intensity',1);
  const mask=g.group('01 - Textured main filament and travelling strand',()=>{
    const a=g.sample(g.texture('_ArcTex',`${fx}/ReferenceArc/T_ArcFilament.png`),uv);
    const b=g.sample(g.texture('_FlowTex',`${fx}/ReferenceArc/T_ArcFlow.png`),g.math('Add',g.math('Multiply',uv,g.combine(g.float('_FlowTiling',2),1)),g.scroll(t,g.float('_FlowSpeed',-1.25),0)));
    return g.math('Saturate',g.math('Maximum',g.math('Multiply',[a,4],g.float('_TextureGain',1.6)),g.math('Multiply',[b,4],g.float('_FlowStrength',.3))));
  });
  g.group('02 - White core / cyan edge / additive opacity',()=>{
    const v=g.node('VertexColor'),vc=g.split([v,0]);
    const color=g.math('Lerp',g.color('_ArcColor',[.025,.4,1]),g.color('_CoreColor',[.75,.96,1]),g.math('Power',mask,g.float('_CorePower',2.5)));
    const rgb=g.combine(vc[0],vc[1],vc[2]);
    const pulse=g.math('Add',.9,g.math('Multiply',g.math('Sine',g.math('Multiply',t,31)),.1));
    g.output('BaseColor',g.math('Multiply',g.math('Multiply',color,rgb),g.math('Multiply',g.math('Multiply',g.float('_Grain',8),pulse),g.math('Maximum',intensity,0))));
    const [x,y]=g.split(uv);
    const edge=g.math('Multiply',g.math('Saturate',g.math('Multiply',g.math('Multiply',x,g.math('OneMinus',x)),60)),g.math('Saturate',g.math('Multiply',g.math('Multiply',y,g.math('OneMinus',y)),60)));
    g.output('Alpha',g.math('Saturate',g.math('Multiply',g.math('Multiply',g.math('Power',mask,g.float('_SoftPower',.72)),edge),vc[3])));
  });
  g.save(arcPath);
}
{
  const g=new G();g.target.m_SurfaceType=0;g.target.m_AlphaMode=0;g.target.m_ZWriteControl=1;g.target.m_RenderFace=2;
  const uv=[g.node('UV'),0],t=g.float('_EffectTime',0);
  g.group('01 - Counter-rotating plasma texture layers',()=>{
    const a=g.sample(g.texture('_PlasmaTex',noise),g.math('Add',g.math('Multiply',uv,g.combine(3,2)),g.scroll(t,.09,-.07)));
    const b=g.sample(g.texture('_FlowTex',noise),g.math('Add',g.math('Multiply',uv,g.combine(2,3)),g.scroll(t,-.06,.1)));
    const filaments=g.math('Power',g.math('Saturate',g.math('Multiply',[a,4],[b,4])),2.1);
    const f=g.node('Fresnel');g.value(f,2,3.5);
    const plasma=g.math('Multiply',filaments,g.color('_PlasmaColor',[.04,1.1,2.2]));
    const rim=g.math('Multiply',[f,3],g.color('_RimColor',[.05,.65,1.1]));
    g.output('BaseColor',g.math('Add',g.color('_DeepColor',[.008,.028,.045]),g.math('Multiply',g.math('Add',plasma,rim),g.math('Add',.75,g.math('Multiply',g.float('_Intensity',1),1.3)))));
    g.output('Alpha',1);
  });
  g.save(orbPath);
}
{
  const g=new G(),uv=[g.node('UV'),0],v=g.node('VertexColor');
  const s=g.sample(g.texture('_Mask',`${fx}/ReferenceArc/T_ArcContact.png`),uv);
  const vc=g.split([v,0]);
  g.output('BaseColor',g.math('Multiply',g.math('Multiply',g.color('_Tint',[.22,.7,1]),g.float('_Gain',5)),g.combine(vc[0],vc[1],vc[2])));
  g.output('Alpha',g.math('Saturate',g.math('Multiply',g.math('Multiply',g.math('Power',[s,4],.75),vc[3]),g.float('_Fade',1))));
  g.save(spritePath);
}
function material(name,shader,overrides={}){
  const p=`${fx}/MyMaterial/${name}.mat`,docs=u.graph(shader),map=new Map(docs.map(o=>[o.m_ObjectId,o]));
  const tex=[],floats=[],colors=[];
  for(const ref of docs[0].m_Properties){const prop=map.get(ref.m_Id),key=prop.m_OverrideReferenceName||prop.m_DefaultReferenceName,v=overrides[key]??prop.m_Value;
    if(prop.m_Type.endsWith('Texture2DShaderProperty')){const r=typeof v==='string'?{fileID:2800000,guid:u.guid(v)}:v.m_SerializedTexture?JSON.parse(v.m_SerializedTexture).texture:{fileID:0};tex.push(`    - ${key}:\n        m_Texture: {fileID: ${r.fileID}${r.guid?`, guid: ${r.guid}, type: 3`:''}}\n        m_Scale: {x: 1, y: 1}\n        m_Offset: {x: 0, y: 0}`);}
    else if(typeof v==='number')floats.push(`    - ${key}: ${v}`);
    else colors.push(`    - ${key}: {r: ${v.r}, g: ${v.g}, b: ${v.b}, a: ${v.a??1}}`);
  }
  u.write(p,`%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_Name: ${name}\n  m_Shader: {fileID: -6465566751694194690, guid: ${u.guid(shader)}, type: 3}\n  m_Parent: {fileID: 0}\n  m_ModifiedSerializedProperties: 0\n  m_ValidKeywords: []\n  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 0\n  m_DoubleSidedGI: 0\n  m_CustomRenderQueue: -1\n  stringTagMap: {}\n  disabledShaderPasses: []\n  m_LockedProperties: \n  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs:\n${tex.join('\n')}\n    m_Ints: []\n    m_Floats:\n${floats.join('\n')}\n    m_Colors:\n${colors.join('\n')}\n  m_BuildTextureStacks: []\n  m_AllowLocking: 1\n`);u.meta(p,'NativeFormatImporter',2100000);return p;
}
// The body retains all original Ellen maps; only its emissive branch changes.
material('M_OverloadBody',bodyPath);
material('M_ElectricArc',arcPath);
const orb=material('M_AetherOrb',orbPath),accent=material('M_ElectricContact',spritePath);
const orbit=material('M_AetherOrbit',spritePath,{_Mask:`${fx}/ReferenceArc/T_ArcFilament.png`,_Tint:{r:.28,g:.85,b:1},_Gain:3});
const gold=material('M_AetherOrbitGold',spritePath,{_Mask:`${fx}/ReferenceArc/T_ArcFilament.png`,_Tint:{r:1,g:.55,b:.14},_Gain:2.5});
const branch=material('M_ElectricBranch',spritePath,{_Mask:lightning,_Tint:{r:.35,g:.8,b:1},_Gain:6});
const runtime=`${fx}/Scripts/Runtime/FXElectricPresentation.cs`;
u.meta(runtime,'MonoImporter');
const scene=u.records(u.read(sp));
const go=name=>scene.docs.find(d=>d.type===1&&d.body.includes(`  m_Name: ${name}\n`));
const component=(name,type)=>scene.docs.find(d=>d.type===type&&d.body.includes(`m_GameObject: {fileID: ${go(name).id}}`));
const root=go('ElectricOverload_FX'),driver=component('ElectricOverload_FX',114),camera=component('Main Camera',20);
const legacy=[];
for(let i=0;i<3;i++)for(const name of [`Conductor_${i}`,`Terminal_${i}`]){const r=component(name,23);legacy.push(r.id);r.body=r.body.replace('m_Enabled: 1','m_Enabled: 0');}
for(let i=0;i<3;i++){
  const arc=component(`PrimaryArc_${i}`,114);arc.body=arc.body.replace('  displacement: 0.2','  displacement: 0.105');
  arc.body+='  minWidth: 0.12\n  maxWidth: 0.22\n  driveShader: 1\n';
  const line=component(`PrimaryArc_${i}`,120);line.body=line.body.replace(/widthMultiplier: [^\n]+/,'widthMultiplier: 0.18');
}
const attach='2240000001';if(scene.docs.some(d=>d.id===attach))throw Error('ID collision');
root.body=root.body.replace('  m_Layer: 0',`  - component: {fileID: ${attach}}\n  m_Layer: 0`);
const tr=name=>component(name,4).id;
scene.docs.push({type:114,id:attach,body:`--- !u!114 &${attach}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: ${root.id}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: ${u.guid(runtime)}, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n  sequence: {fileID: ${driver.id}}\n  presentationCamera: {fileID: ${camera.id}}\n  body: {fileID: ${component('Ellen_Body',137).id}}\n  orbMaterial: {fileID: 2100000, guid: ${u.guid(orb)}, type: 2}\n  orbitMaterial: {fileID: 2100000, guid: ${u.guid(orbit)}, type: 2}\n  goldOrbitMaterial: {fileID: 2100000, guid: ${u.guid(gold)}, type: 2}\n  contactMaterial: {fileID: 2100000, guid: ${u.guid(accent)}, type: 2}\n  branchMaterial: {fileID: 2100000, guid: ${u.guid(branch)}, type: 2}\n  terminals:\n${[0,1,2].map(i=>`  - {fileID: ${tr(`Terminal_${i}`)}}`).join('\n')}\n  legacyRenderers:\n${legacy.map(id=>`  - {fileID: ${id}}`).join('\n')}\n  contacts:\n${['Ellen_Left_UpperArm','Ellen_Right_Hand','Ellen_UpperChest','Ellen_Left_Arm','Ellen_Right_UpperLeg','Ellen_Left_LowerLeg'].map(n=>`  - {fileID: ${tr(n)}}`).join('\n')}\n`});
// Keep the old path targets and animations intact. Generated ornaments follow those targets.
u.write(sp,scene.header+scene.docs.map(d=>d.body).join(''));
const volume=`${fx}/Materials/ElectricOverload_Volume.asset`;
u.write(volume,u.read(volume).replace(/(  intensity:\n    m_OverrideState: 1\n    m_Value:) 0.25/,'$1 0.48'));
console.log(JSON.stringify({backup,graphs:[bodyPath,arcPath,orbPath,spritePath],scene:sp},null,2));
