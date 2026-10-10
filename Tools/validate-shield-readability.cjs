const assert = require('assert/strict');
const fs = require('fs');
const zlib = require('zlib');
const u = require('./crystal-assets.cjs');
const docs = u.graph('Assets/PortfolioFX/Shaders/SG_ReactiveShield_HexSequence.shadergraph');
const map = new Map(docs.map(o => [o.m_ObjectId, o]));
function upstream(n, slot) {
  const e = docs[0].m_Edges.find(e => e.m_InputSlot.m_Node.m_Id === n.m_ObjectId && e.m_InputSlot.m_SlotId === slot);
  return map.get(e?.m_OutputSlot.m_Node.m_Id);
}
const wave = docs.find(n => n.m_FunctionName === 'ShieldSequenceWaves');
const center = upstream(wave, 0);
assert(center.m_Type.endsWith('SubtractNode'));
assert(upstream(center, 1).m_Type.endsWith('RejectionNode'));
const noise = upstream(wave, 1);
const noiseScale = upstream(noise, 2);
const xz = upstream(noiseScale, 0);
assert.equal(upstream(upstream(xz, 0), 0), center, 'Noise must use the same cell center');
assert.equal(upstream(xz, 0), upstream(xz, 1), 'Noise XZ channels must share a split');
for (const [name, value] of [['_CellPush', .38], ['_HitSpread', .45], ['_LineThreshold', .12]]) {
  assert.equal(docs.find(o => o.m_DefaultReferenceName === name).m_Value, value);
  const saved = u.read('Assets/PortfolioFX/MyMaterial/M_ReactiveShield.mat').match(new RegExp(`- ${name}: ([^\\r\\n]+)`));
  assert(saved && Number.isFinite(Number(saved[1])), 'Missing or invalid material setting; artist tuning is allowed');
}

// Inspect only this known FBX 7400 fixture, without requiring Unity or changing its importer.
const bytes = fs.readFileSync('Assets/PortfolioFX/ReferenceShield/ReferenceHexagon.fbx');
assert.equal(bytes.readUInt32LE(23), 7400, 'Update the fixture reader for a different FBX version');
let offset = 27;
function property() {
  const type = String.fromCharCode(bytes[offset++]);
  const scalar = { Y: ['readInt16LE', 2], C: ['readUInt8', 1], I: ['readInt32LE', 4], F: ['readFloatLE', 4], D: ['readDoubleLE', 8], L: ['readBigInt64LE', 8] }[type];
  if (scalar) { const v = bytes[scalar[0]](offset); offset += scalar[1]; return v; }
  if (type === 'S' || type === 'R') {
    const size = bytes.readUInt32LE(offset); offset += 4;
    const b = bytes.subarray(offset, offset + size); offset += size;
    return type === 'S' ? b.toString('utf8') : b;
  }
  const array = { d: ['readDoubleLE', 8], f: ['readFloatLE', 4], i: ['readInt32LE', 4], l: ['readBigInt64LE', 8], b: ['readUInt8', 1], c: ['readUInt8', 1] }[type];
  assert(array, `Unsupported FBX property ${type}`);
  const length = bytes.readUInt32LE(offset), compressed = bytes.readUInt32LE(offset + 4), size = bytes.readUInt32LE(offset + 8); offset += 12;
  let b = bytes.subarray(offset, offset + size); offset += size;
  if (compressed) b = zlib.inflateSync(b);
  return Array.from({ length }, (_, i) => b[array[0]](i * array[1]));
}
function node() {
  const end = bytes.readUInt32LE(offset);
  if (!end) { offset += 13; return null; }
  const count = bytes.readUInt32LE(offset + 4), nameLength = bytes[offset + 12]; offset += 13;
  const name = bytes.toString('utf8', offset, offset + nameLength); offset += nameLength;
  const props = Array.from({ length: count }, property), children = [];
  while (offset < end) { const child = node(); if (!child) break; children.push(child); }
  offset = end;
  return { name, props, children };
}
const all = [];
function visit(n) { all.push(n); n.children.forEach(visit); }
while (offset + 13 < bytes.length) { const n = node(); if (!n) break; visit(n); }
const geometries = all.filter(n => n.name === 'Geometry' && n.props[2] === 'Mesh');
let polygons = 0, maxCenterError = 0, maxWorldError = 0;
const child = (n, name) => n.children.find(c => c.name === name);
for (const geometry of geometries) {
  const vertices = child(geometry, 'Vertices').props[0];
  const min = [Infinity, Infinity, Infinity], max = [-Infinity, -Infinity, -Infinity];
  vertices.forEach((v, i) => { const axis = i % 3; min[axis] = Math.min(min[axis], v); max[axis] = Math.max(max[axis], v); });
  const diameter = Math.max(...max.map((v, axis) => v - min[axis]));
  let geometryError = 0;
  const indices = child(geometry, 'PolygonVertexIndex').props[0];
  const layer = child(geometry, 'LayerElementNormal');
  assert.equal(child(layer, 'MappingInformationType').props[0], 'ByPolygonVertex');
  const normals = child(layer, 'Normals').props[0];
  const reference = child(layer, 'ReferenceInformationType').props[0];
  assert(['Direct', 'IndexToDirect'].includes(reference));
  const normalIndices = child(layer, 'NormalsIndex')?.props[0];
  let firstCenter, firstNormal;
  indices.forEach((index, corner) => {
    const v = (index < 0 ? -index - 1 : index) * 3;
    const ni = (reference === 'Direct' ? corner : normalIndices[corner]) * 3;
    const n = normals.slice(ni, ni + 3), length = Math.hypot(...n);
    const unit = n.map(x => x / length);
    const distance = unit.reduce((sum, x, axis) => sum + x * vertices[v + axis], 0);
    const c = unit.map(x => x * distance);
    if (!firstCenter) { firstCenter = c; firstNormal = unit; }
    geometryError = Math.max(geometryError, Math.hypot(...c.map((x, axis) => x - firstCenter[axis])));
    assert(Math.hypot(...unit.map((x, axis) => x - firstNormal[axis])) < 1e-4, 'Cell has smooth rather than flat normals');
    if (index < 0) { polygons++; firstCenter = firstNormal = null; }
  });
  // FBX source units are not world units; the presentation normalizes diameter to 3.2.
  maxCenterError = Math.max(maxCenterError, geometryError);
  maxWorldError = Math.max(maxWorldError, geometryError * 3.2 / diameter);
  console.log(`FBX geometry diameter ${diameter}; source center error ${geometryError}; world error ${geometryError * 3.2 / diameter}`);
}
assert(polygons > 0);
// Imported faces are slightly nonplanar. Allow 1 mm on the 3.2-unit shield,
// less than 1/450 of the wave width; a smooth sphere fails the normal test above.
assert(maxWorldError < .001, `Cell center differs between corners in world units: ${maxWorldError}`);
console.log(`PASS: shared hit/noise cell centers, material defaults; ${polygons} FBX polygons, max intra-cell center error ${maxWorldError} world units (${maxCenterError} source units).`);
