const u=require('./crystal-assets.cjs');
const p='Assets/PortfolioFX/Scenes/ElectricOverload.unity';
const s=u.records(u.read(p));
const go=name=>s.docs.find(d=>d.type===1&&d.body.includes(`  m_Name: ${name}\n`));
const c=(name,type)=>s.docs.find(d=>d.type===type&&d.body.includes(`m_GameObject: {fileID: ${go(name).id}}`));
const targets=['Ellen_Left_UpperArm','Ellen_Right_Hand','Ellen_UpperChest'];
for(let i=0;i<3;i++){
  const beam=c(`PrimaryArc_${i}`,114),line=c(`PrimaryArc_${i}`,120);
  beam.body=beam.body.replace(/  to: \{fileID: \d+\}/,`  to: {fileID: ${c(targets[i],4).id}}`);
  line.body=line.body.replace('m_CastShadows: 1','m_CastShadows: 0').replace('m_ReceiveShadows: 1','m_ReceiveShadows: 0').replace('numCornerVertices: 0','numCornerVertices: 2');
}
const sparks=c('ShortCircuitSparks',199);
sparks.body=sparks.body.replace(/(m_Materials:\n  - )[^\n]+/,`$1{fileID: 2100000, guid: ${u.guid('Assets/PortfolioFX/MyMaterial/M_ElectricContact.mat')}, type: 2}`);
u.write(p,s.header+s.docs.map(d=>d.body).join(''));
console.log('Bound three arcs to animated bones and replaced placeholder spark material.');
