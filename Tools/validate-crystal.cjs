const fs=require('fs');
const path=require('path');
const assert=require('assert/strict');
const u=require('./crystal-assets.cjs');
process.chdir(path.resolve(__dirname,'..'));
const fx='Assets/PortfolioFX';
const guids=new Map();
function scan(dir){for(const ent of fs.readdirSync(dir,{withFileTypes:true})){const p=`${dir}/${ent.name}`;if(ent.isDirectory())scan(p);else if(p.endsWith('.meta')){const match=u.read(p).match(/^guid: (\w+)/m);if(match)guids.set(match[1],p.slice(0,-5));}}}
scan('Assets');
function externalRefs(text){return [...text.matchAll(/guid[\\"\s]*:[\\"\s]*([a-f0-9]{32})/g)].map(m=>m[1]);}
function localRefs(p){const {docs}=u.records(u.read(p)),ids=new Set(docs.map(d=>d.id));assert.equal(ids.size,docs.length,`${p}: duplicate fileIDs`);for(const d of docs)for(const m of d.body.matchAll(/\{fileID: (-?\d+)\}/g))if(m[1]!=='0')assert(ids.has(m[1]),`${p}: missing local fileID ${m[1]}`);return docs;}
let edges=0;
for(const name of ['SG_CrystalCollapse','SG_CrystalBack','SG_CrystalDiamondFront']){
  const p=`${fx}/Shaders/${name}.shadergraph`,docs=u.graph(p),map=new Map(docs.map(o=>[o.m_ObjectId,o]));
  assert.equal(map.size,docs.length,`${name}: duplicate object IDs`);
  const inputs=new Set();
  for(const e of docs[0].m_Edges){
    for(const [key,dir] of [['m_OutputSlot',1],['m_InputSlot',0]]){
      const end=e[key],node=map.get(end.m_Node.m_Id);assert(node,`${name}: missing node`);
      assert(node.m_Slots.some(r=>map.get(r.m_Id)?.m_Id===end.m_SlotId&&map.get(r.m_Id)?.m_SlotType===dir),`${name}: invalid slot`);
    }
    const input=`${e.m_InputSlot.m_Node.m_Id}:${e.m_InputSlot.m_SlotId}`;assert(!inputs.has(input),`${name}: two edges into one input`);inputs.add(input);
  }
  for(const g of externalRefs(u.read(p)))assert(guids.has(g),`${name}: unresolved asset ${g}`);
  assert(docs.find(o=>o.m_Type.endsWith('.UniversalTarget')).m_AlphaClip);
  assert(!docs.some(o=>/SceneColorNode|VoronoiNode|TimeNode/.test(o.m_Type)),`${name}: unwanted sparkle/screen color node`);
  const sg=docs.find(o=>o.m_Name==='SGN_CrystalDissolve');assert(sg);
  const sub=u.graph(`${fx}/SubGraphs/SGN_CrystalDissolve.shadersubgraph`);
  const props=sub[0].m_Properties.map(r=>sub.find(o=>o.m_ObjectId===r.m_Id));
  for(const g of sg.m_PropertyGuids)assert(props.some(p=>p.m_Guid.m_GuidSerialized===g),'Subgraph property mismatch');
  edges+=docs[0].m_Edges.length;
}
const scene=localRefs(`${fx}/Scenes/CrystalCollapse.unity`);
localRefs(`${fx}/Prefabs/CrystalCollapse_FX.prefab`);
localRefs(`${fx}/Characters/Ellen/EllenPortraitModel.prefab`);
const previous=u.read('RepairBackups/CrystalReference_2026-09-18T06-58-51-968Z/CrystalCollapse.unity');
const previousRefs=new Set(externalRefs(previous));
for(const g of externalRefs(u.read(`${fx}/Scenes/CrystalCollapse.unity`)))assert(guids.has(g)||previousRefs.has(g),`New unresolved scene asset ${g}`);
assert.equal(scene.filter(d=>d.type===1&&/m_Name: Shard_\d+\n/.test(d.body)).length,12);
const capture=scene.find(d=>d.type===114&&d.body.includes('  ellenPrefab:'));assert(capture,'Capture component missing');
assert(capture.body.includes(u.guid(`${fx}/Characters/Ellen/EllenPortraitModel.prefab`)));
const core=scene.find(d=>d.id==='1426640268');assert(core.body.includes(u.guid(`${fx}/Models/CrystalDiamond/CrystalPortrait.asset`)));
const portrait=u.graph(`${fx}/Shaders/SG_CrystalCollapse.shadergraph`);
assert(!JSON.stringify(portrait.find(o=>o.m_DefaultReferenceName==='_InsideTex')).includes('a3bbb094d7434d618eef4ffb6d623bf0'));
assert(u.read(`${fx}/MyMaterial/M_CrystalCore.mat`).includes(u.guid(`${fx}/Shaders/SG_CrystalCollapse.shadergraph`)));
assert(u.read(`${fx}/MyMaterial/M_CrystalShard.mat`).includes(u.guid(`${fx}/Shaders/SG_CrystalDiamondFront.shadergraph`)));
const mesh=u.read(`${fx}/Models/CrystalDiamond/CrystalPortrait.asset`);
const count=+mesh.match(/m_VertexCount: (\d+)/)[1];
assert.equal(mesh.match(/_typelessdata: ([a-f0-9]+)/)[1].length,count*48*2);
assert.equal(mesh.match(/m_IndexBuffer: ([a-f0-9]+)/)[1].length,count*2*2);

