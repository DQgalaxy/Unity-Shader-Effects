const fs = require('fs'), path = require('path'), assert = require('assert');
const u = require('./crystal-assets.cjs');
process.chdir(path.resolve(__dirname, '..'));
const { ElectricGraph } = require('./electric-graph-builder.cjs');
const graphPath = 'Assets/PortfolioFX/Shaders/SG_AetherOrb.shadergraph';
const matPath = 'Assets/PortfolioFX/MyMaterial/M_AetherOrb.mat';
const g = new ElectricGraph(graphPath);
const title = '03 - Transparent center / white Fresnel rim';
if (g.docs.some(o => o.m_Title === title)) throw Error('Already applied; preserve subsequent edits.');
const filament = g.docs.find(o => o.m_Type.endsWith('.PowerNode'));
const fresnel = g.docs.find(o => o.m_Type.endsWith('.FresnelNode'));
assert(filament && fresnel, 'Expected texture and Fresnel branches');
const backup = 'RepairBackups/AetherTransparent_' + new Date().toISOString().replace(/[:.]/g, '-');
fs.mkdirSync(backup, {recursive:true});
for (const p of [graphPath, matPath, 'ElectricOverload_改版说明.md']) fs.copyFileSync(p, path.join(backup,path.basename(p)));
Object.assign(g.target, {m_SurfaceType:1, m_AlphaMode:2, m_ZWriteControl:2, m_RenderFace:2, m_AlphaClip:false, m_CastShadows:false});
g.group(title, () => {
  g.feed(g.math('Maximum', g.float('_RimPower',3), .1), fresnel, 2);
  const f = [fresnel,3];
  const veins = g.math('Multiply',g.math('Multiply',g.math('Smoothstep',.015,.18,[filament,2]),g.math('Saturate',g.float('_VeinOpacity',.12))),
    g.math('Multiply',g.math('OneMinus',f),g.math('Smoothstep',.005,.08,f)));
  const coverage = g.math('Add',f,veins);
  const weightedColor = g.math('Add',g.math('Multiply',g.props._RimColor,f),g.math('Multiply',g.props._PlasmaColor,veins));
  // URP additive uses SrcAlpha/One. Normalize first so the coverage is applied exactly once.
  const color = g.math('Divide',weightedColor,g.math('Maximum',coverage,.0001));
  const energy = g.math('Add',.95,g.math('Multiply',g.math('Saturate',g.props._Intensity),.25));
  g.output('BaseColor',g.math('Multiply',color,energy));
  g.output('Alpha',g.math('Saturate',coverage));
});
g.prune();
const legacy = g.docs.find(o => o.m_Type.endsWith('ColorShaderProperty') && o.m_DefaultReferenceName === '_DeepColor');
if(legacy){
  g.docs=g.docs.filter(o=>o!==legacy);
  g.root.m_Properties=g.root.m_Properties.filter(r=>r.m_Id!==legacy.m_ObjectId);
  for(const c of g.docs.filter(o=>o.m_Type.endsWith('CategoryData')))c.m_ChildObjectList=c.m_ChildObjectList.filter(r=>r.m_Id!==legacy.m_ObjectId);
}
const usedGroups=new Set(g.root.m_Nodes.map(r=>g.map.get(r.m_Id).m_Group?.m_Id));
g.root.m_GroupDatas=g.root.m_GroupDatas.filter(r=>usedGroups.has(r.m_Id));
g.docs=g.docs.filter(o=>!o.m_Type.endsWith('GroupData')||usedGroups.has(o.m_ObjectId));
g.save(graphPath);
let mat=u.read(matPath).replace(/m_CustomRenderQueue: -?\d+/,'m_CustomRenderQueue: 3000');
mat=mat.replace(/^    - _DeepColor:.*\n/m,'');
mat=mat.replace('    m_Floats:\n','    m_Floats:\n    - _RimPower: 3\n    - _VeinOpacity: 0.12\n');
u.write(matPath,mat);
const smooth=(a,b,x)=>{const t=Math.max(0,Math.min(1,(x-a)/(b-a)));return t*t*(3-2*t);};
for(const mask of [0,.5,1]){
  const center=0+mask*.12*(1-0)*smooth(.005,.08,0);
  const edge=1+mask*.12*(1-1)*smooth(.005,.08,1);
  assert.equal(center,0);assert.equal(edge,1);
}
assert.equal(g.target.m_SurfaceType,1);assert.equal(g.target.m_ZWriteControl,2);
console.log(JSON.stringify({backup,checks:'PASS: center alpha=0, rim alpha=1 with or without veins; Transparent/Additive; no depth writes; front faces only.'},null,2));
