// Keep the standalone debris animation and shared effect counts and seeds.
const {hash} = require('./weather-assets.cjs');
function adaptGroundEffects(source) {
  source = source.replaceAll('\r\n', '\n');

  const debrisCount=Number(source.match(/const int DebrisCount\s*=\s*(\d+)\s*;/)?.[1]);
  const angle=source.match(/const float GoldenAngle\s*=\s*([\d.]+f)\s*;/)?.[1];
  function method(name){
    const body=source.match(new RegExp(`^\\tvoid ${name}\\([^\\n]*\\)\\r?\\n\\t\\{([\\s\\S]*?)^\\t\\}`, 'm'))?.[1];
    if(!body) throw new Error(`Cannot find standalone ${name}.`);
    return body.replaceAll('\t','    ').replaceAll('Smooth(', 'Fade(').replaceAll('Seed(', 'SurfaceSeed(').trimEnd();
  }
  function swap(text,from,to){
    if(!text.includes(from)) throw new Error(`Update the ground effects adapter: missing ${from}`);
    return text.replaceAll(from,to);
  }
  if(!debrisCount||!angle) throw new Error('Could not read standalone debris count or angle.');
  let debris=method('AnimateDebris');
  for(const [from,to] of [
    ['        var sections = StormFunnelSections.For( BaseSilhouette );\n',''],['        var funnelWidth = profile.WidthScale * (1 - .29f * rope);\n',''],
    ['debris.Length','FragmentCount'],['debris[i]','fragments[i].GameObject'],
    ['Math.Clamp( .008f + (rise - .17f * escape) * loft, 0, .97f )','MathF.Min( .97f, MathF.Max( 0f, .008f + (rise - .17f * escape) * loft ) )'],
    ['Blend( sections.Sample( h ) * funnelWidth, StormFunnelSections.WalkInWedge.Sample( h ) * wedgeScale, wedge )','SampleRadii( radii, top, h )'],
    ['Mix( CondensationAxis( h, t, groundScale, rope ), Axis( h, t ) * .42f, wedge )','FunnelAxis( t, h, groundScale, rope )'],
    ['h * profile.FunnelHeight','h * height'],['piece.LocalPosition = position','piece.WorldPosition = WorldPosition + position'],
  ]) debris=swap(debris,from,to);
  const indices=[...source.matchAll(/Seed\( i \+ (\d+) \)/g)].map(m=>Number(m[1]));
  const seedEnd=Math.max(...indices)+debrisCount;
  const seeds=Array.from({length:seedEnd},(_,i)=>`${hash(i,31,851).toPrecision(9)}f`);
  const chunks=Array.from({length:Math.ceil(seeds.length/8)},(_,i)=>`        ${seeds.slice(i*8,i*8+8).join(', ')},`);
  return [
    '    // Effect data and debris animation adapted from StormTornadoFunnel.',
    `    const int FragmentCount = ${debrisCount};`, `    const float GoldenAngle = ${angle};`,
    '    float[] surfaceSeeds = new float[]', '    {', ...chunks, '    };',
    '    float SurfaceSeed( int i ) => surfaceSeeds[i];',
    '    Vector3 Orbit( float angle, float radius, float aspect ) => new Vector3( MathF.Cos( angle ) * radius, MathF.Sin( angle ) * radius * aspect, 0f );',
    '    void UpdateDebris( float t, float height, Vector4 radii, float top, float groundScale, float rope, float circulation, float progress )', '    {'+debris, '    }',
  ].join('\n');
}
module.exports={adaptGroundEffects};
