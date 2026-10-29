const {execFile}=require('node:child_process');const {promisify}=require('node:util');const path=require('node:path');
function parseCs2Processes(text){return typeof text==='string'&&text.split(/\r?\n/).some(line=>/^"cs2\.exe","[1-9]\d*",/i.test(line.trim()));}
async function cs2Running(){try{const {stdout}=await promisify(execFile)(path.join(process.env.SystemRoot||'C:\\Windows','System32','tasklist.exe'),['/FI','IMAGENAME eq cs2.exe','/FO','CSV','/NH'],{windowsHide:true,timeout:4000,maxBuffer:65536});return parseCs2Processes(stdout);}catch{return false;}}
async function waitForCs2({running=cs2Running,pause=()=>new Promise(resolve=>setTimeout(resolve,1500)),cancelled=()=>false}={}){while(!cancelled()){if(await running())return true;await pause();}return false;}
module.exports={parseCs2Processes,cs2Running,waitForCs2};
