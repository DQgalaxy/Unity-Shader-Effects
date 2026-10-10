const fs=require('fs'),assert=require('assert/strict'),crypto=require('crypto');
const u=require('./crystal-assets.cjs');
const fx='Assets/PortfolioFX';
const original=JSON.parse(u.read('RepairBackups/ShieldUpgrade_2026-09-18T09-15-48-711Z/manifest.json'));
for(const [p,h]of Object.entries(original.originalGraphs))assert.equal(crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex'),h,`Original changed: ${p}`);
const guidMap=new Map();
function scan(dir){for(const e of fs.readdirSync(dir,{withFileTypes:true})){const p=`${dir}/${e.name}`;if(e.isDirectory())scan(p);else if(p.endsWith('.meta')){const g=u.read(p).match(/^guid: (\w+)/m);if(g)guidMap.set(g[1],p.slice(0,-5));}}}scan('Assets');
let count=0;
for(const name of ['SG_ReactiveShield_HexSequence','SG_ShieldImpactLayers']){
 const p=`${fx}/Shaders/${name}.shadergraph`,docs=u.graph(p),map=new Map(docs.map(o=>[o.m_ObjectId,o]));
 assert.equal(docs.length,map.size,'Duplicate object IDs');
 const inputs=new Set();
 for(const e of docs[0].m_Edges){for(const [key,direction] of [['m_InputSlot',0],['m_OutputSlot',1]]){
  const end=e[key],n=map.get(end.m_Node.m_Id);assert(n,'Missing node');assert(n.m_Slots.some(r=>map.get(r.m_Id)?.m_Id===end.m_SlotId&&map.get(r.m_Id)?.m_SlotType===direction),'Invalid slot');
 }const key=e.m_InputSlot.m_Node.m_Id+':'+e.m_InputSlot.m_SlotId;assert(!inputs.has(key),'Duplicate input');inputs.add(key);count++;}
 for(const n of docs.filter(o=>o.m_Property))assert(map.has(n.m_Property.m_Id),'Missing blackboard property');
 for(const n of docs.filter(o=>o.m_Value?.m_SerializedTexture)){const t=JSON.parse(n.m_Value.m_SerializedTexture).texture;assert(guidMap.has(t.guid),`Missing texture ${t.guid}`);}
 for(const n of docs.filter(o=>o.m_Type.endsWith('CustomFunctionNode')))assert(guidMap.has(n.m_FunctionSource),'Missing HLSL');
 assert(!docs.some(o=>o.m_Type.endsWith('TimeNode')),'Nondeterministic preview clock');
 const visited=new Set(),active=new Set();
 function visit(id){assert(!active.has(id),'Graph cycle');if(visited.has(id))return;active.add(id);for(const e of docs[0].m_Edges.filter(e=>e.m_InputSlot.m_Node.m_Id===id))visit(e.m_OutputSlot.m_Node.m_Id);active.delete(id);visited.add(id);}
 docs[0].m_Nodes.forEach(r=>visit(r.m_Id));
}
function local(p){const s=u.records(u.read(p)),ids=new Set(s.docs.map(d=>d.id));assert.equal(ids.size,s.docs.length,'Duplicate scene object');for(const d of s.docs)for(const r of d.body.matchAll(/\{fileID: (-?\d+)\}/g))if(r[1]!=='0')assert(ids.has(r[1]),`${p}: dangling fileID ${r[1]}`);return s;}
const scene=local(`${fx}/Scenes/ReactiveShield.unity`);local(`${fx}/Prefabs/ReactiveShield_FX.prefab`);
const controller=scene.docs.find(d=>d.body.includes('  honeycombPrefab:'));
assert(controller.body.includes(u.guid(`${fx}/ReferenceShield/ReferenceHexagon.fbx`)),'Missing source model reference');
assert.equal(fs.readFileSync(`${fx}/ReferenceShield/ReferenceHexagon.fbx`).compare(fs.readFileSync('F:/UnityProjects/1/Assets/Resources/13/hexagon.FBX')),0,'Model differs from source');
const driver=scene.docs.find(d=>d.id==='242489122');assert(driver.body.includes('particles: []'),'Legacy particle schedule still enabled');
assert.equal((scene.docs.find(d=>d.id==='1854099856').body.match(/  - clip:/g)||[]).length,6,'Missing Ellen cues');
for(const [mat,shader]of [['M_ReactiveShield','SG_ReactiveShield_HexSequence'],['M_ShieldImpacting','SG_ShieldImpactLayers']])assert(u.read(`${fx}/MyMaterial/${mat}.mat`).includes(u.guid(`${fx}/Shaders/${shader}.shadergraph`)));
assert(!/Input\.|Mouse|Raycast/.test(u.read(`${fx}/Scripts/Runtime/FXShieldPresentation.cs`)),'Mouse dependency found');
let rsp=u.read('Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp-Editor.rsp').replace('Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.ref.dll','Temp/ShieldRuntimeCheck.ref.dll').replace(/^-out:.+$/m,'-out:"Temp/ShieldEditorCheck.dll"').replace(/^-refout:.+$/m,'-refout:"Temp/ShieldEditorCheck.ref.dll"');
if(!rsp.includes('ShieldPresentationVerification.cs'))rsp+='\n"Assets/PortfolioFX/Editor/ShieldPresentationVerification.cs"\n';
u.write('Temp/ShieldEditorCheck.rsp',rsp);
console.log(`PASS: ${count} graph connections, original graph hashes, model bytes, scene/prefab references, six Ellen cues, material bindings.`);
