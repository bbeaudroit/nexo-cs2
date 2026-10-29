const fs=require('node:fs');
const {powershellEnv}=require('./powershell-env.cjs');
const path=require('node:path');
const crypto=require('node:crypto');
const {spawn:spawnProcess}=require('node:child_process');

const versionName=value=>typeof value==='string'&&/^[0-9][0-9A-Za-z.+-]{0,79}$/.test(value)&&!value.includes('..');
const canonical=value=>path.resolve(value).toLowerCase();
const same=(a,b)=>canonical(a)===canonical(b);
const inside=(root,value)=>{const rel=path.relative(path.resolve(root),path.resolve(value));return rel!==''&&!rel.startsWith('..'+path.sep)&&rel!=='..'&&!path.isAbsolute(rel);};
function plainDirectory(folder){
 const stat=fs.lstatSync(folder);
 if(!stat.isDirectory()||stat.isSymbolicLink()||!same(fs.realpathSync(folder),folder))throw Error('Update directory must not contain links or junctions.');
}
function walk(root){
 plainDirectory(root);const files=[],dirs=[];
 function visit(folder){for(const entry of fs.readdirSync(folder,{withFileTypes:true})){
  const file=path.join(folder,entry.name),stat=fs.lstatSync(file);
  if(!inside(root,file)||stat.isSymbolicLink()||!same(fs.realpathSync(file),file))throw Error('Links or junctions are not allowed in update files.');
  const relative=path.relative(root,file);
  if(stat.isDirectory()){dirs.push(relative);visit(file);}else if(stat.isFile())files.push(relative);else throw Error('Unsupported update file.');
 }}visit(root);return{files:files.sort(),dirs};
}
function atomicText(file,text){const temporary=file+'.'+crypto.randomUUID()+'.tmp';try{fs.writeFileSync(temporary,text,{flag:'wx'});fs.renameSync(temporary,file);}finally{if(fs.existsSync(temporary))fs.unlinkSync(temporary);}}
function atomicJson(file,value){atomicText(file,JSON.stringify(value,null,2));}
function digest(file){const hash=crypto.createHash('sha256'),buffer=Buffer.allocUnsafe(1024*1024),fd=fs.openSync(file,'r');try{let size;while((size=fs.readSync(fd,buffer,0,buffer.length,null))>0)hash.update(buffer.subarray(0,size));return hash.digest('hex');}finally{fs.closeSync(fd);}}
async function digestAsync(file){const hash=crypto.createHash('sha256');for await(const chunk of fs.createReadStream(file))hash.update(chunk);return hash.digest('hex');}

