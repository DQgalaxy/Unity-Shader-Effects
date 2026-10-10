const fs=require('fs'),assert=require('assert'),u=require('./crystal-assets.cjs');
const fx='Assets/PortfolioFX',p=`${fx}/Shaders/SG_Hologram.shadergraph`;
const docs=u.graph(p),root=docs[0],map=new Map(docs.map(o=>[o.m_ObjectId,o]));
assert.equal(map.size,docs.length);
const inputs=new Set();
for(const e of root.m_Edges){
  for(const [key,dir]of [['m_OutputSlot',1],['m_InputSlot',0]]){
    const end=e[key],n=map.get(end.m_Node.m_Id);assert(n);
    const s=n.m_Slots.map(r=>map.get(r.m_Id)).find(s=>s.m_Id===end.m_SlotId);assert(s&&s.m_SlotType===dir);
  }
  const k=e.m_InputSlot.m_Node.m_Id+':'+e.m_InputSlot.m_SlotId;assert(!inputs.has(k));inputs.add(k);
}
const visited=new Set(),pending=new Set();
function visit(id){if(visited.has(id))return;assert(!pending.has(id),'Graph cycle');pending.add(id);for(const e of root.m_Edges.filter(e=>e.m_InputSlot.m_Node.m_Id===id))visit(e.m_OutputSlot.m_Node.m_Id);pending.delete(id);visited.add(id);}
root.m_Nodes.forEach(r=>visit(r.m_Id));
assert(!docs.some(o=>/\.(Arcsine|Arccosine|Fraction|Floor|Sine|Time)Node$/.test(o.m_Type)),'Periodic bands or old flashing logic');
assert(docs.some(o=>o.m_Type.endsWith('.UniversalLitSubTarget')));
const target=docs.find(o=>o.m_Type.endsWith('.UniversalTarget'));assert.equal(target.m_SurfaceType,0);assert.equal(target.m_ZWriteControl,1);assert.equal(target.m_RenderFace,2);assert(target.m_AlphaClip);
const vertex=root.m_VertexContext.m_Blocks.map(r=>map.get(r.m_Id)).find(o=>o.m_SerializedDescriptor==='VertexDescription.Position');
function checkVertex(id){const n=map.get(id);assert(!n.m_Type.endsWith('.SampleTexture2DNode'),'Fragment-only texture fetch in vertex path');for(const e of root.m_Edges.filter(e=>e.m_InputSlot.m_Node.m_Id===id))checkVertex(e.m_OutputSlot.m_Node.m_Id);}
checkVertex(vertex.m_ObjectId);
const vertexNodes=new Set();
function ancestors(id,result){if(result.has(id))return;result.add(id);for(const e of root.m_Edges.filter(e=>e.m_InputSlot.m_Node.m_Id===id))ancestors(e.m_OutputSlot.m_Node.m_Id,result);}
ancestors(vertex.m_ObjectId,vertexNodes);
const lodNodes=docs.filter(o=>o.m_Type.endsWith('.SampleTexture2DLODNode'));
assert.equal(lodNodes.length,2);
for(const block of docs.filter(o=>o.m_SerializedDescriptor?.startsWith('SurfaceDescription.'))){const upstream=new Set();ancestors(block.m_ObjectId,upstream);assert(lodNodes.every(n=>!upstream.has(n.m_ObjectId)),'Vertex noise must not pulse surface brightness or alpha');}
for(const sample of docs.filter(o=>o.m_Type.endsWith('.SampleTexture2DNode'))){
  const e=root.m_Edges.find(e=>e.m_InputSlot.m_Node.m_Id===sample.m_ObjectId&&e.m_InputSlot.m_SlotId===1);
  const property=map.get(map.get(e.m_OutputSlot.m_Node.m_Id).m_Property.m_Id);
  assert.equal(sample.m_TextureType,property.m_DefaultReferenceName==='_NormalMap'?1:0);
}
const props=docs.filter(o=>o.m_Type.endsWith('ShaderProperty')),mat=u.read(`${fx}/MyMaterial/M_HologramBody.mat`);
assert(!mat.includes('}}'));
for(const prop of props){const key=prop.m_DefaultReferenceName;assert(mat.includes(`- ${key}:`));if(prop.m_Type.endsWith('Texture2DShaderProperty')){const tex=JSON.parse(prop.m_Value.m_SerializedTexture).texture;assert(tex.guid);assert(mat.includes(tex.guid));}}
const scene=u.records(u.read(`${fx}/Scenes/HologramMaterialize.unity`)),objects=new Map(scene.docs.map(o=>[o.id,o]));
assert.equal(objects.size,scene.docs.length);
for(const obj of scene.docs)for(const m of obj.body.matchAll(/\{fileID: (-?\d+)\}/g))if(m[1]!=='0')assert(objects.has(m[1]),'Dangling scene reference '+m[1]);
const driver=scene.docs.find(d=>d.type===114&&d.body.includes('  materials:'));
assert(driver.body.includes('target: {fileID: 1909202602}'));
assert(driver.body.includes('start: 8.1'));
assert(objects.get('1909202603').body.includes(u.guid(`${fx}/Models/HologramTransportTube.asset`)));
assert(objects.get('1909202602').body.includes(u.guid(`${fx}/MyMaterial/M_HologramTransport.mat`)));
assert(driver.body.includes('duration: 10'));assert(!driver.body.includes('target: {fileID: 70143867}'));
assert(driver.body.match(/target: \{fileID: 316075090\}[\s\S]*?previewPlaceholder: 0/));
assert(objects.get('70143867').body.includes('m_Enabled: 0'));
assert(driver.body.includes(u.guid(`${fx}/Animations/HologramMaterialize_DetailedSequence.anim`)));
const clip=u.read(`${fx}/Animations/HologramMaterialize_DetailedSequence.anim`);assert(clip.includes('m_StopTime: 10'));assert(clip.includes('path: HologramBody'));assert(clip.includes('m_PositionCurves: []'));assert(!clip.includes('path: ScanRing'));
const clamp=x=>Math.max(0,Math.min(1,x));
const smooth=(a,b,x)=>{const s=clamp((x-a)/(b-a));return s*s*(3-2*s);};
let endpointTests=0;
for(const amount of [0,.035,.18,.4])for(const width of [.005,.045,.08])for(let n=0;n<=10;n++)for(let h=0;h<=100;h++){
  const margin=amount*.5+width*2+.03;
  const d0=-margin-h/100-(n/10-.5)*amount,d1=1+margin-h/100-(n/10-.5)*amount;
  assert.equal(smooth(-.004,.004,d0),0);assert.equal(smooth(-.004,.004,d1),1);
  assert.equal(1-smooth(0,width,Math.abs(d0)),0);assert.equal(1-smooth(0,width,Math.abs(d1)),0);endpointTests++;
}
const beamEnvelope=p=>smooth(0,.08,p)*(1-smooth(.88,1,p));
assert.equal(beamEnvelope(0),0);assert.equal(beamEnvelope(1),0);assert(beamEnvelope(.5)>.99);
const bd=u.graph(`${fx}/Shaders/SG_HologramTransport.shadergraph`),bm=new Map(bd.map(o=>[o.m_ObjectId,o]));
const bt=bd.find(o=>o.m_Type.endsWith('.UniversalTarget'));assert.equal(bt.m_AlphaMode,2);assert.equal(bt.m_ZWriteControl,2);
for(const e of bd[0].m_Edges)for(const [key,dir]of [['m_OutputSlot',1],['m_InputSlot',0]]){const s=e[key],node=bm.get(s.m_Node.m_Id);assert(node);assert(node.m_Slots.some(r=>bm.get(r.m_Id).m_Id===s.m_SlotId&&bm.get(r.m_Id).m_SlotType===dir));}
const mesh=u.read(`${fx}/Models/HologramTransportTube.asset`),data=Buffer.from(mesh.match(/_typelessdata: ([a-f0-9]+)/i)[1],'hex');
assert.equal(data.length,258*32);
for(let i=0;i<258;i++){
  const at=i*32,x=data.readFloatLE(at),y=data.readFloatLE(at+4),z=data.readFloatLE(at+8);
  assert(Math.abs(Math.hypot(x,z)-.88)<.00001);assert(y===0||y===3.5);
}
const dir='Logs/HologramUpgrade';fs.mkdirSync(dir,{recursive:true});
const report={nodes:root.m_Nodes.length,connections:root.m_Edges.length,beamNodes:bd[0].m_Nodes.length,endpointTests,checks:'PASS: graph topology; opaque depth-writing cutout; correct detail texture sample types; vertex-only flowing signal distortion; no brightness/opacity pulses; scene transport bindings; endpoint parameter sweeps; beam geometry and envelope. Unity render verification still required.'};
u.write(`${dir}/static-result.json`,JSON.stringify(report,null,2));console.log(report);
