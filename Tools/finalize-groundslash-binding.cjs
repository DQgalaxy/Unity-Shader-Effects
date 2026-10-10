const assert = require('assert/strict');
const u = require('./crystal-assets.cjs');
const p = 'Assets/PortfolioFX/Shaders/SG_SlashDust.shadergraph';
const docs = u.graph(p), root = docs[0];
const active = new Set(root.m_Properties.map(r => r.m_Id));
const opacity = docs.filter(d => active.has(d.m_ObjectId) &&
  (d.m_OverrideReferenceName || d.m_DefaultReferenceName) === '_Opacity');
assert(opacity.length >= 1 && opacity.length <= 2);
if (opacity.length === 2) {
  const keep = opacity[0], remove = opacity[1];
  for (const doc of docs) {
    if (doc.m_Property?.m_Id === remove.m_ObjectId) doc.m_Property.m_Id = keep.m_ObjectId;
    if (doc.m_Type.endsWith('CategoryData'))
      doc.m_ChildObjectList = doc.m_ChildObjectList.filter(r => r.m_Id !== remove.m_ObjectId);
  }
  root.m_Properties = root.m_Properties.filter(r => r.m_Id !== remove.m_ObjectId);
  u.writeGraph(p, docs.filter(d => d.m_ObjectId !== remove.m_ObjectId));
}
console.log('Dust uses the original, single _Opacity property.');
