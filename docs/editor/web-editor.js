'use strict';
// Browser-only adapter: selected documents never leave this device.
const webFiles=new Map();let webAudioGeneration=0,webChartHandle=null;
function webStatus(message){if(typeof mobileStatus==='function')mobileStatus(message);}
function webChoose(accept,multiple=false,directory=false){return new Promise(resolve=>{
 const picker=document.createElement('input');picker.type='file';picker.accept=accept;picker.multiple=multiple;if(directory)picker.setAttribute('webkitdirectory','');picker.hidden=true;document.body.append(picker);
 picker.onchange=()=>{const files=Array.from(picker.files||[]);picker.remove();resolve(files);};picker.oncancel=()=>{picker.remove();resolve([]);};picker.click();
});}
async function webDecode(bytes){if(!audioCtx||audioCtx.state==='closed'){audioCtx=new (window.AudioContext||window.webkitAudioContext)({latencyHint:'interactive'});bgmGainNode=null;seGainNode=null;}const {decodeAudio}=await import('../play/decoder.mjs');return decodeAudio(audioCtx,bytes);}
async function webLoadAudio(file){if(!file)return;const generation=++webAudioGeneration;webStatus('音源を読み込み中…');try{
 const decoded=await webDecode(await file.arrayBuffer());if(generation!==webAudioGeneration)return;
 audioBuffer=decoded;document.getElementById('meta-song').value=file.name;document.getElementById('audio-label').textContent='曲: '+file.name;extractWaveform(decoded);markEditorDirty();webStatus('音源を読み込みました');
 }catch(error){webStatus('音源の読み込み失敗: '+error.message);alert('音源を読み込めませんでした: '+error.message);}}
function webRemember(files){for(const f of files){webFiles.set(f.name.toLowerCase(),f);if(f.webkitRelativePath)webFiles.set(f.webkitRelativePath.toLowerCase(),f);}}
async function webOpenFiles(files){webRemember(files);const chart=files.find(f=>/\.2d$/i.test(f.name))||files.find(f=>/\.txt$/i.test(f.name));if(!chart){webStatus('.2dまたは.txtの譜面を含むファイルを選択してください');return;}
 if(testPlay.active)exitTestPlay();if(isPlaying)togglePlayback();webAudioGeneration++;audioBuffer=null;waveformData=[];
 importChartText(await chart.text(),chart.name);const name=document.getElementById('meta-song').value.replaceAll('\\','/');const file=webFiles.get(name.toLowerCase())||webFiles.get(name.split('/').pop().toLowerCase());
 if(file)await webLoadAudio(file);else webStatus('譜面を読み込みました。「音源を選ぶ」から指定音源を選択してください');}
async function webSave(text,name){const blob=new Blob([text],{type:'text/plain;charset=utf-8'});const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=name&&/\.(2d|txt)$/i.test(name)?name:'chart.2d';document.body.append(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),60000);markEditorSaved();webStatus(`${/\.txt$/i.test(a.download)?'.txt':'.2d'}をダウンロードしました`);}
window.AndroidEditor={postMessage:message=>{let data;try{data=JSON.parse(message);}catch{return;}void webMessage(data).catch(error=>{webStatus(error.message);alert(error.message);});}};
async function webMessage(data){switch(data.type){
 case 'open-chart':await webOpenFiles(await webChoose('.2d,.txt,text/plain,audio/*,.ogg,.wav,.mp3,.m4a',true));break;
 case 'open-audio':await webLoadAudio((await webChoose('audio/*,.ogg,.mp3,.wav,.m4a'))[0]);break;
 case 'save-chart':await webSave(data.text,data.filename);break;
 case 'load-se':try{const response=await fetch('SE.ogg');if(!response.ok)throw Error('SE取得失敗');seBuffer=await webDecode(await response.arrayBuffer());}catch(e){webStatus('SE読み込み失敗: '+e.message);}break;
 case 'native-test':await audioCtx?.resume();startTestPlay(false);break;
 case 'native-video':alert('Web版ではMP4書き出しは未対応です。PC版エディタを使用してください。');break;
 case 'toggle-fullscreen':if(document.fullscreenElement)await document.exitFullscreen();else await document.documentElement.requestFullscreen?.();break;
 case 'finish':location.href='../';break;
}}
document.addEventListener('DOMContentLoaded',()=>{
 loadAudioFile=input=>void webLoadAudio(input.files?.[0]);
 document.addEventListener('pointerdown',()=>{if(audioCtx&&audioCtx.state!=='running')void audioCtx.resume().catch(e=>webStatus(e.message));},true);
 const original=togglePlayback;togglePlayback=()=>{if(!audioCtx)audioCtx=new (window.AudioContext||window.webkitAudioContext)({latencyHint:'interactive'});void audioCtx.resume().catch(e=>webStatus(e.message));return original();};
 // Visuals follow audible output time; SE must follow the BGM rendering clock.
 // Using the visual beat here would add the output latency a second time.
 const originalCheckSE=checkAndPlaySE;
 checkAndPlaySE=visualBeat=>{
   const audioBeat=isPlaying&&audioBuffer&&audioCtx
     ?timeToBeat((audioCtx.currentTime-startTime)*playbackRate):visualBeat;
   return originalCheckSE(audioBeat);
 };
 mobileAction('フォルダから開く',async()=>webOpenFiles(await webChoose('',true,true)));
 webStatus('Web Editor：譜面と音源は端末内で処理／保存は.2d・.txtダウンロード');
});
