'use strict';
// Only presentation clocks use this; audio scheduling still uses currentTime.
function mobileOutputTime(context){
  const now=performance.now(),current=context.currentTime;
  try{const stamp=context.getOutputTimestamp?.();
    if(context.state==='running'&&stamp?.performanceTime>0&&stamp.contextTime>=0&&now-stamp.performanceTime>=0&&now-stamp.performanceTime<500){
      return Math.min(current,stamp.contextTime+(now-stamp.performanceTime)/1000);
    }
  }catch{}
  const latency=Number.isFinite(context.outputLatency)&&context.outputLatency>0?context.outputLatency:0;
  return Math.max(0,current-Math.min(latency,1));
}
// Native host is deliberately limited to opening/saving user-selected documents.
window.chrome=window.chrome||{};
window.chrome.webview={postMessage:message=>AndroidEditor.postMessage(typeof message==='string'?message:JSON.stringify(message))};
const mobileMenu=document.createElement('div');mobileMenu.id='mobile-menu';document.body.append(mobileMenu);
const mobileToggle=document.createElement('button');mobileToggle.id='mobile-menu-toggle';mobileToggle.textContent='☰';mobileToggle.setAttribute('aria-label','メニュー');document.body.append(mobileToggle);
const mobileMessage=document.createElement('div');mobileMessage.id='mobile-status';document.body.append(mobileMessage);
function mobileStatus(text){mobileMessage.textContent=text;}
async function mobileLoadChartAudio(name){
  const generation=++nativeAudioRequest;
  try{
    const bytes=await fetch('chart-audio').then(r=>r.arrayBuffer());
    if(!audioCtx)audioCtx=new (window.AudioContext||window.webkitAudioContext)({latencyHint:'interactive'});
    const buffer=await audioCtx.decodeAudioData(bytes);
    if(generation!==nativeAudioRequest||name!==effectiveAudioFile())return;
    audioBuffer=buffer;extractWaveform(buffer);
    document.getElementById('audio-label').textContent='曲: '+name;
    mobileStatus('譜面と音源を読み込みました');
  }catch{mobileStatus('音源を読み込めません。メニューから選択してください');}
}
function mobileMenuClose(){mobileMenu.classList.remove('open');}
mobileToggle.onclick=()=>{refreshMobileMenu();mobileMenu.classList.toggle('open');};
let mobileTool='place';
const mobileModeButton=document.createElement('button');mobileModeButton.id='mobile-mode-toggle';mobileModeButton.textContent='配置';mobileModeButton.setAttribute('aria-label','配置と選択を切り替える');document.body.append(mobileModeButton);
function setMobileTool(tool){
  if(mobileDrag){clearTimeout(mobileDrag.timer);if(mobileDrag.started)handleMouseUp(mobileMouse(mobileDrag.start,0,true));mobileDrag=null;}
  mobileTool=tool;
  mobileModeButton.textContent=tool==='select'?'選択':tool==='pan'?'移動':'配置';mobileModeButton.classList.toggle('active',tool==='select');
  mobileStatus(tool==='select'?'長押ししてからドラッグで範囲選択':tool==='place'?'レーンをタップで配置':'スワイプで移動');
}
mobileModeButton.onclick=()=>{document.activeElement?.blur();mobileMenuClose();setMobileTool(mobileTool==='place'?'select':'place');};
const mobileMenuItems=[];
function refreshMobileMenu(){for(const item of mobileMenuItems)item.button.hidden=item.testOnly!==!!testPlay.active;}
function mobileAction(label,run,testOnly=false){const b=document.createElement('button');b.textContent=label;b.onclick=()=>{if(testOnly!==!!testPlay.active)return;document.activeElement?.blur();mobileMenuClose();run();refreshMobileMenu();};mobileMenu.append(b);mobileMenuItems.push({button:b,testOnly});return b;}
const audioPicker=document.createElement('input');audioPicker.type='file';audioPicker.accept='audio/*,.ogg,.mp3,.wav';audioPicker.hidden=true;audioPicker.onchange=()=>{if(audioPicker.files?.[0])document.getElementById('meta-song').value=audioPicker.files[0].name;loadAudioFile(audioPicker);};document.body.append(audioPicker);
const difficultyAudioPicker=document.createElement('input');difficultyAudioPicker.type='file';difficultyAudioPicker.accept='audio/*,.ogg,.mp3,.wav,.m4a';difficultyAudioPicker.hidden=true;document.body.append(difficultyAudioPicker);
function chooseMobileDifficultyAudio(){difficultyAudioPicker.value='';difficultyAudioPicker.click();}
difficultyAudioPicker.onchange=()=>{const file=difficultyAudioPicker.files?.[0];if(!file)return;chartData[currentDifficulty].audio=file.name;document.getElementById('meta-difficulty-song').value=file.name;loadAudioFile(difficultyAudioPicker);syncToGame();markEditorDirty();};
mobileAction('譜面を開く',()=>{if(!hasUnsavedChanges||confirm('未保存の変更があります。別の譜面を開きますか？'))AndroidEditor.postMessage(JSON.stringify({type:'open-chart'}));});
mobileAction('音源を選ぶ',()=>audioPicker.click());
mobileAction('譜面を保存',()=>exportChart());
function mobileKey(code,ctrl=false){window.dispatchEvent(new KeyboardEvent('keydown',{code,ctrlKey:ctrl,bubbles:true,cancelable:true}));}
mobileAction('選択の詳細',()=>{document.getElementById('right-panel').classList.toggle('mobile-details-open');resizeCanvas();});
mobileAction('テスト終了',()=>exitTestPlay(),true);mobileAction('テストやり直し',()=>startTestPlay(true),true);
mobileAction('オート ON / OFF',()=>{testPlay.auto=!testPlay.auto;testPlay.held.clear();},true);mobileAction('ノーツスピード',()=>{if(testPlay.active)toggleTestSpeedMenu();},true);
refreshMobileMenu();
function mobileBack(){if(testPlay.active){exitTestPlay();return;}if(mobileMenu.classList.contains('open')){mobileMenuClose();return;}if(confirm('エディタを終了しますか？ 未保存の変更は保存されません。'))AndroidEditor.postMessage(JSON.stringify({type:'finish'}));}
function mobileMouse(e,button=0,shift=false){return{clientX:e.clientX,clientY:e.clientY,button,shiftKey:shift,ctrlKey:false,altKey:false,metaKey:false,preventDefault(){}};}
// Android-only touch layout: navigation left, chart centre, note palette right.
const mobileLeftRail=document.createElement('div');mobileLeftRail.id='mobile-left-rail';document.body.append(mobileLeftRail);
const mobileNoteDock=document.createElement('div');mobileNoteDock.id='mobile-note-dock';document.body.append(mobileNoteDock);
function mobileDockButton(parent,id,label,run){const b=document.createElement('button');b.id=id;b.textContent=label;b.onclick=()=>{document.activeElement?.blur();mobileMenuClose();run();};parent.append(b);return b;}
mobileDockButton(mobileNoteDock,'mobile-test-button','▶ テスト',()=>startTestPlay(false));
mobileNoteDock.append(mobileModeButton);
const mobilePalette=document.createElement('div');mobilePalette.className='note-type-btn';mobilePalette.id='mobile-note-palette';mobileNoteDock.append(mobilePalette);
for(const [type,label,color] of [['normal','Normal','#e8edf3'],['hold','Hold','#d4a727'],['long','Long','#b387ed'],['guide','Guide','#6fa88b'],['large','Large Notes','#6fa88b'],['largeHold','Large Hold','#d4a727'],['damage','Damage','#f05260']]){
  const b=document.getElementById('btn-'+type);b.textContent=label;b.style.setProperty('--note-color',color);b.onclick=()=>{setNoteType(type);mobileMenuClose();};mobilePalette.append(b);
}
mobileNoteDock.append(document.getElementById('large-note-settings'));
mobileDockButton(mobileLeftRail,'mobile-rail-undo','↶ 戻す',()=>undoLastAction());
mobileDockButton(mobileLeftRail,'mobile-rail-copy','コピー',()=>mobileKey('KeyC',true));
mobileDockButton(mobileLeftRail,'mobile-rail-paste','貼り付け',()=>mobileKey('KeyV',true));
mobileDockButton(mobileLeftRail,'mobile-rail-delete','削除',()=>{deleteSelected();mobileKey('Delete');});
mobileDockButton(mobileLeftRail,'mobile-rail-settings','⚙ 設定',()=>{document.body.classList.toggle('mobile-sidebar-hidden');resizeCanvas();});
const mobilePlayButton=mobileDockButton(mobileLeftRail,'mobile-rail-play','▶',()=>togglePlayback());
const mobileOriginalTogglePlayback=togglePlayback;
togglePlayback=()=>{
  mobileOriginalTogglePlayback();
  mobilePlayButton.textContent=isPlaying?'■':'▶';
  mobilePlayButton.setAttribute('aria-label',isPlaying?'停止':'再生');
};
mobilePlayButton.setAttribute('aria-label','再生');
const mobileSnapPanel=document.getElementById('grid-snap-sidebar');document.body.append(mobileSnapPanel);
const mobileSnapButton=mobileDockButton(document.body,'mobile-snap-toggle','SNAP '+(gridSnap*4),()=>mobileSnapPanel.classList.toggle('mobile-snap-open'));
const mobileOriginalChangeSnap=changeSnap;
changeSnap=value=>{mobileOriginalChangeSnap(value);mobileSnapButton.textContent='SNAP '+(gridSnap*4);if(value!=='custom')mobileSnapPanel.classList.remove('mobile-snap-open');};
document.body.classList.add('mobile-sidebar-hidden');
// Keep the complete note field at the original 6K width (6 x 50 CSS px).
// Dedicated lanes retain their own widths and stay in the same positions.
const mobileOriginalResizeCanvas=resizeCanvas;
resizeCanvas=()=>{
  LANE_WIDTH=300/TOTAL_LANES;
  mobileOriginalResizeCanvas();
  const available=document.getElementById('editor-container').clientWidth;
  const scale=available>0?available/(canvas.width+minimapCanvas.width+9):1;
  canvas.style.width=(canvas.width*scale)+'px';canvas.style.height=canvas.height+'px';
  minimapCanvas.style.width=(minimapCanvas.width*scale)+'px';minimapCanvas.style.height=minimapCanvas.height+'px';
  minimapCanvas.style.marginRight=(8*scale)+'px';
  document.getElementById('canvas-wrapper').style.width=available+'px';
};
const mobileLaneControls=document.createElement('div');mobileLaneControls.id='mobile-lane-controls';mobileNoteDock.append(mobileLaneControls);
let mobileLargeAnchorMode=false;
const mobileLargeMode=mobileDockButton(mobileLaneControls,'mobile-large-mode','Large',()=>{
  mobileLargeAnchorMode=!mobileLargeAnchorMode;
  if(mobileLargeAnchorMode&&currentNoteType!=='large'&&currentNoteType!=='largeHold')setNoteType('large');
  refreshMobileLaneControls();
});
const mobileLaneArrows=document.createElement('div');mobileLaneArrows.id='mobile-lane-arrows';mobileLaneControls.append(mobileLaneArrows);
function mobileLaneArrow(code){mobileKey(code,mobileLargeAnchorMode);refreshMobileLaneControls();}
const mobileLaneLeft=mobileDockButton(mobileLaneArrows,'mobile-lane-left','←',()=>mobileLaneArrow('ArrowLeft'));
const mobileLaneRight=mobileDockButton(mobileLaneArrows,'mobile-lane-right','→',()=>mobileLaneArrow('ArrowRight'));
const mobileLaneLabel=document.createElement('div');mobileLaneLabel.id='mobile-lane-label';mobileLaneControls.append(mobileLaneLabel);
function refreshMobileLaneControls(){
  mobileLargeMode.classList.toggle('active',mobileLargeAnchorMode);mobileLargeMode.setAttribute('aria-pressed',String(mobileLargeAnchorMode));
  const action=mobileLargeAnchorMode?'Largeの持つ位置':'専用レーンページ';
  mobileLaneLeft.setAttribute('aria-label',action+'を左へ');mobileLaneRight.setAttribute('aria-label',action+'を右へ');
  mobileLaneLabel.textContent=mobileLargeAnchorMode?`持つ位置 ${largeNoteAnchor+1}/${largeNoteSize}`:`レーン ${effectLanePage+1}/3`;
}
const mobileOriginalSetNoteType=setNoteType;
setNoteType=type=>{mobileOriginalSetNoteType(type);if(type!=='large'&&type!=='largeHold')mobileLargeAnchorMode=false;refreshMobileLaneControls();};
refreshMobileLaneControls();
let mobileDrag=null;
const editorPointers=new Map();
let mobilePinch=null,pinchGesture=false;
function pinchDistance(){const p=Array.from(editorPointers.values());return p.length>=2?Math.hypot(p[0].x-p[1].x,p[0].y-p[1].y):0;}
canvas.addEventListener('pointerdown',e=>{
  if(e.pointerType==='mouse')return;e.preventDefault();canvas.setPointerCapture(e.pointerId);mobileMenuClose();
  editorPointers.set(e.pointerId,{x:e.clientX,y:e.clientY});
  if(editorPointers.size>=2){
    if(mobileDrag){clearTimeout(mobileDrag.timer);if(mobileDrag.started)handleMouseUp(mobileMouse(mobileDrag.start,0,true));mobileDrag=null;}
    pinchGesture=true;mobilePinch={distance:Math.max(1,pinchDistance()),height:BEAT_HEIGHT};mobileStatus('2本指で拡大・縮小');return;
  }
  if(pinchGesture)return;
  if(mobileDrag)return;
  const drag=mobileDrag={id:e.pointerId,x:e.clientX,y:e.clientY,tool:mobileTool,start:{clientX:e.clientX,clientY:e.clientY},started:false,timer:null};
  if(mobileTool==='select'){
    drag.timer=setTimeout(()=>{if(mobileDrag!==drag||drag.tool!=='select')return;drag.started=true;handleMouseDown(mobileMouse(drag.start,0,true));mobileStatus('範囲選択中：指を動かして離してください');},400);
  }else if(mobileTool==='place'){
    drag.timer=setTimeout(()=>{if(mobileDrag!==drag||drag.tool!=='place')return;drag.tool='pan';mobileStatus('画面移動中：指を動かして離してください');},400);
  }
},{passive:false});
canvas.addEventListener('pointermove',e=>{
  if(editorPointers.has(e.pointerId))editorPointers.set(e.pointerId,{x:e.clientX,y:e.clientY});
  if(pinchGesture){e.preventDefault();if(mobilePinch&&editorPointers.size>=2)BEAT_HEIGHT=Math.max(12,Math.min(800,mobilePinch.height*pinchDistance()/mobilePinch.distance));return;}
  if(!mobileDrag||e.pointerId!==mobileDrag.id)return;e.preventDefault();
  if((mobileDrag.tool==='select'||mobileDrag.tool==='place')&&!mobileDrag.started){
    if(Math.hypot(e.clientX-mobileDrag.start.clientX,e.clientY-mobileDrag.start.clientY)<10)return;
    clearTimeout(mobileDrag.timer);mobileDrag.tool='pan';
  }
  if(mobileDrag.tool==='pan'){if(!isPlaying)viewBeat=Math.max(0,viewBeat+(e.clientY-mobileDrag.y)/BEAT_HEIGHT);document.getElementById('editor-container').scrollLeft+=mobileDrag.x-e.clientX;mobileDrag.y=e.clientY;mobileDrag.x=e.clientX;}
  else handleMouseMove(mobileMouse(e,0,mobileDrag.tool==='select'));
},{passive:false});
let mobileDialogTap=null;
document.addEventListener('click',e=>{
  const tap=mobileDialogTap;mobileDialogTap=null;
  if(tap&&performance.now()-tap.at<300&&Math.abs(e.clientX-tap.x)<3&&Math.abs(e.clientY-tap.y)<3){e.preventDefault();e.stopImmediatePropagation();}
},true);
function mobileDragEnd(e){editorPointers.delete(e.pointerId);if(pinchGesture){e.preventDefault();if(editorPointers.size<2)mobilePinch=null;if(editorPointers.size===0)pinchGesture=false;return;}if(!mobileDrag||e.pointerId!==mobileDrag.id)return;e.preventDefault();clearTimeout(mobileDrag.timer);if(mobileDrag.tool==='place'&&e.type!=='pointercancel'){markEditorDirty();handleMouseDown(mobileMouse(mobileDrag.start));handleMouseUp(mobileMouse(e));if(document.getElementById('gimmick-dialog').style.display==='flex'||document.getElementById('fever-dialog').style.display==='flex')mobileDialogTap={at:performance.now(),x:e.clientX,y:e.clientY};}else if(mobileDrag.started)handleMouseUp(mobileMouse(e,0,mobileDrag.tool==='select'));mobileDrag=null;}
canvas.addEventListener('pointerup',mobileDragEnd);canvas.addEventListener('pointercancel',mobileDragEnd);
let mobileMini=false;
minimapCanvas.addEventListener('pointerdown',e=>{if(e.pointerType==='mouse')return;e.preventDefault();minimapCanvas.setPointerCapture(e.pointerId);mobileMini=true;handleMinimapDown(mobileMouse(e));});
minimapCanvas.addEventListener('pointermove',e=>{if(mobileMini)handleMinimapMove(mobileMouse(e));});
minimapCanvas.addEventListener('pointerup',()=>{mobileMini=false;isMinimapDragging=false;});minimapCanvas.addEventListener('pointercancel',()=>{mobileMini=false;isMinimapDragging=false;});
const testTouchCanvas=document.getElementById('test-play-canvas'),testTouches=new Map();
// Match the actual touch mapping: equal-width strips across the full canvas,
// independent of the chart's camera and moving judgement lines.
const mobileOriginalDrawTestPlay=drawTestPlay;
drawTestPlay=beat=>{
  mobileOriginalDrawTestPlay(beat);
  mobileApplyTestFilter(testTouchCanvas,beat,false);
  if(testPlay.auto)return;
  const g=testTouchCanvas.getContext('2d'),w=testTouchCanvas.width,h=testTouchCanvas.height;
  g.save();g.setTransform(1,0,0,1,0,0);
  g.lineWidth=Math.max(1,w/960);g.strokeStyle='rgba(112,210,255,.65)';
  for(let lane=1;lane<=TOTAL_LANES;lane++){
    const x=(lane-1)*w/TOTAL_LANES,width=w/TOTAL_LANES;
    if(testPlay.held.has(lane)){g.fillStyle='rgba(112,210,255,.12)';g.fillRect(x,0,width,h);}
    g.strokeRect(x+g.lineWidth/2,g.lineWidth/2,width-g.lineWidth,h-g.lineWidth);
  }
  g.restore();
};
testTouchCanvas.addEventListener('pointerdown',e=>{if(!testPlay.active||testPlay.auto||testPlay.speedMenu)return;e.preventDefault();testTouchCanvas.setPointerCapture(e.pointerId);
  const r=testTouchCanvas.getBoundingClientRect(),lane=Math.min(TOTAL_LANES,Math.max(1,Math.floor((e.clientX-r.left)/r.width*TOTAL_LANES)+1));
  testTouches.set(e.pointerId,lane);if(!testPlay.held.has(lane)){testPlay.held.add(lane);hitTestLane(lane);}
},{passive:false});
function mobileRelease(e){const lane=testTouches.get(e.pointerId);testTouches.delete(e.pointerId);if(lane&&!Array.from(testTouches.values()).includes(lane)&&testPlay.held.delete(lane)&&testPlay.active)releaseTestLane(lane);}
testTouchCanvas.addEventListener('pointerup',mobileRelease);testTouchCanvas.addEventListener('pointercancel',mobileRelease);
mobileStatus('配置：タップで配置 / 長押しドラッグで移動');

