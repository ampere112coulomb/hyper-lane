'use strict';
// Keep Android's touch editor unchanged, replacing only its native bridge.
window.AndroidEditor={postMessage:message=>window.webkit.messageHandlers.editor.postMessage(typeof message==='string'?message:JSON.stringify(message))};
window.chrome=window.chrome||{};
window.chrome.webview={postMessage:message=>AndroidEditor.postMessage(message)};
document.addEventListener('click',event=>{
  const button=event.target.closest?.('#mobile-menu button');
  if(button?.textContent==='音源を選ぶ'){
    event.preventDefault();event.stopImmediatePropagation();mobileMenuClose();
    AndroidEditor.postMessage(JSON.stringify({type:'open-audio'}));
  }
},true);
window.addEventListener('pagehide',()=>{if(testPlay.active)exitTestPlay();if(isPlaying)togglePlayback();});
