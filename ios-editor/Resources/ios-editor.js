'use strict';
// Keep Android's touch editor unchanged, replacing only its native bridge.
window.AndroidEditor={postMessage:message=>window.webkit.messageHandlers.editor.postMessage(typeof message==='string'?message:JSON.stringify(message))};
window.chrome=window.chrome||{};
window.chrome.webview={postMessage:message=>AndroidEditor.postMessage(message)};

// WebKit's long-press/callout and multi-touch gestures must not steal gameplay.
const gameplayPointers=new Set();
let gameplayTouchUntil=0;
const isTestSurface=target=>!!target?.closest?.('#test-play-canvas');
document.addEventListener('pointerdown',event=>{
  if(typeof testPlay!=='undefined'&&testPlay.active&&isTestSurface(event.target)){
    gameplayPointers.add(event.pointerId);gameplayTouchUntil=performance.now()+500;
  }
},true);
for(const type of ['pointerup','pointercancel'])document.addEventListener(type,event=>{
  if(gameplayPointers.delete(event.pointerId))gameplayTouchUntil=performance.now()+500;
},true);
for(const type of ['touchstart','touchmove','touchend','contextmenu','selectstart','dragstart']){
  document.addEventListener(type,event=>{
    if(typeof testPlay!=='undefined'&&testPlay.active&&isTestSurface(event.target)&&event.cancelable)event.preventDefault();
  },{capture:true,passive:false});
}
document.addEventListener('click',event=>{
  if(typeof testPlay==='undefined'||!testPlay.active)return;
  if(isTestSurface(event.target)||(event.target.closest?.('#mobile-menu-toggle')&&(gameplayPointers.size||performance.now()<gameplayTouchUntil))){
    event.preventDefault();event.stopImmediatePropagation();
  }
},true);
document.addEventListener('DOMContentLoaded',()=>{
  const style=document.createElement('style');
  style.textContent='#test-play-canvas{-webkit-touch-callout:none;-webkit-user-select:none;user-select:none;touch-action:none}';
  document.head.append(style);
});
document.addEventListener('click',event=>{
  const button=event.target.closest?.('#mobile-menu button');
  if(button?.textContent==='音源を選ぶ'){
    event.preventDefault();event.stopImmediatePropagation();mobileMenuClose();
    AndroidEditor.postMessage(JSON.stringify({type:'open-audio'}));
  }
},true);
window.addEventListener('pagehide',()=>{if(testPlay.active)exitTestPlay();if(isPlaying)togglePlayback();});
