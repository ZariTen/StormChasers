// Bake the weather masks and seamless mono loops off the game's scripting thread.
const {deflateSync} = require('node:zlib');
const clamp = v => Math.max(0, Math.min(1, v));
const smooth = v => { v = clamp(v); return v*v*(3-2*v); };
const lerp = (a,b,t) => a+(b-a)*t;
const u32 = n => n >>> 0;
function hash(x,y,seed) {
  let h = u32(Math.imul(x,374761393) + Math.imul(y,668265263) + Math.imul(seed,2246822519));
  h = u32(Math.imul(h ^ (h >>> 13), 1274126177));
  return ((h ^ (h >>> 16)) & 0xffffff)/16777216;
}
function noise(x,y,seed) {
  const a=Math.floor(x), b=Math.floor(y), u=smooth(x-a), v=smooth(y-b);
  return lerp(lerp(hash(a,b,seed),hash(a+1,b,seed),u),lerp(hash(a,b+1,seed),hash(a+1,b+1,seed),u),v);
}
function png(w,h,pixel) {
  const header=Buffer.alloc(13);
  header.writeUInt32BE(w); header.writeUInt32BE(h,4); header[8]=8; header[9]=6;
  const raw=Buffer.alloc(h*(w*4+1));
  for(let y=0;y<h;y++) for(let x=0;x<w;x++) {
    const [grey,alpha]=pixel((x+.5)/w,(y+.5)/h);
    const at=y*(w*4+1)+1+x*4;
    raw[at]=raw[at+1]=raw[at+2]=Math.floor(255*clamp(grey)); raw[at+3]=Math.floor(255*clamp(alpha));
  }
  function chunk(type,data) {
    const bytes=Buffer.concat([Buffer.from(type),data]), out=Buffer.alloc(bytes.length+8);
    let crc=0xffffffff;
    for(const byte of bytes) {
      crc ^= byte;
      for(let i=0;i<8;i++) crc=(crc>>>1)^((crc&1)?0xedb88320:0);
    }
    out.writeUInt32BE(data.length); bytes.copy(out,4); out.writeUInt32BE(u32(~crc),out.length-4);
    return out;
  }
  return Buffer.concat([Buffer.from('89504e470d0a1a0a','hex'),chunk('IHDR',header),chunk('IDAT',deflateSync(raw,{level:9})),chunk('IEND',Buffer.alloc(0))]);
}
function cloud(seed,storm) {
  return png(512,512,(x,y)=>{
    const u=x*2-1,v=y*2-1,f=storm?1:2.8;
    const bend=noise(u*2.1,v*2.8,seed+7)-.5, drift=noise(u*3.7,v*2.2,seed+33)-.5;
    const px=f*(u*3.1+bend*.9),py=f*(v*4.6+drift*1.5+.44*u*u);
    const broad=noise(px,py,seed),cells=noise(px*2.32+4,py*2.17,seed+43),ripples=noise(px*5.9,py*5.8+15,seed+74);
    const density=.51*broad+.30*cells+.14*ripples+.05*noise(px*14.9,py*13.4,seed+129);
    const edge=smooth((1-Math.max(Math.abs(u),Math.abs(v)))*(storm?11:5));
    return [.20+.86*density+.25*Math.abs(cells-broad)+.09*ripples,edge*(.83+.17*smooth((density-.20)*2.3))];
  });
}
function wav(layer) {
  const rate=24000, frames=rate*12, overlap=rate*2;
  const rain=layer==='rain'||layer==='covered', covered=layer==='covered', wind=layer==='wind';
  let rng=rain?1109:wind?2161:4049;
  const random=()=>{rng^=rng<<13;rng^=rng>>>17;rng^=rng<<5;return u32(rng)/4294967296;};
  const low=(hz)=>{const a=1-Math.exp(-2*Math.PI*hz/rate);let s=0;return n=>s+=(n-s)*a;};
  const sub=low(18), bass=low(86), breath=low(270), body=low(760), hiss=low(5200), pressure=low(255);
  const bed=low(rain?(covered?260:1100):295), soft=low(rain?(covered?330:1450):380);
  const a=new Float32Array(frames+overlap);
  for(let i=0;i<a.length;i++){
    const w=random()*2-1, t=i/rate, bottom=sub(w), rumble=bass(w)-bottom, rush=body(w);
    if(rain) a[i]=soft(bed(w)-bottom)*.24;
    else if(wind) a[i]=soft(bed(w)-bottom)*(.80+.16*Math.sin(t*.67)+.10*Math.sin(t*1.76+1.2));
    else a[i]=(rumble*2.9+(pressure(w*(1+.55*Math.tanh(rumble*13)))-bottom)*1.23+(rush-(breath(w)-bottom))*.59+(hiss(w)-rush)*.064)*(.93+.14*Math.sin(t*5.2+Math.sin(t*1.17))+.17*Math.sin(t*2.31));
  }
  if(rain){
    for(let start=0;start<a.length;){
      start+=1+Math.floor(-Math.log(1-random())*rate/(covered?43:168));
      const k=Math.floor(random()*12), hz=(covered?155:390)+k*(covered?24:82), tau=(covered?.009:.005)+.004*(k%4)/3;
      const n=Math.ceil(rate*tau*5), size=random(), gain=((covered?.021:.016)+(covered?.115:.090)*size*size)*(random()<.5?-1:1);
      for(let j=0;j<n&&start+j<a.length;j++){
        const t=j/rate, phase=2*Math.PI*hz*t;
        a[start+j]+=(Math.sin(phase)+(covered?.12:.24)*Math.sin(phase*1.57+.3))*smooth(t/.004)*Math.exp(-t/tau)*smooth((n-j)/(rate*.004))*gain;
      }
    }
  }
  let sum=0, squares=0;
  for(let i=0;i<frames;i++){
    if(i<overlap) a[i]=lerp(a[frames+i],a[i],smooth(i/overlap));
    sum+=a[i]; squares+=a[i]*a[i];
  }
  const mean=sum/frames, gain=({roar:.205,rain:.108,covered:.089,wind:.083})[layer]/Math.max(.001,Math.sqrt(squares/frames-mean*mean));
  const b=Buffer.alloc(112+frames*2);
  b.write('RIFF'); b.writeUInt32LE(b.length-8,4); b.write('WAVEfmt ',8); b.writeUInt32LE(16,16);
  b.writeUInt16LE(1,20); b.writeUInt16LE(1,22); b.writeUInt32LE(rate,24); b.writeUInt32LE(rate*2,28); b.writeUInt16LE(2,32); b.writeUInt16LE(16,34);
  b.write('smpl',36); b.writeUInt32LE(60,40); b.writeUInt32LE(Math.floor(1e9/rate),52); b.writeUInt32LE(60,56); b.writeUInt32LE(1,72); b.writeUInt32LE(frames-1,92);
  b.write('data',104); b.writeUInt32LE(frames*2,108);
  for(let i=0;i<frames;i++) b.writeInt16LE(Math.trunc(Math.tanh((a[i]-mean)*gain)*32767),112+i*2);
  return b;
}
function* weatherAssets(){
  const root='storm_disasters/weather_';
  for(const [i,seed] of [83,219,901,666].entries()) yield [`${root}cloud_${i}.png`,cloud(seed,i===2)];
  yield [`${root}drop.png`,png(16,64,(u,v)=>[1,(1-smooth((Math.abs(u-.5)/(.29+.19*smooth(v/.72))-.15)/.85))*smooth(v/.51)*smooth((1-v)/.31)])];
  for(const key of ['wind','rain','covered','roar']) yield [`${root}${key}.wav`,wav(key)];
}
module.exports={weatherAssets,hash};
