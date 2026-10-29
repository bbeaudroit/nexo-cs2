const {test}=require('node:test');
const assert=require('node:assert/strict');
const {EventEmitter}=require('node:events');
const fs=require('node:fs');
const os=require('node:os');
const path=require('node:path');
const {UpdateService}=require('../electron/update-service.cjs');
class Updater extends EventEmitter {
 checks=0;downloads=0;installs=[];
 async checkForUpdates(){this.checks++;return {};}
 async downloadUpdate(){this.downloads++;return [];}
 quitAndInstall(...args){this.installs.push(args);}
}
function fixture(t,extra={}){
 const root=fs.mkdtempSync(path.join(os.tmpdir(),'nexo-updater-'));
 t.after(()=>fs.rmSync(root,{recursive:true,force:true}));
 const installDir=path.join(root,'app'),userData=path.join(root,'user');
 fs.mkdirSync(installDir);fs.mkdirSync(userData);
 fs.writeFileSync(path.join(installDir,'Nexo.exe'),'old-exe');
 fs.mkdirSync(path.join(installDir,'resources'));
 fs.writeFileSync(path.join(installDir,'resources','app.asar'),'old-app');
 fs.writeFileSync(path.join(installDir,'nexo-install.json'),'{}');
 const updater=new Updater(),states=[];
 const service=new UpdateService({updater,enabled:true,userData,installDir,version:'0.7.0',onState:s=>states.push(s),...extra});
 t.after(()=>service.dispose());
 return {root,installDir,userData,updater,states,service};
}
const settle=()=>new Promise(resolve=>setImmediate(resolve));
async function ready(f){f.updater.emit('update-available',{version:'0.8.0'});await settle();f.updater.emit('update-downloaded',{version:'0.8.0'});await f.service.whenIdle();}
test('disabled service never checks, downloads, installs or listens',async t=>{
 const f=fixture(t,{enabled:false});await f.service.check();f.updater.emit('update-available',{version:'0.8.0'});await settle();
 assert.equal(f.updater.checks,0);assert.equal(f.updater.downloads,0);assert.equal(f.service.status().phase,'disabled');await assert.rejects(f.service.install());
});
test('accepted updates auto-download once and bound progress',async t=>{
 const f=fixture(t);assert.equal(f.updater.autoDownload,false);assert.equal(f.updater.autoInstallOnAppQuit,false);
 await f.service.check();f.updater.emit('update-available',{version:'0.8.0'});f.updater.emit('update-available',{version:'0.8.0'});await settle();
 assert.equal(f.updater.downloads,1);f.updater.emit('download-progress',{percent:180});assert.equal(f.service.status().progress,100);
});
test('install stays blocked until a complete backup; duplicate events preserve original files',async t=>{
 let prepared=0;const f=fixture(t,{prepareInstall:async()=>{prepared++;}});
 await assert.rejects(f.service.install());await ready(f);
 assert.equal(f.service.status().phase,'ready');assert.equal(f.service.status().canRollback,true);
 fs.writeFileSync(path.join(f.installDir,'Nexo.exe'),'changed-after-snapshot');
 f.updater.emit('update-downloaded',{version:'0.8.0'});await f.service.whenIdle();
 assert.equal(fs.readFileSync(path.join(f.userData,'update-backups','0.7.0','files','Nexo.exe'),'utf8'),'old-exe');
 await f.service.install();assert.equal(prepared,1);assert.deepEqual(f.updater.installs,[[false,true]]);
});
test('failed preparation never calls quitAndInstall and remains retryable',async t=>{
 const f=fixture(t,{prepareInstall:async()=>{throw Error('engine still running');}});await ready(f);
 await assert.rejects(f.service.install(),/engine still running/);assert.equal(f.updater.installs.length,0);assert.equal(f.service.status().phase,'ready');
});
test('symlinks/junctions and overlapping backup roots block install',async t=>{
 const f=fixture(t);const outside=path.join(f.root,'outside');fs.mkdirSync(outside);fs.symlinkSync(outside,path.join(f.installDir,'escape'),'junction');
 await ready(f);assert.equal(f.service.status().phase,'error');assert.equal(f.service.status().canRollback,false);await assert.rejects(f.service.install());
 const overlap=new UpdateService({updater:new Updater(),enabled:true,installDir:f.installDir,userData:path.join(f.installDir,'data'),version:'0.7.0'});t.after(()=>overlap.dispose());
 overlap.updater.emit('update-available',{version:'0.8.0'});await settle();overlap.updater.emit('update-downloaded',{version:'0.8.0'});await overlap.whenIdle();assert.equal(overlap.status().phase,'error');
});
test('rollback helper is only staged, waits for PID, receives safe paths and ignores the broken version',async t=>{
 const spawned=[],order=[];
 const f=fixture(t,{prepareInstall:async()=>order.push('prepare'),quit:()=>order.push('quit'),spawn:(exe,args,options)=>{spawned.push({exe,args,options});order.push('spawn');const child=new EventEmitter();child.unref=()=>{};queueMicrotask(()=>{child.emit('spawn');child.emit('exit',0);});return child;}});
 await ready(f);f.service.dispose();
 const service=new UpdateService({updater:f.updater,enabled:true,userData:f.userData,installDir:f.installDir,version:'0.8.0',prepareInstall:async()=>order.push('prepare'),quit:()=>order.push('quit'),spawn:(exe,args,options)=>{spawned.push({exe,args,options});order.push('spawn');const child=new EventEmitter();child.unref=()=>{};queueMicrotask(()=>{child.emit('spawn');child.emit('exit',0);});return child;}});t.after(()=>service.dispose());
 assert.equal(service.status().canRollback,true);await service.rollback();assert.deepEqual(order,['prepare','spawn','quit']);assert.equal(spawned.length,1);assert.equal(spawned[0].options.shell,false);assert.equal(spawned[0].options.windowsHide,true);assert.equal(spawned[0].options.detached,false);assert.equal(spawned[0].args.at(-2),'-File');assert.equal(Object.keys(spawned[0].options.env).some(k=>k.toLowerCase()==='psmodulepath'),false);assert.match(fs.readFileSync(spawned[0].args.at(-1),'utf8'),/Start-Process.*-WindowStyle Hidden/);
 const script=fs.readFileSync(path.join(f.userData,'updates','rollback.ps1'),'utf8');assert.match(script,/WaitForExit/);assert.match(script,/ReparsePoint/);assert.match(script,/-LiteralPath/);assert.equal(fs.readFileSync(path.join(f.installDir,'Nexo.exe'),'utf8'),'old-exe');
 const nextUpdater=new Updater(),next=new UpdateService({updater:nextUpdater,enabled:true,userData:f.userData,installDir:f.installDir,version:'0.7.0'});t.after(()=>next.dispose());nextUpdater.emit('update-available',{version:'0.8.0'});await settle();assert.equal(nextUpdater.downloads,0);assert.equal(next.status().phase,'ignored');
});
test('rollback rejects a backup junction or tampered origin; never starts a helper',async t=>{
 let spawns=0;const f=fixture(t,{spawn:()=>{spawns++;throw Error('must not spawn');}});await ready(f);
 const backup=path.join(f.userData,'update-backups','0.7.0','files');fs.symlinkSync(f.installDir,path.join(backup,'escape'),'junction');await assert.rejects(f.service.rollback());assert.equal(spawns,0);
});
test('spawn failure does not quit or leave an ignored version',async t=>{
 let quits=0;const f=fixture(t,{quit:()=>quits++,spawn:()=>{throw Error('spawn failed');}});await ready(f);await assert.rejects(f.service.rollback(),/spawn failed/);assert.equal(quits,0);
 assert.equal(JSON.parse(fs.readFileSync(path.join(f.userData,'updates','state.json'),'utf8')).ignoredVersion||'', '');
});
test('check concurrency and disposal do not duplicate requests or retain listeners',async t=>{
 const f=fixture(t);let resolve;f.updater.checkForUpdates=()=>{f.updater.checks++;return new Promise(r=>resolve=r);};const a=f.service.check(),b=f.service.check();assert.equal(f.updater.checks,1);resolve({});await Promise.all([a,b]);f.service.dispose();assert.equal(f.updater.listenerCount('update-available'),0);assert.equal(f.updater.listenerCount('error'),0);await f.service.check();assert.equal(f.updater.checks,1);
});
test('ignored releases stay ignored after restart but a newer release downloads',async t=>{
 const f=fixture(t);fs.mkdirSync(path.join(f.userData,'updates'),{recursive:true});fs.writeFileSync(path.join(f.userData,'updates','state.json'),JSON.stringify({installDir:f.installDir,ignoredVersion:'0.8.0'}));f.service.dispose();
 const service=new UpdateService({updater:f.updater,enabled:true,userData:f.userData,installDir:f.installDir,version:'0.7.0'});t.after(()=>service.dispose());f.updater.emit('update-available',{version:'0.8.0'});f.updater.emit('update-downloaded',{version:'0.8.0'});await service.whenIdle();assert.equal(f.updater.downloads,0);f.updater.emit('update-available',{version:'0.9.0'});await settle();assert.equal(f.updater.downloads,1);assert.equal(service.status().version,'0.9.0');
});
test('malformed versions never become paths or download targets',async t=>{
 const f=fixture(t);for(const version of ['../escape','0.8/else','0.8..0','x',null]){f.updater.emit('update-available',{version});f.updater.emit('update-downloaded',{version});}await f.service.whenIdle();await settle();assert.equal(f.updater.downloads,0);assert.equal(f.service.status().phase,'idle');
});
test('disabled constructor does not create any directories',t=>{
 const f=fixture(t,{enabled:false});assert.equal(fs.existsSync(path.join(f.userData,'updates')),false);assert.equal(fs.existsSync(path.join(f.userData,'update-backups')),false);
});
test('a tampered manifest cannot restore to another installation',async t=>{
 let spawned=false;const f=fixture(t,{spawn:()=>{spawned=true;throw Error('unexpected');}});await ready(f);const file=path.join(f.userData,'update-backups','0.7.0','manifest.json');const manifest=JSON.parse(fs.readFileSync(file));manifest.installDir=f.root;fs.writeFileSync(file,JSON.stringify(manifest));await assert.rejects(f.service.rollback(),/origin/);assert.equal(spawned,false);
});
test('a failed snapshot can retry the same cached release after the problem is fixed',async t=>{
 const f=fixture(t),outside=path.join(f.root,'outside');fs.mkdirSync(outside);const link=path.join(f.installDir,'escape');fs.symlinkSync(outside,link,'junction');await ready(f);assert.equal(f.service.status().phase,'error');fs.unlinkSync(link);await ready(f);assert.equal(f.updater.downloads,2);assert.equal(f.service.status().phase,'ready');
});
test('PowerShell helper parses without running any restore commands',{skip:process.platform!=='win32'},()=>{
 const source=fs.readFileSync(path.join(__dirname,'../electron/update-service.cjs'),'utf8');const script=source.split('const RESTORE_SCRIPT=String.raw`')[1].split('`;')[0];
 const command="$tokens=$null;$errors=$null;$null=[System.Management.Automation.Language.Parser]::ParseInput([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('"+Buffer.from(script).toString('base64')+"')),[ref]$tokens,[ref]$errors);if($errors.Count){$errors | ForEach-Object {$_.Message};exit 1}";
 require('node:child_process').execFileSync('powershell.exe',['-NoProfile','-NonInteractive','-EncodedCommand',Buffer.from(command,'utf16le').toString('base64')],{windowsHide:true,stdio:'pipe'});
});
test('corrupted backup bytes block install and rollback before preparation or spawn',async t=>{
 let prepares=0,spawns=0;const f=fixture(t,{prepareInstall:async()=>prepares++,spawn:()=>{spawns++;throw Error('must not spawn');}});await ready(f);
 fs.writeFileSync(path.join(f.userData,'update-backups','0.7.0','files','Nexo.exe'),'corrupt-exe');await assert.rejects(f.service.install(),/hash|integrity/i);await assert.rejects(f.service.rollback(),/hash|integrity/i);assert.equal(prepares,0);assert.equal(spawns,0);
});
test('source mutation during copy rejects the entire snapshot and leaves no completed backup',async t=>{
 const f=fixture(t);const copy=fs.promises.copyFile;let changed=false;fs.promises.copyFile=async(src,dst,flags)=>{await copy(src,dst,flags);if(!changed&&src===path.join(f.installDir,'Nexo.exe')){changed=true;fs.writeFileSync(src,'modified-while-copying');}};
 try{await ready(f);assert.equal(f.service.status().phase,'error');assert.equal(fs.existsSync(path.join(f.userData,'update-backups','0.7.0')),false);}finally{fs.promises.copyFile=copy;}
});
test('a synchronously rejected check may be retried',async t=>{
 const f=fixture(t);let calls=0;f.updater.checkForUpdates=()=>{calls++;if(calls===1)throw Error('offline');return Promise.resolve({});};await assert.rejects(f.service.check(),/offline/);await f.service.check();assert.equal(calls,2);
});
