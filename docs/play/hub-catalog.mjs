import {unzip} from './vendor/fflate.mjs';
export const HUB='https://hyper-lane-chart-hub.arcane-bard-0210.chatgpt.site';
export async function publicCharts(){
 const response=await fetch(HUB+'/api/web-catalog',{cache:'no-store',credentials:'omit',signal:AbortSignal.timeout(8000)});
 if(!response.ok)throw Error('譜面配布サイトの一覧を取得できません');
 const data=await response.json();if(!Array.isArray(data.items))throw Error('譜面一覧が不正です');
 return data.items.filter(c=>/^[a-f0-9-]{36}$/i.test(c.id)&&Array.isArray(c.difficulties)).map(c=>({id:'hub:'+c.id,hubId:c.id,hub:true,local:false,title:c.title,composer:c.composer,designer:c.designer,difficulties:c.difficulties,hubJacket:c.jacket?HUB+'/api/web-files/'+c.id+'?kind=jacket':null}));
}
export async function publicPackage(id){
 if(!/^[a-f0-9-]{36}$/i.test(id))throw Error('譜面IDが不正です');
 const response=await fetch(HUB+'/api/web-files/'+id,{credentials:'omit',signal:AbortSignal.timeout(120000)});
 if(!response.ok)throw Error(response.status===404?'この譜面は配布終了しています':'譜面のダウンロードに失敗しました');
 if(Number(response.headers.get('content-length'))>100*1024*1024)throw Error('ZIPが大きすぎます');
 const bytes=new Uint8Array(await response.arrayBuffer());if(bytes.length>100*1024*1024)throw Error('ZIPが大きすぎます');
 let count=0,total=0;
 return new Promise((resolve,reject)=>unzip(bytes,{filter:file=>{
  if(++count>10000)throw Error('ファイルが多すぎます');if(file.name.endsWith('/'))return false;
  if(file.name.startsWith('/')||file.name.includes('\\')||file.name.includes(':')||file.name.split('/').some(p=>!p||p==='.'||p==='..'))throw Error('ZIPのパスが不正です');
  total+=file.originalSize;if(file.originalSize>256*1024*1024||total>512*1024*1024)throw Error('展開サイズが大きすぎます');return true;
 }},(error,data)=>error?reject(error):resolve(data)));
}
