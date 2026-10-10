const u=require('./crystal-assets.cjs');
for(const name of ['SG_OverloadBody','SG_ArcRibbon','SG_AetherOrb','SG_ElectricAccent']){
  const p=`Assets/PortfolioFX/Shaders/${name}.shadergraph`,docs=u.graph(p),root=docs[0];
  let category=docs.find(o=>o.m_Type.endsWith('.CategoryData'));
  if(!category){
    category={m_SGVersion:0,m_Type:'UnityEditor.ShaderGraph.CategoryData',m_ObjectId:u.id(),m_Name:'',m_ChildObjectList:[]};
    docs.push(category);root.m_CategoryData.push({m_Id:category.m_ObjectId});
  }
  const assigned=new Set(docs.filter(o=>o.m_Type.endsWith('.CategoryData')).flatMap(o=>o.m_ChildObjectList.map(r=>r.m_Id)));
  for(const ref of root.m_Properties)if(!assigned.has(ref.m_Id))category.m_ChildObjectList.push({...ref});
  u.writeGraph(p,docs);
}
u.write('Assets/PortfolioFX/ReferenceArc/body-lightning-source.json',JSON.stringify({
  source:'Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Textures/Lightning.tif',
  intermediate:'Assets/PortfolioFX/ReferenceArc/T_ArcBranchAtlas.png',
  output:'Assets/PortfolioFX/ReferenceArc/T_BodyLightning.png',
  treatment:'R*A mask extraction; 1px main dilation, 2px weighted halo; gain 1.45; linear RGB mask with opaque alpha; Repeat wrap.',
  originalAssetUnmodified:true
},null,2));
console.log('Graph properties categorized; derived-texture provenance recorded.');
