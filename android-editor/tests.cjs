const fs=require('fs'),vm=require('vm'),assert=require('assert');
class Node {
 constructor(tag='DIV'){this.tagName=tag.toUpperCase();this.style={};this.children=[];this.listeners={};this.value='';this.textContent='';this.width=900;this.height=400;this.clientWidth=900;this.clientHeight=400;this.scrollLeft=0;this.classes=new Set();this.classList={add:(...x)=>x.forEach(s=>this.classes.add(s)),remove:(...x)=>x.forEach(s=>this.classes.delete(s)),contains:s=>this.classes.has(s),toggle:(s,force)=>{const on=force??!this.classes.has(s);on?this.classes.add(s):this.classes.delete(s);return on;}};}
 addEventListener(name,fn){(this.listeners[name]??=[]).push(fn);} dispatchEvent(e){for(const f of this.listeners[e.type]??[])f(e);return true;}
 append(...nodes){nodes.forEach(n=>{this.children.push(n);n.parentNode=this;});}appendChild(n){this.append(n);return n;}removeChild(n){this.children=this.children.filter(c=>c!==n);}add(n){this.append(n);}
 replaceChildren(...nodes){this.children=[];this.append(...nodes);}
 setAttribute(k,v){this[k]=v;}getAttribute(k){return this[k];}focus(){}blur(){document.activeElement=document.body;}click(){this.onclick?.();}remove(){}setPointerCapture(){}
 querySelectorAll(){return [];}querySelector(){return new Node();}closest(){return new Node();}getBoundingClientRect(){return{left:0,top:0,width:this.width,height:this.height};}
 getContext(){return new Proxy({measureText:t=>({width:String(t).length*8}),createLinearGradient:()=>({addColorStop(){}}),createRadialGradient:()=>({addColorStop(){}})},{get:(o,k)=>o[k]??(()=>{})});}
}
const html=fs.readFileSync(__dirname+'/../assets/editor.html','utf8').replace('const LANE_WIDTH = 50;','let LANE_WIDTH = 50;'),nodes=new Map();
const document={body:new Node('BODY'),activeElement:new Node('BUTTON'),hidden:false,createElement:tag=>new Node(tag),getElementById:id=>{if(!nodes.has(id))nodes.set(id,new Node());return nodes.get(id);},querySelectorAll:()=>[],querySelector:()=>new Node(),addEventListener(){}};
for(const m of html.matchAll(/<(input|select|button|canvas|div)[^>]*id="([^"]+)"[^>]*>/g)){const n=new Node(m[1]);n.value=m[0].match(/value="([^"]*)"/)?.[1]??'';nodes.set(m[2],n);}
nodes.get('editor-container').clientWidth=769;
nodes.get('minimap-canvas').width=40;
class Event{constructor(type,fields={}){this.type=type;Object.assign(this,fields);}preventDefault(){}stopImmediatePropagation(){}}
for(const n of nodes.values())n.style.setProperty=function(key,value){this[key]=value;};
const window=new Node(),messages=[];
const timers=new Map();let timerId=0;
Object.assign(window,{devicePixelRatio:1,AudioContext:class{currentTime=0;createGain(){return{gain:{value:1},connect(){}}}resume(){}},chrome:{},AndroidEditor:{postMessage:s=>messages.push(JSON.parse(s))}});
const sandbox={window,document,AndroidEditor:window.AndroidEditor,console,localStorage:{getItem:()=>null,setItem(){}},history:{replaceState(){},pushState(){}},location:{href:'https://appassets.androidplatform.net/editor.html'},navigator:{},innerWidth:900,innerHeight:400,devicePixelRatio:1,performance:{now:()=>100},requestAnimationFrame:()=>1,cancelAnimationFrame(){},setInterval(){},setTimeout(fn,ms){timers.set(++timerId,{fn,ms});return timerId;},clearTimeout(id){timers.delete(id);},alert(){},confirm:()=>true,prompt:()=>null,KeyboardEvent:Event,MouseEvent:Event,Image:class{},Option:class extends Node{},URL:{createObjectURL:()=>'',revokeObjectURL(){}},Blob:global.Blob};
vm.createContext(sandbox);vm.runInContext(html.match(/<script>([\s\S]*?)<\/script>/)[1],sandbox,{timeout:5000});
vm.runInContext(fs.readFileSync(__dirname+'/mobile-editor.js','utf8'),sandbox,{timeout:5000});
const run=s=>vm.runInContext(s,sandbox);
run('window.onload()');
const initialLanes=run('TOTAL_LANES');run('changeLaneCount(4)');const fourWidth=run('canvas.width');
assert.equal(run('canvas.style.width'),fourWidth+'px');
for(const lanes of [3,4,5,6,8,12]){run('changeLaneCount('+lanes+')');assert.equal(run('canvas.width'),fourWidth,'total field width stays fixed');assert.equal(run('LANE_WIDTH*TOTAL_LANES'),300,'note field uses 6K total width');assert.equal(run('canvas.style.width'),run('canvas.width')+'px');}
run('changeLaneCount('+initialLanes+')');
assert(Math.abs(run("mobileOutputTime({currentTime:10,state:'running',getOutputTimestamp:()=>({contextTime:9.88,performanceTime:100})})")-9.88)<1e-9,'presentation timestamp removes output delay');
assert(Math.abs(run('mobileOutputTime({currentTime:10,outputLatency:.12})')-9.88)<1e-9,'latency fallback');
assert.equal(run('mobileOutputTime({currentTime:10})'),10,'unsupported output timing preserves existing clock');
assert(messages.some(m=>m.type==='load-se'));
assert.equal(run('mobileMenu.children.length'),8);
assert.deepEqual(Array.from(run('mobileMenu.children').filter(b=>!b.hidden),b=>b.textContent),['譜面を開く','音源を選ぶ','譜面を保存','選択の詳細']);
run('testPlay.active=true;mobileToggle.click()');
assert.deepEqual(Array.from(run('mobileMenu.children').filter(b=>!b.hidden),b=>b.textContent),['テスト終了','テストやり直し','オート ON / OFF','ノーツスピード']);
run('testPlay.active=false;mobileMenuClose();refreshMobileMenu()');
for(const label of ['テストプレイ','元に戻す','コピー','貼り付け','選択を削除','設定欄 表示 / 非表示','Large 持つ位置 ←','Large 持つ位置 →','専用レーン ←','専用レーン →'])assert(!run('mobileMenu.children').some(b=>b.textContent===label),'duplicated menu item removed: '+label);
for(const label of ['再生 / 停止','移動（スワイプ）','ノーツ・ギミック配置','範囲選択','開始拍を指定','拡大','縮小'])assert(!run('mobileMenu.children').some(b=>b.textContent===label),'removed menu item: '+label);
assert.equal(run('mobileTool'),'place');assert.equal(run('mobileModeButton.textContent'),'配置');
assert.equal(run('mobilePalette.children.length'),7,'right dock contains every note type');
assert.equal(run('mobileLeftRail.children.length'),6,'left rail contains edit/play controls');
assert(run("document.body.classList.contains('mobile-sidebar-hidden')"),'settings are initially collapsed');
nodes.get('btn-hold').click();assert.equal(run('currentNoteType'),'hold');nodes.get('btn-normal').click();
run('mobileSnapButton.click()');assert(run("mobileSnapPanel.classList.contains('mobile-snap-open')"));run('changeSnap(16)');assert.equal(run('mobileSnapButton.textContent'),'SNAP 16');run('changeSnap(8)');
run('effectLanePage=0;mobileLaneRight.click()');assert.equal(run('effectLanePage'),1);run('mobileLaneLeft.click()');assert.equal(run('effectLanePage'),0);
run('largeNoteSize=3;largeNoteAnchor=1;mobileLargeMode.click()');assert.equal(run('currentNoteType'),'large');assert.equal(run('mobileLargeAnchorMode'),true);
run('mobileLaneLeft.click();mobileLaneLeft.click()');assert.equal(run('largeNoteAnchor'),0,'anchor stops at left edge');assert.equal(run('effectLanePage'),0,'Large arrows must not switch lane page');
run('mobileLaneRight.click();mobileLaneRight.click();mobileLaneRight.click()');assert.equal(run('largeNoteAnchor'),2,'anchor stops at right edge');
run("setNoteType('largeHold');mobileLaneLeft.click()");assert.equal(run('largeNoteAnchor'),1,'Large Hold also supports anchors');
run("setNoteType('normal')");assert.equal(run('mobileLargeAnchorMode'),false);run('mobileLaneRight.click()');assert.equal(run('effectLanePage'),1);run('effectLanePage=0');
run('mobileModeButton.click()');assert.equal(run('mobileTool'),'select');assert.equal(run('mobileModeButton.textContent'),'選択');
run('mobileModeButton.click()');assert.equal(run('mobileTool'),'place');
const action=label=>{const aliases={'元に戻す':'↶ 戻す','選択を削除':'削除'};const b=[...run('mobileMenu.children'),...run('mobileLeftRail.children')].find(b=>b.textContent===(aliases[label]??label));assert(b,label);b.click();};
assert.equal(run('mobileTool'),'place');
run('viewBeat=10; currentNoteType="normal";');
const canvas=nodes.get('editor-canvas');
const touch=(type,x,y)=>canvas.dispatchEvent(new Event(type,{pointerType:'touch',pointerId:1,clientX:x,clientY:y}));
touch('pointerdown',125,340);touch('pointerup',125,340);
assert.equal(run('notes.length'),1,'touch places one note without duplicate mouse input');
action('元に戻す');assert.equal(run('notes.length'),0);
const pinchTouch=(type,id,x,y)=>canvas.dispatchEvent(new Event(type,{pointerType:'touch',pointerId:id,clientX:x,clientY:y}));
const initialZoom=run('BEAT_HEIGHT');
pinchTouch('pointerdown',1,100,200);pinchTouch('pointerdown',2,200,200);pinchTouch('pointermove',2,300,200);
assert.equal(run('BEAT_HEIGHT'),initialZoom*2,'pinch out zooms in');
pinchTouch('pointermove',2,150,200);assert.equal(run('BEAT_HEIGHT'),initialZoom/2,'pinch in zooms out');
pinchTouch('pointerup',2,150,200);pinchTouch('pointerup',1,100,200);assert.equal(run('notes.length'),0,'pinch must not place notes');assert.equal(run('pinchGesture'),false);
run('BEAT_HEIGHT='+initialZoom);
const beforeHoldBeat=run('viewBeat');
touch('pointerdown',125,200);for(const [id,t] of timers)if(t.ms===400){timers.delete(id);t.fn();}touch('pointermove',125,300);touch('pointerup',125,300);
assert(run('viewBeat> '+beforeHoldBeat),'placement long press pans');assert.equal(run('notes.length'),0,'long press must not place notes');
touch('pointerdown',125,340);touch('pointercancel',125,340);assert.equal(run('notes.length'),0,'cancel must not place notes');
touch('pointerdown',125,200);touch('pointermove',125,300);touch('pointerup',125,300);assert(run('viewBeat>10'),'swipe changes beat');
touch('pointerdown',125,200);touch('pointermove',25,200);touch('pointerup',25,200);assert.equal(nodes.get('editor-container').scrollLeft,100,'horizontal swipe exposes dedicated lanes');
action('譜面を保存');assert(!messages.some(m=>m.type==='save-chart'),'empty chart does not export');
touch('pointerdown',125,340);touch('pointerup',125,340);action('譜面を保存');assert(messages.some(m=>m.type==='save-chart'&&m.text.includes('TITLE')));
run('mobileModeButton.click()');touch('pointerdown',110,320);touch('pointerup',110,320);assert.equal(run('selectedNotes.length'),0,'short tap must not range-select');
touch('pointerdown',110,320);for(const [id,t] of timers)if(t.ms===400){timers.delete(id);t.fn();}touch('pointermove',149,360);touch('pointerup',149,360);assert.equal(run('selectedNotes.length'),1,'long press then drag selects note');
touch('pointerdown',110,320);touch('pointercancel',110,320);assert(!Array.from(timers.values()).some(t=>t.ms===400),'cancelled long press must not fire later');
action('コピー');run('viewBeat+=4');action('貼り付け');assert.equal(run('notes.length'),2,'menu copy/paste uses PC editor undoable operations');
run('selectedNotes=notes.slice()');action('選択を削除');assert.equal(run('notes.length'),0);
run('savedMobileHitTestLane=hitTestLane;testPlay.active=true;testPlay.auto=false;testHits=0;testReleases=0;hitTestLane=()=>testHits++;releaseTestLane=()=>testReleases++;');
const testCanvas=nodes.get('test-play-canvas');
testCanvas.dispatchEvent(new Event('pointerdown',{pointerId:2,clientX:100,clientY:300}));testCanvas.dispatchEvent(new Event('pointerdown',{pointerId:3,clientX:600,clientY:300}));assert.equal(run('testHits'),2);
testCanvas.dispatchEvent(new Event('pointerup',{pointerId:2}));testCanvas.dispatchEvent(new Event('pointercancel',{pointerId:3}));assert.equal(run('testReleases'),2);assert.equal(run('testPlay.held.size'),0);
run('testPlay.active=true;testPlay.auto=true;');nodes.get('test-play-canvas').dispatchEvent(new Event('pointerdown',{pointerId:2,clientX:100,clientY:300}));assert.equal(run('testTouches.size'),0,'AUTO ignores touch input');
run('testPlay.active=false;setMobileTool("place");effectLanePage=1;currentLayer=1;changeEvents=[];');
const filterX=run('START_X+TOTAL_LANES*LANE_WIDTH+CAMERA_LANE_GAP+CAMERA_LANE_WIDTH+BS_LANE_GAP+BS_LANE_WIDTH+BPM_LANE_GAP+BPM_LANE_WIDTH+SIG_LANE_GAP+SIG_LANE_WIDTH/2');
touch('pointerdown',filterX,200);touch('pointerup',filterX,200);
assert.equal(nodes.get('gimmick-dialog').style.display,'flex','filter lane tap opens dialog');
assert.equal(nodes.get('dialog-type').value,'FILTER');
nodes.get('filter-kind').value='BRIGHTNESS';nodes.get('filter-strength').value='-60';nodes.get('filter-duration').value='4';nodes.get('filter-mode').value='INSTANT';nodes.get('filter-target').value='BACKGROUND';
run('saveGimmickDialog()');assert.equal(run('changeEvents[0].type'),'FILTER');
assert.equal(run('changeEvents[0].val'), 'BRIGHTNESS,-60,4,INSTANT,BACKGROUND');
touch('pointerdown',filterX,200);touch('pointerup',filterX,200);
assert.equal(nodes.get('gimmick-dialog').style.display,'flex','existing filter can be edited');
const mobileCss=fs.readFileSync(__dirname+'/mobile-editor.css','utf8');
assert(fs.readFileSync(__dirname+'/mobile-editor.js','utf8').includes('mobileApplyTestFilter(testTouchCanvas,beat,false);\n  if(testPlay.auto)return;'),'AUTO retains filters but skips touch hitbox overlay');
assert(mobileCss.includes('#dialog-overlay{z-index:12000}'),'dialog overlay blocks Android controls');
assert(mobileCss.includes('#gimmick-dialog,#fever-dialog{z-index:12001;'),'dialogs appear above Android controls');
run('changeEvents=[{type:"FILTER",beat:2,val:"BRIGHTNESS,-100,4,INSTANT,ALL"}];testPlay.filterEvents=mobileFilterEvents();');
assert.equal(run('mobileFilterAt(testPlay.filterEvents,1)'),null);
assert.equal(run('mobileFilterAt(testPlay.filterEvents,2).strength'),-100);
assert.equal(run('mobileFilterAt(testPlay.filterEvents,6)'),null,'instant expires at exact end');
run('changeEvents=[{type:"FILTER",beat:0,val:"BRIGHTNESS,100,4,RAMP,ALL"},{type:"FILTER",beat:2,val:"BRIGHTNESS,-100,4,RAMP,ALL"}];testPlay.filterEvents=mobileFilterEvents();');
assert.equal(run('mobileFilterAt(testPlay.filterEvents,1).strength'),25);
assert.equal(run('mobileFilterAt(testPlay.filterEvents,2).strength'),50,'ramp starts from previous interpolated value');
assert.equal(run('mobileFilterAt(testPlay.filterEvents,4).strength'),-25);
assert.equal(run('mobileFilterAt(testPlay.filterEvents,100).strength'),-100,'ramp holds until next event');
for(const kind of ['MONO','INVERT','BLOOM','SCANLINE','MOSAIC','GLITCH','BRIGHTNESS']){
 run(`changeEvents=[{type:'FILTER',beat:0,val:'${kind},100,4,INSTANT,BACKGROUND'}];testPlay.filterEvents=mobileFilterEvents();`);
 assert.equal(run('mobileFilterAt(testPlay.filterEvents,1).kind'),kind);assert.equal(run('mobileFilterAt(testPlay.filterEvents,1).background'),true);
}
run('changeEvents=[{type:"FILTER",beat:0,val:"BRIGHTNESS,100,4,RAMP,ALL"},{type:"FILTER",beat:2,val:"BRIGHTNESS,100,4,RAMP,BACKGROUND"}];testPlay.filterEvents=mobileFilterEvents();');
assert.equal(run('mobileFilterAt(testPlay.filterEvents,3).strength'),25,'target change ramps from zero');
console.log('PASS: touch editing, filter dialog and all filter timing/target semantics');
run(`hitTestLane=savedMobileHitTestLane;notes=[{type:'normal',beat:0,lane:1},{type:'hold',beat:.0625,lane:1}];testPlay.anchorBeat=0;testPlay.states=new Map();testPlay.timing=new Map([[notes[0],{start:0}],[notes[1],{start:.03125}]]);testSongSeconds=()=>.04;testPlaySE=()=>{};hitTestLane(1);`);
assert.equal(run('notes.every(n=>testStateFor(n).done)'),true,'Normal and Hold share one late touch');
console.log('PASS: mobile editor Normal+Hold co-hit');
