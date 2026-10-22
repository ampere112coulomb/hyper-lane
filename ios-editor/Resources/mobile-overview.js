'use strict';
// PNG export is local only; the native bridge opens a document/share picker.
mobileAction('譜面画像 (PNG)',()=>openMobileOverview());
function openMobileOverview(){
  if(document.getElementById('mobile-overview'))return;
  if(isPlaying)togglePlayback();
  const overlay=document.createElement('div');overlay.id='mobile-overview';overlay.style.cssText='position:fixed;inset:0;z-index:100000;background:#0d1117;color:#eef4ff;padding:max(12px,env(safe-area-inset-top)) max(14px,env(safe-area-inset-right)) max(12px,env(safe-area-inset-bottom)) max(14px,env(safe-area-inset-left));display:flex;flex-direction:column;gap:10px;touch-action:auto;overflow:auto;user-select:none';
  overlay.innerHTML='<div style="display:flex;align-items:center;gap:12px;flex-wrap:wrap"><strong>譜面画像 (PNG)</strong><button data-close style="margin-left:auto">閉じる</button></div><div data-controls style="display:flex;gap:12px;flex-wrap:wrap;align-items:center;font-size:13px"><label>1列の拍数 <select data-beats><option>4</option><option>8</option><option selected>16</option><option>32</option><option>64</option><option>128</option><option>256</option></select></label><label>列高さ(px) <input data-height type="number" min="400" max="4000" value="1000" style="width:75px"></label><label>レーン幅(px) <input data-width type="number" min="6" max="48" value="12" style="width:60px"></label><label><input data-guides type="checkbox" checked>ガイド表示</label><label><input data-layers type="checkbox" checked>全レイヤー</label><button data-update>プレビュー更新</button><button data-save>PNGを保存・共有</button></div><div style="flex:1;min-height:120px;overflow:auto;touch-action:pan-x pan-y;background:#080c14"><canvas style="width:100%;height:auto;display:block"></canvas></div><div data-status style="font-size:12px;color:#9bb0c8" role="status"></div>';
  document.body.append(overlay);
  const find=s=>overlay.querySelector(s),canvas=find('canvas'),status=find('[data-status]'),save=find('[data-save]');let dirty=true,closed=false;
  const close=()=>{closed=true;overlay.remove();};find('[data-close]').onclick=close;
  function draw(){
    try{const data=chartData[currentDifficulty],diff={name:currentDifficulty,level:String(data.level??''),label:data.levelLabel||'',lanes:TOTAL_LANES,notes:layerNumbers().flatMap(layer=>layerData(currentDifficulty,layer).notes.filter(n=>n.type!=='laneOpacity').map(n=>({...n,endBeat:Number.isFinite(n.endBeat)?n.endBeat:n.beat,size:largeNoteSpan(n),layer})))};
      const settings={beats:+find('[data-beats]').value,height:+find('[data-height]').value,laneWidth:+find('[data-width]').value,guides:find('[data-guides]').checked,layer:find('[data-layers]').checked?'all':String(currentLayer)};
      const plan=HLOverview.layout(diff,settings);if(plan.width*plan.height>16000000)throw Error('スマホ版の画像上限は1600万画素です。拍数を増やすか高さ・幅を小さくしてください。');
      HLOverview.drawOverview(canvas,{title:document.getElementById('meta-title').value},diff,settings);dirty=false;save.disabled=false;status.textContent=plan.width+' × '+plan.height+' px ／ 下→上、左列→右列';
    }catch(error){dirty=true;save.disabled=true;status.textContent=error.message;}
  }
  find('[data-update]').onclick=draw;
  for(const input of overlay.querySelectorAll('input,select'))input.onchange=()=>{dirty=true;save.disabled=true;status.textContent='プレビュー更新を押してください。';};
  save.onclick=()=>{
    if(dirty||closed)return;save.disabled=true;
    try{const data=canvas.toDataURL('image/png'),base64=data.split(',')[1];if(base64.length>33554432)throw Error('PNGが大きすぎます。サイズを小さくしてください。');
      const name=(document.getElementById('meta-title').value+'-'+currentDifficulty+'-overview').replace(/[\\/:*?"<>|\x00-\x1f]/g,'_').slice(0,120)+'.png';
      AndroidEditor.postMessage(JSON.stringify({type:'save-overview-png',filename:name,base64}));status.textContent='保存先・共有先を選択してください。';
    }catch(error){status.textContent=error.message;}finally{save.disabled=dirty;}
  };
  draw();
}
window.drawMobileSideLights=(poly,scroll,noteSpeed,ns)=>{
  const scale=noteSpeed*10*Math.max(ns,.1);if(!Number.isFinite(scroll)||!Number.isFinite(scale)||scale<=0)return;
  const spacing=4.8/Math.max(noteSpeed*10,.01),first=Math.floor((scroll-12/scale)/spacing),count=Math.min(160,Math.ceil(64/scale/spacing)+2);
  for(const side of [-1,1])for(let i=0;i<count;i++){
    const index=first+i;let seed=(Math.imul(index>>>0,747796405)+(side<0?2891336453:12582917))>>>0;seed=Math.imul(seed^(seed>>>16),2246822519)>>>0;seed=(seed^(seed>>>13))>>>0;
    const z=(index*spacing-scroll)*scale,near=Math.max(-11,z),far=Math.min(51,z+3*(.65+(seed%1000)/1000*1.7));if(far<=near)continue;
    const x=side*(1.65+((seed>>>10)%1000)/1000*3.2),width=.006+(seed%7)*.001,fade=Math.max(0,Math.min(1,(z+11)/5))*Math.max(0,Math.min(1,(51-z)/10)),a=fade*(48+(seed>>>22)%36)/255;
    for(let section=0;section<4;section++){const start=near+(far-near)*section/4,end=near+(far-near)*(section+1)/4;quad(width*3,a*(1-section/4)/5,'70,170,255');quad(width,a*(1-section/4),'160,225,255');function quad(w,alpha,rgb){poly([{x:x-w,y:0,z:start},{x:x+w,y:0,z:start},{x:x+w,y:0,z:end},{x:x-w,y:0,z:end}],`rgba(${rgb},${alpha})`);}}
  }
};
