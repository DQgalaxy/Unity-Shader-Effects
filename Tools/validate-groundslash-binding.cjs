const assert = require('assert/strict');
const fs = require('fs');
const u = require('./crystal-assets.cjs');
const root = 'Assets/PortfolioFX/';
const checks = [];
function check(condition, label) { assert(condition, label); checks.push(label); }
const shader = n => root + 'Shaders/SG_' + n + '.shadergraph';
const mat = n => root + 'MyMaterial/M_' + n + '.mat';
for (const name of ['GroundSlash', 'SlashWake', 'SlashScythe', 'SlashGroundCut', 'SlashDust']) {
  const docs = u.graph(shader(name)), graph = docs[0];
  const by = new Map(docs.map(d => [d.m_ObjectId, d]));
  check(by.size === docs.length, name + ': unique object IDs');
  const nodes = graph.m_Nodes.map(r => by.get(r.m_Id));
  const refs = graph.m_Properties.map(r => by.get(r.m_Id))
    .map(p => p.m_OverrideReferenceName || p.m_DefaultReferenceName);
  check(new Set(refs).size === refs.length, name + ': unique property reference names');
  const inputs = new Set();
  for (const edge of graph.m_Edges) {
    for (const [end, type] of [[edge.m_OutputSlot, 1], [edge.m_InputSlot, 0]]) {
      const node = by.get(end.m_Node.m_Id);
      assert(node && nodes.includes(node), name + ': edge node missing');
      const slot = node.m_Slots.map(r => by.get(r.m_Id)).find(s => s.m_Id === end.m_SlotId);
      assert(slot && slot.m_SlotType === type, name + ': edge slot missing or reversed');
    }
    const input = edge.m_InputSlot.m_Node.m_Id + ':' + edge.m_InputSlot.m_SlotId;
    assert(!inputs.has(input), name + ': duplicate input edge'); inputs.add(input);
  }
  check(true, name + ': edges resolve to valid input/output slots');
  for (const node of nodes.filter(n => n.m_Type.endsWith('PropertyNode')))
    assert(graph.m_Properties.some(r => r.m_Id === node.m_Property.m_Id));
  const target = docs.find(d => d.m_Type.endsWith('UniversalTarget'));
  check(target.m_SurfaceType === 1 && target.m_ZWriteControl === 2 &&
    target.m_AlphaMode === (['GroundSlash', 'SlashWake', 'SlashScythe'].includes(name) ? 2 : 0),
    name + ': transparent blend and depth settings');
  const alpha = nodes.find(n => n.m_Name === 'SurfaceDescription.Alpha');
  check(graph.m_Edges.some(e => e.m_InputSlot.m_Node.m_Id === alpha.m_ObjectId), name + ': Alpha connected');
}
for (const [material, graph] of [['GroundSlash', 'GroundSlash'], ['SlashScythe', 'SlashScythe'],
  ['SlashSparks', 'ShieldImpactLayers'], ['SlashWake', 'SlashWake'],
  ['SlashFlash', 'GroundSlash'], ['SlashGroundCut', 'SlashGroundCut'], ['SlashDust', 'SlashDust']]) {
  const text = u.read(mat(material));
  check(text.includes('guid: ' + u.guid(shader(graph))), material + ': shader binding');
}
for (const [material, property, texture] of [['GroundSlash', '_MaskTex', 'Blade.png'],
  ['SlashFlash', '_MaskTex', 'Flash.png'], ['SlashWake', '_MaskTex', 'Trail.png'],
  ['SlashGroundCut', '_CrackTex', 'Crack.tif'], ['SlashDust', '_DustTex', 'Dust.png']]) {
  const match = u.read(mat(material)).match(new RegExp('- ' + property + ':\\n[^\\n]+'));
  check(match && match[0].includes(u.guid(root + 'ReferenceGroundSlash/T_Slash' + texture)),
    material + ': correct texture');
}
const scene = u.records(u.read(root + 'Scenes/GroundSlash.unity'));
const by = new Map(scene.docs.map(d => [d.id, d]));
check(by.size === scene.docs.length, 'Scene: unique file IDs');
for (const d of scene.docs) {
  for (const m of d.body.matchAll(/\{fileID: (-?\d+)\}/g))
    assert(m[1] === '0' || by.has(m[1]), 'Scene: missing local reference ' + m[1]);
}
check(true, 'Scene: all local object references resolve');
const lesson = by.get('2109210102').body;
check(lesson.includes(u.guid(root + 'Scripts/Runtime/FXGroundSlashLesson.cs')), 'Scene: presentation script bound');
check(lesson.includes('hand: {fileID: 1158883386}') &&
  by.get('1158883386').body.includes('m_GameObject: {fileID: 1158883385}') &&
  by.get('1158883385').body.includes('m_Name: Ellen_Left_Hand'), 'Scene: release uses Ellen left hand');
check(lesson.includes('releaseSize: 0.45') && lesson.includes('handForwardOffset: 0.32'),
  'Scene: compact release with forward hand clearance');
for (const name of ['SlashScythe', 'SlashSparks', 'SlashFlash', 'SlashGroundCut', 'SlashDust', 'SlashDebris'])
  check(lesson.includes(u.guid(mat(name))), 'Scene: M_' + name + ' assigned');
const old = scene.docs.filter(d => d.type === 1 &&
  /m_Name: (SlashRibbon|ContactWave|GroundSpray|GroundDebris_\d+|EllenStaff|Staff_Body)\n/.test(d.body));
check(old.length === 12 && old.every(d => d.body.includes('m_IsActive: 0')), 'Scene: 12 old effect/weapon objects inactive');
check(/materials: \[\]\n  particles: \[\]/.test(by.get('1646226398').body), 'Scene: old material/particle drivers cleared');
check(by.get('2109210111').body.includes('y: 0.01') && by.get('2109210121').body.includes('y: 0.01'),
  'Scene: hit and collision plane align with platform top at y=0.01');
const rockMeta = u.read('Assets/UnityTechnologies/ParticlePack/EffectExamples/Magic Effects/Models/RockDebris.FBX.meta');
check(rockMeta.includes('4300000: RockDebris_Low') && lesson.includes('fileID: 4300000'), 'Scene: debris uses RockDebris_Low submesh');
const report = { checks, count: checks.length, unityRendered: false,
  limitation: 'Static asset validation only. Unity import and visual verification are separate.' };
fs.mkdirSync('Logs/GroundSlashBinding', { recursive: true });
u.write('Logs/GroundSlashBinding/static-checks.json', JSON.stringify(report, null, 2));
console.log(JSON.stringify(report, null, 2));
