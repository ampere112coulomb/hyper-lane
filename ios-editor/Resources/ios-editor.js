'use strict';
// Keep Android's touch editor unchanged, replacing only its native bridge.
window.AndroidEditor={postMessage:message=>window.webkit.messageHandlers.editor.postMessage(typeof message==='string'?message:JSON.stringify(message))};
window.chrome=window.chrome||{};
window.chrome.webview={postMessage:message=>AndroidEditor.postMessage(message)};

// Touch placement already synthesizes one editor mouse action on pointerup.
// Do not also run the desktop handlers for WebKit's compatibility mouse events.
let editorTouchUntil=0;
const editorTouchPointers=new Set();
const isEditorSurface=target=>!!target?.closest?.('#editor-canvas,#minimap-canvas');
const editable=target=>!!target?.closest?.('input,textarea,[contenteditable="true"]');
document.addEventListener('pointerdown',event=>{
  if(event.pointerType!=='mouse'&&isEditorSurface(event.target)){
    editorTouchPointers.add(event.pointerId);editorTouchUntil=performance.now()+1000;
  }
},true);
for(const type of ['pointerup','pointercancel'])document.addEventListener(type,event=>{
  if(editorTouchPointers.delete(event.pointerId))editorTouchUntil=performance.now()+1000;
},true);
for(const type of ['mousedown','mousemove','mouseup','click'])document.addEventListener(type,event=>{
  if(isEditorSurface(event.target)&&(editorTouchPointers.size||performance.now()<editorTouchUntil||event.sourceCapabilities?.firesTouchEvents)){
    event.preventDefault();event.stopImmediatePropagation();
  }
},true);
for(const type of ['selectstart','contextmenu','dragstart'])document.addEventListener(type,event=>{
  if(!editable(event.target)){event.preventDefault();}
},true);

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
  style.textContent='html,body,canvas,button,label,summary{-webkit-touch-callout:none;-webkit-user-select:none;user-select:none}input,textarea,[contenteditable="true"]{-webkit-user-select:text;user-select:text}#test-play-canvas,#editor-canvas,#minimap-canvas{touch-action:none}';
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
