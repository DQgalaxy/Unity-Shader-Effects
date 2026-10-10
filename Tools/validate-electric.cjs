const fs=require('fs'),assert=require('assert'),u=require('./crystal-assets.cjs');
const fx='Assets/PortfolioFX';let nodes=0,edges=0;
for(const name of ['SG_OverloadBody','SG_ArcRibbon','SG_AetherOrb','SG_ElectricAccent']){
  const docs=u.graph(`${fx}/Shaders/${name}.shadergraph`),map=new Map(docs.map(o=>[o.m_ObjectId,o])),g=docs[0];
  assert.equal(map.size,docs.length,'Duplicate ID');
  const inputs=new Set();
  for(const e of g.m_Edges){
    for(const [side,dir]of [['m_OutputSlot',1],['m_InputSlot',0]]){
      const end=e[side],n=map.get(end.m_Node.m_Id);assert(n,'Missing node');
      const slot=n.m_Slots.map(r=>map.get(r.m_Id)).find(o=>o.m_Id===end.m_SlotId);
      assert(slot&&slot.m_SlotType===dir,'Missing/wrong-direction port');
    }
    const key=e.m_InputSlot.m_Node.m_Id+':'+e.m_InputSlot.m_SlotId;assert(!inputs.has(key),'Two sources for input');inputs.add(key);
  }
  for(const r of g.m_Nodes)assert(map.has(r.m_Id));
  nodes+=g.m_Nodes.length;edges+=g.m_Edges.length;
  console.log(`PASS ${name}: ${g.m_Nodes.length} nodes, ${g.m_Edges.length} connections`);
}
const s=u.records(u.read(`${fx}/Scenes/ElectricOverload.unity`)),map=new Map(s.docs.map(d=>[d.id,d]));
assert.equal(map.size,s.docs.length);
for(const d of s.docs)for(const m of d.body.matchAll(/\{fileID: (-?\d+)\}/g))if(m[1]!=='0')assert(map.has(m[1]),`Dangling scene reference ${m[1]}`);
for(const name of ['Conductor_0','Conductor_1','Conductor_2','Terminal_0','Terminal_1','Terminal_2']){
  const go=s.docs.find(d=>d.type===1&&d.body.includes(`  m_Name: ${name}\n`));
  const r=s.docs.find(d=>d.type===23&&d.body.includes(`m_GameObject: {fileID: ${go.id}}`));assert(r.body.includes('m_Enabled: 0'));
}
const arcs=s.docs.filter(d=>d.type===114&&d.body.includes('  displacement:'));assert.equal(arcs.length,3);
for(const d of arcs){assert(d.body.includes('driveShader: 1'));assert(d.body.includes('maxWidth: 0.22'));assert(!d.body.includes('to: {fileID: 768511049}'));}
const comp=s.docs.find(d=>d.type===114&&d.body.includes('  orbMaterial:'));assert(comp);
for(const p of ['M_OverloadBody','M_ElectricArc','M_AetherOrb','M_AetherOrbit','M_AetherOrbitGold','M_ElectricContact','M_ElectricBranch']){
  const mat=u.read(`${fx}/MyMaterial/${p}.mat`);assert(mat.includes('m_Shader:'));assert(!mat.includes('undefined'));assert(!mat.includes('NaN'));
  assert(!mat.includes('}}'),'Malformed YAML flow map');
  for(const m of mat.matchAll(/- (_(?:BaseMap|NormalMap|MetallicSmoothMap|OcclusionMap|LightningTex|ArcTex|FlowTex|PlasmaTex|Mask)):\n\s+m_Texture: \{fileID: ([^}]+)\}/g))
    assert(m[2].startsWith('2800000, guid:'),`${p}: empty required texture ${m[1]}`);
}
const runtime='Assets/PortfolioFX/Scripts/Runtime/FXElectricPresentation.cs';
const editor='Assets/PortfolioFX/Editor/ElectricPresentationVerification.cs';
u.meta(editor,'MonoImporter');
// Compile to Logs, never overwrite Unity's live assemblies or response files.
const dir='Logs/ElectricUpgrade';fs.mkdirSync(dir,{recursive:true});
for(const kind of ['Assembly-CSharp','Assembly-CSharp-Editor']){
  let rsp=u.read(`Library/Bee/artifacts/1900b0aE.dag/${kind}.rsp`);
  rsp=rsp.replace(/^-out:.*$/m,`-out:"${dir}/${kind}.dll"`).replace(/^-refout:.*$/m,`-refout:"${dir}/${kind}.ref.dll"`);
  if(kind==='Assembly-CSharp'){
    if(!rsp.includes(runtime))rsp+=`\n"${runtime}"\n`;
  }else{
    if(!rsp.includes(editor))rsp+=`\n"${editor}"\n`;
    rsp=rsp.replace(/^-r:"[^"\n]*Assembly-CSharp(?:\.ref)?\.dll"$/m,`-r:"${dir}/Assembly-CSharp.ref.dll"`);
  }
  u.write(`${dir}/${kind}.rsp`,rsp);
}
const report={graphNodes:nodes,graphConnections:edges,sceneObjects:s.docs.length,result:'PASS: graph references, slot directions, unique inputs, scene references, bindings. GPU shader compilation and appearance require Unity verification.'};
u.write(`${dir}/static-result.json`,JSON.stringify(report,null,2));console.log(report);
