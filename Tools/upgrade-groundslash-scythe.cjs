const fs = require('fs');
const assert = require('assert/strict');
const u = require('./crystal-assets.cjs');
const { ElectricGraph } = require('./electric-graph-builder.cjs');
const root = 'Assets/PortfolioFX/';
const graphPath = root + 'Shaders/SG_SlashScythe.shadergraph';
const materialPath = root + 'MyMaterial/M_SlashScythe.mat';
const sparksPath = root + 'MyMaterial/M_SlashSparks.mat';
assert(!fs.existsSync(graphPath), 'Upgrade already applied; do not overwrite user edits.');

const g = new ElectricGraph();
g.root.m_Path = 'PortfolioFX/GroundSlash';
g.target.m_AllowMaterialOverride = false;
const uv = g.node('UV'); uv.m_OutputChannel = 0;
const [across, along] = g.split([uv, 0]);
const time = g.float('_EffectTime', 0);
const opacity = g.float('_ObjectOpacity', 1);
const brightness = g.float('_Brightness', 3.2);
const wakeStrength = g.float('_WakeStrength', .32);
const edgeVisibility = g.float('_EdgeVisibility', 1);
const edgeColor = g.color('_EdgeColor', [.7, .93, 1]);
const wakeColor = g.color('_WakeColor', [.025, .27, .58]);
const flowTex = g.texture('_FlowTex', root + 'Textures/NoiseRG.png');
const front = g.group('Bright leading edge - UV.x = 0', () =>
  g.math('OneMinus', g.math('Smoothstep', .045, .19, across)));
const tip = g.group('Taper both crescent tips - UV.y', () =>
  g.math('Power', g.math('Saturate', g.math('Sine', g.math('Multiply', along, Math.PI))), .6));
const noise = g.group('Flowing texture in the wake', () => {
  const sampleUV = g.math('Add', g.math('Multiply', [uv, 0], [2, 7, 0, 0]), g.scroll(time, -1.1, .15));
  const sample = g.sample(flowTex, sampleUV);
  return g.math('Add', .35, g.math('Multiply', [sample, 4], .65));
});
const alpha = g.group('Single continuous blade and fading rear', () => {
  const outer = g.math('Smoothstep', 0, .025, across);
  const rear = g.math('Power', g.math('Saturate', g.math('OneMinus', across)), 1.35);
  const textured = g.math('Lerp', noise, 1, front);
  const strength = g.math('Lerp', wakeStrength, edgeVisibility, front);
  return g.math('Saturate', g.math('Multiply', opacity,
    g.math('Multiply', strength, g.math('Multiply', textured,
      g.math('Multiply', outer, g.math('Multiply', rear, tip))))));
});
const color = g.math('Multiply', brightness, g.math('Lerp', wakeColor, edgeColor, front));
g.output('BaseColor', color);
g.output('Alpha', alpha);
g.prune(); g.save(graphPath);

function setFloat(s, name, value) {
  const re = new RegExp('    - ' + name + ': [^\\n]+');
  return re.test(s) ? s.replace(re, `    - ${name}: ${value}`) :
    s.replace('    m_Floats:\n', `    m_Floats:\n    - ${name}: ${value}\n`);
}
let material = u.read(root + 'MyMaterial/M_GroundSlash.mat');
material = material.replace('m_Name: M_GroundSlash', 'm_Name: M_SlashScythe')
  .replace(/(m_Shader: \{fileID: [^,]+, guid: )\w+/, '$1' + u.guid(graphPath));
material = material.replace('    m_TexEnvs:\n',
  `    m_TexEnvs:\n    - _FlowTex:\n        m_Texture: {fileID: 2800000, guid: ${u.guid(root + 'Textures/NoiseRG.png')}, type: 3}\n        m_Scale: {x: 1, y: 1}\n        m_Offset: {x: 0, y: 0}\n`);
material = material.replace('    m_Colors:\n', '    m_Colors:\n    - _WakeColor: {r: 0.025, g: 0.27, b: 0.58, a: 1}\n');
material = material.replace(/    - _EdgeColor: [^\n]+/, '    - _EdgeColor: {r: 0.7, g: 0.93, b: 1, a: 1}');
for (const [key, value] of Object.entries({_Brightness: 3.2, _WakeStrength: .32, _EdgeVisibility: 1}))
  material = setFloat(material, key, value);
u.write(materialPath, material); u.meta(materialPath, 'NativeFormatImporter', 2100000);

// The existing additive sprite graph already reads particle vertex color and alpha.
let sparks = u.read(root + 'MyMaterial/M_ShieldSparks.mat')
  .replace('m_Name: M_ShieldSparks', 'm_Name: M_SlashSparks')
  .replace(/    - _Tint: [^\n]+/, '    - _Tint: {r: 1, g: 1, b: 1, a: 1}');
sparks = setFloat(sparks, '_Gain', 4);
u.write(sparksPath, sparks); u.meta(sparksPath, 'NativeFormatImporter', 2100000);

const scenePath = root + 'Scenes/GroundSlash.unity';
const scene = u.records(u.read(scenePath));
const component = scene.docs.find(d => d.id === '2109210102');
assert(component);
component.body = component.body
  .replace(/  bladeMaterial: [^\n]+/, `  bladeMaterial: {fileID: 2100000, guid: ${u.guid(materialPath)}, type: 2}`)
  .replace(/  trailMaterial: [^\n]+\n/, '')
  .replace(/  dustMaterial: [^\n]+/, m => m + `\n  sparkMaterial: {fileID: 2100000, guid: ${u.guid(sparksPath)}, type: 2}`)
  .replace('  brightness: 3.5', '  brightness: 3.2')
  .replace('  bladeSize: 1.65', '  bladeSize: 1.8');
component.body += '  trailDuration: 0.13\n';
u.write(scenePath, scene.header + scene.docs.map(d => d.body).join(''));
console.log('Bound single scythe mesh shader and impact sparks.');
