const fs=require('fs'),path=require('path'),crypto=require('crypto'),u=require('./crystal-assets.cjs');
const {ElectricGraph:BaseGraph}=require('./electric-graph-builder.cjs');
class G extends BaseGraph {
 node(type){if(type!=='Fraction')return super.node(type);const n=super.node('Absolute');n.m_Type='UnityEditor.ShaderGraph.FractionNode';n.m_Name='Fraction';return n;}
}
process.chdir(path.resolve(__dirname,'..'));
const fx='Assets/PortfolioFX',sp=`${fx}/Scenes/ArcaneChargeShot.unity`;
if(u.read(sp).includes('  fireMaterial:'))throw Error('Fireball presentation already exists. Preserve subsequent edits.');
const backup=`RepairBackups/ChargeFireball_${new Date().toISOString().replace(/[:.]/g,'-')}`;
fs.mkdirSync(backup,{recursive:true});
for(const p of [sp,`${fx}/Shaders/SG_ChargeOrb.shadergraph`,`${fx}/MyMaterial/M_ChargeOrb.mat`,`${fx}/Materials/ArcaneChargeShot_Volume.asset`])fs.copyFileSync(p,`${backup}/${path.basename(p)}`);
const protectedPaths=[`${fx}/Scenes/HologramMaterialize.unity`,`${fx}/Scenes/ElectricOverload.unity`,`${fx}/Scenes/ReactiveShield.unity`,`${fx}/Shaders/SG_Hologram.shadergraph`,`${fx}/Shaders/SG_ArcRibbon.shadergraph`];
const hashes=Object.fromEntries(protectedPaths.map(p=>[p,crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex')]));
const tex='Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Textures';
function floor(g,v){const n=g.node('Fraction');n.m_Type='UnityEditor.ShaderGraph.FloorNode';n.m_Name='Floor';g.feed(v,n,0);return[n,1];}
function material(name,shader,overrides={}){
 const p=`${fx}/MyMaterial/${name}.mat`,docs=u.graph(shader),map=new Map(docs.map(o=>[o.m_ObjectId,o]));const textures=[],floats=[],colors=[];
 for(const ref of docs[0].m_Properties){const prop=map.get(ref.m_Id),key=prop.m_OverrideReferenceName||prop.m_DefaultReferenceName,v=overrides[key]??prop.m_Value;
 if(prop.m_Type.endsWith('Texture2DShaderProperty')){const r=typeof v==='string'?{fileID:2800000,guid:u.guid(v)}:JSON.parse(v.m_SerializedTexture).texture;textures.push(`    - ${key}:\n        m_Texture: {fileID: ${r.fileID}, guid: ${r.guid}, type: 3}\n        m_Scale: {x: 1, y: 1}\n        m_Offset: {x: 0, y: 0}`);}
 else if(typeof v==='number')floats.push(`    - ${key}: ${v}`);else colors.push(`    - ${key}: {r: ${v.r}, g: ${v.g}, b: ${v.b}, a: ${v.a??1}}`);}
 u.write(p,`%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_Name: ${name}\n  m_Shader: {fileID: -6465566751694194690, guid: ${u.guid(shader)}, type: 3}\n  m_Parent: {fileID: 0}\n  m_ModifiedSerializedProperties: 0\n  m_ValidKeywords: []\n  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 0\n  m_DoubleSidedGI: 0\n  m_CustomRenderQueue: -1\n  stringTagMap: {}\n  disabledShaderPasses: []\n  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs:\n${textures.join('\n')}\n    m_Ints: []\n    m_Floats:\n${floats.join('\n')}\n    m_Colors:\n${colors.join('\n')}\n  m_BuildTextureStacks: []\n`);u.meta(p,'NativeFormatImporter',2100000);return p;
}
const orb=`${fx}/Shaders/SG_ChargeOrb.shadergraph`,ribbon=`${fx}/Shaders/SG_FireRibbon.shadergraph`,halo=`${fx}/Shaders/SG_FireHalo.shadergraph`;
{
 const g=new G();g.root.m_Path='PortfolioFX/Fireball';g.target.m_AlphaMode=0;
 const uv=[g.node('UV'),0],frame=g.math('Maximum',g.float('_Frame',20),0),cols=g.float('_Columns',7),rows=g.float('_Rows',7);
 const base=g.texture('_BaseMap',`${tex}/Fireball.tif`),em=g.texture('_EmissionMap',`${tex}/FireBallEmission.tif`);
 function sampleFrame(f){const row=floor(g,g.math('Divide',f,cols)),column=g.math('Subtract',f,g.math('Multiply',row,cols));
 const inner=g.math('Add',g.math('Multiply',uv,.992),.004);
 const coord=g.math('Divide',g.math('Add',inner,g.combine(column,g.math('Subtract',g.math('Subtract',rows,1),row))),g.combine(cols,rows));
 return [g.sample(base,coord),g.sample(em,coord)];}
 const count=g.math('Multiply',cols,rows),wrapped=g.math('Multiply',g.math('Fraction',g.math('Divide',frame,count)),count),f=floor(g,wrapped);
 const next=floor(g,g.math('Multiply',g.math('Fraction',g.math('Divide',g.math('Add',f,1),count)),count));
 const a=sampleFrame(f),b=sampleFrame(next),blend=g.math('Fraction',wrapped);
 const color=g.math('Lerp',[a[0],0],[b[0],0],blend),emit=g.math('Lerp',[a[1],4],[b[1],4],blend);
 const c=g.split(color),mask=g.math('Saturate',g.math('Maximum',g.math('Maximum',c[0],c[1]),emit));
 g.output('BaseColor',g.math('Add',g.math('Multiply',color,g.math('Multiply',g.color('_Tint',[1,1,1]),g.float('_Gain',2.2))),g.math('Multiply',emit,g.color('_CoreColor',[1.3,.65,.15]))));
 g.output('Alpha',g.math('Multiply',g.math('Power',mask,.65),g.float('_Fade',1)));
 g.save(orb);
}
{
 const g=new G();g.root.m_Path='PortfolioFX/Fireball';const uv=[g.node('UV'),0],t=g.float('_EffectTime',0),xy=g.split(uv);
 const texture=g.sample(g.texture('_TrailMask','Assets/Trail/Textures/Trail_02.png'),g.math('Add',uv,g.scroll(t,-1.7,0)));
 const noise=g.sample(g.texture('_Noise',`${fx}/ReferenceShield/FlowLight.png`),g.math('Add',g.math('Multiply',uv,g.combine(3,1)),g.scroll(t,-1.2,.1)));
 const lateral=g.math('Power',g.math('Saturate',g.math('Multiply',g.math('Multiply',xy[1],g.math('OneMinus',xy[1])),4)),1.2);
 const mask=g.math('Multiply',g.math('Multiply',g.math('OneMinus',[texture,4]),g.math('Lerp',.35,1,[noise,4])),lateral);
 const ends=g.math('Multiply',g.math('Smoothstep',0,.035,xy[0]),g.math('OneMinus',g.math('Smoothstep',.7,1,xy[0])));
 g.output('BaseColor',g.math('Multiply',g.math('Lerp',g.color('_EmberColor',[1,.065,.008]),g.color('_HotColor',[1,.7,.18]),g.math('Power',mask,2)),g.float('_Gain',4)));
 g.output('Alpha',g.math('Multiply',g.math('Multiply',mask,ends),g.float('_Fade',1)));g.save(ribbon);
}
{
 const g=new G();g.root.m_Path='PortfolioFX/Fireball';const uv=[g.node('UV'),0],t=g.float('_EffectTime',0);
 const mask=g.sample(g.texture('_Mask',`${tex}/shockwave.tif`),uv),noise=g.sample(g.texture('_Noise',`${fx}/ReferenceShield/HitNoise.png`),g.math('Add',g.math('Multiply',uv,3),g.scroll(t,.12,-.16)));
 const broken=g.math('Smoothstep',.15,.75,[noise,4]);
 g.output('BaseColor',g.math('Multiply',g.color('_Tint',[1,.3,.045]),g.float('_Gain',3)));
 g.output('Alpha',g.math('Multiply',g.math('Multiply',[mask,4],g.math('Lerp',.15,1,broken)),g.float('_Fade',1)));g.save(halo);
}
const mats={fireMaterial:material('M_ChargeOrb',orb),flameMaterial:material('M_ChargeFlame',orb,{_BaseMap:`${tex}/Flame02.tif`,_EmissionMap:`${tex}/Flame02.tif`,_Columns:6,_Rows:6,_Gain:1.5,_CoreColor:{r:.6,g:.3,b:.04}}),ribbonMaterial:material('M_FireRibbon',ribbon),haloMaterial:material('M_FireChargeHalo',halo),explosionMaterial:material('M_FireImpact',orb,{_BaseMap:`${tex}/Explosion.tif`,_EmissionMap:`${tex}/ExplosionEmission.tif`,_Columns:6,_Rows:6,_Tint:{r:1,g:.3,b:.055},_Gain:2.7,_CoreColor:{r:2.2,g:.8,b:.12}}),smokeMaterial:material('M_FireImpactDust',orb,{_BaseMap:`${tex}/Explosion.tif`,_EmissionMap:`${tex}/Explosion.tif`,_Columns:6,_Rows:6,_Tint:{r:.28,g:.25,b:.23},_Gain:.8,_CoreColor:{r:0,g:0,b:0}})};
// The original rune material already targets a graph; create isolated URP/Lit variants.
function stoneMaterial(name,base,normal){
 let s=u.records(u.read('Assets/Viking Village/Models/Props/Materials/mat_rune_01.mat')).docs.find(d=>d.type===21).body;
 s=s.replace(/m_Name: [^\n]+/,`m_Name: ${name}`).replace(/m_Shader: \{[^}]+\}/,'m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}').replace(/m_ValidKeywords: \[\]/,'m_ValidKeywords:\n  - _NORMALMAP').replace(/  m_InvalidKeywords:[\s\S]*?(?=  m_LightmapFlags:)/,'  m_InvalidKeywords: []\n');
 for(const key of ['_BaseMap','_MainTex'])s=s.replace(new RegExp(`(- ${key}:\\n        m_Texture:) \\{[^}]+\\}`),`$1 {fileID: 2800000, guid: ${u.guid(base)}, type: 3}`);
 s=s.replace(/(- _BumpMap:\n        m_Texture:) \{[^}]+\}/,`$1 {fileID: 2800000, guid: ${u.guid(normal)}, type: 3}`);
 for(const [key,v]of Object.entries({_Surface:0,_Blend:0,_ZWrite:1,_Cull:2,_Smoothness:.22,_Metallic:0,_BumpScale:1,_WorkflowMode:1})){
 const re=new RegExp(`(- ${key}:) [^\\n]+`);s=re.test(s)?s.replace(re,`$1 ${v}`):s.replace('    m_Floats:\n',`    m_Floats:\n    - ${key}: ${v}\n`);}
 s=s.replace(/(- _BaseColor:) \{[^}]+\}/,'$1 {r: 0.8, g: 0.85, b: 0.9, a: 1}');
 const p=`${fx}/MyMaterial/${name}.mat`;u.write(p,'%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+s);u.meta(p,'NativeFormatImporter',2100000);return p;
}
mats.targetMaterial=stoneMaterial('M_FireTargetRune','Assets/Viking Village/Textures/Props/prop_rune_01_d.tif','Assets/Viking Village/Textures/Props/prop_rune_01_n.tif');
mats.fractureMaterial=stoneMaterial('M_FireTargetInterior','Assets/Viking Village/Textures/Props/prop_boulder_01_d.tif','Assets/Viking Village/Textures/Props/prop_boulder_01_n.tif');
for(const p of ['FXFireballPresentation','FXFireballFragments'])u.meta(`${fx}/Scripts/Runtime/${p}.cs`,'MonoImporter');
const scene=u.records(u.read(sp));
const go=name=>scene.docs.find(d=>d.type===1&&d.body.includes(`m_Name: ${name}\n`));
const component=(name,type)=>scene.docs.find(d=>d.type===type&&d.body.includes(`m_GameObject: {fileID: ${go(name).id}}`));
const driver=component('ArcaneChargeShot_FX',114),root=go('ArcaneChargeShot_FX');
const old=[];
for(const name of ['ChargeProjectile','MuzzleCharge','Impact','ReleaseStreak','TargetPillar','Staff_Body']){
 const r=scene.docs.find(d=>[23,120,137].includes(d.type)&&d.body.includes(`m_GameObject: {fileID: ${go(name).id}}`));old.push(r.id);r.body=r.body.replace('m_Enabled: 1','m_Enabled: 0');}
go('EllenStaff').body=go('EllenStaff').body.replace('m_IsActive: 1','m_IsActive: 0');
component('ReleaseStreak',114).body=component('ReleaseStreak',114).body.replace('m_Enabled: 1','m_Enabled: 0');
component('ImpactFragments',198).body=component('ImpactFragments',198).body.replace(/playOnAwake: 1/,'playOnAwake: 0');
go('ImpactFragments').body=go('ImpactFragments').body.replace('m_IsActive: 1','m_IsActive: 0');
driver.body=driver.body.replace(/  materials:[\s\S]*?(?=  characters:)/,'  materials: []\n  particles: []\n  arcs: []\n').replace('duration: 5','duration: 6.5');
driver.body=driver.body.replace(/  effectLight: \{[^}]+\}/,'  effectLight: {fileID: 0}');
component('EffectLight',108).body=component('EffectLight',108).body.replace('m_Enabled: 1','m_Enabled: 0');
component('HitPoint',4).body=component('HitPoint',4).body.replace(/m_LocalPosition: \{[^}]+\}/,'m_LocalPosition: {x: 2.3, y: 1.33, z: 0}');
const originalClip=`${fx}/Animations/ArcaneChargeShot_Sequence.anim`,newClip=`${fx}/Animations/ArcaneChargeShot_FireballSequence.anim`;
let clip=u.read(originalClip).replace('m_Name: ArcaneChargeShot_Sequence','m_Name: ArcaneChargeShot_FireballSequence').replace(/  m_PositionCurves:[\s\S]*?(?=  m_FloatCurves:)/,'  m_PositionCurves: []\n  m_ScaleCurves: []\n').replace('m_StopTime: 5','m_StopTime: 6.5');
u.write(newClip,clip);u.meta(newClip,'NativeFormatImporter',7400000);
driver.body=driver.body.replace(/  sequence: \{[^}]+\}/,`  sequence: {fileID: 7400000, guid: ${u.guid(newClip)}, type: 2}`);
const character=component('Caster',114);
// Remove the unrelated crouching landing cue; let the casting pose blend back to idle.
character.body=character.body.replace(/  - clip: \{fileID: 7400000, guid: 83a6af93c7cbfa14790087fe8bd0f8b1[\s\S]*$/,'');
const id='1909211001';root.body=root.body.replace('  m_Component:\n',`  m_Component:\n  - component: {fileID: ${id}}\n`);
scene.docs.push({type:114,id,body:`--- !u!114 &${id}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: ${root.id}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: ${u.guid(`${fx}/Scripts/Runtime/FXFireballPresentation.cs`)}, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n  sequence: {fileID: ${driver.id}}\n  character: {fileID: ${character.id}}\n  hand: {fileID: ${component('Ellen_Right_Hand',4).id}}\n  presentationCamera: {fileID: ${component('Main Camera',20).id}}\n  targetMesh: {fileID: 4300000, guid: ${u.guid('Assets/Viking Village/Models/Props/prop_rune_01.fbx')}, type: 3}\n${Object.entries(mats).map(([k,p])=>`  ${k}: {fileID: 2100000, guid: ${u.guid(p)}, type: 2}\n`).join('')}  legacyRenderers:\n${old.map(id=>`  - {fileID: ${id}}\n`).join('')}  weapon: {fileID: ${go('EllenStaff').id}}\n  targetBase: {x: 2.6, y: 0.08, z: 0}\n  targetHeight: 2.35\n  releaseTime: 1.8\n  hitTime: 2.45\n`});
u.write(sp,scene.header+scene.docs.map(d=>d.body).join(''));
const vp=`${fx}/Materials/ArcaneChargeShot_Volume.asset`;u.write(vp,u.read(vp).replace(/(  intensity:\n    m_OverrideState: 1\n    m_Value:) 0.25/,'$1 0.4'));
for(const[p,h]of Object.entries(hashes))if(crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex')!==h)throw Error('Unexpected change '+p);
u.write(`${backup}/manifest.json`,JSON.stringify({backup,scene:sp,materials:mats,protectedAssets:hashes,sources:{fireball:`${tex}/Fireball.tif`,fireEmission:`${tex}/FireBallEmission.tif`,flame:`${tex}/Flame02.tif`,explosion:`${tex}/Explosion.tif`,target:'Assets/Viking Village/Models/Props/prop_rune_01.fbx',trail:'Assets/Trail/Textures/Trail_02.png'}},null,2));
console.log({backup,scene:sp,materials:mats});
