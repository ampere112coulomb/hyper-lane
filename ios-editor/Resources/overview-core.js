(()=>{
'use strict';
const numberPattern='(?:\\d+(?:\\.\\d*)?|\\.\\d+)';
const notePattern=new RegExp(`^(${numberPattern})([HMDW]?)(?:-(${numberPattern})([HG]?))?$`,'i');
function parseChart(text){
  if(typeof text!=='string'||text.length>10*1024*1024)throw Error('10MB以下の譜面ファイルを選んでください。');
  const chart={title:'Untitled',difficulties:[],warnings:0};let active=null,layer=1,lanes=4,count=0;
  const difficulty=name=>{let d=chart.difficulties.find(x=>x.name.toLowerCase()===name.toLowerCase());if(!d){d={name,level:'',label:'',lanes,notes:[]};chart.difficulties.push(d);}return d;};
  for(let line of text.replace(/^\uFEFF/,'').split(/\r?\n/)){
    line=line.trim();if(!line||line.startsWith('//')||line.startsWith('#'))continue;
    const header=line.match(/^([A-Z_]+)\s*:\s*(.*)$/i);
    if(header){const key=header[1].toUpperCase(),value=header[2].trim();
      if(key==='TITLE')chart.title=value||'Untitled';
      else if(key==='DIFFICULTY'){active=difficulty(value||'Master');layer=1;}
      else if(key==='LANES'){const n=Number(value);if(!Number.isInteger(n)||n<1||n>32)throw Error('対応レーン数は1〜32です。');if(active)active.lanes=n;else lanes=n;}
      else if(key==='LAYER'){const n=Number(value);if(!Number.isSafeInteger(n)||n<1||n>256)throw Error('レイヤー番号が不正です。');layer=n;}
      else if(key==='LEVEL')(active??=difficulty('Master')).level=value;
      else if(key==='LEVEL_DISPLAY')(active??=difficulty('Master')).label=value;
      continue;
    }
    const parts=line.split(',').map(x=>x.trim()),match=parts[0].match(notePattern),lane=Number(parts[1]);
    if(!match||!Number.isSafeInteger(lane)||Math.abs(lane)>1000000){chart.warnings++;continue;}
    active??=difficulty('Master');
    const beat=Number(match[1]),endBeat=match[3]===undefined?beat:Number(match[3]);
    if(!Number.isFinite(beat)||!Number.isFinite(endBeat)||endBeat<beat||endBeat>1000000){chart.warnings++;continue;}
    const suffix=match[2].toUpperCase(),endSuffix=(match[4]||'').toUpperCase();
    const type=match[3]!==undefined?(endSuffix==='G'?'guide':'long'):({H:'hold',M:'large',W:'largeHold',D:'damage'}[suffix]||'normal');
    const rawSize=parts.find(x=>/^SIZE=/i.test(x));let size=rawSize?Number(rawSize.slice(5)):(type==='large'||type==='largeHold'?2:1);
    if(!Number.isSafeInteger(size)||size<1)size=1;
    const color=parts.find(x=>/^#[\da-f]{6}$/i.test(x));
    active.notes.push({beat,endBeat,lane,size,type,layer,color,holdStart:suffix==='H',holdEnd:endSuffix==='H'});
    if(++count>150000)throw Error('ノーツ数の上限（150,000）を超えています。');
  }
  chart.difficulties=chart.difficulties.filter(d=>d.notes.length);
  if(!chart.difficulties.length)throw Error('読み込めるノーツがありません。.2d形式の譜面を選んでください。');
  return chart;
}

function layout(diff,settings){
  const beats=Number(settings.beats),height=Number(settings.height),laneWidth=Number(settings.laneWidth);
  if(!Number.isInteger(beats)||beats<4||beats>256||!Number.isInteger(height)||height<400||height>4000||!Number.isInteger(laneWidth)||laneWidth<6||laneWidth>48)throw Error('設定範囲を確認してください（拍数4〜256／高さ400〜4000／幅6〜48）。');
  const notes=diff.notes.filter(n=>(settings.guides||n.type!=='guide')&&(settings.layer==='all'||n.layer===Number(settings.layer)));
  if(!notes.length)throw Error('この設定で表示するノーツがありません。');
  // Keep the full note footprint, including off-lane starts and oversized Large notes.
  // Reserve it for every column so adjacent columns and measure labels never overlap.
  const leftLane=notes.reduce((m,n)=>Math.min(m,n.lane-1),0);
  const rightLane=notes.reduce((m,n)=>Math.max(m,n.lane-1+n.size),diff.lanes);
  const contentWidth=(rightLane-leftLane)*laneWidth;
  const last=notes.reduce((m,n)=>Math.max(m,n.endBeat),0),columns=Math.floor(last/beats)+1,stride=38+contentWidth+18;
  const width=Math.max(640,24+columns*stride),totalHeight=92+height+36;
  if(columns>200||width>16384||width*totalHeight>48000000)throw Error('画像が大きすぎます。1列の拍数を増やすか、高さ・レーン幅を小さくしてください。');
  return {notes,last,columns,stride,width,height:totalHeight,columnHeight:height,laneWidth,beats,leftLane,rightLane,contentWidth};
}
const colors={normal:'#f5f8ff',hold:'#d4a727',large:'#6fa88b',largeHold:'#d4a727',long:'#be97f4',damage:'#ff6b81',guide:'#6ae0b6'};
function drawOverview(canvas,chart,diff,settings){
  const plan=layout(diff,settings),{columns,stride,width,height,columnHeight,laneWidth,beats,notes,leftLane,contentWidth}=plan;
  canvas.width=width;canvas.height=height;const ctx=canvas.getContext('2d');if(!ctx)throw Error('このブラウザで画像を生成できません。');
  ctx.fillStyle='#1c1e25';ctx.fillRect(0,0,width,height);ctx.textBaseline='top';ctx.fillStyle='#ebf0fa';ctx.font='bold 24px "Yu Gothic UI",sans-serif';
  ctx.fillText(chart.title,18,12,width-36);ctx.fillStyle='#a0adc0';ctx.font='12px "Yu Gothic UI",sans-serif';ctx.fillText(`${diff.name} Lv.${diff.label||diff.level||'—'}`,18,47,width-36);
  const buckets=Array.from({length:columns},()=>[]);
  for(const note of notes){const first=Math.floor(note.beat/beats),last=Math.min(columns-1,Math.floor(note.endBeat/beats));for(let c=first;c<=last;c++)buckets[c].push(note);}
  for(let col=0;col<columns;col++){
    const contentLeft=12+col*stride+38,x=contentLeft-leftLane*laneWidth,trackWidth=diff.lanes*laneWidth,start=col*beats,end=start+beats,y=b=>92+columnHeight-(b-start)*columnHeight/beats;
    ctx.fillStyle='#05090f';ctx.fillRect(x,92,trackWidth,columnHeight);ctx.lineWidth=1;ctx.strokeStyle='#232d3b';ctx.beginPath();
    for(let lane=0;lane<=diff.lanes;lane++){ctx.moveTo(x+lane*laneWidth+.5,92);ctx.lineTo(x+lane*laneWidth+.5,92+columnHeight);}ctx.stroke();
    ctx.font='10px Consolas,monospace';
    for(let b=0;b<=beats;b++){
      const strong=(start+b)%4===0;ctx.strokeStyle=strong?'#647891':'#202b38';ctx.beginPath();ctx.moveTo(x,y(start+b)+.5);ctx.lineTo(x+trackWidth,y(start+b)+.5);ctx.stroke();
      if(strong){ctx.fillStyle='#a0adc0';ctx.fillText(String((start+b)/4),contentLeft-36,y(start+b)-5);}
    }
    ctx.save();ctx.beginPath();ctx.rect(contentLeft,92,contentWidth,columnHeight);ctx.clip();
    for(const n of buckets[col])if(n.type==='long'||n.type==='guide'){
      const left=x+(n.lane-1)*laneWidth+1,w=n.size*laneWidth-2,top=y(Math.min(end,n.endBeat)),h=y(Math.max(start,n.beat))-top,color=n.color||colors[n.type];
      if(h>0){ctx.globalAlpha=n.type==='guide'?.25:.43;ctx.fillStyle=color;ctx.fillRect(left,top,w,h);ctx.globalAlpha=.7;ctx.strokeStyle=color;ctx.strokeRect(left+.5,top,w-1,h);ctx.globalAlpha=1;}
    }
    for(const n of buckets[col].slice().sort((a,b)=>b.beat-a.beat)){
      if(n.type==='guide')continue;
      head(n,n.beat,n.holdStart||n.type==='hold'||n.type==='largeHold');
      if(n.type==='long')head(n,n.endBeat,n.holdEnd);
    }
    ctx.restore();
    function head(n,beat,hold){if(beat<start||beat>=end)return;const left=x+(n.lane-1)*laneWidth+1,w=n.size*laneWidth-2,top=y(beat)-2;ctx.fillStyle=hold?colors.hold:(n.color||colors[n.type]);ctx.fillRect(left,top,w,4);ctx.fillStyle='#e6ffff';ctx.fillRect(left,top,w,1);}
  }
  return plan;
}

function sampleChart(){
  const diff={name:'Master',level:'10.7',lanes:4,notes:[]};
  for(let b=0;b<128;b+=.5){const index=Math.round(b*2);diff.notes.push({beat:b,endBeat:b,lane:1+(index*7+Math.floor(b/4))%4,size:1,type:index%9===0?'hold':'normal',layer:1});
    if(index%12===4)diff.notes.push({beat:b,endBeat:b+2.5,lane:2,size:2,type:'long',holdStart:true,holdEnd:false,layer:1});
    if(index%20===8)diff.notes.push({beat:b+.25,endBeat:b+.25,lane:1,size:2,type:'damage',layer:1});
  }
  diff.notes.push({beat:8,endBeat:15,lane:4,size:1,type:'guide',layer:1});
  return {title:'Hyper Lane — SAMPLE',difficulties:[diff],warnings:0};
}


window.HLOverview={drawOverview,layout};
})();
