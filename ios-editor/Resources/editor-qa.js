// Runs only when explicitly requested by the disposable simulator CI host.
async function iosEditorQa(phase){
  const tests=[];
  function check(ok,name){if(!ok)throw Error(name);tests.push(name);}
  try{
    resizeCanvas();
    check(typeof mobileFilterAt==='function'&&typeof startTestPlay==='function','touch editor and test play loaded');
    check(!!document.getElementById('mobile-mode-toggle'),'touch dock loaded');
    setMobileTool('select');check(mobileTool==='select','selection mode');setMobileTool('place');
    const width=canvas.width;
    for(const lanes of [4,6]){changeLaneCount(lanes);check(canvas.width===width,'fixed total width '+lanes+'K');}
    changeLaneCount(4);
    notes=[{beat:1,lane:1,type:'normal'},{beat:2,lane:2,type:'hold',endBeat:3},{beat:4,lane:3,type:'long',endBeat:6},{beat:2,lane:4,type:'guide',endBeat:5,color:'#62a582',guideOpacity:50}];
    viewBeat=0;
    const sample=document.createElement('canvas');sample.width=160;sample.height=80;
    const g=sample.getContext('2d');
    const results={};
    for(const kind of ['MONO','INVERT','BLOOM','SCANLINE','MOSAIC','GLITCH','BRIGHTNESS']){
      g.fillStyle='rgb(200,80,20)';g.fillRect(0,0,160,80);
      testPlay.filterEvents=[{beat:0,kind,strength:100,duration:4,ramp:false,background:false}];
      mobileApplyTestFilter(sample,1,false);check(!mobileFilterGpuFailed,'WebKit filter '+kind);
      results[kind]=Array.from(g.getImageData(10,10,1,1).data);
    }
    check(results.BRIGHTNESS[0]===255&&results.BRIGHTNESS[1]===255,'brightness white');
    check(Math.abs(results.INVERT[0]-55)<3,'inversion pixel');
    check(Math.abs(results.MONO[0]-results.MONO[1])<3,'monochrome pixel');
    changeEvents=[{type:'FILTER',beat:0,val:'BRIGHTNESS,100,4,RAMP,BACKGROUND'}];
    layerData(currentDifficulty,currentLayer).notes=notes;
    layerData(currentDifficulty,currentLayer).changeEvents=changeEvents;
    const events=mobileFilterEvents();check(mobileFilterAt(events,1).strength===25,'filter ramp');
    check(mobileFilterAt(events,1).background,'background-only targeting');
    const send=AndroidEditor.postMessage;let saved;
    AndroidEditor.postMessage=s=>{const m=JSON.parse(s);if(m.type==='save-chart')saved=m;};
    try{exportChart();}finally{AndroidEditor.postMessage=send;}
    check(!!saved?.text&&saved.text.includes('FILTER'),'chart export with filter');
    importChartText(saved.text,'qa.2d');check(notes.length===4,'chart import round trip');
    audioCtx??=new (window.AudioContext||window.webkitAudioContext)();
    audioBuffer=audioCtx.createBuffer(2,audioCtx.sampleRate*20,audioCtx.sampleRate);
    if(phase==='test'){
      startTestPlay(false);check(testPlay.active,'start test');
      testPlay.auto=false;testPlay.held.add(1);testStateFor(notes[0]).done=true;
      startTestPlay(true);check(testPlay.active&&!testPlay.held.size&&!testStateFor(notes[0]).done,'restart restores notes');
      testPlay.auto=true;refreshMobileMenu();
      check(mobileMenuItems.filter(i=>!i.button.hidden).every(i=>i.testOnly),'test-only menu');
      changeEvents=[{type:'FILTER',beat:0,val:'MONO,75,20,INSTANT,ALL'}];testPlay.filterEvents=mobileFilterEvents();
    }else if(phase==='filter'){
      document.getElementById('dialog-type').value='FILTER';
      openGimmickDialog(1);
    }
    await new Promise(resolve=>setTimeout(resolve,500));
    AndroidEditor.postMessage(JSON.stringify({type:'qa-report',report:{pass:true,phase,tests,results}}));
  }catch(error){AndroidEditor.postMessage(JSON.stringify({type:'qa-report',report:{pass:false,phase,tests,error:String(error)}}));}
}
