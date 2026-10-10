const fs = require('fs');
const path = require('path');
const u = require('./crystal-assets.cjs');
process.chdir(path.resolve(__dirname, '..'));
const fx = 'Assets/PortfolioFX';
const baseline = 'RepairBackups/CrystalReference_2026-09-18T06-58-51-968Z';
const scenePath = `${fx}/Scenes/CrystalCollapse.unity`;
const meshPath = `${fx}/Models/CrystalDiamond/CrystalPortrait.asset`;
const clipPath = `${fx}/Animations/CrystalCollapse_EllenPresentation.anim`;
const portraitScript = `${fx}/Scripts/Runtime/FXCrystalEllenPortrait.cs`;
const prefabPath = `${fx}/Characters/Ellen/EllenPortraitModel.prefab`;
const num = x => +x.toFixed(7);
const vec = a => `{x: ${num(a[0])}, y: ${num(a[1])}, z: ${num(a[2])}}`;
const backup = `RepairBackups/CrystalScene_${new Date().toISOString().replace(/[:.]/g, '-')}`;
fs.mkdirSync(backup, {recursive:true});
for (const p of [scenePath, `${fx}/MyMaterial/M_CrystalCore.mat`, `${fx}/MyMaterial/M_CrystalShard.mat`, `${fx}/Prefabs/CrystalCollapse_FX.prefab`]) fs.copyFileSync(p, `${backup}/${path.basename(p)}`);
// Keep the user's original graph name/GUID on the hero crystal, not on its satellites.
const legacyPortrait=`${fx}/Shaders/SG_CrystalPortrait.shadergraph`;
if(fs.existsSync(legacyPortrait)) {
  const main=`${fx}/Shaders/SG_CrystalCollapse.shadergraph`,front=`${fx}/Shaders/SG_CrystalDiamondFront.shadergraph`;
  u.write(front,u.read(main));u.write(main,u.read(legacyPortrait));
  fs.renameSync(legacyPortrait+'.meta',front+'.meta');fs.unlinkSync(legacyPortrait);
}

// Flatten OBJ face-corners without welding: preserve the reference mesh's hard edges and UV seams.
const positions = [], normals = [], uv = [], corners = [];
for (const line of u.read(`${fx}/ReferenceDiamond/Crystal/Crystal01.obj`).split('\n')) {
  const [tag,...args] = line.trim().split(/\s+/);
  if (tag === 'v') positions.push(args.map(Number));
  if (tag === 'vn') normals.push(args.map(Number));
  if (tag === 'vt') uv.push(args.map(Number));
  if (tag === 'f') for (let j = 1; j < args.length - 1; j++) {
    // Mirror X into Unity's handedness and reverse winding along with it.
    for (const token of [args[0],args[j+1],args[j]]) corners.push(token.split('/').map(v=>+v-1));
  }
}
const min = [0,1,2].map(i=>Math.min(...positions.map(p=>p[i])));
const max = [0,1,2].map(i=>Math.max(...positions.map(p=>p[i])));
const center = min.map((v,i)=>(v+max[i])/2), scale = 2.8/(max[1]-min[1]);
const extent = min.map((v,i)=>(max[i]-v)*scale/2);
const vertices = corners.map(([p,t,n])=>({
  p:positions[p].map((v,i)=>(v-center[i])*scale*(i===0?-1:1)),
  n:normals[n].map((v,i)=>v*(i===0?-1:1)), uv:uv[t].slice(0,2)
}));
const sub = (a,b)=>a.map((v,i)=>v-b[i]);
const dot = (a,b)=>a.reduce((v,x,i)=>v+x*b[i],0);
const cross = (a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
const normalize = a=>{const l=Math.hypot(...a);if(l<1e-9)throw Error('Degenerate tangent');return a.map(v=>v/l);};
for (let i=0;i<vertices.length;i+=3) {
  const [a,b,c]=vertices.slice(i,i+3), e1=sub(b.p,a.p),e2=sub(c.p,a.p),d1=sub(b.uv,a.uv),d2=sub(c.uv,a.uv);
  const det=d1[0]*d2[1]-d1[1]*d2[0];
  const t=Math.abs(det)>1e-9?e1.map((v,k)=>(v*d2[1]-e2[k]*d1[1])/det):[1,0,0];
  const bt=Math.abs(det)>1e-9?e2.map((v,k)=>(v*d1[0]-e1[k]*d2[0])/det):[0,1,0];
  for(const v of [a,b,c]) {
    v.n=normalize(v.n);
    const ortho=sub(t,v.n.map(x=>x*dot(v.n,t)));
    const tangent=normalize(Math.hypot(...ortho)>1e-9?ortho:cross(v.n,[0,0,1]));
    v.t=[...tangent,dot(cross(v.n,tangent),bt)<0?-1:1];
  }
}
if(vertices.length>65535)throw Error('Mesh needs UInt32 indices');
const data=Buffer.alloc(vertices.length*48),indices=Buffer.alloc(vertices.length*2);
vertices.forEach((v,i)=>{[...v.p,...v.n,...v.t,...v.uv].forEach((x,j)=>data.writeFloatLE(x,i*48+j*4));indices.writeUInt16LE(i,i*2);});
let mesh=u.read(`${fx}/Models/CoreCrystal.asset`)
  .replace('m_Name: CoreCrystal','m_Name: CrystalPortrait')
  .replace(/indexCount: \d+/g,`indexCount: ${vertices.length}`)
  .replace(/vertexCount: \d+/g,`vertexCount: ${vertices.length}`)
  .replace(/m_VertexCount: \d+/g,`m_VertexCount: ${vertices.length}`)
  .replace(/m_IndexBuffer: [a-f0-9]*/g,`m_IndexBuffer: ${indices.toString('hex')}`)
  .replace(/m_DataSize: \d+/g,`m_DataSize: ${data.length}`)
  .replace(/_typelessdata: [a-f0-9]*/g,`_typelessdata: ${data.toString('hex')}`)
  .replace(/m_Extent: \{[^}]+\}/g,`m_Extent: ${vec(extent)}`);
