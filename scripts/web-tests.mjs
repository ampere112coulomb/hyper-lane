import assert from 'node:assert/strict';
import {parseChart,Timing,Session,Profile,filterAt,grade,assetURL} from '../docs/play/core.mjs';
import {Projection,lineAt,scalarAt,touchLane} from '../docs/play/motion.mjs';
import {parseDan,DanRun,meters} from '../docs/play/dan.mjs';
{
const advanced=parseChart('TITLE: Motion\nBPM: 120\nDIFFICULTY: MASTER\nCHANGE: 0,CAMERA_ADV,(0,2.5,-6),(90,0,0)1,0\nCHANGE: 0,JUDGE_LINE,0;50;0;50,4,EASE_OUT\nCHANGE: 0,NS,2,2,4\nCHANGE: 0,UI_VIS,0,4\nFEVER: 0.5-4.5\nLAYER: 2\nCHANGE: 0,BPM,240\nCHANGE: 1,BS,0\n2,1');
const ad=advanced.difficulties[0];assert.equal(ad.notes[0].startSeconds,.5);assert.equal(ad.fever[0].start,.5);assert.ok(new Projection(ad.camera,2).forward.every(Number.isFinite));assert.equal(lineAt(ad.lines,4,4)[1],50);assert.equal(scalarAt(ad.ns,2),1.125);assert.equal(scalarAt(ad.uiVisibility,4),0);assert.equal(touchLane(4,10,700,1920,1080,50),null);assert.equal(touchLane(4,50,700,1920,1080,50),1);
assert.equal(meters({badMissMaxExclusive:20},[0,0,0,2,3],[])[0].number,'15');assert.equal(meters({badMissMaxExclusive:20},[0,0,0,10,10],[])[0].passed,false);
const definition=parseDan({version:1,title:'Test',tracks:[{chart:'1/chart.2d',difficulty:'MASTER'},{chart:'2/chart.2d',difficulty:'MASTER'}],conditions:{gaugeMin:90,badMissMaxExclusive:20}}),simple=parseChart('BPM: 120\nDIFFICULTY: MASTER\n1,1').difficulties[0],dan=new DanRun({definition,tracks:[{diff:simple},{diff:simple}]});for(let i=0;i<2;i++){const s=new Session(simple,true);dan.configure(s);s.advance(1,new Set());assert.equal(dan.finish(s),i?'end':'next');}assert.equal(dan.passed,true);assert.equal(dan.eligible,false);assert.equal(dan.globalGauge,100);
}
const c=parseChart('TITLE: テスト\nBPM: 120\nLANES: 6\nDIFFICULTY: MASTER\nLEVEL: 30\nCHANGE: 1,BPM,240\nCHANGE: 2,BS,64\nCHANGE: 2.00390625,BS,0\nCHANGE: 3,BS,1\n1,1\n2-3H,2\n4-5G,3,#ffffff,0.2,fade\n6M,4,SIZE=2,SS=-0.5');
assert.equal(c.difficulties.length,1);const d=c.difficulties[0];assert.equal(d.notes[2].opacity,.2);assert.equal(d.notes[3].ss,-.5);assert.equal(d.notes[3].span,2);
assert.equal(d.timing.seconds(3),1);assert.equal(d.timing.beat(1),3);assert.equal(d.timing.scroll(2.00390625)-d.timing.scroll(2),.125);assert.equal(d.timing.scroll(2.9),d.timing.scroll(2.00390625));
for(const b of [-8,0,1,1.2,2.00390625,20])assert.ok(Math.abs(d.timing.beat(d.timing.seconds(b))-b)<1e-10);
assert.deepEqual([40,60,100,120,121].map(grade),[0,1,2,3,-1]);
const filters=[{beat:0,kind:'BRIGHTNESS',strength:-60,duration:4,ramp:true,backgroundOnly:true},{beat:4,kind:'BRIGHTNESS',strength:0,duration:4,ramp:true,backgroundOnly:true}];assert.equal(filterAt(filters,2).strength,-30);assert.equal(filterAt(filters,6).strength,-30);assert.equal(filterAt(filters,8),null);
assert.equal(filterAt([{beat:1,kind:'MONO',strength:100,duration:4,ramp:false}],5),null);
const simple=parseChart('BPM: 120\nDIFFICULTY: EASY\n1,1\n2-3,2').difficulties[0];const s=new Session(simple);s.press(1,.5);s.press(2,1);s.release(2,1.5);assert.equal(s.score,1000000);assert.equal(s.maxCombo,3);
const auto=new Session(simple,true);auto.advance(2,new Set());assert.equal(auto.score,1000000);auto.press(1,2);assert.equal(auto.score,1000000);
const storage={value:null,getItem(){return this.value;},setItem(k,v){this.value=v;}};const p=new Profile(storage);p.data.name='Ampere';assert.equal(p.record('song','EASY',s),true);assert.equal(p.record('song','EASY',auto),false);assert.equal(new Profile(storage).data.name,'Ampere');const worse=new Session(simple);worse.advance(10,new Set());p.record('song','EASY',worse);assert.equal(new Profile(storage).data.scores['["song","EASY"]'].score,1000000);
assert.equal(assetURL('曲名/music.ogg','https://example.com/play/song/a/'),'https://example.com/play/song/a/%E6%9B%B2%E5%90%8D/music.ogg');for(const bad of ['../x','https://evil.test/x','/x','a\\b'])assert.throws(()=>assetURL(bad,'https://example.com/play/song/a/'));
console.log('PASS Android-aligned timing, fine BS pulse, note parsing, score, AUTO exclusion, filters, local profile and asset boundaries');
