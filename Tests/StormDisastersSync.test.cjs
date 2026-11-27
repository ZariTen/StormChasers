const {test} = require('node:test');
const assert = require('node:assert/strict');
const {spawnSync} = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const root = path.join(__dirname, '..');
const generator = path.join(root, 'tools/sync-storm-disasters.cjs');

test('checked-in add-on output matches Storm Chasers', () => {
  const run = spawnSync(process.execPath, [generator, '--check'], {encoding: 'utf8'});
  assert.equal(run.status, 0, run.stderr);
});

test('game profile, shape, core and shader edits regenerate the add-on', () => {
  const {render} = require(generator);
  const game = file => fs.readFileSync(path.join(root, file), 'utf8');
  const source = {
    shader: game('Assets/shaders/storm_funnel.shader'),
    debrisShader: game('Assets/shaders/storm_debris.shader'),
    profiles: game('Code/StormTornadoProfile.cs'),
    shapes: game('Code/StormFunnelShape.cs'),
    funnel: game('Code/StormTornadoFunnel.cs'),
    template: game('tools/storm-disasters/StormDisasterFunnel.csx.in'),
  };
  const old = render(source);
  const changed = render({...source,
    shader: source.shader + '\n// test marker',
    debrisShader: source.debrisShader + '\n// debris marker',
    profiles: source.profiles.replace('3480f', '3590f'),
    shapes: source.shapes.replace('56, 61, 66, 82', '57, 61, 66, 82'),
    funnel: source.funnel.replace('480f * WedgeScale', '490f * WedgeScale'),
  });
  assert.notEqual(changed.shader, old.shader);
  assert.equal(changed.shader, source.shader + '\n// test marker');
  assert.equal(changed.debrisShader, source.debrisShader + '\n// debris marker');
  assert.match(changed.script, /3590f/);
  assert.match(changed.script, /57f, 61f, 66f, 82f/);
  assert.match(changed.script, /490f/);
  assert.notEqual(changed.script, old.script);
  assert.throws(() => render({...source, profiles: ''}), /rating 1/);
});