// Use the same event replacement/ramp semantics as MobileFilters.At in the game.
function mobileFilterAt(events,beat){
  let active=null,from=0;
  const strength=(f,initial,b)=>!f?0:f.ramp?initial+(f.strength-initial)*(f.duration<=0?1:Math.max(0,Math.min(1,(b-f.beat)/f.duration))):b<f.beat+f.duration?f.strength:0;
  for(const event of events){
    if(event.beat>beat)break;
    const previous=strength(active,from,event.beat);
    from=active&&active.kind===event.kind&&active.background===event.background?previous:0;active=event;
  }
  if(!active)return null;
  const value=strength(active,from,beat);return value!==0?{...active,strength:value}:null;
}
function mobileFilterEvents(){
  return changeEvents.filter(e=>e.type==='FILTER'||e.type==='GLITCH').map(e=>{
    const legacy=e.type==='GLITCH',p=String(e.val).split(',').map(x=>x.trim().toUpperCase());
    const kind=legacy?'GLITCH':p[0],value=Number(p[legacy?0:1])||0;
    return{beat:e.beat,kind,strength:Math.max(kind==='BRIGHTNESS'?-100:0,Math.min(100,value)),duration:legacy&&p.length<2?Infinity:Math.max(0,Number(p[legacy?1:2])||0),ramp:!legacy&&p[3]==='RAMP',background:!legacy&&p[4]==='BACKGROUND'};
  }).sort((a,b)=>a.beat-b.beat);
}
const mobileFilterStartTest=startTestPlay;
startTestPlay=(...args)=>{testPlay.filterEvents=mobileFilterEvents();return mobileFilterStartTest(...args);};
window.testPlayFilterBackground=(canvas,beat)=>mobileApplyTestFilter(canvas,beat,true);
let mobileFilterGpu=null,mobileFilterGpuFailed=false;
function mobileCreateFilterGpu(){
  const surface=document.createElement('canvas'),gl=surface.getContext('webgl',{alpha:false,antialias:false,preserveDrawingBuffer:true});
  if(!gl)throw new Error('WebGL unavailable');
  function shader(type,source){const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw new Error(gl.getShaderInfoLog(s));return s;}
  const program=gl.createProgram();
  gl.attachShader(program,shader(gl.VERTEX_SHADER,'attribute vec2 pos;varying vec2 uv;void main(){gl_Position=vec4(pos,0.,1.);uv=pos*.5+.5;}'));
  gl.attachShader(program,shader(gl.FRAGMENT_SHADER,`precision mediump float;
    varying vec2 uv;uniform sampler2D image;uniform vec2 texel;uniform int mode;uniform float amount,time;
    vec3 at(vec2 p){return texture2D(image,clamp(p,vec2(0.),vec2(1.))).rgb;}
    vec3 bright(vec2 p){vec3 c=at(p);return c*smoothstep(.48,.82,max(max(c.r,c.g),c.b));}
    float rnd(float n){return fract(sin(n*12.9898)*43758.5453);}
    void main(){vec2 p=vec2(uv.x,1.-uv.y);vec3 c=at(p);
      if(mode==0)c=mix(c,vec3(dot(c,vec3(.299,.587,.114))),amount);
      else if(mode==1)c=mix(c,1.-c,amount);
      else if(mode==2){vec3 b=bright(p)*.18;for(int i=1;i<=4;i++){float f=float(i);float weight=i==1?.15:i==2?.12:i==3?.09:.05;vec2 dx=vec2(texel.x*f*4.,0.),dy=vec2(0.,texel.y*f*4.);b+=(bright(p+dx)+bright(p-dx)+bright(p+dy)+bright(p-dy))*weight*.5;}c+=b*amount*2.5;}
      else if(mode==6){vec2 s=texel*(1.+amount*79.);c=at((floor(p/s)+.5)*s);}
      else if(mode==7){if(mod(floor(p.y/texel.y),4.)<2.)c=mix(c,vec3(0.,8./255.,18./255.),amount*210./255.);}
      else if(mode==8){float tick=floor(time*30.),band=floor(p.y*(32.+amount*280.)),r=rnd(band+tick*917.);float shift=(rnd(band*2.+tick*43.)-.5)*amount*.8*step(r,.2+amount*.8);vec2 q=vec2(fract(p.x+shift),p.y);float rgb=amount*.018;c=vec3(at(q+vec2(rgb,0.)).r,at(q).g,at(q-vec2(rgb,0.)).b);c=mix(c,c.brg,step(.7,r)*amount);c=mix(c,vec3(rnd(band+tick),rnd(band+2.+tick),rnd(band+4.+tick)),step(.88,r)*amount);}
      gl_FragColor=vec4(c,1.);
    }`));
  gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw new Error(gl.getProgramInfoLog(program));gl.useProgram(program);
  const buffer=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,buffer);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array([-1,-1,1,-1,-1,1,1,1]),gl.STATIC_DRAW);
  const pos=gl.getAttribLocation(program,'pos');gl.enableVertexAttribArray(pos);gl.vertexAttribPointer(pos,2,gl.FLOAT,false,0,0);
  const texture=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,texture);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);
  return{surface,gl,program,texture};
}
function mobileApplyTestFilter(canvas,beat,background){
  const filter=mobileFilterAt(testPlay.filterEvents||[],beat);
  if(!filter||filter.background!==background)return;
  const g=canvas.getContext('2d'),w=canvas.width,h=canvas.height;
  g.save();g.setTransform(1,0,0,1,0,0);g.globalAlpha=1;
  try{
    if(filter.kind==='BRIGHTNESS'){g.fillStyle=filter.strength>0?'white':'black';g.globalAlpha=Math.abs(filter.strength)/100;g.fillRect(0,0,w,h);return;}
    if(mobileFilterGpuFailed)return;
    mobileFilterGpu??=mobileCreateFilterGpu();const{surface,gl,program,texture}=mobileFilterGpu;
    if(surface.width!==w||surface.height!==h){surface.width=w;surface.height=h;}
    gl.viewport(0,0,w,h);gl.useProgram(program);gl.bindTexture(gl.TEXTURE_2D,texture);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,canvas);
    gl.uniform1i(gl.getUniformLocation(program,'mode'),{MONO:0,INVERT:1,BLOOM:2,MOSAIC:6,SCANLINE:7,GLITCH:8}[filter.kind]??-1);
    gl.uniform1f(gl.getUniformLocation(program,'amount'),filter.strength/100);gl.uniform2f(gl.getUniformLocation(program,'texel'),1/w,1/h);gl.uniform1f(gl.getUniformLocation(program,'time'),testPlay.active?testSongSeconds():beat);
    gl.drawArrays(gl.TRIANGLE_STRIP,0,4);g.drawImage(surface,0,0,w,h);
  }catch(error){mobileFilterGpuFailed=true;mobileStatus('フィルター描画エラー: '+error.message);}
  finally{g.restore();}
}
