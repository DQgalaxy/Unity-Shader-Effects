const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const root = path.resolve(__dirname, '..');
process.chdir(root);
const source = 'F:/UnityProjects/1/Assets';
const fx = 'Assets/PortfolioFX';
const main = `${fx}/Shaders/SG_CrystalCollapse.shadergraph`;
const read = p => fs.readFileSync(p, 'utf8').replace(/^\uFEFF/, '');
const id = () => crypto.randomUUID().replaceAll('-', '');
const clone = o => JSON.parse(JSON.stringify(o));
function parseGraph(p) {
  const s = read(p), out = [];
  let depth = 0, quoted = false, escape = false, start = 0;
  for (let i = 0; i < s.length; i++) {
    const c = s[i];
    if (quoted) { if (escape) escape = false; else if (c === '\\') escape = true; else if (c === '"') quoted = false; continue; }
    if (c === '"') quoted = true;
    if (c === '{') { if (!depth) start = i; depth++; }
    if (c === '}' && --depth === 0) out.push(JSON.parse(s.slice(start, i + 1)));
  }
  if (depth || quoted) throw Error(`Malformed graph ${p}`);
  return out;
}
if (!process.env.CRYSTAL_BASELINE) throw Error('Set CRYSTAL_BASELINE to the backed-up original-graph.json before rebuilding.');
const old = JSON.parse(read(process.env.CRYSTAL_BASELINE)), oldMap = new Map(old.map(o => [o.m_ObjectId, o]));
const backup = `RepairBackups/CrystalReference_${new Date().toISOString().replace(/[:.]/g, '-')}`;
fs.mkdirSync(backup, {recursive:true});
for (const p of [main, `${fx}/MyMaterial/M_CrystalCore.mat`, `${fx}/MyMaterial/M_CrystalShard.mat`, `${fx}/Scenes/CrystalCollapse.unity`, `${fx}/Prefabs/CrystalCollapse_FX.prefab`, `${fx}/Animations/CrystalCollapse_DiamondPresentation.anim`]) {
  if (fs.existsSync(p)) fs.copyFileSync(p, `${backup}/${path.basename(p)}`);
}
fs.writeFileSync(`${backup}/original-graph.json`, JSON.stringify(old));
function meta(p, importer = 'DefaultImporter', guid = id()) {
  if (!fs.existsSync(p + '.meta')) fs.writeFileSync(p + '.meta', `fileFormatVersion: 2\nguid: ${guid}\n${importer}:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
  return read(p + '.meta').match(/guid: (\w+)/)[1];
}
const remap = new Map();
const resourceDir = `${fx}/ReferenceDiamond`;
function copyResource(rel) {
  const src = `${source}/Resources/6/CS07/${rel}`, dst = `${resourceDir}/${rel}`;
  fs.mkdirSync(path.dirname(dst), {recursive:true});
  const oldGuid = read(src + '.meta').match(/guid: (\w+)/)[1];
  if (!fs.existsSync(dst)) fs.copyFileSync(src, dst);
  if (!fs.existsSync(dst + '.meta')) fs.writeFileSync(dst + '.meta', read(src + '.meta').replace(oldGuid, id()));
  remap.set(oldGuid, read(dst + '.meta').match(/guid: (\w+)/)[1]);
}
for (const rel of ['Diamond.fbx', 'Crystal/Crystal01.obj', 'Crystal/card_ahri.dds', 'Crystal/card_ekko.dds', 'Crystal/card_zhaoxin.dds', 'Crystal/Crystal01_Normal.png', 'Crystal/Crystal01_Emission.png', 'Crystal/Crystal01_Emission2.png', 'Cubemap/GemRefractions.jpg', 'Cubemap/ReflectionProbe.cubemap']) copyResource(rel);
function replaceGuids(s) { for (const [a,b] of remap) s = s.replaceAll(a,b); return s; }
const dissolve = old.find(o => o.m_Type.endsWith('.SubGraphNode') && o.m_Name === 'SGN_DissolveEdge');
if (!dissolve) throw Error('Existing dissolve subgraph not found; no graphs changed.');
const required = new Set();
function visit(nodeId) {
  if (required.has(nodeId)) return;
  required.add(nodeId);
  for (const e of old[0].m_Edges.filter(e => e.m_InputSlot.m_Node.m_Id === nodeId)) visit(e.m_OutputSlot.m_Node.m_Id);
}
visit(dissolve.m_ObjectId);
function buildGraph(src, dst, kind) {
  const docs = parseGraph(src), graph = docs[0], map = new Map(docs.map(o=>[o.m_ObjectId,o]));
  function insert(o) { if (!map.has(o.m_ObjectId)) { docs.push(o); map.set(o.m_ObjectId,o); } }
  const nodeList = new Set(graph.m_Nodes.map(r=>r.m_Id));
  function addNode(o) { insert(o); if (!nodeList.has(o.m_ObjectId)) { graph.m_Nodes.push({m_Id:o.m_ObjectId}); nodeList.add(o.m_ObjectId); } }
  function link(from, slot, to, input) { graph.m_Edges.push({m_OutputSlot:{m_Node:{m_Id:from.m_ObjectId},m_SlotId:slot},m_InputSlot:{m_Node:{m_Id:to.m_ObjectId},m_SlotId:input}}); }
  const importedProperties = [];
  for (const nId of required) {
    const n = clone(oldMap.get(nId));
    n.m_Group = {m_Id:''};
    n.m_DrawState.m_Position.x += 1900;
    n.m_DrawState.m_Position.y += 2000;
    n.m_PreviewExpanded = false;
    addNode(n);
    for (const ref of n.m_Slots) insert(clone(oldMap.get(ref.m_Id)));
    if (n.m_Property) importedProperties.push(oldMap.get(n.m_Property.m_Id));
  }
  graph.m_Edges.push(...clone(old[0].m_Edges.filter(e=>required.has(e.m_InputSlot.m_Node.m_Id))));
  const categories = graph.m_CategoryData.map(r=>map.get(r.m_Id));
  function addProperty(p) {
    insert(clone(p));
    if (!graph.m_Properties.some(r=>r.m_Id===p.m_ObjectId)) {
      graph.m_Properties.push({m_Id:p.m_ObjectId});
      if (categories.length) categories[0].m_ChildObjectList.push({m_Id:p.m_ObjectId});
    }
  }
  importedProperties.forEach(addProperty);
  function property(reference, x, y) {
    const p = old.find(o=>o.m_DefaultReferenceName===reference);
    if (!p) throw Error(`Missing property ${reference}`);
    addProperty(p);
    const template = old.find(o=>o.m_Property?.m_Id===p.m_ObjectId);
    const n=clone(template); n.m_ObjectId=id(); n.m_Group={m_Id:''}; n.m_Slots=[];
    n.m_DrawState.m_Position={x,y,width:180,height:50};
    for (const r of template.m_Slots) {const s=clone(oldMap.get(r.m_Id));s.m_ObjectId=id();insert(s);n.m_Slots.push({m_Id:s.m_ObjectId});}
    addNode(n);return n;
  }
  function math(type, x,y, value) {
    const t=old.find(o=>o.m_Type===`UnityEditor.ShaderGraph.${type}Node`);
    const n=clone(t);n.m_ObjectId=id();n.m_Group={m_Id:''};n.m_Slots=[];n.m_PreviewExpanded=false;
    n.m_DrawState.m_Position={x,y,width:190,height:120};
    for(const r of t.m_Slots){const s=clone(oldMap.get(r.m_Id));s.m_ObjectId=id();if(s.m_Id===1 && value!==undefined) {
      if(typeof s.m_Value==='number')s.m_Value=value;
      else for(const k of Object.keys(s.m_Value))s.m_Value[k]=value;
    }insert(s);n.m_Slots.push({m_Id:s.m_ObjectId});}
    addNode(n);return n;
  }
  function block(descriptor) {
    const existing=docs.find(o=>o.m_SerializedDescriptor===descriptor);if(existing)return existing;
    const template=old.find(o=>o.m_SerializedDescriptor===descriptor);
    const n=clone(template);n.m_ObjectId=id();n.m_Slots=[];
    for(const r of template.m_Slots){const s=clone(oldMap.get(r.m_Id));s.m_ObjectId=id();insert(s);n.m_Slots.push({m_Id:s.m_ObjectId});}
    addNode(n);graph.m_FragmentContext.m_Blocks.push({m_Id:n.m_ObjectId});return n;
  }
  const base=block('SurfaceDescription.BaseColor');
  const colorEdge=graph.m_Edges.find(e=>e.m_InputSlot.m_Node.m_Id===base.m_ObjectId);
  graph.m_Edges=graph.m_Edges.filter(e=>e!==colorEdge);
  const sg=map.get(dissolve.m_ObjectId);
  const sgSlots=sg.m_Slots.map(r=>map.get(r.m_Id));
  const keepSlot=sgSlots.find(s=>s.m_DisplayName==='Keep').m_Id;
  const edgeSlot=sgSlots.find(s=>s.m_DisplayName==='Edge').m_Id;
  const strength=math('Multiply',2350,2250,3);
  link(sg,edgeSlot,strength,0);
  const tint=math('Multiply',2600,2250);link(strength,2,tint,0);link(property('_EdgeColor',2350,2450),0,tint,1);
  const gain=math('Multiply',2850,2250);link(tint,2,gain,0);link(property('_Intensity',2600,2450),0,gain,1);
  const sum=math('Add',3100,1600);link(map.get(colorEdge.m_OutputSlot.m_Node.m_Id),colorEdge.m_OutputSlot.m_SlotId,sum,0);link(gain,2,sum,1);link(sum,2,base,0);
  const alpha=block('SurfaceDescription.Alpha');
  graph.m_Edges=graph.m_Edges.filter(e=>e.m_InputSlot.m_Node.m_Id!==alpha.m_ObjectId);
  link(sg,keepSlot,alpha,0);
  const cutoff=block('SurfaceDescription.AlphaClipThreshold');map.get(cutoff.m_Slots[0].m_Id).m_Value=.5;
  // Keep endpoint behavior independent of noise texture extrema.
  const dissolvePropertyNode=docs.find(o=>o.m_Property && map.get(o.m_Property.m_Id)?.m_DefaultReferenceName==='_Dissolve');
  const thresholdEdge=graph.m_Edges.find(e=>e.m_OutputSlot.m_Node.m_Id===dissolvePropertyNode.m_ObjectId && e.m_InputSlot.m_Node.m_Id===sg.m_ObjectId);
  const mul=math('Multiply',1750,2800,1.04), sub=math('Subtract',2000,2800,.02);
  graph.m_Edges=graph.m_Edges.filter(e=>e!==thresholdEdge);link(dissolvePropertyNode,0,mul,0);link(mul,2,sub,0);link(sub,2,sg,thresholdEdge.m_InputSlot.m_SlotId);
  const target=docs.find(o=>o.m_Type.endsWith('.UniversalTarget'));
  target.m_AlphaClip=true; target.m_CastShadows=false;
  if(kind==='back')target.m_RenderFace=1;
  graph.m_Path='PortfolioFX';
  graph.m_StickyNoteDatas=[];
  const filtered=docs.filter(o=>!o.m_Type.endsWith('.StickyNoteData'));
  // No point-sparkle or Scene Color chains are retained in the reference shading.
  const known=new Map(filtered.map(o=>[o.m_ObjectId,o]));
  for(const e of graph.m_Edges)for(const [key,dir] of [['m_OutputSlot',1],['m_InputSlot',0]]) {
    const end=e[key],n=known.get(end.m_Node.m_Id);
    if(!n || !n.m_Slots.some(r=>known.get(r.m_Id)?.m_Id===end.m_SlotId && known.get(r.m_Id)?.m_SlotType===dir))throw Error(`Broken edge ${dst}`);
  }
  fs.writeFileSync(dst,replaceGuids(filtered.map(o=>JSON.stringify(o,null,4)).join('\n\n')+'\n'));
  if(!fs.existsSync(dst+'.meta'))fs.writeFileSync(dst+'.meta',read(main+'.meta').replace(/guid: \w+/,`guid: ${id()}`));
  console.log(`${dst}: ${graph.m_Nodes.length} nodes, ${graph.m_Edges.length} edges validated`);
}
buildGraph(`${source}/MyShaders/06_4CrystalCard.shadergraph`,main,'portrait');
buildGraph(`${source}/MyShaders/06_2Back.shadergraph`,`${fx}/Shaders/SG_CrystalBack.shadergraph`,'back');
buildGraph(`${source}/MyShaders/06_1Front.shadergraph`,`${fx}/Shaders/SG_CrystalDiamondFront.shadergraph`,'front');
fs.writeFileSync(`${backup}/manifest.json`,JSON.stringify({backup,resources:[...remap],main},null,2));
console.log(`Backup: ${backup}`);
