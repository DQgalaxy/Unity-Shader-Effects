const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const u=require('./crystal-assets.cjs');
const root='Assets/PortfolioFX/';
const scenes=['MeteorStrike','FoldstepPortal','WaterShockwave','DepthIntersectionLab'];
const backup='RepairBackups/RemainingFX_'+new Date().toISOString().replace(/[:.]/g,'-');
const mat=n=>root+'MyMaterial/M_'+n+'.mat';
function duplicate(source,name,changes={}) {
  const p=mat(name);assert(!fs.existsSync(p),'Preserve existing material '+p);
  let text=u.read(source).replace(/m_Name: [^\n]+/,'m_Name: M_'+name);
  for(const [key,v]of Object.entries(changes)){
    const re=new RegExp('    - '+key+': [^\\n]+');
    const value=Array.isArray(v)?`{r: ${v[0]}, g: ${v[1]}, b: ${v[2]}, a: ${v[3]??1}}`:v;
    assert(re.test(text),'Missing '+key);text=text.replace(re,`    - ${key}: ${value}`);
  }
  u.write(p,text);u.meta(p,'NativeFormatImporter',2100000);
}
// Do not rerun over an edited scene or overwrite any of the first sixteen chapters.
for(const name of scenes) {
  const p=root+'Scenes/'+name+'.unity';assert(!u.read(p).includes('&2109240017'),'Already upgraded '+name);
  fs.mkdirSync(backup+'/'+path.dirname(p),{recursive:true});fs.copyFileSync(p,backup+'/'+p);
}
duplicate(mat('SlashFlash'),'MeteorFlash',{_EdgeColor:[1,.42,.08],_CoreColor:[1,.8,.4],_Brightness:4});
duplicate(mat('SlashGroundCut'),'MeteorCrater',{_HotColor:[1,.18,.01]});
// The shared impact graph is left untouched; only local scene materials are assigned.
duplicate(root+'Materials/Metal.mat','PortalFrame',{_BaseColor:[.12,.23,.27],_Metallic:.65,_Smoothness:.65});
for(const n of ['FXShowcaseUtility','FXMeteorPresentation','FXPortalPresentation','FXWaterPresentation','FXDepthLabPresentation'])
  u.meta(root+'Scripts/Runtime/'+n+'.cs','MonoImporter');
