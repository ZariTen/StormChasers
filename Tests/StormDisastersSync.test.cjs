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
    weather: game('tools/storm-disasters/Weather.csx.in'),
    volume: game('Code/StormFunnelVolume.cs'),

  };
  const old = render(source);
  const changed = render({...source,
    shader: source.shader + '\n// test marker',
    debrisShader: source.debrisShader + '\n// debris marker',
    profiles: source.profiles.replace('3480f', '3590f').replace('4150f', '4255f').replace('3.85f', '3.95f'),
    shapes: source.shapes.replace('56, 61, 66, 82', '57, 61, 66, 82')
      .replace('58, 129, 213, 306', '59, 129, 213, 306')
      .replace('108, 117, 137, 201', '109, 117, 137, 201')
      .replace('173, 113, 91, 145', '174, 113, 91, 145'),
    funnel: source.funnel.replace('480f * WedgeScale', '490f * WedgeScale'),
  });
  assert.notEqual(changed.shader, old.shader);
  assert.equal(changed.shader, source.shader + '\n// test marker');
  assert.equal(changed.debrisShader, source.debrisShader + '\n// debris marker');
  assert.match(changed.script, /3590f/);
  assert.match(changed.script, /Strength == 3 \) return 4255f/);
  assert.match(changed.script, /rating == 4 \) return 3\.95f/);
  assert.equal(render({...source, funnel: source.funnel.replaceAll('\n', '\r\n')}).script, old.script);
  assert.match(changed.script, /57f, 61f, 66f, 82f/);
  assert.match(changed.script, /Shape == 3 \) return new Vector4\( 59f, 129f, 213f, 306f/);
  assert.match(changed.script, /Shape == 4 \) return new Vector4\( 109f, 117f, 137f, 201f/);
  assert.match(changed.script, /Shape == 5 \) return new Vector4\( 174f, 113f, 91f, 145f/);
  assert.match(changed.script, /490f/);
  assert.notEqual(changed.script, old.script);
  assert.equal(Buffer.from(JSON.parse(old.noise), 'base64').length, 32**3*4);
  assert.notEqual(render({...source, volume: source.volume.replace('4, 701', '4, 702')}).noise, old.noise);
  assert.doesNotMatch(old.script, /dust_mask_|UpdateDustShroud/);
  assert.match(render({...source, funnel: source.funnel.replace('DebrisCount = 56', 'DebrisCount = 60').replace('145 + 420 * drift', '155 + 420 * drift')}).script, /155 \+ 420 \* drift/);
  assert.throws(() => render({...source, profiles: ''}), /rating 0/);
});
