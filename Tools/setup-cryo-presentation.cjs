const fs = require('fs'), path = require('path'), crypto = require('crypto');
const u = require('./crystal-assets.cjs');
process.chdir(path.resolve(__dirname, '..'));
const fx = 'Assets/PortfolioFX', scenePath = `${fx}/Scenes/CryoFreeze.unity`;
const script = `${fx}/Scripts/Runtime/FXCryoPresentation.cs`;
if (u.read(scenePath).includes('  iceMeshes:')) throw Error('Cryo presentation already configured; do not overwrite later edits.');
const backup = `RepairBackups/CryoPresentation_${new Date().toISOString().replace(/[:.]/g, '-')}`;
fs.mkdirSync(backup, { recursive: true });
const protectedPaths = [`${fx}/Shaders/SG_FreezeBody.shadergraph`, `${fx}/MyMaterial/M_FreezeBody.mat`, `${fx}/Animations/CryoFreeze_Sequence.anim`];
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const hashes = Object.fromEntries(protectedPaths.map(p => [p, hash(p)]));
for (const p of [scenePath, ...protectedPaths]) fs.copyFileSync(p, `${backup}/${path.basename(p)}`);
const dest = `${fx}/ReferenceCryo`;
fs.mkdirSync(dest, { recursive: true });
const resources = [];
function copy(source, filename, metaTemplate = source + '.meta') {
  const p = `${dest}/${filename}`;
  if (fs.existsSync(p)) throw Error(`Do not overwrite existing resource ${p}`);
  fs.copyFileSync(source, p);
  u.write(p + '.meta', u.read(metaTemplate).replace(/^guid: \w+/m, `guid: ${u.id()}`));
  resources.push({ source, destination: p });
  return p;
}
const pack = 'Assets/UnityTechnologies/ParticlePack/EffectExamples/Magic Effects';
const normal = copy(`${pack}/Textures/IceNormals.tif`, 'IceSurface_Normal.tif');
const rough = copy('F:/UnityProjects/4/Assets/Resource/12/Section12/TexturesCom_Ice_Cracked_1K_roughness.tif', 'IceCracks_Roughness.tif', `${fx}/Textures/IceGrey.png.meta`);
copy(`${pack}/Textures/IceCurvature.tif`, 'IceSurface_Curvature.tif');
const cube = copy('F:/UnityProjects/1/Assets/Resources/7/Textures/Studio 05.exr', 'IceStudio.exr');

function makeMaterial(name, transparent) {
  const original = u.records(u.read(`${fx}/Materials/CryoFreeze_Surface_PLACEHOLDER.mat`));
  let b = original.docs.find(d => d.type === 21).body;
  b = b.replace('m_Name: CryoFreeze_Surface_PLACEHOLDER', `m_Name: ${name}`);
  b = b.replace('m_ValidKeywords: []', `m_ValidKeywords:\n  - _NORMALMAP${transparent ? '\n  - _SURFACE_TYPE_TRANSPARENT\n  - _ALPHAPREMULTIPLY_ON' : ''}`);
  b = b.replace('m_CustomRenderQueue: -1', `m_CustomRenderQueue: ${transparent ? 3000 : 2000}`);
  b = b.replace('RenderType: Opaque', `RenderType: ${transparent ? 'Transparent' : 'Opaque'}`);
  b = b.replace(/(- _BumpMap:\n\s+m_Texture:) [^\n]+/, `$1 {fileID: 2800000, guid: ${u.guid(normal)}, type: 3}`);
  const floats = { _BumpScale: transparent ? .3 : .22, _Metallic: 0, _Smoothness: transparent ? .95 : .7, _Glossiness: transparent ? .95 : .7, _GlossyReflections: 1, _Surface: transparent ? 1 : 0, _Blend: transparent ? 1 : 0, _SrcBlend: 1, _DstBlend: transparent ? 10 : 0, _SrcBlendAlpha: 1, _DstBlendAlpha: transparent ? 10 : 0, _ZWrite: transparent ? 0 : 1 };
  for (const [key, value] of Object.entries(floats)) b = b.replace(new RegExp(`(- ${key}:) [^\\n]+`), `$1 ${value}`);
  for (const key of ['_BaseColor', '_Color']) b = b.replace(new RegExp(`(- ${key}:) [^\\n]+`), `$1 ${transparent ? '{r: 0.3, g: 0.5, b: 0.61, a: 0.72}' : '{r: 0.64, g: 0.76, b: 0.8, a: 1}'}`);
  b = b.replace(/(- _EmissionColor:) [^\n]+/, '$1 {r: 0, g: 0, b: 0, a: 1}');
  const p = `${fx}/MyMaterial/${name}.mat`;
  if (fs.existsSync(p)) throw Error(`Material already exists: ${p}`);
  u.write(p, original.header + b); u.meta(p, 'NativeFormatImporter', 2100000);
  return p;
}
const clear = makeMaterial('M_CryoShard_Clear', true), frost = makeMaterial('M_CryoShard_Frost', false);
u.meta(script, 'MonoImporter');
u.write(script + '.meta', u.read(script + '.meta').replace('  mainObjectFileID: 0', '  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}'));

