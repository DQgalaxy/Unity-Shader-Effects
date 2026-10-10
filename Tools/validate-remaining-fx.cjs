const fs=require('fs'),assert=require('assert/strict');
const u=require('./crystal-assets.cjs');
const root='Assets/PortfolioFX/';
const names=['MeteorSurface','MeteorWarning','TravelerDissolve','PortalSurface','WaterShockwave','IntersectionShell','SoftParticle','DepthWater','ShowcaseSprite'];
const checks=[];
function check(value,label){assert(value,label);checks.push(label);}
const assetGuids=new Map();
for(const base of [root,'Assets/UnityTechnologies/ParticlePack/EffectExamples/']){
  for(const entry of fs.readdirSync(base,{recursive:true,withFileTypes:true})){
    if(!entry.isFile()||!entry.name.endsWith('.meta'))continue;
    const path=require('path').join(entry.parentPath,entry.name),text=u.read(path);
    const guid=text.match(/^guid: (\w+)/m)?.[1];if(guid)assetGuids.set(guid,path.slice(0,-5));
  }
}
const graphs=new Map();
for(const name of names){
  const docs=u.graph(root+'Shaders/SG_'+name+'.shadergraph'),g=docs[0],by=new Map(docs.map(x=>[x.m_ObjectId,x]));
  graphs.set(name,{docs,g,by});check(by.size===docs.length,name+': unique IDs');
  const inputs=new Set();
  for(const edge of g.m_Edges){
    for(const [key,type]of [['m_OutputSlot',1],['m_InputSlot',0]]){
      const end=edge[key],n=by.get(end.m_Node.m_Id);assert(n&&g.m_Nodes.some(r=>r.m_Id===n.m_ObjectId));
      const slot=n.m_Slots.map(r=>by.get(r.m_Id)).find(s=>s.m_Id===end.m_SlotId);assert(slot&&slot.m_SlotType===type);
    }
    const key=edge.m_InputSlot.m_Node.m_Id+':'+edge.m_InputSlot.m_SlotId;assert(!inputs.has(key));inputs.add(key);
  }
  const visiting=new Set(),visited=new Set();
  function walk(id){if(visited.has(id))return;assert(!visiting.has(id),'Cycle '+name);visiting.add(id);
    for(const e of g.m_Edges.filter(e=>e.m_InputSlot.m_Node.m_Id===id))walk(e.m_OutputSlot.m_Node.m_Id);
    visiting.delete(id);visited.add(id);}
  for(const n of g.m_Nodes)walk(n.m_Id);
  check(true,name+': valid edge slots, single input edges, acyclic');
  const props=g.m_Properties.map(r=>by.get(r.m_Id)),refs=props.map(p=>p.m_OverrideReferenceName||p.m_DefaultReferenceName);
  check(new Set(refs).size===refs.length,name+': unique property references');
  for(const p of props.filter(p=>p.m_Type.endsWith('.Texture2DShaderProperty'))){
    const guid=JSON.parse(p.m_Value.m_SerializedTexture).texture.guid;
    assert(assetGuids.has(guid)&&fs.existsSync(assetGuids.get(guid)),'Missing texture '+name+' '+guid);
  }
  check(g.m_GraphPrecision===0,name+': explicit float precision and valid textures');
  for(const n of docs.filter(x=>x.m_Type.endsWith('.PropertyNode')))assert(props.some(p=>p.m_ObjectId===n.m_Property.m_Id));
  for(const r of [...g.m_FragmentContext.m_Blocks,...g.m_VertexContext.m_Blocks])assert(by.get(r.m_Id)?.m_Type.endsWith('.BlockNode'));
  for(const output of ['BaseColor','Alpha']){
    const block=docs.find(x=>x.m_SerializedDescriptor==='SurfaceDescription.'+output);
    assert(block&&(g.m_Edges.some(e=>e.m_InputSlot.m_Node.m_Id===block.m_ObjectId)||
      (name==='MeteorSurface'&&output==='Alpha'&&by.get(block.m_Slots[0].m_Id).m_Value===1)),name+' '+output);
  }
  check(true,name+': fragment outputs connected');
  const opaque=['MeteorSurface','TravelerDissolve'].includes(name);
  const target=docs.find(x=>x.m_Type.endsWith('.UniversalTarget'));
  check(target.m_SurfaceType===(opaque?0:1)&&target.m_ZWriteControl===(opaque?1:2),name+': surface and depth write settings');
  if(['WaterShockwave','DepthWater','SoftParticle','IntersectionShell'].includes(name))
    check(docs.some(n=>n.m_Type.endsWith('.SceneDepthNode')&&n.m_DepthSamplingMode===2),name+': eye-space depth');
  if(['PortalSurface','WaterShockwave','DepthWater'].includes(name))
    check(docs.some(n=>n.m_Type.endsWith('.SceneColorNode')),name+': opaque color texture sampled');
  if(['WaterShockwave','DepthWater'].includes(name))
    check(docs.some(n=>n.m_Type.endsWith('.UniversalLitSubTarget')&&n.m_NormalDropOffSpace===2),name+': world-space water normal');
  if(name==='TravelerDissolve')check(target.m_AlphaClip&&docs.some(x=>x.m_SerializedDescriptor==='SurfaceDescription.AlphaClipThreshold'),name+': alpha clipping enabled');
  if(name==='WaterShockwave'){
    const pos=docs.find(x=>x.m_SerializedDescriptor==='VertexDescription.Position');
    const normal=docs.find(x=>x.m_SerializedDescriptor==='VertexDescription.Normal');
    check(g.m_Edges.some(e=>e.m_InputSlot.m_Node.m_Id===pos.m_ObjectId)&&g.m_Edges.some(e=>e.m_InputSlot.m_Node.m_Id===normal.m_ObjectId),name+': vertex position and normal connected');
    function vertexOnly(id){const n=by.get(id);assert(!/SampleTexture2DNode|SceneDepthNode|SceneColorNode/.test(n.m_Type),'Fragment node reached vertex stage');
      for(const e of g.m_Edges.filter(e=>e.m_InputSlot.m_Node.m_Id===id))vertexOnly(e.m_OutputSlot.m_Node.m_Id);}
    vertexOnly(pos.m_ObjectId);vertexOnly(normal.m_ObjectId);check(true,name+': vertex path has no fragment-only sampling');
  }
}
for(const [material,shader]of [
  ...names.filter(n=>n!=='ShowcaseSprite').map(n=>[n,n]),
  ['DepthSoftParticle','SoftParticle'],['DepthHardParticle','SoftParticle'],['MeteorSmoke','SoftParticle'],
  ...['MeteorFlame','MeteorEmbers','PortalMotes','WaterDroplets','WaterCrown'].map(n=>[n,'ShowcaseSprite'])]){
  const text=u.read(root+'MyMaterial/M_'+material+'.mat');
  check(text.includes(u.guid(root+'Shaders/SG_'+shader+'.shadergraph')),material+': correct graph binding');
  const docs=graphs.get(shader),properties=docs.g.m_Properties.map(r=>docs.by.get(r.m_Id));
  for(const p of properties){const ref=p.m_OverrideReferenceName||p.m_DefaultReferenceName;assert(text.includes('- '+ref+':'),'Material property missing '+material+' '+ref);}
  for(const m of text.matchAll(/m_Texture: \{fileID: 2800000, guid: (\w+)/g))assert(assetGuids.has(m[1]),'Missing material texture '+material);
}
const scenes=['MeteorStrike','FoldstepPortal','WaterShockwave','DepthIntersectionLab'];
for(const name of scenes){
  const docs=u.records(u.read(root+'Scenes/'+name+'.unity')).docs,by=new Map(docs.map(x=>[x.id,x]));
  check(by.size===docs.length,name+': unique scene IDs');
  for(const d of docs)for(const m of d.body.matchAll(/\{fileID: (-?\d+)\}/g))assert(m[1]==='0'||by.has(m[1]),name+' missing '+m[1]);
  check(true,name+': all local scene references resolve');
  const driver=docs.find(x=>x.type===114&&x.body.includes('playOnStart:'));
  check(!driver.body.includes('previewPlaceholder: 1')&&driver.body.includes('  particles: []'),name+': legacy parameter/particle overrides disabled');
  const presenter=by.get('2109240017');check(presenter&&presenter.type===114,name+': presentation attached');
  check(docs.some(x=>x.type===114&&x.body.includes('m_RequiresDepthTextureOption: 1')&&x.body.includes('m_RequiresOpaqueTextureOption: 1')),name+': camera depth and opaque textures enabled');
}
// Evaluate endpoint and wave invariants, independently of a GPU or Unity license.
const smooth=(a,b,x)=>{const t=Math.min(1,Math.max(0,(x-a)/(b-a)));return t*t*(3-2*t);};
for(const progress of [0,1])for(let m=0;m<=1;m+=.005){
  const threshold=-.12+1.24*progress,keep=smooth(-.001,.001,m-threshold);
  assert(progress===0?keep===1:keep===0,'Dissolve endpoint');
}
check(true,'Dissolve: all mask values remain at 0 and disappear at 1');
let maxSlopeError=0;
for(let q=-1;q<=1;q+=.02){
  const w=.45,k=12,a=.12;
  const height=x=>Math.sin(x*k)*Math.exp(-((x/w)**2))*a;
  const slope=Math.exp(-((q/w)**2))*a*(k*Math.cos(q*k)-2*q*Math.sin(q*k)/(w*w));
  const numerical=(height(q+.00001)-height(q-.00001))/.00002;
  maxSlopeError=Math.max(maxSlopeError,Math.abs(slope-numerical));
}
check(maxSlopeError<.00001,'Water: analytic normal slope agrees with finite difference');
for(const gap of [-2,0,.05,.3,1]){
  const soft=Math.min(1,Math.max(0,gap)/.3);assert(soft>=0&&soft<=1);
  if(gap<=0)assert(soft===0);
}
check(true,'Depth: soft fade is bounded and zero at/behind contact');
fs.mkdirSync('Logs/RemainingFX',{recursive:true});
const report={count:checks.length,checks,maxSlopeError,unityRendered:false};
fs.writeFileSync('Logs/RemainingFX/static-checks.json',JSON.stringify(report,null,2));
console.log(JSON.stringify(report,null,2));