// Evaluate scalar dissolve logic from serialized nodes, not a separate guessed formula.
function evaluator(docs,params={}){
  const map=new Map(docs.map(o=>[o.m_ObjectId,o])),cache=new Map();
  const scalar=v=>typeof v==='number'?v:v?.e00??v?.x??0;
  function input(n,slot){const e=docs[0].m_Edges.find(e=>e.m_InputSlot.m_Node.m_Id===n.m_ObjectId&&e.m_InputSlot.m_SlotId===slot);if(e)return out(e.m_OutputSlot.m_Node.m_Id,e.m_OutputSlot.m_SlotId);return scalar(map.get(n.m_Slots.find(r=>map.get(r.m_Id).m_Id===slot).m_Id).m_Value);}
  function out(id,slot){const key=id+':'+slot;if(cache.has(key))return cache.get(key);const n=map.get(id),type=n.m_Type.split('.').pop();let v;
    const a=()=>input(n,0),b=()=>input(n,1),c=()=>input(n,2),sat=x=>Math.max(0,Math.min(1,x));
    switch(type){
      case 'PropertyNode':{const p=map.get(n.m_Property.m_Id);v=params[p.m_DefaultReferenceName]??scalar(p.m_Value);break;}
      case 'PositionNode':v=[0,params.y??0,0];break;
      case 'SplitNode':v=a()[slot-1];break;
      case 'SampleTexture2DNode':v=params.noise??.5;break;
      case 'AddNode':v=a()+b();break;
      case 'SubtractNode':v=a()-b();break;
      case 'MultiplyNode':v=a()*b();break;
      case 'DivideNode':v=a()/b();break;
      case 'MaximumNode':v=Math.max(a(),b());break;
      case 'SaturateNode':v=sat(a());break;
      case 'OneMinusNode':v=1-a();break;
      case 'StepNode':v=b()>=a()?1:0;break;
      case 'LerpNode':v=a()+(b()-a())*c();break;
      case 'SmoothstepNode':{const t=sat((c()-a())/(b()-a()));v=t*t*(3-2*t);break;}
      case 'SubGraphOutputNode':v=input(n,slot);break;
      default:throw Error(`Unhandled scalar evaluator node ${type}`);
    }cache.set(key,v);return v;
  }return {out,input};
}
const sg=portrait.find(o=>o.m_Name==='SGN_CrystalDissolve');
const dissolveDocs=u.graph(`${fx}/SubGraphs/SGN_CrystalDissolve.shadersubgraph`),output=dissolveDocs.find(o=>o.m_Type.endsWith('.SubGraphOutputNode'));
const slots=sg.m_Slots.map(r=>portrait.find(o=>o.m_ObjectId===r.m_Id));
for(const y of [-1.4,-.7,0,.7,1.4])for(const noise of [0,.25,.5,.75,1])for(const progress of [0,.5,1]){
  const ev=evaluator(portrait,{y,noise,_HeightMin:-1.4,_HeightRange:2.8,_Dissolve:progress,_EdgeWidth:.045});
  const mask=ev.input(sg,slots.find(s=>s.m_DisplayName==='Mask').m_Id);
  assert(mask>=0&&mask<=1.000001,`Unnormalized mask ${mask}`);
  const p=ev.input(sg,slots.find(s=>s.m_DisplayName==='Progress').m_Id);
  const sub=evaluator(dissolveDocs,{_Mask:mask,_Progress:p,_Width:.045});
  const keep=sub.out(output.m_ObjectId,1),edge=sub.out(output.m_ObjectId,2);
  if(progress===0)assert.equal(keep,1,'Dissolve=0 clipped a vertex');
  if(progress===1)assert.equal(keep,0,'Dissolve=1 kept a vertex');
  assert(edge>=0&&edge<=1,'Edge is outside [0,1]');
}
const band=evaluator(dissolveDocs,{_Mask:.51,_Progress:.5,_Width:.045});
assert(band.out(output.m_ObjectId,2)>.5,'Visible edge band is absent');
const clip=u.read(`${fx}/Animations/CrystalCollapse_EllenPresentation.anim`);
assert(clip.includes('m_StopTime: 10'));
const sections=new Map([...clip.matchAll(/^  (m_\w+Curves):\n([\s\S]*?)(?=^  m_)/gm)].map(m=>[m[1],m[2]]));
for(const section of ['m_PositionCurves','m_ScaleCurves']){
  const paths=[...sections.get(section).matchAll(/    path: (Shard_\d+)/g)].map(m=>m[1]);assert.equal(new Set(paths).size,12);
  for(const b of sections.get(section).matchAll(/  - curve:\n([\s\S]*?)    path: (Shard_\d+)\n/g)){
    const times=[...b[1].matchAll(/        time: ([\d.]+)/g)].map(m=>+m[1]);assert.equal(times.length,8);assert(times.every((v,i)=>!i||v>times[i-1]));
  }
}
// Compile the editor assembly against the freshly checked runtime assembly.
let rsp=u.read('Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp-Editor.rsp')
  .replace('Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.ref.dll','Temp/CrystalRuntimeCheck.ref.dll')
  .replace(/^-out:.+$/m,'-out:"Temp/CrystalEditorCheck.dll"')
  .replace(/^-refout:.+$/m,'-refout:"Temp/CrystalEditorCheck.ref.dll"');
u.write('Temp/CrystalEditorCheck.rsp',rsp);
console.log(`PASS: ${edges} graph edges; scene/prefab references; ${count} mesh vertices; 75 dissolve samples; 12 position/scale tracks.`);
