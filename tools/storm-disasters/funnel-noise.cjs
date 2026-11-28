// Bake the same tiled RGBA volume as Code/StormFunnelVolume.cs without a Breen voxel loop.
const {hash} = require('./weather-assets.cjs');
const round = Math.fround;
const mix = (a,b,t) => round(a + round(round(b - a)*t));
const smooth = t => round(round(t*t) * round(3-round(2*t)));

function bakeFunnelNoise(source) {
  const size = Number(source.match(/const int NoiseSize\s*=\s*(\d+)\s*;/)?.[1]);
  const channels = [...source.matchAll(/=\s*\(byte\)\(255\s*\*\s*TileNoise\(\s*x,\s*y,\s*z,\s*(\d+),\s*(\d+)\s*\)\s*\)/g)]
    .map(([,cells,seed]) => [Number(cells),Number(seed)]);
  if (!size || size > 128 || channels.length !== 3 || channels.some(([cells]) => !cells || size % cells !== 0)) {
    throw new Error('Update the add-on bake for the changed StormFunnelVolume noise profile.');
  }
  function tiled(x,y,z,cells,seed) {
    const step = size/cells;
    const a = Math.floor(x/step), b = Math.floor(y/step), c = Math.floor(z/step);
    const u = smooth(x%step/step), v = smooth(y%step/step), w = smooth(z%step/step);
    const node = (i,j,k) => hash(i%cells + cells*(j%cells), k%cells, seed);
    const layer = k => mix(mix(node(a,b,k),node(a+1,b,k),u),mix(node(a,b+1,k),node(a+1,b+1,k),u),v);
    return mix(layer(c),layer(c+1),w);
  }
  const rgba = Buffer.alloc(size**3*4,255);
  for (let z=0;z<size;z++) for (let y=0;y<size;y++) for (let x=0;x<size;x++) {
    const at = ((z*size+y)*size+x)*4;
    for (const [i,[cells,seed]] of channels.entries()) rgba[at+i] = Math.trunc(round(255*tiled(x,y,z,cells,seed)));
  }
  return {size, rgba};
}
module.exports = {bakeFunnelNoise};
