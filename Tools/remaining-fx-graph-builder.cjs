const fs = require('fs');
const u = require('./crystal-assets.cjs');
const { ElectricGraph } = require('./electric-graph-builder.cjs');
const clone = x => JSON.parse(JSON.stringify(x));
const root = 'Assets/PortfolioFX/';
const samples = 'Library/PackageCache/com.unity.shadergraph@425e21ee5890/Samples~/NodeReference/Input/';
const pool = ['SceneColor', 'SceneDepth'].flatMap(n => u.graph(samples + n + '.shadergraph'));
const lookup = new Map(pool.map(x => [x.m_ObjectId, x]));

class ShowcaseGraph extends ElectricGraph {
  constructor(lit = false) {
    super();
    this.root.m_Path = 'PortfolioFX/Showcase';
    this.root.m_GraphPrecision = 0;
    this.target.m_AllowMaterialOverride = false;
    if (lit) {
      const source = u.graph(root + 'Shaders/SG_FreezeBody.shadergraph');
      const map = new Map(source.map(x => [x.m_ObjectId, x]));
      const sub = clone(source.find(x => x.m_Type.endsWith('.UniversalLitSubTarget')));
      sub.m_ObjectId = u.id(); this.insert(sub);
      const old = this.target.m_ActiveSubTarget.m_Id;
      this.docs = this.docs.filter(x => x.m_ObjectId !== old);
      this.target.m_ActiveSubTarget = {m_Id: sub.m_ObjectId};
      for (const name of ['NormalTS', 'Metallic', 'Smoothness', 'Occlusion', 'Emission']) {
        const block = source.find(x => x.m_SerializedDescriptor === 'SurfaceDescription.' + name);
        this.importNode(block, map);
        this.root.m_FragmentContext.m_Blocks.push({m_Id: this.root.m_Nodes.at(-1).m_Id});
      }
      this.output('Occlusion', 1); this.output('Metallic', 0);
      this.output('Smoothness', .35); this.output('Emission', [0, 0, 0]);
      this.output('NormalTS', [0, 0, 1]);
    }
  }
  importNode(template, map) {
    const n = clone(template); n.m_ObjectId = u.id(); n.m_Group = {m_Id:''}; n.m_Slots = [];
    n.m_PreviewExpanded = false;
    for (const ref of template.m_Slots) {
      const s = clone(map.get(ref.m_Id)); s.m_ObjectId = u.id();
      this.insert(s); n.m_Slots.push({m_Id:s.m_ObjectId});
    }
    this.insert(n); this.root.m_Nodes.push({m_Id:n.m_ObjectId}); return n;
  }
  node(type) {
    const template = pool.find(x => x.m_Type === 'UnityEditor.ShaderGraph.' + type + 'Node');
    return template ? this.importNode(template, lookup) : super.node(type);
  }
  position(space) { const n = this.node('Position'); n.m_Space = space; return [n,0]; }
  uv() { const n = this.node('UV'); n.m_OutputChannel = 0; return [n,0]; }
  length(v) { return this.math('Power', this.math('Maximum', this.math('DotProduct',v,v),.000001),.5); }
  transform(v,from,to,type=0) {
    const n=this.node('Transform'); n.m_Conversion={from,to};n.m_ConversionType=type;n.m_Normalize=type===2;
    this.feed(v,n,0);return [n,1];
  }
  vertex(name,v) {
    const n=this.docs.find(x=>x.m_SerializedDescriptor==='VertexDescription.'+name);
    this.root.m_Edges=this.root.m_Edges.filter(e=>e.m_InputSlot.m_Node.m_Id!==n.m_ObjectId);
    this.feed(v,n,0);
  }
  output(name,v) {
    if(name==='AlphaClipThreshold'&&!this.docs.some(x=>x.m_SerializedDescriptor==='SurfaceDescription.'+name)) {
      const src=u.graph(root+'Shaders/SG_Hologram.shadergraph');
      const n=this.importNode(src.find(x=>x.m_SerializedDescriptor==='SurfaceDescription.'+name),new Map(src.map(x=>[x.m_ObjectId,x])));
      this.root.m_FragmentContext.m_Blocks.push({m_Id:n.m_ObjectId});
    }
    super.output(name,v);
  }
  screen() { const n=this.node('ScreenPosition');n.m_ScreenSpaceType=0;return [n,0]; }
  sceneColor(uv) {const n=this.node('SceneColor');this.feed(uv,n,0);return [n,1];}
  depth(width=.15,softness=.25) {
    return this.group('Eye depth difference - positive gap only',()=>{
      const n=this.node('SceneDepth');n.m_DepthSamplingMode=2;this.feed(this.screen(),n,0);
      const viewZ=this.split(this.position(1))[2];
      const signed=this.math('Add',[n,1],viewZ);
      const gap=this.math('Maximum',signed,0);
      return {gap,contact:this.math('OneMinus',this.math('Smoothstep',0,this.math('Maximum',width,.001),gap)),
        soft:this.math('Saturate',this.math('Divide',gap,this.math('Maximum',softness,.001)))};
    });
  }
  finish(name,alphaMode=0,opaque=false) {
    Object.assign(this.target,{m_SurfaceType:opaque?0:1,m_AlphaMode:alphaMode,m_ZWriteControl:opaque?1:2,
      m_CastShadows:opaque,m_ReceiveShadows:opaque,m_RenderFace:opaque?2:0});
    this.prune();this.save(root+'Shaders/SG_'+name+'.shadergraph');
    return this;
  }
}

function material(name, graphName, graph, overrides={}) {
  const path=root+'MyMaterial/M_'+name+'.mat';
  const texture=[],floats=['    - _QueueControl: 0'],colors=[];
  for (const ref of graph.root.m_Properties) {
    const p=graph.map.get(ref.m_Id),key=p.m_OverrideReferenceName||p.m_DefaultReferenceName;
    const v=key in overrides?overrides[key]:p.m_Value;
    if(p.m_Type.endsWith('.Texture2DShaderProperty')){
      const t=typeof v==='string'?{fileID:2800000,guid:u.guid(v)}:JSON.parse(v.m_SerializedTexture).texture;
      texture.push(`    - ${key}:\n        m_Texture: {fileID: ${t.fileID}, guid: ${t.guid}, type: 3}\n        m_Scale: {x: 1, y: 1}\n        m_Offset: {x: 0, y: 0}`);
    } else if(typeof v==='number')floats.push(`    - ${key}: ${v}`);
    else colors.push(`    - ${key}: {r: ${v.r??v.x??v[0]??0}, g: ${v.g??v.y??v[1]??0}, b: ${v.b??v.z??v[2]??0}, a: ${v.a??v.w??v[3]??1}}`);
  }
  const source=u.records(u.read(root+'MyMaterial/M_SlashScythe.mat')).docs.find(d=>d.type===21).body;
  const text=source.replace(/m_Name: [^\n]+/,'m_Name: M_'+name)
    .replace(/m_Shader: \{[^}]+\}/,`m_Shader: {fileID: -6465566751694194690, guid: ${u.guid(root+'Shaders/SG_'+graphName+'.shadergraph')}, type: 3}`)
    .replace(/  m_SavedProperties:[\s\S]*?  m_BuildTextureStacks:/,
      `  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs:${texture.length?'\n'+texture.join('\n'):' []'}\n    m_Ints: []\n    m_Floats:\n${floats.join('\n')}\n    m_Colors:${colors.length?'\n'+colors.join('\n'):' []'}\n  m_BuildTextureStacks:`);
  u.write(path,'%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+text);u.meta(path,'NativeFormatImporter',2100000);
}
module.exports={ShowcaseGraph,material};
