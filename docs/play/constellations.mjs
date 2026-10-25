import {clamp} from './core.mjs';
import {scalarAt} from './motion.mjs';
const mod=(v,p)=>(v%p+p)%p;
const distance=(a,b)=>Math.hypot(...a.map((v,i)=>v-b[i]));
function seed(id,side){let s=(Math.imul(id,747796405)+Math.imul(7,2891336453)+(side<0?12582917:98764321))>>>0;s=Math.imul(s^(s>>>16),2246822519)>>>0;return (s^(s>>>13))>>>0;}
export function fragment(id,side,depth,beat){const s=seed(id,side);return [side*(3.6+s%1000*.0035),-.7+(s>>>10)%1000*.0045+.13*Math.sin(beat*.17+id),52-mod(depth+id*2.6,56)];}
export function chain(points,start){const nodes=[start];while(nodes.length<5){let next=-1,closest=7;for(let i=0;i<points.length;i++){if(nodes.includes(i))continue;const d=distance(points[nodes.at(-1)],points[i]);if(d<closest){closest=d;next=i;}}if(next<0)break;nodes.push(next);}return nodes;}
// A fixed beat-grid integral is independent of rendering FPS and supports seeks.
// NS changes affect future travel, not the entire distance already travelled.
export class Travel{
  constructor(diff){this.diff=diff;this.positions=[0];}
  at(beat){const d=this.diff,step=1/64,scroll=b=>d.timing.scroll(b),ns=b=>Math.max(.1,scalarAt(d.ns,b));if(!Number.isFinite(beat))return 0;if(beat<=0)return (scroll(beat)-scroll(0))*ns(0);const index=Math.floor(beat/step);while(this.positions.length<=index){const b=(this.positions.length-1)*step;this.positions.push(this.positions.at(-1)+(scroll(b+step)-scroll(b))*ns(b+step/2));}const b=index*step;return this.positions[index]+(scroll(beat)-scroll(b))*ns(b+step/2);}
}
export class Constellations{
  constructor(diff,session){this.diff=diff;this.session=session;this.travel=new Travel(diff);this.pulses=[];this.ids=new Map(session.notes.map((n,i)=>[n,i]));}
  depth(beat,speed){return this.travel.at(beat)*speed*10;}
  collect(seconds,speed,enabled){const events=this.session.visualJudgements||[];for(const e of events){if(!enabled||seconds-e.seconds>=.8||e.seconds>seconds)continue;const beat=this.diff.timing.beat(e.seconds),depth=this.depth(beat,speed),side=e.note.lane<=this.diff.lanes/2?1:-1,id=((this.ids.get(e.note)||0)*7+e.note.lane*3)%24,points=Array.from({length:24},(_,i)=>fragment(i,side,depth,beat)),nodes=chain(points,id);if(nodes.length>1)this.pulses.push({side,seconds:e.seconds,nodes});}events.length=0;this.pulses=this.pulses.filter(p=>seconds>=p.seconds&&seconds-p.seconds<.8).slice(-64);if(!enabled)this.pulses=[];}
  geometry(projection,seconds,speed,enabled=true){this.collect(seconds,speed,enabled);if(!enabled)return [];const beat=this.diff.timing.beat(seconds),depth=this.depth(beat,speed),data=[];
    const triangle=(a,b,c,color,alpha)=>{if(alpha<=0)return;for(const point of [a,b,c])data.push(...projection.clip(point),0,0,...color,alpha);};
    const line=(a,b,color,alpha,width)=>{const dx=b[0]-a[0],dz=b[2]-a[2],length=Math.hypot(dx,dz);const offset=length>1e-6?[-dz/length*width/2,0,dx/length*width/2]:[width/2,0,0],p=a.map((v,i)=>v-offset[i]),q=b.map((v,i)=>v-offset[i]),r=b.map((v,i)=>v+offset[i]),s=a.map((v,i)=>v+offset[i]);triangle(p,q,r,color,alpha);triangle(p,r,s,color,alpha);};
    for(const side of [-1,1])for(let id=0;id<24;id++){const p=fragment(id,side,depth,beat),fade=clamp((p[2]+4)/5,0,1)*clamp((56-p[2])/8,0,1),angle=beat*.12+id*1.7,r=.09+id%4*.025,color=id%3===0?[.647,.478,.973]:[.357,.722,.949],points=[0,2.1,4.2].map((v,i)=>[p[0]+Math.cos(angle+v)*r,p[1]+Math.sin(angle+v)*r,p[2]+[0,.08,-.08][i]]);triangle(...points,color,32/255*fade);for(let i=0;i<3;i++)line(points[i],points[(i+1)%3],color,.4*fade,.012);}
    for(const pulse of this.pulses){const fade=(1-(seconds-pulse.seconds)/.8)**1.3;for(let i=0;i<pulse.nodes.length-1;i++){const a=fragment(pulse.nodes[i],pulse.side,depth,beat),b=fragment(pulse.nodes[i+1],pulse.side,depth,beat);if(distance(a,b)>7)break;line(a,b,[.47,.792,1],.176*fade,.065);line(a,b,[.824,.961,1],.94*fade,.026);}}
    let active=0;for(const n of this.session.notes){if(n.kind!=='L'||!n.started||n.done)continue;if(active++>=32)break;const side=n.lane<=this.diff.lanes/2?1:-1,end=fragment((this.ids.get(n)||0)%24,side,depth,beat),start=[side*3.4,.1,-2],growth=clamp((beat-n.beat)/4+.08,0,1),point=t=>start.map((v,i)=>v+(end[i]-v)*t+(i===1?Math.sin(t*Math.PI)*(.35+.08*Math.sin(beat*.4)):0));for(let i=0;i<24;i++)line(point(i/24*growth),point((i+1)/24*growth),[.569,.827,1],.35,.018);}
    return data;
  }
}
