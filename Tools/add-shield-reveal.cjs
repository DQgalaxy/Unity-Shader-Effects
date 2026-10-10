// Add a reveal branch to the current graph without regenerating scene or existing properties.
const u = require('./crystal-assets.cjs');
const { Graph } = require('./shield-graph-builder.cjs');
const path = 'Assets/PortfolioFX/Shaders/SG_ReactiveShield_HexSequence.shadergraph';
const docs = u.graph(path);
if (docs.some(o => o.m_DefaultReferenceName === '_RevealProgress')) throw Error('Reveal is already installed.');
const g = Object.create(Graph.prototype);
Object.assign(g, { docs, root: docs[0], map: new Map(docs.map(o => [o.m_ObjectId, o])), props: {} });
for (const n of docs.filter(o => o.m_Property)) {
  const p = g.map.get(n.m_Property.m_Id);
  g.props[p.m_OverrideReferenceName || p.m_DefaultReferenceName] = [n, 0];
}
const pos = docs.find(n => n.m_Type.endsWith('.PositionNode') && n.m_Space === 2);
const cellNoise = docs.find(n => n.m_Type.endsWith('.SampleTexture2DLODNode'));
const split = g.node('Split'); g.feed([pos, 0], split, 0);
const xy = g.node('Combine'); g.feed([split, 1], xy, 0); g.feed([split, 2], xy, 1);
const detail = g.sample(g.props._HitNoiseMap, g.math('Multiply', [xy, 6], 2.5));
const noise = g.math('Lerp', [cellNoise, 5], [detail, 4], .35);
const height = g.math('Saturate', g.math('Divide',
  g.math('Subtract', [split, 2], g.float('_RevealBottom', -.3)),
  g.math('Maximum', g.float('_RevealHeight', 3.2), .001)));
const field = g.math('Saturate', g.math('Lerp', height, noise,
  g.math('Saturate', g.float('_RevealNoiseAmount', .26))));
const softness = g.math('Maximum', g.float('_RevealSoftness', .03), .002);
const width = g.math('Maximum', g.float('_RevealEdgeWidth', .07), .002);
const negativeSoftness = g.math('Negate', softness);
const front = g.math('Lerp', negativeSoftness, g.math('Add', 1, g.math('Add', softness, width)),
  g.math('Saturate', g.float('_RevealProgress', 1)));
const delta = g.math('Subtract', front, field);
const mask = g.math('Smoothstep', negativeSoftness, softness, delta);
const edge = g.math('Multiply', mask, g.math('OneMinus',
  g.math('Smoothstep', softness, g.math('Add', softness, width), delta)));

function detachOutput(name) {
  const n = docs.find(o => o.m_SerializedDescriptor === `SurfaceDescription.${name}`);
  const e = g.root.m_Edges.find(e => e.m_InputSlot.m_Node.m_Id === n.m_ObjectId && e.m_InputSlot.m_SlotId === 0);
  g.root.m_Edges = g.root.m_Edges.filter(x => x !== e);
  return [g.map.get(e.m_OutputSlot.m_Node.m_Id), e.m_OutputSlot.m_SlotId];
}
const oldColor = detachOutput('BaseColor'), oldAlpha = detachOutput('Alpha');
g.output('BaseColor', g.math('Add', oldColor, g.math('Multiply', edge, g.color('_RevealEdgeColor', [.15, 2.4, 3]))));
const edgeCoverage = g.math('Multiply', g.math('Multiply', edge, .35), g.props._Visibility);
g.output('Alpha', g.math('Multiply', g.math('Saturate', g.math('Add', oldAlpha, edgeCoverage)), mask));
g.save(path);
console.log(`Reveal added: ${g.root.m_Edges.length} graph connections; original color/alpha branches retained.`);
