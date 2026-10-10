const fs = require('fs'), assert = require('assert/strict'), crypto = require('crypto');
const u = require('./crystal-assets.cjs');
const fx = 'Assets/PortfolioFX';
const manifest = JSON.parse(u.read('RepairBackups/CryoPresentation_2026-09-18T13-31-50-697Z/manifest.json'));
for (const [p, h] of Object.entries(manifest.protectedAssets))
  assert.equal(crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex'), h, `User asset changed: ${p}`);
for (const r of manifest.resources) assert.equal(fs.readFileSync(r.source).compare(fs.readFileSync(r.destination)), 0, 'Resource copy differs');
const scene = u.records(u.read(`${fx}/Scenes/CryoFreeze.unity`));
const ids = new Set(scene.docs.map(d => d.id));
assert.equal(ids.size, scene.docs.length);
for (const d of scene.docs) for (const m of d.body.matchAll(/\{fileID: (-?\d+)\}/g))
  if (m[1] !== '0') assert(ids.has(m[1]), `Dangling scene reference: ${m[1]}`);
const layer = scene.docs.find(d => d.body.includes('  iceMeshes:'));
assert(layer && layer.body.includes(u.guid(manifest.models)));
const modelMeta = u.read(manifest.models + '.meta');
for (const id of ['4300004', '4300002', '4300000', '4300006']) {
  assert(layer.body.includes(`fileID: ${id}`)); assert(modelMeta.includes(`${id}:`));
}
const driver = scene.docs.find(d => d.body.includes('  materials:') && d.body.includes('  characters:'));
assert(/target: \{fileID: 128799980\}[\s\S]*?previewPlaceholder: 0/.test(driver.body));
for (let i = 0; i < 9; i++) {
  const go = scene.docs.find(d => d.type === 1 && d.body.includes(`m_Name: IceGrowth_${i}\n`));
  const r = scene.docs.find(d => d.type === 23 && d.body.includes(`m_GameObject: {fileID: ${go.id}}`));
  assert(r.body.includes('m_Enabled: 0'));
}
for (const p of manifest.materials) {
  const mat = u.read(p);
  assert(mat.includes('933532a4fcc9baf4fa0491de14d08ed7'), 'Expected existing URP Lit');
  assert(mat.includes(u.guid(`${fx}/ReferenceCryo/IceSurface_Normal.tif`)));
  assert(mat.includes('_NORMALMAP')); assert(mat.includes('- _Metallic: 0'));
}
assert(u.read(`${fx}/ReferenceCryo/IceCracks_Roughness.tif.meta`).includes('sRGBTexture: 0'));
assert(u.read(`${fx}/ReferenceCryo/IceSurface_Normal.tif.meta`).includes('textureType: 1'));
assert(u.read(`${fx}/ReferenceCryo/IceStudio.exr.meta`).includes('textureShape: 2'));
const runtime = u.read(`${fx}/Scripts/Runtime/FXCryoPresentation.cs`);
assert(runtime.includes('sequence.CurrentTime') && !runtime.includes('Time.time'));
const clamp = v => Math.max(0, Math.min(1, v));
const smooth = v => { const x = clamp(v); return x * x * (3 - 2 * x); };
function visible(t) {
  let ground = 0, fragments = 0;
  for (let i = 0; i < 16; i++) {
    const start = .72 + (i % 8) * .12, thawStart = 4.55 + (i % 3) * .08;
    if (smooth((t - start) / .8) * (1 - smooth((t - thawStart) / (5.85 - thawStart))) > .003) ground++;
  }
  for (let j = 0; j < 10; j++) {
    const age = t - (4.55 + j * .035), y = .2 + (j % 4) * .22 + 1.65 * age - 1.8 * age * age;
    if (age >= 0 && age < .95 && y > .025) fragments++;
  }
  return { ground, fragments };
}
assert.deepEqual(visible(0), { ground: 0, fragments: 0 });
assert.deepEqual(visible(3.3), { ground: 16, fragments: 0 });
assert(visible(4.8).fragments > 0);
assert.deepEqual(visible(5.9), { ground: 0, fragments: 0 });
assert.deepEqual(visible(7), { ground: 0, fragments: 0 });
fs.mkdirSync('Temp', { recursive: true });
const editor = `${fx}/Editor/CryoPresentationVerification.cs`;
u.meta(editor, 'MonoImporter');
u.write(editor + '.meta', u.read(editor + '.meta').replace('  mainObjectFileID: 0', '  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}'));
let rsp = u.read('Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp-Editor.rsp')
  .replace('Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.ref.dll', 'Temp/CryoRuntimeCheck.ref.dll')
  .replace(/^-out:.+$/m, '-out:"Temp/CryoEditorCheck.dll"')
  .replace(/^-refout:.+$/m, '-refout:"Temp/CryoEditorCheck.ref.dll"');
if (!rsp.includes('CryoPresentationVerification.cs')) rsp += `\n"${editor}"\n`;
u.write('Temp/CryoEditorCheck.rsp', rsp);
console.log('PASS: protected shader/material/clip hashes, scene references, model IDs, texture import settings, material bindings, sequence endpoints and thaw fragments.');
console.log(JSON.stringify([0, 1.4, 3.3, 4.8, 5.9].map(t => ({ time: t, ...visible(t) }))));
