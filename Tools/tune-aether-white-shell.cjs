const fs = require('fs');
const path = require('path');
const u = require('./crystal-assets.cjs');
process.chdir(path.resolve(__dirname, '..'));
const { ElectricGraph } = require('./electric-graph-builder.cjs');
const graphPath = 'Assets/PortfolioFX/Shaders/SG_AetherOrb.shadergraph';
const matPath = 'Assets/PortfolioFX/MyMaterial/M_AetherOrb.mat';
const g = new ElectricGraph(graphPath);
if (g.docs.some(o => o.m_Title === '02 - White luminous shell / cyan interior'))
  throw Error('White-shell tuning already applied; preserve later edits.');
const backup = 'RepairBackups/AetherWhiteShell_' + new Date().toISOString().replace(/[:.]/g, '-');
fs.mkdirSync(backup, { recursive: true });
for (const p of [graphPath, matPath, 'ElectricOverload_改版说明.md']) fs.copyFileSync(p, path.join(backup, path.basename(p)));

const filament = g.docs.find(o => o.m_Type.endsWith('.PowerNode'));
const fresnel = g.docs.find(o => o.m_Type.endsWith('.FresnelNode'));
if (!filament || !fresnel) throw Error('Expected orb texture/Fresnel branches not found.');
const colors = {
  _DeepColor: { r: 1.15, g: 1.2, b: 1.28, a: 1 },
  _PlasmaColor: { r: .025, g: .48, b: 1.55, a: 1 },
  _RimColor: { r: 2, g: 2.05, b: 2.15, a: 1 }
};
for (const [key, value] of Object.entries(colors)) {
  const p = g.docs.find(o => o.m_Type.endsWith('ColorShaderProperty') && o.m_DefaultReferenceName === key);
  if (!p) throw Error('Missing property ' + key);
  p.m_Value = value;
}
g.value(fresnel, 2, 2.6);
g.group('02 - White luminous shell / cyan interior', () => {
  const veins = g.math('Multiply', g.math('Smoothstep', .015, .18, [filament, 2]), g.math('OneMinus', [fresnel, 3]));
  // Mix instead of adding blue onto white, so the white base cannot wash out the colored current.
  const surface = g.math('Lerp', g.props._DeepColor, g.props._PlasmaColor, veins);
  const rim = g.math('Multiply', [fresnel, 3], g.props._RimColor);
  const energy = g.math('Add', .95, g.math('Multiply', g.math('Saturate', g.props._Intensity), .25));
  g.output('BaseColor', g.math('Multiply', g.math('Add', surface, rim), energy));
});
g.prune();
g.save(graphPath);
let mat = u.read(matPath);
for (const [key, c] of Object.entries(colors)) {
  const regex = new RegExp(`(- ${key}:) [^\\n]+`);
  if (!regex.test(mat)) throw Error('Missing material property ' + key);
  mat = mat.replace(regex, `$1 {r: ${c.r}, g: ${c.g}, b: ${c.b}, a: 1}`);
}
u.write(matPath, mat);
console.log(JSON.stringify({backup, colors, formula:'(lerp(WhiteShell,CyanCurrent,veins*(1-fresnel))+WhiteRim*fresnel)*(.95+.25*saturate(Intensity))'}, null, 2));