const scene = u.records(u.read(scenePath));
const map = new Map(scene.docs.map(d => [d.id, d]));
const go = name => scene.docs.find(d => d.type === 1 && d.body.includes(`  m_Name: ${name}\n`));
const component = (obj, type) => scene.docs.find(d => d.type === type && d.body.includes(`m_GameObject: {fileID: ${obj.id}}`));
const root = go('CryoFreeze_FX'), camera = component(go('Main Camera'), 20);
const driver = scene.docs.find(d => d.type === 114 && d.body.includes(`m_GameObject: {fileID: ${root.id}}`) && d.body.includes('  materials:'));
const ellen = component(go('Ellen_Body'), 137);
const legacy = [];
for (let i = 0; i < 9; i++) {
  const r = component(go(`IceGrowth_${i}`), 23);
  legacy.push(r.id); r.body = r.body.replace('m_Enabled: 1', 'm_Enabled: 0');
}
// Clear the placeholder switch only for the real character material; preserve all other bindings.
driver.body = driver.body.replace(new RegExp(`(- target: \\{fileID: ${ellen.id}\\}[\\s\\S]*?previewPlaceholder:) 1`), '$1 0');
driver.body = driver.body.replace(/  time: [^\n]+/, '  time: 0').replace(/  progress: [^\n]+/, '  progress: 0').replace(/  intensity: [^\n]+/, '  intensity: 0');
const id = '2230000001';
if (map.has(id)) throw Error('Scene ID collision');
root.body = root.body.replace('  m_Layer: 0', `  - component: {fileID: ${id}}\n  m_Layer: 0`);
const meshGUID = u.guid(`${pack}/Models/IceShard.FBX`);
scene.docs.push({ type: 114, id, body: `--- !u!114 &${id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${root.id}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${u.guid(script)}, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  sequence: {fileID: ${driver.id}}
  presentationCamera: {fileID: ${camera.id}}
  iceMeshes:
  - {fileID: 4300004, guid: ${meshGUID}, type: 3}
  - {fileID: 4300002, guid: ${meshGUID}, type: 3}
  - {fileID: 4300000, guid: ${meshGUID}, type: 3}
  - {fileID: 4300006, guid: ${meshGUID}, type: 3}
  clearIce: {fileID: 2100000, guid: ${u.guid(clear)}, type: 2}
  frostedIce: {fileID: 2100000, guid: ${u.guid(frost)}, type: 2}
  studioReflection: {fileID: 8900000, guid: ${u.guid(cube)}, type: 3}
  legacyCrystals:
${legacy.map(id => `  - {fileID: ${id}}`).join('\n')}
  reflectionIntensity: 0.9
` });
u.write(scenePath, scene.header + scene.docs.map(d => d.body).join(''));
for (const [p, before] of Object.entries(hashes)) if (hash(p) !== before) throw Error(`Protected user asset changed: ${p}`);
const manifest = { backup, protectedAssets: hashes, resources, models: `${pack}/Models/IceShard.FBX`, materials: [clear, frost], roughness: rough };
u.write(`${backup}/manifest.json`, JSON.stringify(manifest, null, 2));
console.log(JSON.stringify(manifest, null, 2));
