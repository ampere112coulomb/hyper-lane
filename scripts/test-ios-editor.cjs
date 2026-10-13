const fs=require('fs'),path=require('path'),assert=require('assert');
let playwright;try{playwright=require('playwright');}catch{playwright=require(path.join(require('os').homedir(),'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright'));}
const {chromium}=playwright;
(async()=>{
  const root=path.join(__dirname,'../ios-editor/Resources');
  const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true,args:['--enable-unsafe-swiftshader']});
  try{
    for(const phase of ['editing','filter','test']){
      const page=await browser.newPage({viewport:{width:1366,height:1024},hasTouch:true});const errors=[];
      page.on('pageerror',e=>errors.push(e.message));
      await page.addInitScript(()=>{window.qaReport=null;window.webkit={messageHandlers:{editor:{postMessage:s=>{const m=JSON.parse(s);if(m.type==='qa-report')window.qaReport=m.report;}}}};});
      await page.route('http://editor.test/**',async r=>{
        const name=new URL(r.request().url()).pathname.slice(1)||'editor.html';
        let body=fs.readFileSync(path.join(root,name));
        if(name==='editor.html')body=Buffer.from(body.toString().replace('const LANE_WIDTH = 50;','let LANE_WIDTH = 50;').replace('function saveEditorBackupNow() {','function saveEditorBackupNow() { return;').replace('setTimeout(restoreEditorBackupIfNeeded, 300);','').replace('<head>','<head><meta name="viewport" content="width=device-width,initial-scale=1"><script src="ios-editor.js"></script>').replace('</head>','<link rel="stylesheet" href="mobile-editor.css"></head>').replace('</body>','<script src="mobile-editor.js"></script><script src="editor-qa.js"></script></body>'));
        await r.fulfill({contentType:name.endsWith('.html')?'text/html':name.endsWith('.js')?'application/javascript':'text/css',body});
      });
      await page.goto('http://editor.test/',{waitUntil:'load'});
      await page.evaluate(p=>{void iosEditorQa(p);},phase);
      await page.waitForFunction(()=>window.qaReport,{timeout:15000});
      const report=await page.evaluate(()=>window.qaReport);console.log(phase,JSON.stringify(report));
      assert(report.pass,report.error);assert.deepEqual(errors,[]);
      await page.close();
    }
  }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