// Fixed helper; the only dynamic input is a base64-encoded JSON plan. No shell text
// is interpolated from paths, release metadata, or configuration values.
const RESTORE_SCRIPT=String.raw`
$ErrorActionPreference='Stop'
function Normalize([string]$value){ return [IO.Path]::GetFullPath($value).TrimEnd([IO.Path]::DirectorySeparatorChar) }
function EqualPath([string]$a,[string]$b){ return [string]::Equals((Normalize $a),(Normalize $b),[StringComparison]::OrdinalIgnoreCase) }
function Under([string]$root,[string]$value){ return (Normalize $value).StartsWith((Normalize $root)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) }
function PlainTree([string]$root){
 $item=Get-Item -LiteralPath $root -Force
 if(-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Invalid restore directory'}
 $parent=$item.Parent
 while($null -ne $parent){if($parent.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked restore parent'};$parent=$parent.Parent}
 $pending=New-Object 'System.Collections.Generic.Stack[string]';$pending.Push($root)
 $files=New-Object 'System.Collections.Generic.List[string]'
 while($pending.Count){foreach($entry in Get-ChildItem -LiteralPath ($pending.Pop()) -Force){
  if(-not (Under $root $entry.FullName) -or ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Linked or escaped restore file'}
  if($entry.PSIsContainer){$pending.Push($entry.FullName)}else{$files.Add($entry.FullName)}
 }}
 return $files.ToArray()
}
function VerifyHashes($manifest,[string]$source,$expected){
 foreach($relative in $expected){
  $file=Join-Path $source $relative
  if(-not (Under $source $file)){throw 'Escaped backup file'}
  $property=$manifest.hashes.PSObject.Properties[$relative]
  if($null -eq $property -or $property.Value -notmatch '^[a-f0-9]{64}$'){throw 'Missing backup integrity hash'}
  $actual=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
  if(-not [string]::Equals($actual,$property.Value,[StringComparison]::OrdinalIgnoreCase)){throw 'Backup integrity hash mismatch'}
 }
}
try{
 $install=Normalize $plan.installDir;$backup=Normalize $plan.backupDir;$data=Normalize $plan.userData
 if(-not (Under (Join-Path $data 'update-backups') $backup) -or -not (EqualPath $backup (Join-Path (Join-Path $data 'update-backups') $plan.backupVersion))){throw 'Unexpected backup path'}
 if((Under $install $data) -or (Under $data $install) -or (EqualPath $install $data)){throw 'Overlapping restore paths'}
 $source=Join-Path $backup 'files'
 $sourceFiles=@(PlainTree $source);$null=PlainTree $install
 $manifest=Get-Content -LiteralPath (Join-Path $backup 'manifest.json') -Raw | ConvertFrom-Json
 if(-not (EqualPath $manifest.installDir $install) -or $manifest.version -ne $plan.backupVersion -or $manifest.executable -ne 'Nexo.exe'){throw 'Backup origin mismatch'}
 $expected=@($manifest.files | Sort-Object)
 $actual=@($sourceFiles | ForEach-Object {$_.Substring($source.Length+1)} | Sort-Object)
 if(@(Compare-Object $expected $actual).Length){throw 'Backup file list mismatch'}
 VerifyHashes $manifest $source $expected
 $running=Get-Process -Id $plan.pid -ErrorAction SilentlyContinue
 if($null -ne $running -and -not $running.WaitForExit(120000)){throw 'Nexo is still running; restore cancelled'}
 $sourceFiles=@(PlainTree $source);$targetFiles=@(PlainTree $install)
 $actual=@($sourceFiles | ForEach-Object {$_.Substring($source.Length+1)} | Sort-Object)
 if(@(Compare-Object $expected $actual).Length){throw 'Backup changed while waiting'}
 VerifyHashes $manifest $source $expected
 foreach($relative in $expected){$src=Join-Path $source $relative;$dst=Join-Path $install $relative;if(-not (Under $source $src) -or -not (Under $install $dst)){throw 'Escaped restore file'};[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($dst)) | Out-Null;Copy-Item -LiteralPath $src -Destination $dst -Force}
 foreach($file in $targetFiles){$relative=$file.Substring($install.Length+1);if($expected -notcontains $relative){Remove-Item -LiteralPath $file -Force}}
 $exe=Join-Path $install 'Nexo.exe';if(-not (Test-Path -LiteralPath $exe -PathType Leaf)){throw 'Restored executable missing'}
 Start-Process -FilePath $exe -WorkingDirectory $install -WindowStyle Hidden
}catch{
 $_.Exception.Message | Set-Content -LiteralPath (Join-Path $plan.userData 'updates\rollback-error.txt')
 exit 1
}
`;

