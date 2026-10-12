// Only expanded folders containing .2d charts are published. ZIP archives are ignored.
import {readdir,readFile,mkdir,writeFile,copyFile,lstat} from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {parseChart} from '../docs/play/core.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const source=path.resolve(process.argv[2]||path.join(root,'song'));
const output=path.resolve(process.argv[3]||path.join(root,'docs/play/song'));
if(source===output||output.startsWith(source+path.sep))throw Error('Output must not be inside source');
const files=[];
async function walk(dir){for(const entry of await readdir(dir,{withFileTypes:true})){if(entry.name.startsWith('.'))continue;const full=path.join(dir,entry.name);if(entry.isSymbolicLink())throw Error('Song symlinks are not allowed');if(entry.isDirectory())await walk(full);else if(entry.isFile()&&entry.name.endsWith('.2d'))files.push(full);}}
await walk(source);const songs=[],copied=new Set();
async function copy(from,relative){if(copied.has(relative))return;const dest=path.join(output,relative);await mkdir(path.dirname(dest),{recursive:true});await copyFile(from,dest);copied.add(relative);}
for(const file of files.sort()){const relative=path.relative(source,file),folder=path.dirname(file),chart=parseChart(await readFile(file,'utf8'));
  if(!chart.difficulties.length)throw Error(`No difficulties: ${relative}`);
  if(!chart.audio)throw Error(`No audio specified: ${relative}`);
  await copy(file,relative);
  for(const [kind,value] of [['audio',chart.audio],['jacket',chart.jacket],['background',chart.background],['video',chart.video]]){
    if(!value)continue;if(path.isAbsolute(value)||value.includes('\\')||value.split('/').some(p=>p==='..'||p==='.')||/^[a-z]+:/i.test(value))throw Error(`Unsafe ${kind} path: ${relative}`);
    const full=path.resolve(folder,value);if(!full.startsWith(folder+path.sep))throw Error(`Outside chart folder: ${relative}`);
    // Reject symlinks in every component, not just the file itself.
    let cursor=folder;for(const part of value.split('/')){cursor=path.join(cursor,part);try{if((await lstat(cursor)).isSymbolicLink())throw Error(`Symlink asset: ${relative}`);}catch(e){if(e.code==='ENOENT'&&kind!=='audio'){cursor=null;break;}throw e;}}
    if(cursor)await copy(full,path.relative(source,full));
  }
  const encoded=relative.split(path.sep).map(encodeURIComponent).join('/');songs.push({id:relative.split(path.sep).join('/'),chart:`song/${encoded}`,title:chart.title,composer:chart.composer,difficulties:chart.difficulties.map(d=>({name:d.name,level:d.level,lanes:d.lanes}))});
}
await mkdir(output,{recursive:true});await writeFile(path.join(output,'index.json'),JSON.stringify({version:1,songs},null,2)+'\n');console.log(`Generated ${songs.length} songs. Only referenced chart assets copied.`);