u.write(meshPath,mesh);u.meta(meshPath,'NativeFormatImporter',4300000);

// Keep the user's shared subgraph untouched: repair this effect in a private copy.
const dissolvePath=`${fx}/SubGraphs/SGN_CrystalDissolve.shadersubgraph`;
const dissolveGraph=u.graph(`${fx}/SubGraphs/SGN_DissolveEdge.shadersubgraph`);
const dissolveMap=new Map(dissolveGraph.map(o=>[o.m_ObjectId,o]));
const threshold=dissolveGraph.find(o=>o.m_Type.endsWith('.LerpNode'));
for(const r of threshold.m_Slots){const s=dissolveMap.get(r.m_Id);if(s.m_Id===0)s.m_Value={x:-.001,y:0,z:0,w:0};if(s.m_Id===1)s.m_Value={x:1.001,y:0,z:0,w:0};}
const difference=dissolveGraph.find(o=>o.m_Type.endsWith('.SubtractNode'));
const smooth=dissolveGraph.find(o=>o.m_Type.endsWith('.SmoothstepNode'));
const edgeInput=dissolveGraph[0].m_Edges.find(e=>e.m_InputSlot.m_Node.m_Id===smooth.m_ObjectId&&e.m_InputSlot.m_SlotId===2);
edgeInput.m_OutputSlot={m_Node:{m_Id:difference.m_ObjectId},m_SlotId:2};
u.writeGraph(dissolvePath,dissolveGraph);
if(!fs.existsSync(dissolvePath+'.meta'))u.write(dissolvePath+'.meta',u.read(`${fx}/SubGraphs/SGN_DissolveEdge.shadersubgraph.meta`).replace(/^guid: \w+/m,`guid: ${u.id()}`));
for(const name of ['SG_CrystalCollapse','SG_CrystalBack','SG_CrystalDiamondFront']){
  const p=`${fx}/Shaders/${name}.shadergraph`,docs=u.graph(p);
  const node=docs.find(o=>o.m_Type.endsWith('.SubGraphNode')&&/^SGN_(DissolveEdge|CrystalDissolve)$/.test(o.m_Name));
  node.m_Name='SGN_CrystalDissolve';
  const ref=JSON.parse(node.m_SerializedSubGraph);ref.subGraph.guid=u.guid(dissolvePath);node.m_SerializedSubGraph=JSON.stringify(ref);
  u.writeGraph(p,docs);
}
const portraitGraphPath=`${fx}/Shaders/SG_CrystalCollapse.shadergraph`;
const portraitGraph=u.graph(portraitGraphPath);
const inside=portraitGraph.find(o=>o.m_DefaultReferenceName==='_InsideTex');
inside.m_Value={m_SerializedTexture:'{"texture":{"instanceID":0}}',m_Guid:''};
inside.m_DefaultType=1; // Black fallback; no source card character remains bound.
const normalNode=portraitGraph.find(o=>o.m_ObjectId==='a7c583cbf8cc47d8a7eb042756d0bce4');
for(const e of portraitGraph[0].m_Edges)if(e.m_OutputSlot.m_Node.m_Id===normalNode.m_ObjectId)e.m_OutputSlot.m_SlotId=0;
u.writeGraph(portraitGraphPath,portraitGraph);

