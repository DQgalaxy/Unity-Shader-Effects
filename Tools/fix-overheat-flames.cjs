const fs = require('fs');
const assert = require('assert/strict');
const u = require('./crystal-assets.cjs');
const { ElectricGraph } = require('./electric-graph-builder.cjs');
const scene = 'Assets/PortfolioFX/Scenes/Overheat.unity';
const clip = 'Assets/PortfolioFX/Animations/Overheat_Sequence.anim';
const shader = 'Assets/PortfolioFX/Shaders/SG_FlameSheet.shadergraph';
const material = 'Assets/PortfolioFX/MyMaterial/M_OverheatFlame.mat';
const script = 'Assets/PortfolioFX/Scripts/Runtime/FXOverheatFlames.cs';
const data = u.records(u.read(scene));
assert(!data.docs.some(d => d.id === '2109202601'), 'Already applied');
const backup = 'RepairBackups/OverheatUpright_' + new Date().toISOString().replace(/[:.]/g, '-');
for (const path of [scene, clip, shader, material]) {
  fs.mkdirSync(backup + '/' + require('path').dirname(path), { recursive: true });
  fs.copyFileSync(path, backup + '/' + path);
  fs.copyFileSync(path + '.meta', backup + '/' + path + '.meta');
}

const g = new ElectricGraph(shader);
g.root.m_Path = u.graph(shader)[0].m_Path;
Object.assign(g.target, {m_CastShadows:false, m_ReceiveShadows:false, m_AlphaMode:2});
const node = id => { const n = g.map.get(id); assert(n, id); return n; };
const reconnect = (value, target, slot) => {
  g.root.m_Edges = g.root.m_Edges.filter(e => e.m_InputSlot.m_Node.m_Id !== target.m_ObjectId || e.m_InputSlot.m_SlotId !== slot);
  g.feed(value, target, slot);
};
const uv = node('bd8eb55f7a4847b5988e73de96923eed');
const [x, y] = g.split([uv, 0]);
const fade = g.group('Soft card boundaries', () => {
  const side = g.math('Multiply', g.math('Smoothstep', 0, .16, x), g.math('OneMinus', g.math('Smoothstep', .84, 1, x)));
  const vertical = g.math('Multiply', g.math('Smoothstep', 0, .14, y), g.math('OneMinus', g.math('Smoothstep', .78, 1, y)));
  return g.math('Multiply', side, vertical);
});
g.group('Upright flame sway', () => {
  const amount = [node('20ea96a5a92049c29be8b7999c42c848'), 2];
  const sway = g.math('Multiply', amount, g.math('Smoothstep', 0, .75, y));
  const vector = g.node('Combine'); g.feed(sway, vector, 0); g.value(vector, 1, 0);
  reconnect([vector, 6], node('daf7a17487a44e53b5a88be580543a0e'), 0);
});
for (const n of g.docs.filter(n => n.m_Type.endsWith('.SubGraphNode'))) {
  const speed = g.slot(n, -1571033815);
  speed.m_Value.x = 0;
}
// Preserve inverse noise masking without reversed Smoothstep edges.
const threshold = node('56ced6a9b1264133901f817068462aa6');
g.value(threshold, 0, .2); g.value(threshold, 1, .85);
reconnect(g.math('OneMinus', [threshold, 3]), node('8d1f7155c19242918b84de7d12b23140'), 0);
g.output('Alpha', g.math('Saturate', g.math('Multiply', [node('83628d25f16e4075bac0f9c37a670cf9'), 2], fade)));
g.output('BaseColor', g.math('Multiply', [node('a5adcaa6f92d4b14971af6c5e2bf3750'), 2], 2.2));
const distortion = g.docs.find(n => n.m_Type.endsWith('ShaderProperty') && (n.m_OverrideReferenceName || n.m_DefaultReferenceName) === '_Distortion');
assert(distortion); distortion.m_Value = .16;
g.save(shader);
u.write(material, u.read(material).replace(/- _Distortion: [^\n]+/, '- _Distortion: 0.16'));

// Scale only flame curves, including tangents; other animation channels remain byte-for-byte unchanged.
let animation = u.read(clip), count = 0;
animation = animation.replace(/  - curve:\n[\s\S]*?    path: FlameLayer_([012])\n/g, (part, index) => {
  assert(!part.includes('path: OverheatPressure'));
  const width = index === '2' ? .66 : .46;
  const heightFactor = index === '2' ? .68 : .60;
  count++;
  return part.replace(/(value|inSlope|outSlope): \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}/g, (_, kind, vx, vy, vz) =>
    `${kind}: {x: ${kind === 'value' ? width : Number(vx) * width / .7}, y: ${Number((Number(vy) * heightFactor).toFixed(8))}, z: ${vz}}`);
});
assert.equal(count, 3); u.write(clip, animation);
u.meta(script, 'MonoImporter');
const by = new Map(data.docs.map(d => [d.id, d]));
const root = by.get('1442915063');
root.body = root.body.replace('  m_Layer:', '  - component: {fileID: 2109202601}\n  m_Layer:');
const settings = [
  {renderer:'1924736955', transform:'1924736957', anchor:'1051275662', end:'643407020', along:.62, offset:-.02, phase:0, x:-.23, y:.68, width:.46, factor:.60},
  {renderer:'782624362', transform:'782624364', anchor:'458788770', end:'1636144051', along:.62, offset:-.02, phase:1.73, x:.23, y:.68, width:.46, factor:.60},
  {renderer:'1771092356', transform:'1771092358', anchor:'1542519030', end:'1906959784', along:.15, offset:-.10, phase:3.41, x:0, y:1.4, width:.66, factor:.68}
];
for (const s of settings) {
  const r = by.get(s.renderer); r.body = r.body.replace('m_CastShadows: 1', 'm_CastShadows: 0').replace('m_ReceiveShadows: 1', 'm_ReceiveShadows: 0');
  const tr = by.get(s.transform);
  tr.body = tr.body.replace(/m_LocalPosition: \{[^}]+\}/, `m_LocalPosition: {x: ${s.x}, y: ${s.y}, z: -0.07}`)
    .replace(/m_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}/, (_,x,y,z) => `m_LocalScale: {x: ${s.width}, y: ${Number(y)*s.factor}, z: ${z}}`);
}
const body = `--- !u!114 &2109202601
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1442915063}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${u.guid(script)}, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  sequence: {fileID: 1442915064}
  presentationCamera: {fileID: 2018382096}
  attachments:
${settings.map(s=>`  - flame: {fileID: ${s.renderer}}
    anchor: {fileID: ${s.anchor}}
    end: {fileID: ${s.end}}
    alongBone: ${s.along}
    heightOffset: ${s.offset}
    surfaceOffset: 0.07
    flowPhase: ${s.phase}
`).join('')}`;
const rootsIndex = data.docs.findIndex(d => d.type === 1660057539);
data.docs.splice(rootsIndex < 0 ? data.docs.length : rootsIndex, 0, {type:114,id:'2109202601',body});
u.write(scene, data.header + data.docs.map(d=>d.body).join(''));
fs.mkdirSync('Logs/OverheatUpright', {recursive:true});
u.write('Logs/OverheatUpright/changes.json', JSON.stringify({backup,scene,clip,shader,material,settings}, null, 2));
console.log('Updated Overheat; backup:', backup);
