// One-shot, scoped migration of the existing shield graph. Never rebuild the scene.
const u = require('./crystal-assets.cjs');
const { Graph } = require('./shield-graph-builder.cjs');
const p = 'Assets/PortfolioFX/Shaders/SG_ReactiveShield_HexSequence.shadergraph';
const docs = u.graph(p);
if (docs.some(o => o.m_DefaultReferenceName === '_LineThreshold')) {
  throw Error('Readability tuning already applied; edit the material for further tuning.');
}
const g = Object.create(Graph.prototype);
Object.assign(g, { docs, root: docs[0], map: new Map(docs.map(o => [o.m_ObjectId, o])), props: {} });
const nodes = type => docs.filter(o => o.m_Type === `UnityEditor.ShaderGraph.${type}Node`);
const input = (n, slot) => g.root.m_Edges.find(e => e.m_InputSlot.m_Node.m_Id === n.m_ObjectId && e.m_InputSlot.m_SlotId === slot);
const source = (n, slot) => g.map.get(input(n, slot).m_OutputSlot.m_Node.m_Id);
const replace = (n, slot, value) => {
  g.root.m_Edges = g.root.m_Edges.filter(e => e !== input(n, slot));
  g.feed(value, n, slot);
};
const property = name => docs.find(o => o.m_DefaultReferenceName === name);
const propertyNode = name => nodes('Property').find(n => n.m_Property.m_Id === property(name).m_ObjectId);

// The source mesh has planar, flat-normal cells around its object origin.
// Remove the tangent-plane offset: all vertices in one face share this center.
const pos = nodes('Position').find(n => n.m_Space === 2);
const normal = nodes('NormalVector').find(n => n.m_Space === 2);
const origin = g.node('Transform');
origin.m_Conversion = { from: 0, to: 2 }; origin.m_ConversionType = 0;
g.value(origin, 0, [0, 0, 0]);
const relative = g.math('Subtract', [pos, 0], [origin, 1]);
const tangent = g.math('Rejection', relative, [normal, 0]);
const center = g.math('Subtract', [pos, 0], tangent);
const wave = nodes('CustomFunction').find(n => n.m_FunctionName === 'ShieldSequenceWaves');
replace(wave, 0, center);

// Sample noise at the cell center too, so noise cannot bend a cell's corners.
const centerSplit = g.node('Split'); g.feed(center, centerSplit, 0);
const centerXZ = g.node('Combine');
g.feed([centerSplit, 1], centerXZ, 0); g.feed([centerSplit, 3], centerXZ, 1);
const noise = nodes('SampleTexture2DLOD')[0];
replace(source(noise, 2), 0, [centerXZ, 6]);

const hexSampler = nodes('SampleTexture2D').find(n => source(n, 1) === propertyNode('_HexagonMap'));
const thresholdEdge = g.root.m_Edges.find(e => e.m_OutputSlot.m_Node.m_Id === hexSampler.m_ObjectId && e.m_OutputSlot.m_SlotId === 4);
const threshold = g.map.get(thresholdEdge.m_InputSlot.m_Node.m_Id);
g.feed(g.float('_LineThreshold', .12), threshold, 1);
property('_CellPush').m_Value = .38;
property('_HitSpread').m_Value = .45;
property('_HitFadePower').m_Value = .5;

// Boost line coverage, not the transparent interior of the entire shield.
for (const n of nodes('Multiply')) {
  if (!input(n, 1)) {
    const v = g.slot(n, 1).m_Value;
    const scalar = typeof v === 'number' ? v : Object.values(v)[0];
    if (Math.abs(scalar - .24) < 1e-6) g.value(n, 1, .55);
    if (Math.abs(scalar - 1.15) < 1e-6) g.value(n, 1, 1.65);
  }
}
g.save(p);
console.log(`Updated ${p}; ${g.root.m_Edges.length} connections. Scene and original graphs untouched.`);