function material(name,graphName,overrides,queue=-1) {
  const graphPath=`${fx}/Shaders/${graphName}.shadergraph`,docs=u.graph(graphPath),map=new Map(docs.map(o=>[o.m_ObjectId,o]));
  const tex=[],floats=[],colors=[];
  for(const ref of docs[0].m_Properties) {
    const p=map.get(ref.m_Id),key=p.m_OverrideReferenceName||p.m_DefaultReferenceName;
    const value=overrides[key]??p.m_Value;
    if(p.m_Type.endsWith('Texture2DShaderProperty')||p.m_Type.endsWith('CubemapShaderProperty')) {
      const json=JSON.parse(value.m_SerializedTexture||value.m_SerializedCubemap);
      const v=json.texture||json.cubemap;
      const r=v.guid?`{fileID: ${v.fileID}, guid: ${v.guid}, type: ${v.type}}`:'{fileID: 0}';
      tex.push(`    - ${key}:\n        m_Texture: ${r}\n        m_Scale: {x: 1, y: 1}\n        m_Offset: {x: 0, y: 0}`);
    } else if(typeof value==='number')floats.push(`    - ${key}: ${num(value)}`);
    else {
      const vals=['r','g','b','a'].map((c,i)=>value[c]??value['xyzw'[i]]??0);
      colors.push(`    - ${key}: {r: ${num(vals[0])}, g: ${num(vals[1])}, b: ${num(vals[2])}, a: ${num(vals[3])}}`);
    }
  }
  const p=`${fx}/MyMaterial/${name}.mat`;
  u.write(p,`%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_Name: ${name}\n  m_Shader: {fileID: -6465566751694194690, guid: ${u.guid(graphPath)}, type: 3}\n  m_Parent: {fileID: 0}\n  m_ModifiedSerializedProperties: 0\n  m_ValidKeywords: []\n  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 0\n  m_DoubleSidedGI: 0\n  m_CustomRenderQueue: ${queue}\n  stringTagMap: {}\n  disabledShaderPasses: []\n  m_LockedProperties: \n  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs:\n${tex.join('\n')}\n    m_Ints: []\n    m_Floats:\n${floats.join('\n')}\n    m_Colors:\n${colors.join('\n')}\n  m_BuildTextureStacks: []\n  m_AllowLocking: 1\n`);
  u.meta(p,'NativeFormatImporter',2100000);return u.guid(p);
}
const white={r:1,g:1,b:1,a:1};
const coreMat=material('M_CrystalCore','SG_CrystalCollapse',{
  _Tint:white,_RimColor:{r:.18,g:.38,b:1,a:1},_GemStrength:1,_ReflectionStrength:1.3,
  _NormalStrength:.65,_CrackStrength:.4,_RimStrength:.65,_RimPower:3,
  _ParallaxDepth:.065,_UVDistort:0,_InsideTiling:{x:1,y:1},_InsideOffset:{x:0,y:0},
  _HeightMin:-1.4,_HeightRange:2.8,_EdgeWidth:.045,_Intensity:0,_Dissolve:0,
  _EdgeColor:{r:.25,g:.85,b:1,a:1}
});
const frontMat=material('M_CrystalShard','SG_CrystalDiamondFront',{
  _Tint:white,_GemStrength:5.46,_ReflectionStrength:6.83,_RimPower:8.16,_RimStrength:.72,
  _RimColor:{r:0,g:.8,b:1,a:1},_F0:.05,_CubeLOD:0,_Dissolve:0,_Intensity:0
},3001);
const backMat=material('M_CrystalDiamondBack','SG_CrystalBack',{_Tint:white,_BackStrength:.84,_Dissolve:0,_Intensity:0},3000);