const rockPath='Assets/UnityTechnologies/ParticlePack/EffectExamples/Magic Effects/Models/RockDebris.FBX';
const rockRef=`{fileID: 4300000, guid: ${u.guid(rockPath)}, type: 3}`;
const ref=(file,id=2100000)=>`{fileID: ${id}, guid: ${u.guid(file)}, type: 2}`;
for(const name of scenes){
  const p=root+'Scenes/'+name+'.unity',scene=u.records(u.read(p)),docs=scene.docs;
  function go(n){const d=docs.find(d=>d.type===1&&d.body.includes('  m_Name: '+n+'\n'));assert(d,n);return d;}
  function comp(n,type){const id=go(n).id,d=docs.find(d=>d.type===type&&d.body.includes('m_GameObject: {fileID: '+id+'}'));assert(d,n+' type '+type);return d;}
  function renderer(n){return comp(n,n==='Ellen_Body'?137:23);}
  function bind(n,material){const r=renderer(n);r.body=r.body.replace(/  m_Materials:\n(?:  - [^\n]+\n)+/,'  m_Materials:\n  - '+ref(mat(material))+'\n');return r.id;}
  function rock(n){const d=comp(n,33);d.body=d.body.replace(/m_Mesh: \{[^}]+\}/,'m_Mesh: '+rockRef);bind(n,'SlashDebris');}
  function disable(n){const d=go(n);d.body=d.body.replace('m_IsActive: 1','m_IsActive: 0');}
  const driver=docs.find(d=>d.type===114&&d.body.includes('  playOnStart:'));
  driver.body=driver.body.replace(/previewPlaceholder: 1/g,'previewPlaceholder: 0').replace(/  particles:[\s\S]*?  arcs:/,'  particles: []\n  arcs:');
  const rootGo=go(name+'_FX');rootGo.body=rootGo.body.replace('  m_Component:\n','  m_Component:\n  - component: {fileID: 2109240017}\n');
  const camera=comp('Main Camera',20);
  let script,fields;
  if(name==='MeteorStrike'){
    script='FXMeteorPresentation';
    bind('MeteorBody','MeteorSurface');bind('WarningRing','MeteorWarning');
    comp('MeteorBody',33).body=comp('MeteorBody',33).body.replace(/m_Mesh: \{[^}]+\}/,'m_Mesh: '+rockRef);
    for(const n of ['MeteorTrail','ImpactBurst','DustCloud',...Array.from({length:8},(_,i)=>'Ejecta_'+i)])disable(n);
    fields=`  viewCamera: {fileID: ${camera.id}}\n  body: {fileID: ${renderer('MeteorBody').id}}\n  warning: {fileID: ${renderer('WarningRing').id}}\n  shockwave: {fileID: ${renderer('Shockwave').id}}\n  rockMesh: ${rockRef}\n`+
      ['flameMaterial:MeteorFlame','emberMaterial:MeteorEmbers','smokeMaterial:MeteorSmoke','rockMaterial:SlashDebris','flashMaterial:MeteorFlash','craterMaterial:MeteorCrater'].map(s=>{const[a,b]=s.split(':');return `  ${a}: ${ref(mat(b))}\n`;}).join('')+
      '  impactTime: 2.4\n  start: {x: -2.4, y: 5.8, z: 0.2}\n  landing: {x: 0, y: 0.42, z: 0}\n';
    driver.body=driver.body.replace(/  time: [^\n]+/,'  time: 1.95');
  } else if(name==='FoldstepPortal'){
    script='FXPortalPresentation';bind('Ellen_Body','TravelerDissolve');
    for(let i=0;i<2;i++){bind('PortalSurface_'+i,'PortalSurface');bind('PortalFrame_'+i,'PortalFrame');disable('PortalMotes_'+i);}
    fields=`  viewCamera: {fileID: ${camera.id}}\n  traveler: {fileID: ${renderer('Ellen_Body').id}}\n  surfaces:\n  - {fileID: ${renderer('PortalSurface_0').id}}\n  - {fileID: ${renderer('PortalSurface_1').id}}\n  frames:\n  - {fileID: ${renderer('PortalFrame_0').id}}\n  - {fileID: ${renderer('PortalFrame_1').id}}\n  moteMaterial: ${ref(mat('PortalMotes'))}\n`;
    driver.body=driver.body.replace(/  time: [^\n]+/,'  time: 1.3');
  } else if(name==='WaterShockwave'){
    script='FXWaterPresentation';bind('WaterSurface','WaterShockwave');
    for(let i=0;i<4;i++)rock('ShoreRock_'+i);rock('WaterDrop');disable('SplashDroplets');
    const crown='Assets/UnityTechnologies/ParticlePack/EffectExamples/Water Effects/Models/DropSplash.fbx';
    fields=`  water: {fileID: ${renderer('WaterSurface').id}}\n  drop: {fileID: ${renderer('WaterDrop').id}}\n  shoreRocks:\n`+
      Array.from({length:4},(_,i)=>`  - {fileID: ${renderer('ShoreRock_'+i).id}}\n`).join('')+
      `  rockMesh: ${rockRef}\n  crownMesh: {fileID: 4300002, guid: ${u.guid(crown)}, type: 3}\n  crownMaterial: ${ref(mat('WaterCrown'))}\n  dropletMaterial: ${ref(mat('WaterDroplets'))}\n  impactTime: 1.3\n`;
    driver.body=driver.body.replace(/  time: [^\n]+/,'  time: 1.8');
  } else {
    script='FXDepthLabPresentation';bind('IntersectionSphere','IntersectionShell');bind('DepthWaterPlane','DepthWater');disable('SoftIntersectionParticles');
    fields=`  particleMaterial: ${ref(mat('DepthSoftParticle'))}\n  useSoftFade: 1\n  softness: 0.3\n`;
    driver.body=driver.body.replace(/  time: [^\n]+/,'  time: 1.5');
  }
  docs.push({type:114,id:'2109240017',body:`--- !u!114 &2109240017\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: ${rootGo.id}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: ${u.guid(root+'Scripts/Runtime/'+script+'.cs')}, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n  sequence: {fileID: ${driver.id}}\n${fields}`});
  u.write(p,scene.header+docs.map(d=>d.body).join(''));
  console.log(name+': '+script+' assigned; placeholders disabled.');
}
console.log('Backup: '+backup);
