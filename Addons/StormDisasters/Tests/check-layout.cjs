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
const guids = new Set();
const entityNames = new Set();
const grouped = read('Assets/storm_disasters/tornadoes.spawn');
assert.equal(grouped.Title, 'Tornadoes');
assert.ok(!grouped.HideInSidebar);
const expectedItems = [];
for (const [shape, label, legacyRating, index] of [
  ['rope', 'Rope', 1, 0], ['funnel', 'Cone', 2, 1], ['wedge', 'Wedge', 5, 2],
  ['v_shape', 'V-shaped', null, 3], ['cylinder', 'Cylinder', null, 4], ['hourglass', 'Hourglass', null, 5],
]) {
  expectedItems.push({Kind: 'Title', Title: label});
  for (let rating = 0; rating <= 5; rating++) {
    const base = `${shape}_tornado${rating === legacyRating ? '' : `_f${rating}`}`;
    const resource = `storm_disasters/${base}`;
    entityNames.add(`${base}.sent`);
    const entry = read(`Assets/${resource}.sent`);
    assert.equal(entry.Title, `${label} Tornado (F${rating})`);
    assert.equal(entry.Category, 'Tornadoes');
    expectedItems.push({Kind: 'Item', Ident: `entity:${resource}.sent`, Title: entry.Title});
    assert.equal(entry.Prefab, `${resource}.prefab`);
    const prefab = read(`Assets/${entry.Prefab}`).RootObject;
    assert.equal(prefab.Name, entry.Title);
    assert.deepEqual(prefab.Tags.split(','), ['storm_disaster_tornado', `storm_disasters_rating_f${rating}`, ...(shape === 'wedge' ? ['storm_disasters_wedge'] : [])]);
    const component = prefab.Components.find(c => c.__type === 'StormDisasterFunnel');
    assert.equal(component.Strength, rating);
    assert.equal(component.Shape, index);
    assert.equal(prefab.NetworkMode, 2);
    for (const guid of [prefab.__guid, component.__guid, read(`Assets/${resource}.prefab.meta`).guid, read(`Assets/${resource}.sent.meta`).guid]) {
      assert.match(guid, /^[0-9a-f-]{36}$/);
      assert.ok(!guids.has(guid), `Duplicate GUID: ${guid}`);
      guids.add(guid);
    }
  }
}
assert.deepEqual(grouped.Items, expectedItems);
assert.deepEqual(new Set(fs.readdirSync(path.join(root, 'Assets/storm_disasters')).filter(f => f.endsWith('.sent'))), entityNames);
assert.match(fs.readFileSync(path.join(root, 'Assets/storm_disasters/StormDisasterFunnel.csx'), 'utf8'), /class StormDisasterFunnel\s*:\s*Component/);
console.log(`${entityNames.size} native Sandbox entities resolve to separate shape/rating presets.`);