// Use the already imported Ellen skeleton and avatar, with explicit prefab-local IDs.
const ellenScene=u.records(u.read(`${fx}/Scenes/Overheat.unity`)),ellenMap=new Map(ellenScene.docs.map(d=>[d.id,d]));
const ellenRoot=ellenScene.docs.find(d=>d.type===1&&/m_Name: EllenVisual\n/.test(d.body));
const ellenTransform=ellenScene.docs.find(d=>d.type===4&&d.body.includes(`m_GameObject: {fileID: ${ellenRoot.id}}`));
const included=new Set();
function includeTransform(t) {
  included.add(t.id);
  const go=ellenMap.get(t.body.match(/m_GameObject: \{fileID: (\d+)\}/)[1]);included.add(go.id);
  for(const m of go.body.matchAll(/component: \{fileID: (\d+)\}/g))included.add(m[1]);
  const children=t.body.match(/  m_Children:([\s\S]*?)  m_Father:/)[1];
  for(const m of children.matchAll(/fileID: (\d+)/g))includeTransform(ellenMap.get(m[1]));
}
includeTransform(ellenTransform);
const ellenDocs=ellenScene.docs.filter(d=>included.has(d.id));
for(const d of ellenDocs) {
  if(d.id===ellenRoot.id)d.body=d.body.replace('m_Name: EllenVisual','m_Name: EllenPortraitModel');
  if(d.id===ellenTransform.id)d.body=d.body.replace(/m_Father: \{fileID: \d+\}/,'m_Father: {fileID: 0}').replace(/m_LocalPosition: \{[^}]*\}/,`m_LocalPosition: ${vec([0,0,0])}`).replace(/m_LocalScale: \{[^}]*\}/,`m_LocalScale: ${vec([1,1,1])}`);
  if(d.type===137)d.body=d.body.replace(/m_Materials:\n(?:  - .+\n)+/,`m_Materials:\n  - {fileID: 2100000, guid: ${u.guid(`${fx}/Characters/Ellen/Materials/Ellen_Base.mat`)}, type: 2}\n`);
}
u.write(prefabPath,ellenScene.header+ellenDocs.map(d=>d.body).join(''));u.meta(prefabPath,'PrefabImporter');

// Retiming all stored runtime and editor curves keeps preview and playback consistent.
const factor=5/3;
let clip=u.read(`${baseline}/CrystalCollapse_DiamondPresentation.anim`)
  .replace('m_Name: CrystalCollapse_DiamondPresentation','m_Name: CrystalCollapse_EllenPresentation')
  .replace(/^(\s*time: )([\d.eE+-]+)$/gm,(_,a,b)=>a+num(+b*factor))
  .replace(/^(\s*(?:inSlope|outSlope): )([^\n]+)$/gm,(_,a,b)=>a+b.replace(/-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?/g,n=>num(+n/factor)))
  .replace(/m_StopTime: 6\b/,'m_StopTime: 10');
