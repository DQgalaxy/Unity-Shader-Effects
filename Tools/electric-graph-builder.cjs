const fs = require('fs');
const u = require('./crystal-assets.cjs');
const { Graph } = require('./shield-graph-builder.cjs');
const copy = o => JSON.parse(JSON.stringify(o));
const pool = fs.readdirSync('Assets/PortfolioFX/Shaders').filter(p => p.endsWith('.shadergraph')).flatMap(p => u.graph('Assets/PortfolioFX/Shaders/' + p));
const lookup = new Map(pool.map(o => [o.m_ObjectId, o]));

class ElectricGraph extends Graph {
  constructor(existing) {
    super();
    if (existing) {
      this.docs = u.graph(existing); this.root = this.docs[0];
      this.map = new Map(this.docs.map(o => [o.m_ObjectId, o]));
      this.target = this.docs.find(o => o.m_Type.endsWith('.UniversalTarget'));
      for (const ref of this.root.m_Properties) {
        const p = this.map.get(ref.m_Id);
        const n = this.docs.find(o => o.m_Property?.m_Id === p.m_ObjectId);
        if (n) this.props[p.m_OverrideReferenceName || p.m_DefaultReferenceName] = [n, 0];
      }
    }
    this.root.m_Path = 'PortfolioFX/Electric';
  }
  node(type) {
    const template = pool.find(o => o.m_Type === `UnityEditor.ShaderGraph.${type}Node`);
    if (!template) return super.node(type);
    const n = copy(template); n.m_ObjectId = u.id(); n.m_Group = {m_Id: ''}; n.m_Slots = []; n.m_PreviewExpanded = false;
    for (const ref of template.m_Slots) {
      const s = copy(lookup.get(ref.m_Id)); s.m_ObjectId = u.id(); n.m_Slots.push({m_Id: s.m_ObjectId}); this.insert(s);
    }
    this.insert(n); this.root.m_Nodes.push({m_Id: n.m_ObjectId}); return n;
  }
  output(name, value) {
    const n = this.docs.find(o => o.m_SerializedDescriptor === `SurfaceDescription.${name}`);
    this.root.m_Edges = this.root.m_Edges.filter(e => e.m_InputSlot.m_Node.m_Id !== n.m_ObjectId);
    this.feed(value, n, 0);
  }
  split(value) { const n = this.node('Split'); this.feed(value,n,0); return [1,2,3,4].map(i => [n,i]); }
  combine(x,y,z=0) { const n = this.node('Combine'); [x,y,z].forEach((v,i)=>this.feed(v,n,i)); return [n,5]; }
  group(label, build) {
    const start = this.docs.length; const result = build(); const id = u.id();
    this.insert({m_SGVersion:0,m_Type:'UnityEditor.ShaderGraph.GroupData',m_ObjectId:id,m_Title:label,m_Position:{x:0,y:0}});
    this.root.m_GroupDatas.push({m_Id:id});
    for(const n of this.docs.slice(start)) if(n.m_Slots) n.m_Group={m_Id:id};
    return result;
  }
  triplanar(texture, scale, offset) {
    const position=this.node('Position');position.m_Space=0;
    const normal=this.node('NormalVector');normal.m_Space=0;
    const p=this.split(this.math('Add',this.math('Multiply',[position,0],scale),offset));
    const weights=this.split(this.math('Power',this.math('Absolute',[normal,0]),4));
    const xy=this.sample(texture,this.combine(p[0],p[1]));
    const zy=this.sample(texture,this.combine(p[2],p[1]));
    const xz=this.sample(texture,this.combine(p[0],p[2]));
    const sum=this.math('Add',this.math('Add',weights[0],weights[1]),weights[2]);
    const result=this.math('Add',this.math('Add',this.math('Multiply',[xy,4],weights[2]),this.math('Multiply',[zy,4],weights[0])),this.math('Multiply',[xz,4],weights[1]));
    return this.math('Divide',result,this.math('Maximum',sum,.001));
  }
  prune() {
    const keep=new Set(), visit=id=>{ if(keep.has(id))return;keep.add(id);for(const e of this.root.m_Edges.filter(e=>e.m_InputSlot.m_Node.m_Id===id))visit(e.m_OutputSlot.m_Node.m_Id); };
    for(const ref of [...this.root.m_VertexContext.m_Blocks,...this.root.m_FragmentContext.m_Blocks])visit(ref.m_Id);
    this.root.m_Nodes=this.root.m_Nodes.filter(r=>keep.has(r.m_Id));
    this.root.m_Edges=this.root.m_Edges.filter(e=>keep.has(e.m_InputSlot.m_Node.m_Id));
    const referenced=new Set([this.root.m_ObjectId,...keep]);
    for(const id of keep)for(const r of this.map.get(id).m_Slots||[])referenced.add(r.m_Id);
    for(const ref of this.root.m_Properties)referenced.add(ref.m_Id);
    for(const ref of this.root.m_GroupDatas)referenced.add(ref.m_Id);
    for(const ref of this.root.m_CategoryData)referenced.add(ref.m_Id);
    for(const ref of this.root.m_ActiveTargets){referenced.add(ref.m_Id);referenced.add(this.map.get(ref.m_Id).m_ActiveSubTarget.m_Id);}
    this.docs=this.docs.filter(o=>referenced.has(o.m_ObjectId));this.root.m_StickyNoteDatas=[];
  }
  save(p) {
    let category=this.docs.find(o=>o.m_Type.endsWith('.CategoryData'));
    if(!category){
      category=this.insert({m_SGVersion:0,m_Type:'UnityEditor.ShaderGraph.CategoryData',m_ObjectId:u.id(),m_Name:'',m_ChildObjectList:[]});
      this.root.m_CategoryData.push({m_Id:category.m_ObjectId});
    }
    const assigned=new Set(this.docs.filter(o=>o.m_Type.endsWith('.CategoryData')).flatMap(o=>o.m_ChildObjectList.map(r=>r.m_Id)));
    for(const ref of this.root.m_Properties)if(!assigned.has(ref.m_Id))category.m_ChildObjectList.push({...ref});
    super.save(p);
  }
}
module.exports = { ElectricGraph };
