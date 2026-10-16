// Android-aligned chart parsing, timing, filters, judgement and persisted records.
export const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
function parseBaseChart(text){
  const c={title:'Untitled',composer:'',designer:'',audio:'',jacket:'',background:'',video:'',bpm:120,offset:0,lanes:6,difficulties:[]};let d=null,layer=1;
  const names={TITLE:'title',COMPOSER:'composer',CHARTER:'designer',CHARTDESIGNER:'designer',CHART_DESIGNER:'designer',SONG:'audio',AUDIO:'audio',JACKET:'jacket',COVER:'jacket',BACKGROUND:'background',BG:'background',MOVIE:'video',VIDEO:'video',MV:'video'};
  for(const raw of text.replace(/\r/g,'').split('\n')){
    const line=raw.split('//')[0].trim();if(!line)continue;
    const field=line.match(/^([A-Z_]+)\s*:\s*(.*)$/);
    if(field){const [,key,value]=field;
      if(key in names)c[names[key]]=value.trim();
      else if(key==='BPM'&&Number(value)>0)c.bpm=Number(value);
      else if(key==='OFFSET'&&Number.isFinite(Number(value)))c.offset=Number(value);
      else if(key==='LANES'){const n=clamp(Math.trunc(Number(value)||6),3,12);if(d)d.lanes=n;else c.lanes=n;}
      else if(key==='DIFFICULTY'){layer=1;d={name:value,level:'0',lanes:c.lanes,notes:[],bpm:[],bs:[],filters:[],events:[],unsupported:[]};c.difficulties.push(d);}
      else if(key==='LEVEL'&&d)d.level=value;
      else if(key==='LAYER')layer=Number(value);
      else if(key==='CHANGE'&&d){const p=value.split(',').map(s=>s.trim()),beat=Number(p.shift()),kind=p.shift();d.events.push({beat,kind,spec:p,layer});
        if(layer!==1){d.unsupported.push('追加レイヤー');continue;}
        if(kind==='BPM'&&Number(p[0])>0)d.bpm.push({beat,value:Number(p[0])});
        else if(kind==='BS'&&Number.isFinite(Number(p[0])))d.bs.push({beat,value:Number(p[0])});
        else if(kind==='FILTER')d.filters.push({beat,kind:p[0],strength:Number(p[1]),duration:Number(p[2]),ramp:p[3]==='RAMP',backgroundOnly:p[4]==='BACKGROUND'});
        else if(kind==='GLITCH')d.filters.push({beat,kind:'GLITCH',strength:Number(p[0]),duration:p[1]===undefined?Infinity:Number(p[1]),ramp:false,backgroundOnly:false});
        else d.unsupported.push(kind);
      }else if(key==='FEVER'&&d)d.unsupported.push('FEVER');
      continue;
    }
    if(!d)continue;
    const m=line.match(/^([0-9.]+)([HDMW]?)(?:-([0-9.]+)([GH]?))?\s*,\s*(-?[0-9]+)(?:\s*,\s*(.*))?$/);if(!m)continue;
    const n={beat:Number(m[1]),end:Number(m[3]??m[1]),lane:Number(m[5]),kind:m[3]?(m[4]==='G'?'G':'L'):(m[2]||'N'),ss:1,span:1,holdEnd:m[4]==='H',opacity:1,color:null,guideMode:'solid',layer};
    for(const extra of (m[6]||'').split(',').map(s=>s.trim())){if(extra.startsWith('SS=')){const v=Number(extra.slice(3));n.ss=v===0?.01:Math.sign(v)*clamp(Math.abs(v),.01,1000);}else if(extra.startsWith('SIZE='))n.span=Number(extra.slice(5));else if(/^#[a-f0-9]{6}$/i.test(extra))n.color=extra;else if(n.kind==='G'&&['solid','fade','rise'].includes(extra))n.guideMode=extra;else if(n.kind==='G'&&extra!==''&&Number.isFinite(Number(extra)))n.opacity=clamp(Number(extra),0,1);}
    if(layer!==1)d.unsupported.push('追加レイヤー');
    if([n.beat,n.end,n.lane,n.ss,n.span].every(Number.isFinite)&&n.end>=n.beat&&n.span>=1&&n.lane<=d.lanes&&n.lane+n.span>1)d.notes.push(n);
  }
  for(const diff of c.difficulties){for(const key of ['notes','bpm','bs','filters'])diff[key].sort((a,b)=>a.beat-b.beat);diff.unsupported=[...new Set(diff.unsupported)];diff.timing=new Timing(diff,c.bpm);for(const n of diff.notes){n.startSeconds=diff.timing.seconds(n.beat);n.endSeconds=diff.timing.seconds(n.end);}}
  return c;
}
export class Timing{
  constructor(d,bpm){this.tempo=[];let beat=0,seconds=0;for(const e of d.bpm){if(e.beat<=0){bpm=e.value;continue;}this.tempo.push({beat,seconds,bpm});seconds+=(e.beat-beat)*60/bpm;beat=e.beat;bpm=e.value;}this.tempo.push({beat,seconds,bpm});
    this.scrollSegments=[];let pos=0,bs=1;beat=0;seconds=0;const boundaries=[...new Set([...d.bpm.map(e=>e.beat),...d.bs.map(e=>e.beat)].filter(b=>b>0))].sort((a,b)=>a-b);
    for(const e of d.bs)if(e.beat<=0)bs=e.value;
    for(const next of [...boundaries,Infinity]){this.scrollSegments.push({beat,seconds,pos,bs});if(!Number.isFinite(next))break;const s=this.seconds(next);pos+=(s-seconds)*2*bs;beat=next;seconds=s;for(const e of d.bs)if(e.beat===next)bs=e.value;}
  }
  find(list,v,key='beat'){let lo=0,hi=list.length;while(lo+1<hi){const mid=(lo+hi)>>1;if(list[mid][key]<=v)lo=mid;else hi=mid;}return list[lo];}
  seconds(b){const s=this.find(this.tempo,b);return s.seconds+(b-s.beat)*60/s.bpm;}
  beat(t){const s=this.find(this.tempo,t,'seconds');return s.beat+(t-s.seconds)*s.bpm/60;}
  scroll(b){const s=this.find(this.scrollSegments,b);return s.pos+(this.seconds(b)-s.seconds)*2*s.bs;}
}
export function filterAt(changes,beat){let active=null,from=0;const strength=(v,f,b)=>!v?0:!v.ramp?(b<v.beat+v.duration?v.strength:0):f+(v.strength-f)*(v.duration<=0?1:clamp((b-v.beat)/v.duration,0,1));for(const e of changes){if(e.beat>beat)break;const prev=strength(active,from,e.beat);from=active&&active.kind===e.kind&&active.backgroundOnly===e.backgroundOnly?prev:0;active=e;}const v=strength(active,from,beat);return v?{...active,strength:v}:null;}
export const grade=ms=>{ms=Math.abs(ms);return ms<=40?0:ms<=60?1:ms<=100?2:ms<=120?3:-1;};
export class Session{
  constructor(d,auto=false){this.notes=d.notes.map(n=>({...n,done:false,started:false}));this.auto=auto;this.counts=[0,0,0,0,0];this.combo=0;this.maxCombo=0;this.gauge=0;this.units=Math.max(1,this.notes.reduce((a,n)=>a+(n.kind==='G'?0:n.kind==='L'?2:1),0));this.longs=new Map();this.last='';this.offsets=[];}
  get score(){return Math.round(this.counts.reduce((a,v,i)=>a+v*[1,.75,.5,.25,0][i],0)/this.units*1000000);}
  judge(g,delta=0){this.counts[g]++;if(g<4)this.offsets.push(delta);this.last=['PERFECT','GREAT','GOOD','BAD','MISS'][g];if(g<=2){this.combo++;this.maxCombo=Math.max(this.maxCombo,this.combo);}else this.combo=0;this.gauge=clamp(this.gauge+400/3/this.units*[1,.5,.25,-2,-2][g],0,100);this.onJudge?.(g,delta);}
  press(lane,t){if(this.auto)return;let closest=null,delta=Infinity;for(const n of this.notes){if(n.startSeconds>t+.12)break;if(n.done||n.started||lane<n.lane||lane>=n.lane+n.span||['D','G'].includes(n.kind))continue;const ms=(t-n.startSeconds)*1000;if(['H','W'].includes(n.kind)&&(ms<0||ms>60))continue;if(Math.abs(ms)<Math.abs(delta)){closest=n;delta=ms;}}const g=grade(delta);if(!closest||g<0)return;if(closest.kind==='L'){closest.started=true;this.longs.set(lane,closest);}else closest.done=true;this.judge(g,delta);}
  release(lane,t){if(this.auto)return;const n=this.longs.get(lane);if(!n)return;this.longs.delete(lane);n.done=true;const delta=(t-n.endSeconds)*1000,g=grade(delta);this.judge(g<0?4:g,delta);}
  advance(t,held){for(const n of this.notes){if(n.done||n.kind==='G')continue;if(n.startSeconds>t+.2)break;const ms=(t-n.startSeconds)*1000;
    if(this.auto){if(ms<0)continue;if(n.kind==='L'){if(!n.started){n.started=true;this.judge(0);}if(t>=n.endSeconds){n.done=true;this.judge(0);}}else if(n.kind!=='D'||ms>=80){n.done=true;this.judge(0);}}
    else if(['H','W'].includes(n.kind)){if(ms>=0&&[...held].some(l=>l>=n.lane&&l<n.lane+n.span)){n.done=true;this.judge(0);}else if(ms>60){n.done=true;this.judge(4);}}
    else if(n.kind==='D'){if(ms>=80){n.done=true;this.judge(held.has(n.lane)?4:0);}}
    else if(n.kind==='L'){if(!n.started&&ms>=0&&ms<=130&&held.has(n.lane)){n.started=true;this.longs.set(n.lane,n);this.judge(0);}else if(!n.started&&ms>130){n.started=true;this.judge(4);}else if(n.started&&n.holdEnd&&t>=n.endSeconds&&held.has(n.lane)){n.done=true;this.longs.delete(n.lane);this.judge(0);}else if(n.started&&t>n.endSeconds+.13){n.done=true;this.longs.delete(n.lane);this.judge(4);}}
    else if(ms>130){n.done=true;this.judge(4);}
  }}
}
export class Profile{
  constructor(storage){this.key='hyperlane.web.profile.v1';this.data={version:1,name:'',settings:{speed:1.6,volume:80,previewVolume:40,seVolume:80,offset:0,auto:false,autoSound:true,touchHeight:50,backgroundBrightness:100,previewBrightness:100,laneTransparency:0,laneCover:0,keyBeamOpacity:0,keyBeamLength:50,startMargin:1300,feverBrightness:100,preferVideo:true},scores:{},replays:{},danPassed:[],danRevealed:[],presets:[]};this.error='';try{this.storage=storage||globalThis.localStorage;const old=JSON.parse(this.storage.getItem(this.key)||'null');if(old?.version===1){this.data.name=typeof old.name==='string'?old.name.slice(0,10):'';this.data.settings={...this.data.settings,...old.settings};for(const key of ['scores','replays'])if(old[key]&&typeof old[key]==='object')this.data[key]=old[key];for(const key of ['danPassed','danRevealed','presets'])if(Array.isArray(old[key]))this.data[key]=old[key];}}catch{this.error='保存データを読み込めません。';}}
  save(){try{this.storage.setItem(this.key,JSON.stringify(this.data));return true;}catch{this.error='このブラウザでは保存できません。';return false;}}
  record(id,diff,s,metadata={}){if(s.auto)return false;const key=JSON.stringify([id,diff]);const old=this.data.scores[key];if(!old||old.hash!==metadata.hash||s.score>old.score){this.data.scores[key]={...metadata,score:s.score,counts:[...s.counts],maxCombo:s.maxCombo,gauge:s.gauge,accuracy:s.score/10000,date:new Date().toISOString()};return this.save();}return false;}
}
export function assetURL(path,base){if(!path||path.startsWith('/')||path.includes('\\')||path.split('/').some(p=>p==='..'||p==='.')||/^[a-z]+:/i.test(path))throw Error('素材のパスが不正です');const url=new URL(path.split('/').map(encodeURIComponent).join('/'),base);if(url.origin!==new URL(base).origin||!url.pathname.startsWith(new URL(base).pathname))throw Error('素材の場所が不正です');return url.href;}
export function parseChart(text){
  const c=parseBaseChart(text),sig=text.match(/^TIMESIG:\s*(\d+)\/(\d+)/m),preview=text.match(/^PREVIEW:\s*([\d.]+)\s*-\s*([\d.]+)/m);c.measure=sig&&Number(sig[2])>0?Number(sig[1])*4/Number(sig[2]):4;c.signature=sig?sig[1]+'/'+sig[2]:'4/4';c.preview=preview?[Number(preview[1]),Number(preview[2])]:[0,16];
  for(const d of c.difficulties){d.layers={};d.camera=[];d.lines=[];d.laneVisibility=[];d.uiVisibility=[];d.ns=[];d.sig=[];d.fever=[];d.unsupported=[];
    const layerTiming=n=>n===1?d:(d.layers[n]??={bpm:[],bs:[],ns:[],sig:[]});
    for(const e of d.events){const p=e.spec,k=e.kind,values=p.map(Number),target=layerTiming(e.layer),raw=p.join(',');
      if(e.layer!==1&&k==='BPM'){if(values[0]>0)target.bpm.push({beat:e.beat,value:values[0]});continue;}
      if(e.layer!==1&&k==='BS'){if(Number.isFinite(values[0]))target.bs.push({beat:e.beat,value:values[0]});continue;}
      if(k==='NS'){target.ns.push({beat:e.beat,value:values[0],mode:values[1]||1,duration:values[2]||0});continue;}
      if(k==='SIG'){const [a,b]=p[0].split('/').map(Number);if(a>0&&b>0)target.sig.push({beat:e.beat,value:a*4/b,label:p[0]});continue;}
      if(e.layer!==1)continue;
      if(['BPM','BS','FILTER','GLITCH'].includes(k))continue;
      if(k==='LANE_VIS'||k==='UI_VIS'){(k==='LANE_VIS'?d.laneVisibility:d.uiVisibility).push({beat:e.beat,value:clamp(values[0]/100,0,1),duration:values[1]||0});continue;}
      if(k==='JUDGE_LINE'){const positions=p[0].split(';').map(Number);if(positions.every(v=>Number.isFinite(v)&&v>=0&&v<=100)&&values[1]>=0&&['RAMP','INSTANT','EASE_IN','EASE_OUT'].includes(p[2]))d.lines.push({beat:e.beat,positions,duration:values[1],ramp:p[2]!=='INSTANT',motion:p[2]});else d.unsupported.push('不正な判定線');continue;}
      if(k==='CAMERA'||k==='CAMERA_ADV'){const m=raw.match(k==='CAMERA'?/^\s*\(\s*([^,]+),\s*([^,]+),\s*([^\)]+)\)\s*([123])(?:\s*,\s*([^,]+))?\s*$/:/^\s*\(\s*([^,]+),\s*([^,]+),\s*([^\)]+)\)\s*,\s*\(\s*([^,]+),\s*([^,]+),\s*([^\)]+)\)\s*([123])(?:\s*,\s*([^,]+))?\s*$/);if(m){const advanced=k==='CAMERA_ADV',a=m.slice(1).map(Number),pose=advanced?a.slice(0,6):[clamp(a[0],-4,4),clamp(a[1],.5,8),clamp(a[2],-20,-1),0,0,0];if(advanced){pose[0]=clamp(pose[0],-20,20);pose[1]=clamp(pose[1],-20,20);pose[2]=clamp(pose[2],-50,20);}d.camera.push({beat:e.beat,pose,mode:a[advanced?6:3],duration:clamp(Number.isFinite(a[advanced?7:4])?a[advanced?7:4]:1,0,32),advanced});}else d.unsupported.push('不正なカメラ');continue;}
      d.unsupported.push(k);
    }
    // Fever belongs to the current difficulty, including events declared between layer blocks.
    let active=false;for(const raw of text.split('\n')){const line=raw.split('//')[0].trim();if(line.startsWith('DIFFICULTY:'))active=line.slice(11).trim()===d.name;if(active&&line.startsWith('FEVER:')){const m=line.slice(6).trim().match(/^([\d.]+)\s*-\s*([\d.]+)$/);if(m&&Number(m[2])>Number(m[1]))d.fever.push({start:Number(m[1]),end:Number(m[2])});}}
    for(const n of d.notes)layerTiming(n.layer);
    for(const layer of Object.values(d.layers)){layer.bpm.sort((a,b)=>a.beat-b.beat);layer.bs.sort((a,b)=>a.beat-b.beat);layer.timing=new Timing(layer,c.bpm);}
    for(const n of d.notes){const timing=n.layer===1?d.timing:d.layers[n.layer].timing;n.startSeconds=timing.seconds(n.beat);n.endSeconds=timing.seconds(n.end);n.scrollStart=timing.scroll(n.beat);n.scrollEnd=timing.scroll(n.end);}
    d.notes.sort((a,b)=>a.startSeconds-b.startSeconds);d.unsupported=[...new Set(d.unsupported)];
    d.measurePositions=[];let b=0,length=c.measure,index=0,last=Math.max(0,...d.notes.filter(n=>n.layer===1).map(n=>n.end))+64;
    for(let count=0;count<100000&&b<=last;count++){while(index<d.sig.length&&d.sig[index].beat<=b+1e-9)length=d.sig[index++].value;d.measurePositions.push(d.timing.scroll(b));let next=b+length;if(index<d.sig.length&&d.sig[index].beat<next)next=d.sig[index].beat;if(next<=b)break;b=next;}
  }return c;
}
export function rank(score){return score>=1000000?'Psi':score>=995000?'Ex+':score>=990000?'Ex':score>=950000?'SS':score>=900000?'S':score>=860000?'A':score>=820000?'B':score>=780000?'C':'';}
export function chartRating(level,score){return clamp(Number(level)||0,0,30)*(clamp(score,0,1000000)/1000000)**3;}
export const difficultyName=name=>!name||!name.trim()||name.trim().toLowerCase()==='custom'?'SPECIAL':name.trim().toUpperCase();
