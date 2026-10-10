const fs = require('fs'), assert = require('assert/strict');
const u = require('./crystal-assets.cjs');
const { ElectricGraph } = require('./electric-graph-builder.cjs');
const scene = 'Assets/PortfolioFX/Scenes/GroundSlash.unity';
const mat = name => 'Assets/PortfolioFX/MyMaterial/M_' + name + '.mat';
const shader = name => 'Assets/PortfolioFX/Shaders/SG_' + name + '.shadergraph';
const docs = u.records(u.read(scene));
assert(!docs.docs.some(d => d.id === '2109210100'), 'Binding already applied');
const backup = 'RepairBackups/GroundSlashBinding_' + new Date().toISOString().replace(/[:.]/g, '-');
const changed = [scene, mat('SlashFlash'), mat('SlashDust'), shader('SlashWake'), shader('SlashDust')];
for (const p of changed) {
  fs.mkdirSync(backup+'/'+require('path').dirname(p),{recursive:true});
  fs.copyFileSync(p,backup+'/'+p); fs.copyFileSync(p+'.meta',backup+'/'+p+'.meta');
}
function link(g, a, output, b, input) {
  g.root.m_Edges=g.root.m_Edges.filter(e=>e.m_InputSlot.m_Node.m_Id!==b.m_ObjectId||e.m_InputSlot.m_SlotId!==input);
  g.link(a,output,b,input);
}
// Repair only the unconnected tail mask; retain the rest of the user's graph.
{
  const g=new ElectricGraph(shader('SlashWake'));
  g.root.m_Path=u.graph(shader('SlashWake'))[0].m_Path;
  const find = prefix=>g.docs.find(d=>d.m_ObjectId.startsWith(prefix));
  const split=find('18e3c8'), sample=find('4d15f6'), multiply=find('a4c729');
  assert(split&&sample&&multiply);
  link(g,sample,7,multiply,0);
  const tail=g.math('OneMinus',g.math('Smoothstep',.05,1,[split,1]));
  link(g,tail[0],tail[1],multiply,1);
  g.save(shader('SlashWake'));
}
{
  const g=new ElectricGraph(shader('SlashDust'));
  g.root.m_Path=u.graph(shader('SlashDust'))[0].m_Path;
  const find=prefix=>g.docs.find(d=>d.m_ObjectId.startsWith(prefix));
  const opacity=g.float('_Opacity',.35);
  const alpha=g.math('Multiply',[find('803b68'),2],opacity);
  link(g,alpha[0],alpha[1],find('54df08'),0);
  link(g,find('336dce'),0,find('dbd6c3'),1);
  g.save(shader('SlashDust'));
}
function setFloat(s,name,value) {
  const re=new RegExp('    - '+name+': [^\\n]+');
  return re.test(s)?s.replace(re,`    - ${name}: ${value}`):s.replace('    m_Floats:\n',`    m_Floats:\n    - ${name}: ${value}\n`);
}
function setColor(s,name,rgb) {
  const line=`    - ${name}: {r: ${rgb[0]}, g: ${rgb[1]}, b: ${rgb[2]}, a: 1}`;
  const re=new RegExp('    - '+name+': \\{[^}]+\\}');
  return re.test(s)?s.replace(re,line):s.replace('    m_Colors:\n','    m_Colors:\n'+line+'\n');
}
let flash=u.read(mat('GroundSlash')).replace('m_Name: M_GroundSlash','m_Name: M_SlashFlash');
flash=flash.replace(/(- _MaskTex:\n\s+m_Texture: \{fileID: 2800000, guid: )\w+/, '$1'+u.guid('Assets/PortfolioFX/ReferenceGroundSlash/T_SlashFlash.png'));
for(const [n,v]of Object.entries({_NoiseAmount:0,_EdgePower:1,_Brightness:4,_ObjectOpacity:1}))flash=setFloat(flash,n,v);
flash=setColor(setColor(flash,'_EdgeColor',[.8,.95,1]),'_CoreColor',[.8,.95,1]);
u.write(mat('SlashFlash'),flash);
u.write(mat('SlashDust'),setFloat(u.read(mat('SlashDust')),'_Opacity',.35));
const runtime='Assets/PortfolioFX/Scripts/Runtime/FXGroundSlashLesson.cs';
for(const p of [runtime,'Assets/PortfolioFX/Scripts/Runtime/FXGroundSlashParticles.cs','Assets/PortfolioFX/Scripts/Editor/FXGroundSlashLessonEditor.cs'])u.meta(p,'MonoImporter');
const by=new Map(docs.docs.map(d=>[d.id,d]));
const disabled=[];
for(const d of docs.docs.filter(d=>d.type===1)) {
  const name=d.body.match(/m_Name: (.*)/)?.[1];
  if(/^(SlashRibbon|ContactWave|GroundSpray|GroundDebris_\d+|EllenStaff|Staff_Body)$/.test(name)) {
    d.body=d.body.replace('m_IsActive: 1','m_IsActive: 0'); disabled.push(name);
  }
}
const driver=by.get('1646226398');
driver.body=driver.body.replace(/  materials:\n[\s\S]*?  arcs:/,'  materials: []\n  particles: []\n  arcs:');
driver.body=driver.body.replace(/  time: [^\n]+/,'  time: 0.85');
by.get('1646226399').body=by.get('1646226399').body.replace('  m_Children:\n','  m_Children:\n  - {fileID: 2109210101}\n');
function record(type,id,body){docs.docs.push({type,id:String(id),body:`--- !u!${type} &${id}\n${body}\n`});}
function go(id,name,components){record(1,id,`GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
${components.map(c=>`  - component: {fileID: ${c}}`).join('\n')}
  m_Layer: 0
  m_Name: ${name}
  m_TagString: Untagged
  m_IsActive: 1`);}
function tr(id,go,parent,pos,children){record(4,id,`Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${go}}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: ${pos[0]}, y: ${pos[1]}, z: ${pos[2]}}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: ${children.length?'\n'+children.map(c=>`  - {fileID: ${c}}`).join('\n'):'[]'}
  m_Father: {fileID: ${parent}}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}`);}
go(2109210100,'HandBladeLesson',[2109210101,2109210102]);
tr(2109210101,2109210100,1646226399,[0,0,0],[2109210111,2109210121]);
go(2109210110,'SlashHit',[2109210111]);
tr(2109210111,2109210110,2109210101,[1.6,.01,.55],[]);
go(2109210120,'GroundCollisionPlane',[2109210121]);
tr(2109210121,2109210120,2109210101,[0,.01,0],[]);
record(114,2109210102,`MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2109210100}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${u.guid(runtime)}, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  sequence: {fileID: 1646226398}
  character: {fileID: 341961637}
  hand: {fileID: 452522007}
  viewCamera: {fileID: 272536203}
  hit: {fileID: 2109210111}
  bladeMaterial: {fileID: 2100000, guid: ${u.guid(mat('GroundSlash'))}, type: 2}
  trailMaterial: {fileID: 2100000, guid: ${u.guid(mat('SlashWake'))}, type: 2}
  flashMaterial: {fileID: 2100000, guid: ${u.guid(mat('SlashFlash'))}, type: 2}
  crackMaterial: {fileID: 2100000, guid: ${u.guid(mat('SlashGroundCut'))}, type: 2}
  debrisMaterial: {fileID: 2100000, guid: ${u.guid(mat('SlashDebris'))}, type: 2}
  dustMaterial: {fileID: 2100000, guid: ${u.guid(mat('SlashDust'))}, type: 2}
  debrisMesh: {fileID: 4300000, guid: 88b9906b4442aa2448a9379fa0d1b987, type: 3}
  groundPlane: {fileID: 2109210121}
  releaseTime: 0.62
  impactTime: 0.98
  bladeSize: 1.65
  brightness: 3.5
  trailStrength: 0.32`);
const roots=docs.docs.findIndex(d=>d.type===1660057539);
if(roots>=0)docs.docs.push(...docs.docs.splice(roots,1));
u.write(scene,docs.header+docs.docs.map(d=>d.body).join(''));
fs.mkdirSync('Logs/GroundSlashBinding',{recursive:true});
u.write('Logs/GroundSlashBinding/changes.json',JSON.stringify({backup,changed,disabled},null,2));
console.log('Bound scene, materials and scripts. Backup:',backup);