// Core yaw travels -12 to +12 degrees and back, not away from the audience.
function vectorCurve(times,values,objectPath='CrystalCore') {
  return `  - curve:\n      serializedVersion: 2\n      m_Curve:\n`+times.map((t,i)=>`      - serializedVersion: 3\n        time: ${t}\n        value: ${vec(values[i])}\n        inSlope: ${vec([0,0,0])}\n        outSlope: ${vec([0,0,0])}\n        tangentMode: 0\n        weightedMode: 0\n        inWeight: ${vec([0,0,0])}\n        outWeight: ${vec([0,0,0])}\n`).join('')+`      m_PreInfinity: 2\n      m_PostInfinity: 2\n      m_RotationOrder: 4\n    path: ${objectPath}\n`;
}
clip=clip.replace(/(  m_EulerCurves:\n)  - curve:[\s\S]*?    path: CrystalCore\n/,(_,a)=>a+vectorCurve([0,5,10],[[0,-12,0],[0,12,0],[0,-12,0]]));
const scalarCurves=new Map();
scalarCurves.set('|dissolve',[[0,2.2,5.5,7.1,9.2,10],[0,0,1,1,0,0]]);
scalarCurves.set('|intensity',[[0,1.7,2.3,4,5.5,7.1,8.6,9.5,10],[.05,.4,.85,.9,.5,.35,.9,.2,.05]]);
for(let i=0;i<12;i++) {
  const objectPath=`Shard_${String(i).padStart(2,'0')}`,angle=i*Math.PI/6;
  const times=[0,num(2.2+i*.03),num(3.3+i*.03),5.5,7.1,8.6,9.2,10];
  const radii=[.18,.18,1.15,1.65,1.8,.32,.08,.18],turn=[0,0,.15,.7,1.65,2.18,0,0];
  const positions=radii.map((r,k)=>[Math.cos(angle+turn[k])*r,1.5+(k>=2&&k<=4?(.35+Math.sin(angle*2)*.6):0),Math.sin(angle+turn[k])*r]);
  const size=.24+(i%3)*.035,scales=[.001,.001,size,size*.92,size*.85,size*.5,.001,.001].map(s=>[s,s*1.35,s*.7]);
  for(const [section,values,attribute] of [['m_PositionCurves',positions,'m_LocalPosition'],['m_ScaleCurves',scales,'m_LocalScale']]) {
    clip=clip.replace(new RegExp(`(^  ${section}:\\n)([\\s\\S]*?)(?=^  m_)`,'m'),(_,head,body)=>head+body.replace(/  - curve:\n[\s\S]*?    path: [^\n]+\n/g,b=>b.endsWith(`    path: ${objectPath}\n`)?vectorCurve(times,values,objectPath):b));
    for(let axis=0;axis<3;axis++)scalarCurves.set(`${objectPath}|${attribute}.${'xyz'[axis]}`,[times,values.map(v=>v[axis])]);
  }
}
for(const axis of 'xyz')scalarCurves.set(`CrystalCore|localEulerAnglesRaw.${axis}`,[[0,5,10],axis==='y'?[-12,12,-12]:[0,0,0]]);
// Editor scalar curves must agree with runtime vector and component curves.
clip=clip.replace(/  - serializedVersion: 2\n    curve:[\s\S]*?    flags: \d+\n/g,block=>{
  const p=block.match(/    path:([^\n]*)/)[1].trim(),a=block.match(/    attribute: ([^\n]*)/)[1];
  const curve=scalarCurves.get(`${p}|${a}`);if(!curve)return block;
  const [times,values]=curve;
  const keys=times.map((t,i)=>`      - serializedVersion: 3\n        time: ${t}\n        value: ${num(values[i])}\n        inSlope: 0\n        outSlope: 0\n        tangentMode: 0\n        weightedMode: 0\n        inWeight: 0\n        outWeight: 0\n`).join('');
  return block.replace(/(      m_Curve:\n)[\s\S]*?(      m_PreInfinity:)/,(_,a,b)=>a+keys+b);
});
u.write(clipPath,clip);u.meta(clipPath,'NativeFormatImporter',7400000);

