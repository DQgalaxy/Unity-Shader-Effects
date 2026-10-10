const u=require('./crystal-assets.cjs');
const clone=o=>JSON.parse(JSON.stringify(o));
const paths=['Assets/PortfolioFX/Shaders/SG_ImpactRing.shadergraph','F:/UnityProjects/1/Assets/MyShaders/13Hexagon.shadergraph','Assets/PortfolioFX/Shaders/SG_CrystalDiamondFront.shadergraph'];
const pool=[...paths,'Assets/PortfolioFX/Shaders/SG_FlameSheet.shadergraph'].flatMap(u.graph),lookup=new Map(pool.map(o=>[o.m_ObjectId,o]));
class Graph {
  constructor(){
    const base=u.graph(paths[0]),old=new Map(base.map(o=>[o.m_ObjectId,o]));
    this.root=clone(base[0]);this.root.m_ObjectId=u.id();this.root.m_Path='PortfolioFX/Shield';
    const keep=new Set([this.root.m_ActiveTargets[0].m_Id,...this.root.m_VertexContext.m_Blocks.map(r=>r.m_Id),...this.root.m_FragmentContext.m_Blocks.map(r=>r.m_Id)]);
    keep.add(old.get(this.root.m_ActiveTargets[0].m_Id).m_ActiveSubTarget.m_Id);
    for(const id of [...keep])for(const r of old.get(id)?.m_Slots||[])keep.add(r.m_Id);
    this.docs=[this.root,...base.filter(o=>keep.has(o.m_ObjectId)).map(clone)];
    this.root.m_Nodes=this.root.m_Nodes.filter(r=>keep.has(r.m_Id));
    this.root.m_Properties=[];this.root.m_CategoryData=[];this.root.m_GroupDatas=[];this.root.m_StickyNoteDatas=[];this.root.m_Edges=[];
    this.map=new Map(this.docs.map(o=>[o.m_ObjectId,o]));
    this.target=this.docs.find(o=>o.m_Type.endsWith('.UniversalTarget'));
    Object.assign(this.target,{m_SurfaceType:1,m_RenderFace:0,m_AlphaMode:2,m_ZWriteControl:2,m_CastShadows:false,m_ReceiveShadows:false,m_AlphaClip:false});
    this.props={};
  }
  insert(o){this.docs.push(o);this.map.set(o.m_ObjectId,o);return o;}
  node(type){
    const template=pool.find(o=>o.m_Type===`UnityEditor.ShaderGraph.${type}Node`);if(!template)throw Error(`Missing ${type}`);
    const n=clone(template);n.m_ObjectId=u.id();n.m_Group={m_Id:''};n.m_Slots=[];n.m_PreviewExpanded=false;
    for(const r of template.m_Slots){const s=clone(lookup.get(r.m_Id));s.m_ObjectId=u.id();n.m_Slots.push({m_Id:s.m_ObjectId});this.insert(s);}
    this.insert(n);this.root.m_Nodes.push({m_Id:n.m_ObjectId});return n;
  }
  slot(n,id){return this.map.get(n.m_Slots.find(r=>this.map.get(r.m_Id).m_Id===id).m_Id);}
  value(n,id,val){const s=this.slot(n,id);if(typeof s.m_Value==='number')s.m_Value=val;else if(typeof val==='object')s.m_Value=Object.fromEntries(Object.keys(s.m_Value).map((k,i)=>[k,val[i]??0]));else for(const k in s.m_Value)s.m_Value[k]=val;return n;}
  link(from,slot,to,input){this.root.m_Edges.push({m_OutputSlot:{m_Node:{m_Id:from.m_ObjectId},m_SlotId:slot},m_InputSlot:{m_Node:{m_Id:to.m_ObjectId},m_SlotId:input}});}
  property(name,type,value){
    if(this.props[name])return this.props[name];
    const p=clone(pool.find(o=>o.m_Type.endsWith(`.${type}ShaderProperty`)));if(!p)throw Error(type);
    p.m_ObjectId=u.id();p.m_Guid={m_GuidSerialized:require('crypto').randomUUID()};p.m_Name=name.slice(1);p.m_DefaultReferenceName=name;p.m_RefNameGeneratedByDisplayName=p.m_Name;p.m_OverrideReferenceName='';p.m_Value=value;
    if(type==='Color')p.m_ColorMode=1;
    this.insert(p);this.root.m_Properties.push({m_Id:p.m_ObjectId});
    const template=pool.find(o=>o.m_Property&&lookup.get(o.m_Property.m_Id)?.m_Type===p.m_Type);
    const n=this.node('Property');n.m_Property={m_Id:p.m_ObjectId};
    for(const r of n.m_Slots)this.docs=this.docs.filter(o=>o.m_ObjectId!==r.m_Id);
    n.m_Slots=[];
    for(const r of template.m_Slots){const s=clone(lookup.get(r.m_Id));s.m_ObjectId=u.id();s.m_DisplayName=p.m_Name;s.m_ShaderOutputName=p.m_Name;this.insert(s);n.m_Slots.push({m_Id:s.m_ObjectId});}
    this.props[name]=[n,0];return this.props[name];
  }
  float(name,value){return this.property(name,'Vector1',value);}
  scroll(time,x,y){const n=this.node('Combine');this.feed(this.math('Multiply',time,x),n,0);this.feed(this.math('Multiply',time,y),n,1);return [n,6];}
  color(name,value){return this.property(name,'Color',{r:value[0],g:value[1],b:value[2],a:value[3]??1});}
  texture(name,path){return this.property(name,'Texture2D',{m_SerializedTexture:JSON.stringify({texture:{fileID:2800000,guid:u.guid(path),type:3}}),m_Guid:''});}
  feed(v,n,slot){if(Array.isArray(v)&&v[0]?.m_ObjectId)this.link(v[0],v[1],n,slot);else this.value(n,slot,v);}
  math(type,...args){const n=this.node(type);args.forEach((a,i)=>this.feed(a,n,i));return [n,this.map.get(n.m_Slots.find(r=>this.map.get(r.m_Id).m_SlotType===1).m_Id).m_Id];}
  sample(tex,uv){const n=this.node('SampleTexture2D');n.m_TextureType=0;n.m_NormalMapSpace=0;this.feed(tex,n,1);if(uv)this.feed(uv,n,2);return n;}
  output(name,v){const n=this.docs.find(o=>o.m_SerializedDescriptor===`SurfaceDescription.${name}`);this.feed(v,n,0);}
  save(p){
    const nodes=this.root.m_Nodes.map(r=>this.map.get(r.m_Id)),depth=new Map();
    const level=n=>{if(depth.has(n.m_ObjectId))return depth.get(n.m_ObjectId);const edges=this.root.m_Edges.filter(e=>e.m_InputSlot.m_Node.m_Id===n.m_ObjectId);const d=edges.length?Math.max(...edges.map(e=>level(this.map.get(e.m_OutputSlot.m_Node.m_Id))))+1:0;depth.set(n.m_ObjectId,d);return d;};
    const rows={};for(const n of nodes){const l=level(n),r=rows[l]||0;rows[l]=r+1;n.m_DrawState.m_Position={serializedVersion:'2',x:l*260,y:r*200,width:210,height:150};}
    const x=(Math.max(...depth.values())+1)*260;this.root.m_VertexContext.m_Position={x,y:-300};this.root.m_FragmentContext.m_Position={x,y:0};
    for(const e of this.root.m_Edges)for(const [k,dir]of [['m_OutputSlot',1],['m_InputSlot',0]]){const n=this.map.get(e[k].m_Node.m_Id);if(this.slot(n,e[k].m_SlotId).m_SlotType!==dir)throw Error('Slot direction');}
    u.writeGraph(p,this.docs);if(!require('fs').existsSync(p+'.meta'))u.write(p+'.meta',u.read(paths[0]+'.meta').replace(/^guid: \w+/m,`guid: ${u.id()}`));
  }
}
module.exports={Graph};
