// Use a normal Playwright install, or the desktop runtime when running locally in Codex.
let playwright;try{playwright=require('playwright');}catch{playwright=require(require('node:path').join(require('node:os').homedir(),'.cache','codex-runtimes','codex-primary-runtime','dependencies','node','node_modules','playwright'));}module.exports=playwright;
