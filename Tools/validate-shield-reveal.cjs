// Evaluate the serialized reveal branch, rather than duplicating its formula in the test.
const assert = require('assert/strict');
const u = require('./crystal-assets.cjs');
const docs = u.graph('Assets/PortfolioFX/Shaders/SG_ReactiveShield_HexSequence.shadergraph');
const map = new Map(docs.map(o => [o.m_ObjectId, o]));
const incoming = new Map(docs[0].m_Edges.map(e => [`${e.m_InputSlot.m_Node.m_Id}:${e.m_InputSlot.m_SlotId}`, e.m_OutputSlot]));
const source = (n, slot) => incoming.get(`${n.m_ObjectId}:${slot}`);
const sourceNode = (n, slot) => map.get(source(n, slot).m_Node.m_Id);
const alpha = docs.find(n => n.m_SerializedDescriptor === 'SurfaceDescription.Alpha');
const finalAlpha = sourceNode(alpha, 0);
const mask = source(finalAlpha, 1);
const alphaSum = sourceNode(sourceNode(finalAlpha, 0), 0);
const originalAlpha = sourceNode(alphaSum, 0);
const baseColor = docs.find(n => n.m_SerializedDescriptor === 'SurfaceDescription.BaseColor');
const edgeColor = sourceNode(sourceNode(baseColor, 0), 1);
const edge = source(edgeColor, 0);
const state = { progress: 0, y: 0, cellNoise: 0, detailNoise: 0, visibility: 1 };
const clamp = x => Math.min(1, Math.max(0, x));
const override = {};
function evaluate(ref) {
  const n = map.get(ref.m_Node.m_Id), out = ref.m_SlotId;
  if (n === originalAlpha) return .45 * state.visibility;
  function input(id) {
    const r = source(n, id);
    if (r) return evaluate(r);
    const slot = map.get(n.m_Slots.find(r => map.get(r.m_Id).m_Id === id).m_Id);
    return typeof slot.m_Value === 'number' ? slot.m_Value : Object.values(slot.m_Value)[0];
  }
  const type = n.m_Type.split('.').pop();
  switch (type) {
    case 'PropertyNode': {
      const p = map.get(n.m_Property.m_Id), name = p.m_OverrideReferenceName || p.m_DefaultReferenceName;
      if (name === '_RevealProgress') return state.progress;
      if (name === '_Visibility') return state.visibility;
      return override[name] ?? p.m_Value;
    }
    case 'PositionNode': return [0, state.y, 0, 0];
    case 'SplitNode': return input(0)[out - 1];
    case 'SampleTexture2DNode': assert.equal(out, 4); return state.detailNoise;
    case 'SampleTexture2DLODNode': assert.equal(out, 5); return state.cellNoise;
    case 'AddNode': return input(0) + input(1);
    case 'SubtractNode': return input(0) - input(1);
    case 'MultiplyNode': return input(0) * input(1);
    case 'DivideNode': return input(0) / input(1);
    case 'MaximumNode': return Math.max(input(0), input(1));
    case 'SaturateNode': return clamp(input(0));
    case 'NegateNode': return -input(0);
    case 'OneMinusNode': return 1 - input(0);
    case 'LerpNode': return input(0) + (input(1) - input(0)) * input(2);
    case 'SmoothstepNode': {
      const t = clamp((input(2) - input(0)) / (input(1) - input(0)));
      return t * t * (3 - 2 * t);
    }
    default: throw Error(`Unexpected node in reveal branch: ${type}`);
  }
}
let samples = 0;
for (const soft of [0, .03, .1]) for (const width of [0, .07, .2]) {
  override._RevealSoftness = soft; override._RevealEdgeWidth = width;
  for (const noiseAmount of [0, .26, 1]) {
    override._RevealNoiseAmount = noiseAmount;
    for (const y of [-.5, -.3, .5, 1.3, 2.1, 2.9, 3.3]) for (const noise of [0, .5, 1]) {
      Object.assign(state, { y, cellNoise: noise, detailNoise: 1 - noise, visibility: 1 });
      let previous = 0;
      for (let step = 0; step <= 20; step++) {
        state.progress = step / 20;
        const m = evaluate(mask), e = evaluate(edge);
        assert(Number.isFinite(m) && m >= previous - 1e-12 && m <= 1);
        assert(Number.isFinite(e) && e >= -1e-12 && e <= 1);
        if (step === 0) { assert.equal(m, 0); assert.equal(e, 0); assert.equal(evaluate(source(alpha, 0)), 0); }
        if (step === 20) { assert.equal(m, 1); assert(Math.abs(e) < 1e-12); assert(Math.abs(evaluate(source(alpha, 0)) - .45) < 1e-12); }
        previous = m; samples++;
      }
      state.progress = 0;
      assert.equal(evaluate(source(alpha, 0)), 0, 'Backward seek must hide the shield again');
    }
  }
}
for (const name of ['_RevealSoftness', '_RevealEdgeWidth', '_RevealNoiseAmount']) delete override[name];
Object.assign(state, { progress: .5, y: 1.3, cellNoise: .5, detailNoise: .5, visibility: 0 });
assert.equal(evaluate(source(alpha, 0)), 0, 'Reveal edge must obey ending visibility');
const script = u.read('Assets/PortfolioFX/Scripts/Runtime/FXShieldPresentation.cs');
assert(script.includes('Mathf.InverseLerp(.05f, 1.4f, t)'));
assert(script.includes('readonly float[] hits = { 1.5f, 3.6f, 5.7f }'));
console.log(`PASS: ${samples} serialized graph samples; hidden start, monotonic reveal, clean end, backward reset, visibility, unchanged hit times.`);
