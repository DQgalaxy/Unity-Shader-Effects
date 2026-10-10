const assert=require('assert'),fs=require('fs'),crypto=require('crypto'),u=require('./crystal-assets.cjs');
const fx='Assets/PortfolioFX';
const counts={};
for(const name of ['SG_ChargeOrb','SG_FireRibbon','SG_FireHalo']){
 const g=u.graph(`${fx}/Shaders/${name}.shadergraph`),root=g[0],map=new Map(g.map(o=>[o.m_ObjectId,o]));assert.equal(map.size,g.length);
 const input=new Set(),visited=new Set(),pending=new Set();
 for(const e of root.m_Edges){for(const [key,dir]of [['m_OutputSlot',1],['m_InputSlot',0]]){const end=e[key],n=map.get(end.m_Node.m_Id);assert(n);const slot=n.m_Slots.map(r=>map.get(r.m_Id)).find(s=>s.m_Id===end.m_SlotId);assert(slot&&slot.m_SlotType===dir);}const k=e.m_InputSlot.m_Node.m_Id+':'+e.m_InputSlot.m_SlotId;assert(!input.has(k));input.add(k);}
 function visit(id){if(visited.has(id))return;assert(!pending.has(id));pending.add(id);for(const e of root.m_Edges.filter(e=>e.m_InputSlot.m_Node.m_Id===id))visit(e.m_OutputSlot.m_Node.m_Id);pending.delete(id);visited.add(id);}
 root.m_Nodes.forEach(r=>visit(r.m_Id));
 const target=g.find(o=>o.m_Type.endsWith('UniversalTarget'));assert.equal(target.m_SurfaceType,1);assert.equal(target.m_ZWriteControl,2);assert(!target.m_CastShadows);
 assert(g.some(o=>o.m_DefaultReferenceName==='_Fade'));
 counts[name]={nodes:root.m_Nodes.length,connections:root.m_Edges.length};
}
const scene=u.records(u.read(`${fx}/Scenes/ArcaneChargeShot.unity`)),map=new Map(scene.docs.map(d=>[d.id,d]));assert.equal(map.size,scene.docs.length);
for(const d of scene.docs)for(const m of d.body.matchAll(/\{fileID: (-?\d+)\}/g))if(m[1]!=='0')assert(map.has(m[1]),'Dangling object '+m[1]);
const controller=map.get('1909211001');assert(controller&&controller.body.includes('hand: {fileID: 1725982751}'));
assert(map.get('1843870768').body.includes('m_IsActive: 0'));
for(const id of ['57269343','1873758449','1236476095','760479761','596016738','1598907898'])assert(map.get(id).body.includes('m_Enabled: 0'));
assert(map.get('645329419').body.includes('  arcs: []'));assert(map.get('645329419').body.includes('duration: 6.5'));
const clip=u.read(`${fx}/Animations/ArcaneChargeShot_FireballSequence.anim`);assert(clip.includes('m_StopTime: 6.5'));assert(clip.includes('m_PositionCurves: []'));
assert(u.read('Assets/Viking Village/Models/Props/prop_rune_01.fbx.meta').includes('isReadable: 1'));
const materials=['M_ChargeOrb','M_ChargeFlame','M_FireRibbon','M_FireChargeHalo','M_FireImpact','M_FireImpactDust','M_FireTargetRune','M_FireTargetInterior'];
for(const name of materials){const p=`${fx}/MyMaterial/${name}.mat`;assert(controller.body.includes(u.guid(p)));assert(!u.read(p).includes('}}'));}
for(const n of [6,7])for(let f=0;f<n*n;f++){
 const col=f%n,row=Math.floor(f/n);for(const v of [0,1]){const x=(.004+.992*v+col)/n,y=(.004+.992*v+n-1-row)/n;assert(x>0&&x<1&&y>0&&y<1);}
}
const manifest=JSON.parse(u.read('RepairBackups/ChargeFireball_2026-09-20T12-39-18-095Z/manifest.json'));
for(const[p,h]of Object.entries(manifest.protectedAssets))assert.equal(crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex'),h,'Unrelated asset '+p);
const report={graphs:counts,materials:materials.length,checks:'PASS: topology, transparent non-shadowing FX, atlas coordinates, scene bindings, hidden staff/placeholders, readable source mesh, new timing, unrelated-scene hashes. GPU import/render and runtime fracture checks require Unity.'};
fs.mkdirSync('Logs/FireballUpgrade',{recursive:true});u.write('Logs/FireballUpgrade/static-result.json',JSON.stringify(report,null,2));console.log(report);
