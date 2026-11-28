const {createHash} = require('node:crypto');

// Preserve the paths, prefab object identities, and asset IDs of the original menu entries.
const shapes = [
  ['rope', 'Rope', 1, 'A thin, winding funnel.', ['a8a84f8d-f425-4da9-9189-0d163a53d74e', '7012b856-8cfd-48a5-abb5-4754a0c5bf9a', 'eb1892f9-8082-4393-ad3a-ed2a1174693a', 'efab1742-3628-4de1-bc7e-ed3c78d5c8a3']],
  ['funnel', 'Cone', 2, 'A classic, rotating cone.', ['8c83a640-8264-4de9-abe6-0d9db3a067b3', '5427a1ab-f3cb-47a1-b6e8-9a0549dea18a', '38657042-8f79-4328-b60a-46f65bb284bb', '3acb27ab-f40b-44f5-a993-8067d3013557']],
  ['wedge', 'Wedge', 5, 'A wide, dark funnel with a rolling sandy base.', ['443d5557-20dd-4e0e-b7cf-a5c7e4d9087d', '71bc9527-1420-44e4-9cd0-75f263f553ba', 'e017b459-74f0-452c-8be6-a7efab8d854c', '88c0ebb5-d034-4763-a11d-09c03e6134a0']],
  ['v_shape', 'V-shaped', null, 'A steep-sided funnel that fans out toward the cloud base.'],
  ['cylinder', 'Cylinder', null, 'A broad, nearly straight-sided column.'],
  ['hourglass', 'Hourglass', null, 'A funnel with a pinched waist and flared ends.'],
];

function stableGuid(key) {
  const s = createHash('sha256').update(`storm-disasters:${key}`).digest('hex');
  return `${s.slice(0,8)}-${s.slice(8,12)}-8${s.slice(13,16)}-a${s.slice(17,20)}-${s.slice(20,32)}`;
}

function* presetFiles() {
  const json = value => JSON.stringify(value, null, 2) + '\n';
  const items = [];
  for (const [shape, [key, label, legacyRating, description, legacyGuids]] of shapes.entries()) {
    items.push({Kind: 'Title', Title: label});
    for (let strength = 0; strength <= 5; strength++) {
      const legacy = strength === legacyRating;
      const stem = `storm_disasters/${key}_tornado${legacy ? '' : `_f${strength}`}`;
      const title = `${label} Tornado (F${strength})`;
      const wind = strength >= 3 ? 'Can unfreeze nearby props.' : strength === 0 ? 'Light wind nudges loose props.' : 'Wind moves loose props.';
      const [objectGuid, componentGuid, prefabGuid, entryGuid] = legacy ? legacyGuids :
        ['root', 'component', 'prefab', 'entry'].map(part => stableGuid(`${stem}:${part}`));
      yield [`${stem}.sent`, json({
        Prefab: `${stem}.prefab`, Title: title, Description: `${description} ${wind} Use Sandbox undo to remove it.`,
        Category: 'Tornadoes', __references: [], __version: 0,
      })];
      items.push({Kind: 'Item', Ident: `entity:${stem}.sent`, Title: title});
      yield [`${stem}.prefab`, json({
        RootObject: {
          __guid: objectGuid, __version: 2, Flags: 0, Name: title, Position: '0,0,0', Rotation: '0,0,0,1', Scale: '1,1,1',
          Tags: `storm_disaster_tornado,storm_disasters_rating_f${strength}${key === 'wedge' ? ',storm_disasters_wedge' : ''}`, Enabled: true, NetworkMode: 2, NetworkFlags: 0, NetworkOrphaned: 0,
          NetworkTransmit: true, OwnerTransfer: 1,
          Components: [{__type: 'StormDisasterFunnel', __guid: componentGuid, __enabled: true, Flags: 0, Strength: strength, Shape: shape}],
          Children: [],
        },
        ResourceVersion: 3, __references: [], __version: 3,
      })];
      yield [`${stem}.prefab.meta`, json({guid: prefabGuid})];
      yield [`${stem}.sent.meta`, json({guid: entryGuid})];
    }
  }
  const spawnlist = 'storm_disasters/tornadoes.spawn';
  yield [spawnlist, json({
    Title: 'Tornadoes', Description: 'Storm Disasters tornadoes grouped by shape, with F0–F5 presets.',
    Icon: 'cyclone', HideInSidebar: false, Items: items, __references: [], __version: 0,
  })];
  yield [`${spawnlist}.meta`, json({guid: stableGuid(spawnlist)})];
}

module.exports = {presetFiles};