const scene=u.records(u.read(`${baseline}/CrystalCollapse.unity`)),map=new Map(scene.docs.map(d=>[d.id,d]));
function change(id,f){const d=map.get(String(id));if(!d)throw Error(`No scene object ${id}`);d.body=f(d.body);}
const materialList=guids=>'m_Materials:\n'+guids.map(g=>`  - {fileID: 2100000, guid: ${g}, type: 2}\n`).join('');
change(1426640268,b=>b.replace(/m_Mesh: \{[^}]+\}/,`m_Mesh: {fileID: 4300000, guid: ${u.guid(meshPath)}, type: 2}`));
change(1426640267,b=>b.replace(/m_Materials:\n(?:  - .+\n)+/,materialList([coreMat])).replace('m_CastShadows: 1','m_CastShadows: 0'));
change(1426640269,b=>b.replace(/m_LocalRotation: \{[^}]+\}/,'m_LocalRotation: {x: 0, y: -0.1045285, z: 0, w: 0.9945219}').replace(/m_LocalEulerAnglesHint: \{[^}]+\}/,'m_LocalEulerAnglesHint: {x: 0, y: -12, z: 0}'));
for(const go of scene.docs.filter(d=>d.type===1&&/m_Name: Shard_\d+\n/.test(d.body))) {
  const renderer=scene.docs.find(d=>d.type===23&&d.body.includes(`m_GameObject: {fileID: ${go.id}}`));
  renderer.body=renderer.body.replace(/m_Materials:\n(?:  - .+\n)+/,materialList([backMat,frontMat])).replace('m_CastShadows: 1','m_CastShadows: 0');
  const transform=scene.docs.find(d=>d.type===4&&d.body.includes(`m_GameObject: {fileID: ${go.id}}`));
  transform.body=transform.body.replace(/m_LocalScale: \{[^}]+\}/,`m_LocalScale: ${vec([.001,.001,.001])}`);
}
change(176448233,b=>b.replace(/sequence: \{[^}]+\}/,`sequence: {fileID: 7400000, guid: ${u.guid(clipPath)}, type: 2}`).replace('duration: 6','duration: 10').replace(/^  (progress|dissolve|intensity|impact|time): .+$/gm,(_,k)=>`  ${k}: 0`).replace('start: 1.5','start: 2.5').replace('stop: 3.9','stop: 6.5'));
const captureId='2200000001';
change(176448232,b=>b.replace('  m_Layer: 0',`  - component: {fileID: ${captureId}}\n  m_Layer: 0`));
const scriptGuid=u.meta(portraitScript,'MonoImporter');
scene.docs.push({id:captureId,type:114,body:`--- !u!114 &${captureId}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 176448232}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: ${scriptGuid}, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n  sequence: {fileID: 176448233}\n  crystal: {fileID: 1426640267}\n  ellenPrefab: {fileID: ${ellenRoot.id}, guid: ${u.guid(prefabPath)}, type: 3}\n  ellenMaterial: {fileID: 2100000, guid: ${u.guid(`${fx}/Characters/Ellen/Materials/Ellen_Base.mat`)}, type: 2}\n  idle: {fileID: 7400000, guid: ${u.guid(`${fx}/Characters/Ellen/Animations/Ellen_Idle.anim`)}, type: 2}\n  action: {fileID: 7400000, guid: ${u.guid(`${fx}/Characters/Ellen/Animations/EllenRandomIdle_1.anim`)}, type: 2}\n  resolution: 512\n`});
const pitch=Math.atan2(2.8-1.5,8.8)/2;
change(230612343,b=>b.replace(/m_LocalPosition: \{[^}]+\}/,`m_LocalPosition: ${vec([0,2.8,-8.8])}`).replace(/m_LocalRotation: \{[^}]+\}/,`m_LocalRotation: {x: ${num(Math.sin(pitch))}, y: 0, z: 0, w: ${num(Math.cos(pitch))}}`));
change(230612342,b=>b.replace('field of view: 38','field of view: 36'));
const output=scene.header+scene.docs.map(d=>d.body).join('');
u.write(scenePath,output);
// Refresh the effect prefab from this exact scene hierarchy; stage and camera stay in the scene.
const selected=new Set();
function include(tid){const t=map.get(String(tid));selected.add(t.id);const gid=t.body.match(/m_GameObject: \{fileID: (\d+)\}/)[1];const go=map.get(gid);selected.add(gid);for(const m of go.body.matchAll(/component: \{fileID: (\d+)\}/g))selected.add(m[1]);for(const m of t.body.match(/m_Children:([\s\S]*?)  m_Father:/)[1].matchAll(/fileID: (\d+)/g))include(m[1]);}
include(176448234);
u.write(`${fx}/Prefabs/CrystalCollapse_FX.prefab`,scene.header+scene.docs.filter(d=>selected.has(d.id)).map(d=>d.body).join(''));
console.log(JSON.stringify({backup,vertices:vertices.length,bounds:extent,ellenObjects:ellenDocs.length,scene:scenePath,clip:clipPath},null,2));