class UpdateService{
 constructor({updater,enabled=false,userData,installDir,version,onState=()=>{},prepareInstall=async()=>{},quit=null,spawn=spawnProcess}){
  this.updater=updater;this.enabled=!!enabled;this.userData=path.resolve(userData);this.installDir=path.resolve(installDir);this.version=version;
  this.onState=onState;this.prepareInstall=prepareInstall;this.quit=quit;this.spawn=spawn;this.disposed=false;this.listeners=[];this._work=Promise.resolve();this._check=null;this._download=null;this._transition=null;this.acceptedVersion='';this.downloadVersion='';this.saved={};
  this.dataDir=path.join(this.userData,'updates');this.backupRoot=path.join(this.userData,'update-backups');this.stateFile=path.join(this.dataDir,'state.json');
  this.view={phase:this.enabled?'idle':'disabled',version:'',currentVersion:version,progress:0,message:this.enabled?'Updates have not been checked.':'Updates are available in the installed edition.',enabled:this.enabled,canRollback:false,backupVersion:''};
  if(!this.enabled)return;
  try{this._roots();if(fs.existsSync(this.stateFile)){if(fs.lstatSync(this.stateFile).isSymbolicLink())throw Error('Linked update state is not allowed.');const stored=JSON.parse(fs.readFileSync(this.stateFile,'utf8'));if(stored&&same(stored.installDir||'',this.installDir))this.saved=stored;}this._refreshBackup();}catch(e){this.view.phase='error';this.view.message=e.message;}
  updater.autoDownload=false;updater.autoInstallOnAppQuit=false;
  this._listen('checking-for-update',()=>{if(this.view.phase!=='ready')this._set({phase:'checking',message:'Checking for updates…'});});
  this._listen('update-not-available',()=>{if(this.view.phase!=='ready')this._set({phase:'current',message:'You have the latest version.'});});
  this._listen('update-available',info=>this._available(info));
  this._listen('download-progress',info=>{if(this.acceptedVersion&&this.view.phase==='downloading')this._set({progress:Number.isFinite(info?.percent)?Math.max(0,Math.min(100,info.percent)):0});});
  this._listen('update-downloaded',info=>{
   const target=info?.version;if(!versionName(target)||target===this.saved.ignoredVersion||target!==this.acceptedVersion||['backing-up','ready','installing','restoring'].includes(this.view.phase))return;
   this._set({phase:'backing-up',progress:100,message:'Saving the installed version before update…'});
   this._work=this._work.then(async()=>{await this._backup();if(!this.disposed)this._set({phase:'ready',version:target,progress:100,message:'Update downloaded. Ready to install.'});}).catch(e=>{this.downloadVersion='';if(!this.disposed)this._set({phase:'error',message:'Update backup failed: '+e.message});});
  });
  this._listen('error',error=>this._set({phase:this.view.phase==='ready'?'ready':'error',message:'Update failed: '+String(error?.message||'Unknown error').slice(0,250)}));
 }
 _listen(name,handler){this.updater.on(name,handler);this.listeners.push([name,handler]);}
 _set(patch){if(this.disposed)return;this.view={...this.view,...patch};try{this.onState(this.status());}catch{}}
 status(){return{...this.view};}
 _roots(){
  if(!versionName(this.version))throw Error('Invalid installed version.');
  plainDirectory(this.installDir);
  if(same(this.installDir,this.userData)||inside(this.installDir,this.userData)||inside(this.userData,this.installDir))throw Error('Update backup must be outside the installation.');
  fs.mkdirSync(this.userData,{recursive:true});plainDirectory(this.userData);
  for(const folder of [this.dataDir,this.backupRoot]){fs.mkdirSync(folder,{recursive:true});plainDirectory(folder);}
 }
 _persist(){this._roots();atomicJson(this.stateFile,{...this.saved,installDir:this.installDir});}
 _backupPath(version){if(!versionName(version))throw Error('Invalid backup version.');return path.join(this.backupRoot,version);}
 _validatedBackup(version){
  this._roots();const directory=this._backupPath(version);plainDirectory(directory);
  const manifestFile=path.join(directory,'manifest.json');if(fs.lstatSync(manifestFile).isSymbolicLink())throw Error('Linked backup manifest.');
  const manifest=JSON.parse(fs.readFileSync(manifestFile,'utf8'));
  if(manifest.version!==version||!same(manifest.installDir||'',this.installDir)||manifest.executable!=='Nexo.exe'||!Array.isArray(manifest.files)||!manifest.hashes||typeof manifest.hashes!=='object')throw Error('Backup origin or integrity metadata mismatch.');
  const source=path.join(directory,'files'),tree=walk(source);
  if(JSON.stringify([...manifest.files].sort())!==JSON.stringify(tree.files)||!tree.files.includes('Nexo.exe'))throw Error('Backup file list mismatch.');
  for(const relative of tree.files)if(!/^[a-f0-9]{64}$/.test(manifest.hashes[relative]||'')||digest(path.join(source,relative))!==manifest.hashes[relative])throw Error('Backup integrity hash mismatch.');
  return directory;
 }
 _refreshBackup(){
  let valid=false;try{if(this.saved.backupVersion){this._validatedBackup(this.saved.backupVersion);valid=true;}}catch{}
  this.view={...this.view,canRollback:valid,backupVersion:valid?this.saved.backupVersion:''};
 }
 async _backup(){
  this._roots();const destination=this._backupPath(this.version);
  if(fs.existsSync(destination))this._validatedBackup(this.version);
  else{
   const tree=walk(this.installDir);if(!tree.files.includes('Nexo.exe'))throw Error('Installed executable missing.');
   const staging=path.join(this.backupRoot,'.pending-'+crypto.randomUUID());fs.mkdirSync(staging);const files=path.join(staging,'files'),hashes={};fs.mkdirSync(files);
   try{
    for(const dir of tree.dirs)await fs.promises.mkdir(path.join(files,dir),{recursive:true});
    for(const relative of tree.files){
     const src=path.join(this.installDir,relative),dst=path.join(files,relative);if(fs.lstatSync(src).isSymbolicLink()||!same(fs.realpathSync(src),src))throw Error('Installation changed during backup.');
     const before=await digestAsync(src);await fs.promises.copyFile(src,dst,fs.constants.COPYFILE_EXCL);
     if(before!==await digestAsync(src)||before!==await digestAsync(dst))throw Error('Installation changed during backup; retry after asset import finishes.');hashes[relative]=before;
    }
    if(JSON.stringify(walk(this.installDir).files)!==JSON.stringify(tree.files))throw Error('Installation file list changed during backup.');
    for(const relative of tree.files)if(await digestAsync(path.join(this.installDir,relative))!==hashes[relative])throw Error('Installation changed before backup completed.');
    atomicJson(path.join(staging,'manifest.json'),{version:this.version,installDir:this.installDir,executable:'Nexo.exe',createdAt:new Date().toISOString(),files:tree.files,hashes});
    walk(files);fs.renameSync(staging,destination);
   }catch(e){if(inside(this.backupRoot,staging)&&!fs.lstatSync(staging).isSymbolicLink())await fs.promises.rm(staging,{recursive:true,force:true});throw e;}
  }
  this.saved.backupVersion=this.version;this._persist();this._refreshBackup();
 }
 _available(info){
  if(this.disposed||!versionName(info?.version))return;
  if(info.version===this.saved.ignoredVersion){this._set({phase:'ignored',version:info.version,message:'This version was restored. Waiting for a newer update.'});return;}
  if(['backing-up','ready','installing','restoring'].includes(this.view.phase))return;
  if(this.view.phase==='downloading')return;
  this.acceptedVersion=info.version;this._set({phase:'available',version:info.version,message:'A new version is available.'});void this.download().catch(()=>{});
 }
 async check(){
  if(!this.enabled||this.disposed)return this.status();
  if(this._check)return this._check;
  if(['downloading','backing-up','installing','restoring'].includes(this.view.phase))return this.status();
  let request;try{request=Promise.resolve(this.updater.checkForUpdates());}catch(e){request=Promise.reject(e);}
  this._check=request.catch(e=>{this._set({phase:this.view.phase==='ready'?'ready':'error',message:'Could not check updates: '+e.message});throw e;}).finally(()=>{this._check=null;});return this._check;
 }
 async download(){
  if(!this.enabled||this.disposed)throw Error('Updates are disabled.');
  if(!this.acceptedVersion||this.acceptedVersion===this.saved.ignoredVersion)throw Error('No accepted update.');
  if(this.downloadVersion===this.acceptedVersion)return this._download;
  this.downloadVersion=this.acceptedVersion;this._set({phase:'downloading',progress:0,message:'Downloading update…'});
  this._download=Promise.resolve().then(()=>{if(this.disposed)throw Error('Updates disposed.');return this.updater.downloadUpdate();}).catch(e=>{this.downloadVersion='';this._set({phase:'error',message:'Download failed: '+e.message});throw e;});return this._download;
 }
 whenIdle(){return this._work;}
 async install(){
  if(!this.enabled||this.disposed||this.view.phase!=='ready'||!this.view.canRollback||this._transition)throw Error('A downloaded update and complete backup are required.');
  this._validatedBackup(this.version);this._transition='install';this._set({phase:'installing',message:'Closing Nexo to install the update…'});
  try{await this.prepareInstall();this.updater.quitAndInstall(false,true);}catch(e){this._set({phase:'ready',message:'Installation could not start: '+e.message});throw e;}finally{this._transition=null;}
 }
 async rollback(){
  if(!this.enabled||this.disposed||!this.view.canRollback||this._transition||['downloading','backing-up','installing','restoring'].includes(this.view.phase))throw Error('No complete backup is available, or another update is running.');
  const backupVersion=this.saved.backupVersion,backupDir=this._validatedBackup(backupVersion);walk(this.installDir);
  if(typeof this.quit!=='function')throw Error('Restore requires an application exit callback.');
  this._transition='rollback';const previousIgnored=this.saved.ignoredVersion||'';this._set({phase:'restoring',message:'Closing Nexo to restore version '+backupVersion+'…'});
  try{
   const plan={pid:process.pid,installDir:this.installDir,userData:this.userData,backupDir,backupVersion};
   const script="$plan=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('"+Buffer.from(JSON.stringify(plan)).toString('base64')+"')) | ConvertFrom-Json\n"+RESTORE_SCRIPT;
   const scriptPath=path.join(this.dataDir,'rollback.ps1');atomicText(scriptPath,script);
   this.saved.ignoredVersion=this.version;this._persist();await this.prepareInstall();
   const launcherPath=path.join(this.dataDir,'launch-rollback.ps1');
   const launcher="$ErrorActionPreference='Stop'\n$script=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('"+Buffer.from(scriptPath).toString('base64')+"'))\nStart-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -WindowStyle Hidden -ArgumentList ('-NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -File '+[char]34+$script+[char]34)\n";
   atomicText(launcherPath,launcher);
   const child=this.spawn('powershell.exe',['-NoProfile','-NonInteractive','-ExecutionPolicy','RemoteSigned','-File',launcherPath],{detached:false,windowsHide:true,shell:false,stdio:'ignore',env:powershellEnv()});
   await new Promise((resolve,reject)=>{child.once('error',reject);child.once('exit',code=>code===0?resolve():reject(Error('Restore launcher failed with exit '+code)));});this.quit();
  }catch(e){this.saved.ignoredVersion=previousIgnored;try{this._persist();}catch{}this._set({phase:'error',message:'Restore could not start: '+e.message});throw e;}finally{this._transition=null;}
 }
 dispose(){if(this.disposed)return;this.disposed=true;for(const [name,fn]of this.listeners)this.updater.removeListener(name,fn);this.listeners=[];}
}
module.exports={UpdateService};
