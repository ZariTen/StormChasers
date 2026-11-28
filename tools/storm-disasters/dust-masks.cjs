// Standalone smoke masks, baked into texture bytes instead of generated in Breen.
const {hash} = require('./weather-assets.cjs');
const F=Math.fround;
const add=(a,b)=>F(a+b), sub=(a,b)=>F(a-b), mul=(a,b)=>F(a*b), div=(a,b)=>F(a/b);
const clamp=x=>Math.max(0,Math.min(1,x));
const smooth=x=>mul(mul(x,x),sub(3,mul(2,x)));
const mix=(a,b,t)=>add(a,mul(sub(b,a),t));
function noise(x,y,seed) {
  const ix=Math.floor(x), iy=Math.floor(y), a=smooth(sub(x,ix)), b=smooth(sub(y,iy));
  return mix(mix(hash(ix,iy,seed),hash(ix+1,iy,seed),a),mix(hash(ix,iy+1,seed),hash(ix+1,iy+1,seed),a),b);
}
function bakeSmoke(source) {
  const size=Number(source.match(/const int Resolution\s*=\s*(\d+)\s*;/)?.[1]);
  const count=Number(source.match(/const int Variants\s*=\s*(\d+)\s*;/)?.[1]);
  const seed=source.match(/var seed\s*=\s*(\d+)\s*\+\s*variant\s*\*\s*(\d+)\s*;/);
  if(!size||size>512||!count||count>16||!seed) throw new Error('Update the add-on bake for the changed StormSmokeTexture profile.');
  const masks=[];
  for(let variant=0;variant<count;variant++){
    const pixels=Buffer.alloc(size*size*4), n=+seed[1]+variant*(+seed[2]);
    for(let y=0;y<size;y++)for(let x=0;x<size;x++){
      const u=sub(div(mul(add(x,.5),2),size),1), v=sub(div(mul(add(y,.5),2),size),1);
      const wx=mul(sub(noise(mul(u,2.5),mul(v,F(2.5)),n+8),.5),F(.52));
      const wy=mul(sub(noise(add(mul(u,2.5),20),mul(v,2.5),n+19),.5),F(.46));
      const broad=noise(mul(add(u,wx),F(4.1)),mul(add(v,wy),F(4.1)),n);
      const medium=noise(add(mul(u,F(9.4)),wy),mul(v,F(9.4)),n+29);
      const fine=noise(mul(u,21),mul(v,21),n+43);
      const detail=add(add(mul(F(.58),broad),mul(F(.30),medium)),mul(F(.12),fine));
      const radius=F(Math.sqrt(add(mul(u,u),mul(v,v))));
      const contour=clamp(div(add(sub(F(1.04),radius),mul(sub(detail,F(.52)),F(.58))),F(.66)));
      const border=clamp(mul(sub(1,Math.max(Math.abs(u),Math.abs(v))),7));
      const alpha=mul(mul(smooth(contour),border),add(F(.40),mul(F(.60),detail)));
      const shade=clamp(sub(add(F(.56),mul(F(.32),detail)),mul(F(.08),v)));
      const i=(y*size+x)*4;
      pixels[i]=pixels[i+1]=pixels[i+2]=Math.trunc(mul(shade,255));
      pixels[i+3]=Math.trunc(mul(alpha,255));
    }
    masks.push(JSON.stringify(pixels.toString('base64'))+'\n');
  }
  return {size,masks};
}
module.exports={bakeSmoke};
