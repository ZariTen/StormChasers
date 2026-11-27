const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const read = file => JSON.parse(fs.readFileSync(path.join(root, file), 'utf8'));
const projectFile = path.join(root, 'storm_disasters.sbproj');
assert.ok(fs.existsSync(projectFile), 'The Sandbox add-on project does not exist');
const project = read('storm_disasters.sbproj');
assert.equal(project.Type, 'addon');
assert.equal(project.Metadata.ParentPackage, 'facepunch.sandbox');
for (const [name, strength] of [['rope_tornado', 1], ['funnel_tornado', 2], ['wedge_tornado', 5]]) {
  const resource = `storm_disasters/${name}`;
  const entry = read(`Assets/${resource}.sent`);
  assert.equal(entry.Category, 'Disasters');
  assert.equal(entry.Prefab, `${resource}.prefab`);
  const prefab = read(`Assets/${entry.Prefab}`).RootObject;
  assert.equal(prefab.Components.find(c => c.__type === 'StormDisasterFunnel')?.Strength, strength);
  assert.equal(prefab.NetworkMode, 2);
}
assert.match(fs.readFileSync(path.join(root, 'Assets/storm_disasters/StormDisasterFunnel.csx'), 'utf8'), /class StormDisasterFunnel\s*:\s*Component/);
console.log('Three native Sandbox entities resolve to their scripted prefab and configured strength.');
