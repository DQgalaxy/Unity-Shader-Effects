const fs = require('fs');
const crypto = require('crypto');
const read = p => fs.readFileSync(p, 'utf8').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');
const write = (p, s) => fs.writeFileSync(p, s);
const id = () => crypto.randomUUID().replaceAll('-', '');
const guid = p => read(p + '.meta').match(/^guid: (\w+)/m)[1];
function meta(p, importer = 'NativeFormatImporter', main = 0) {
  if (!fs.existsSync(p + '.meta')) write(p + '.meta', `fileFormatVersion: 2\nguid: ${id()}\n${importer}:\n  externalObjects: {}\n  mainObjectFileID: ${main}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
  return guid(p);
}
function graph(p) {
  const s = read(p), docs = [];
  let depth = 0, quoted = false, escape = false, start = 0;
  for (let i = 0; i < s.length; i++) {
    const c = s[i];
    if (quoted) { if (escape) escape = false; else if (c === '\\') escape = true; else if (c === '"') quoted = false; continue; }
    if (c === '"') quoted = true;
    if (c === '{') { if (!depth) start = i; depth++; }
    if (c === '}' && --depth === 0) docs.push(JSON.parse(s.slice(start, i + 1)));
  }
  if (depth || quoted || !docs.length) throw Error(`Invalid graph ${p}`);
  return docs;
}
function writeGraph(p, docs) { write(p, docs.map(o => JSON.stringify(o, null, 4)).join('\n\n') + '\n'); }
// Preserve Unity's tagged YAML verbatim, changing only selected object records.
function records(s) {
  const parts = s.split(/(?=^--- !u!)/m);
  return { header: parts.shift(), docs: parts.map(body => {
    const m = body.match(/^--- !u!(\d+) &(-?\d+)/);
    if (!m) throw Error('Invalid Unity object header');
    return { type: +m[1], id: m[2], body };
  }) };
}
module.exports = {read, write, id, guid, meta, graph, writeGraph, records};
