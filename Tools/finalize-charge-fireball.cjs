const u=require('./crystal-assets.cjs');
const p='Assets/PortfolioFX/Scenes/ArcaneChargeShot.unity',s=u.records(u.read(p));
const driver=s.docs.find(d=>d.id==='645329419');
driver.body=driver.body.replace(/  effectLight: \{[^}]+\}/,'  effectLight: {fileID: 0}');
const light=s.docs.find(d=>d.id==='1453318912');light.body=light.body.replace('m_Enabled: 1','m_Enabled: 0');
const point=s.docs.find(d=>d.id==='734394474');point.body=point.body.replace(/m_LocalPosition: \{[^}]+\}/,'m_LocalPosition: {x: 2.3, y: 1.33, z: 0}');
u.write(p,s.header+s.docs.map(d=>d.body).join(''));
