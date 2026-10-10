const u=require('./crystal-assets.cjs');
const fx='Assets/PortfolioFX';
const entries=[['M_OverloadBody','SG_OverloadBody'],['M_ElectricArc','SG_ArcRibbon'],['M_AetherOrb','SG_AetherOrb'],['M_ElectricContact','SG_ElectricAccent'],['M_AetherOrbit','SG_ElectricAccent','T_ArcFilament'],['M_AetherOrbitGold','SG_ElectricAccent','T_ArcFilament'],['M_ElectricBranch','SG_ElectricAccent','T_BodyLightning']];
for(const [name,graph,mask] of entries){
  const p=`${fx}/MyMaterial/${name}.mat`;let text=u.read(p);
  for(const prop of u.graph(`${fx}/Shaders/${graph}.shadergraph`).filter(o=>o.m_Type.endsWith('Texture2DShaderProperty'))){
    const key=prop.m_OverrideReferenceName||prop.m_DefaultReferenceName;
    if(!prop.m_Value.m_SerializedTexture)continue;
    const ref=JSON.parse(prop.m_Value.m_SerializedTexture).texture;
    if(mask&&key==='_Mask')ref.guid=u.guid(`${fx}/ReferenceArc/${mask}.png`);
    const regex=new RegExp(`(- ${key}:\\n\\s+m_Texture:) [^\\n]+`);
    if(!regex.test(text))throw Error('Missing material property '+key);
    text=text.replace(regex,`$1 {fileID: ${ref.fileID}, guid: ${ref.guid}, type: 3}`);
  }
  u.write(p,text);console.log('Rebound textures: '+name);
}
