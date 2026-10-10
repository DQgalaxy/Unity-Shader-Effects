const assert = require('assert/strict');
const u = require('./crystal-assets.cjs');
const changes = JSON.parse(u.read('Logs/OverheatUpright/changes.json'));
const docs = u.graph(changes.shader), root = docs[0];
const map = new Map(docs.map(n => [n.m_ObjectId, n]));
assert.equal(map.size, docs.length);
const slot = (node, id) => node.m_Slots.map(r => map.get(r.m_Id)).find(s => s.m_Id === id);
const inputs = new Map();
for (const e of root.m_Edges) {
  const out = e.m_OutputSlot, input = e.m_InputSlot;
  assert.equal(slot(map.get(out.m_Node.m_Id), out.m_SlotId).m_SlotType, 1);
  assert.equal(slot(map.get(input.m_Node.m_Id), input.m_SlotId).m_SlotType, 0);
  const key = input.m_Node.m_Id + ':' + input.m_SlotId;
  assert(!inputs.has(key), 'Multiple connections to one input'); inputs.set(key, out);
}
const active = new Set(), visited = new Set();
function visit(id) {
  assert(!active.has(id), 'Graph cycle'); if (visited.has(id)) return;
  active.add(id);
  for (const e of root.m_Edges.filter(e => e.m_InputSlot.m_Node.m_Id === id)) visit(e.m_OutputSlot.m_Node.m_Id);
  active.delete(id); visited.add(id);
}
for (const r of root.m_Nodes) visit(r.m_Id);
const target = docs.find(n => n.m_Type.endsWith('.UniversalTarget'));
assert.equal(target.m_AlphaMode, 2); assert.equal(target.m_CastShadows, false);
assert.equal(target.m_ZWriteControl, 2);

// Evaluate the actual alpha graph with controlled texture/noise samples.
function alphaAt(uv, intensity, noise) {
  function input(n, id) {
    const ref = inputs.get(n.m_ObjectId + ':' + id);
    if (ref) return evaluate(map.get(ref.m_Node.m_Id), ref.m_SlotId);
    const value = slot(n, id).m_Value;
    return typeof value === 'number' ? value : value.x ?? value.e00;
  }
  function evaluate(n, output) {
    const type = n.m_Type.split('.').pop();
    const a = () => input(n, 0), b = () => input(n, 1);
    switch (type) {
      case 'UVNode': return uv;
      case 'SplitNode': return a()[output - 1];
      case 'PropertyNode': return intensity;
      case 'SubGraphNode': return noise;
      case 'SampleTexture2DNode': return .8;
      case 'MultiplyNode': return a() * b();
      case 'OneMinusNode': return 1 - a();
      case 'SaturateNode': return Math.max(0, Math.min(1, a()));
      case 'SmoothstepNode': {
        const lo = a(), hi = b(); assert(hi > lo, 'Reversed Smoothstep');
        const t = Math.max(0, Math.min(1, (input(n, 2) - lo) / (hi - lo)));
        return t * t * (3 - 2 * t);
      }
      default: throw Error(type);
    }
  }
  return input(docs.find(n => n.m_SerializedDescriptor === 'SurfaceDescription.Alpha'), 0);
}
for (const intensity of [0, .2, .6, 1.6]) for (const noise of [0, .3, .7, 1]) {
  for (let i = 0; i <= 10; i++) {
    const p = i / 10;
    for (const uv of [[0,p],[1,p],[p,0],[p,1]]) assert.equal(alphaAt(uv,intensity,noise), 0, 'Visible card border');
    const alpha = alphaAt([p,.5], intensity, noise); assert(alpha >= 0 && alpha <= 1);
  }
}
assert.equal(alphaAt([.5,.5], 0, .3), 0);
assert(alphaAt([.5,.5], .6, .3) > .2);

const scene = u.records(u.read(changes.scene));
const sceneMap = new Map(scene.docs.map(d => [d.id,d]));
assert.equal(sceneMap.size, scene.docs.length);
const component = sceneMap.get('2109202601'); assert(component);
for (const s of changes.settings) {
  for (const id of [s.renderer,s.transform,s.anchor,s.end]) assert(sceneMap.has(id), 'Missing scene attachment');
  assert(sceneMap.get(s.renderer).body.includes('m_CastShadows: 0'));
  assert(component.body.includes('anchor: {fileID: ' + s.anchor + '}'));
}
const allowed = new Set(['1442915063', ...changes.settings.flatMap(s => [s.renderer,s.transform])]);
for (const d of u.records(u.read(changes.backup + '/' + changes.scene)).docs)
  if (!allowed.has(d.id)) assert.equal(sceneMap.get(d.id).body, d.body, 'Unrelated scene record changed: ' + d.id);
const before = u.read(changes.backup + '/' + changes.clip), after = u.read(changes.clip);
assert.equal(after.slice(after.indexOf('  m_FloatCurves:')), before.slice(before.indexOf('  m_FloatCurves:')));
const curves = [...after.matchAll(/  - curve:\n([\s\S]*?)    path: FlameLayer_([012])\n/g)];
assert.equal(curves.length, 3);
for (const [,curve,index] of curves) {
  const values = [...curve.matchAll(/value: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}/g)];
  assert.equal(values.length, 6);
  assert(Math.max(...values.map(m=>+m[2])) <= 1.09);
  assert(values.every(m=>+m[1] === (index === '2' ? .66 : .46)));
}
console.log('PASS: graph references and DAG; alpha borders/range/off state; 3 bone bindings; shadows disabled; reduced flame curves; unrelated scene and animation records unchanged.');
