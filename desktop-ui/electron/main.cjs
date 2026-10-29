const {app,BrowserWindow,ipcMain,globalShortcut,Tray,Menu,nativeImage,protocol,net,shell,safeStorage,screen}=require('electron');
const {MenuSync,accelerator,menuBounds,globalMenuKey}=require('./menu-state.cjs');
const {LicenseClient}=require('./license-client.cjs');const channel=require('../build-channel.json');let licensing;
const {pathToFileURL}=require('node:url');const {resolveAsset}=require('./local-assets.cjs');
protocol.registerSchemesAsPrivileged([{scheme:'nexo-asset',privileges:{standard:true,secure:true,supportFetchAPI:true,corsEnabled:true}}]);
const fs=require('node:fs');const path=require('node:path');const {spawn,execFile}=require('node:child_process');const readline=require('node:readline');
const {promisify}=require('node:util');
const {powershellEnv}=require('./powershell-env.cjs');
const {defaults,patchSettings}=require('./settings.cjs');
const {displayName,encodeConfig,decodeConfig,canonicalConfigName,mergeEngineState}=require('./profile-share.cjs');
const {resolveExternal,externalInfo}=require('./external-links.cjs');
const {isInstalledRoot,initializeRuntime}=require('./runtime-paths.cjs');
const {UpdateService}=require('./update-service.cjs');
const {PrivateSync}=require('./private-sync.cjs');let privateSync;
const {cs2Running,waitForCs2}=require('./game-startup.cjs');let gameRunning=false,gameProbeBusy=false;
function startupCheckMode(){return preview||smokeTest||commercialCheck||menuCheck;}
let dataRoot,updates,installed=false;
let profileName='',profileEntered=false;
const welcomeCheck=process.argv.includes('--welcome-check'),cloudCheck=process.argv.includes('--private-sync-check');
let exitCode=0;
const uiAudit=process.argv.includes('--ui-audit'),preview=process.argv.includes('--preview')||uiAudit||welcomeCheck||cloudCheck,smokeTest=process.argv.includes('--smoke-test');let root,win,tray,engine,bridge,quitting=false,settings,queue=Promise.resolve(),pollBusy=false,engineError='',settingsVersion=-1,lastProtection=false,state={connected:false,players:0,fps:0,readMs:0,webLink:'',webStatus:'Not sharing',spots:[]};
let menuSync=new MenuSync(true),visibilityQueue=Promise.resolve(),registeredKey=null,hotkeyManaged=false,shortcutContext=null,applyingMenu=false;
const commercialCheck=process.argv.includes('--commercial-check');
const menuCheck=process.argv.includes('--menu-check');
if(isInstalledRoot(path.dirname(app.getPath('exe')))){const publicData=path.join(app.getPath('appData'),'Nexo');fs.mkdirSync(publicData,{recursive:true});app.setPath('userData',publicData);}
if(uiAudit||smokeTest||commercialCheck||menuCheck||welcomeCheck||cloudCheck)app.setPath('userData',path.join(app.getPath('temp'),'nexo-check-'+process.pid));
function findRoot(){for(const start of [app.getAppPath(),path.dirname(app.getPath('exe'))]){let p=start;for(let i=0;i<9;i++){if((isInstalledRoot(p)||fs.existsSync(path.join(p,'esp-settings.json')))&&fs.existsSync(path.join(p,'dumps')))return p;const parent=path.dirname(p);if(parent===p)break;p=parent;}}throw Error('Nexo folder not found.');}
function loadSettings(){try{return patchSettings(structuredClone(defaults.settings),JSON.parse(fs.readFileSync(path.join(dataRoot,'esp-settings.json'),'utf8').replace(/^\uFEFF/,'')));}catch{return structuredClone(defaults.settings);}}
function configNames(){try{return fs.readdirSync(path.join(dataRoot,'configs')).filter(n=>/^[\p{L}\p{N}_ -]+\.json$/u.test(n)).map(n=>n.slice(0,-5)).sort();}catch{return[];}}
let configs=[],configAuthors={};
function applyMenuProtection(){
 const wanted=!!settings.ExcludeFromCapture;
 try{if(lastProtection!==wanted){win.setContentProtection(wanted);lastProtection=wanted;}return !wanted||!settings.HideOnCaptureFailure||win.isContentProtected();}
 catch{lastProtection=undefined;engineError='Menu capture protection unavailable';return !wanted||!settings.HideOnCaptureFailure;}
}
function broadcast(){if(win&&!win.isDestroyed()){fallbackShortcut();if(!applyMenuProtection()&&win.isVisible())hide();win.webContents.send('nexo:state',{...state,updates:updates?.status(),menuCaptureProtected:win.isContentProtected(),configs,configAuthors,error:engineError,settings,preview,license:licensing?.status()});}}
async function request(route,method='GET',body){if(!bridge)throw Error('The engine is offline.');const response=await fetch(`http://127.0.0.1:${bridge.port}${route}`,{method,headers:{'X-Nexo-Token':bridge.token,'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body),signal:AbortSignal.timeout(4000)});if(!response.ok)throw Error('The engine could not apply the change.');return response.json();}
function control(action,argument,connection=bridge,guard=()=>true){
 const task=visibilityQueue.then(()=>{if(!connection||bridge!==connection||!guard())return;return request('/command','POST',{Action:action,Argument:argument});});
 visibilityQueue=task.catch(()=>{});return task;
}
function tellVisibility(){if(!bridge||!win||win.isDestroyed())return Promise.resolve();return control('ui-visible',smokeTest||win.isVisible()&&!win.isMinimized()?'1':'0').catch(()=>{});}
function requestMenu(visible){
 const sync=menuSync,intent=sync.request(visible);
 if(!bridge){sync.ack(intent,0);return;}
 control('ui-request',visible?'1':'0').then(version=>{if(sync===menuSync&&version!==undefined)sync.ack(intent,Number(version));}).catch(e=>{sync.fail(intent);engineError=e.message;broadcast();});
}
function placeMenu(){
 const rect=state.gameBounds,display=rect?screen.getDisplayMatching(screen.screenToDipRect(null,rect)):screen.getDisplayMatching(win.getBounds());
 const bounds=menuBounds(display.workArea,win.getBounds());win.setMinimumSize(1,1);win.setBounds({x:bounds.x,y:bounds.y,width:bounds.width,height:bounds.height});win.webContents.setZoomFactor(bounds.scale);
}
function hide(manual=true){if(!win||win.isDestroyed())return;const returnFocus=win.isFocused();if(manual)requestMenu(false);const sync=menuSync,intent=sync.intent;applyingMenu=true;win.hide();applyingMenu=false;tellVisibility();if(returnFocus)control('ui-focus-game','',bridge,()=>menuSync===sync&&sync.intent===intent&&!sync.visible).catch(()=>{});fallbackShortcut();}
function show(manual=true){if(!win||win.isDestroyed()||(!startupCheckMode()&&!gameRunning))return;if(!applyMenuProtection()){engineError='Menu hidden: capture protection unavailable';return;}if(manual)requestMenu(true);applyingMenu=true;if(win.isMinimized())win.restore();placeMenu();win.setAlwaysOnTop(true,'screen-saver');win.show();win.moveTop();win.focus();win.webContents.focus();applyingMenu=false;tellVisibility();fallbackShortcut();}
function fallbackShortcut(){
 if(!win||win.isDestroyed()||quitting)return;
 const wanted=(win.isFocused()||globalMenuKey(settings.MenuKey)&&(state.gameFocused||!bridge))?accelerator(settings.MenuKey):null;
 if(registeredKey!==wanted){if(registeredKey)globalShortcut.unregister(registeredKey);registeredKey=null;if(wanted&&globalShortcut.register(wanted,()=>{
  if(!bridge){if(win.isFocused()||globalMenuKey(settings.MenuKey)){if(menuSync.visible&&!win.isMinimized())hide();else show();}return;}
  const connection=bridge,sync=menuSync;control('ui-toggle','',connection).then(result=>{if(bridge!==connection||menuSync!==sync||typeof result!=='string')return;const match=/^(\d+):([01])$/.exec(result);if(!match||!sync.receive(match[2]==='1',Number(match[1])))return;if(sync.visible)show(false);else hide(false);}).catch(()=>{});
 }))registeredKey=wanted;}
 const managed=!!registeredKey;
 if(bridge&&(shortcutContext!==bridge||hotkeyManaged!==managed)){shortcutContext=bridge;hotkeyManaged=managed;control('ui-hotkey',managed?'1':'0').catch(()=>{if(shortcutContext===bridge)shortcutContext=null;});}
}
function menuDiagnostics(){
 if(!process.argv.includes('--menu-diagnostics')||!win||win.isDestroyed())return;
 try{fs.writeFileSync(path.join(root,'work','menu-status.json'),JSON.stringify({at:new Date().toISOString(),connected:!!state.connected,gameFocused:state.gameFocused,gameMinimized:state.gameMinimized,gameBounds:state.gameBounds,requested:state.menuRequested,version:state.menuVersion,externalVisible:state.externalVisible,desired:menuSync.visible,pending:menuSync.intent!==menuSync.settled,visible:win.isVisible(),minimized:win.isMinimized(),focused:win.isFocused(),bounds:win.getBounds(),zoom:win.webContents.getZoomFactor(),shortcut:registeredKey},null,2));}catch{}
}
function startEngine(){
 if(preview||engine||quitting||!profileEntered||(!startupCheckMode()&&!gameRunning))return;if(!licensing.allowed()){engineError='Activate a license to start the engine.';broadcast();return;}const file=path.join(root,'engine-v7','Nexo.exe');if(!fs.existsSync(file)){engineError='Engine not found. Check the installation.';broadcast();return;}
 const child=spawn(file,['--ui-bridge',...(menuCheck?['--control-test']:[])],{cwd:root,env:{...process.env,...licensing.engineEnv(),NEXO_DATA_ROOT:dataRoot},windowsHide:true,stdio:['ignore','pipe','pipe']});engine=child;
 const lines=readline.createInterface({input:child.stdout});lines.on('line',async line=>{
  if(engine!==child||quitting)return;let message;try{message=JSON.parse(line);}catch{try{fs.appendFileSync(path.join(dataRoot,'nexo-desktop.log'),new Date().toISOString()+' '+line+'\n');}catch{}return;}
  if(message.type!=='bridge-ready'||!Number.isInteger(message.port)||message.port<1||message.port>65535||typeof message.token!=='string'||!/^[A-F0-9]{64}$/.test(message.token))return;
  bridge={port:message.port,token:message.token};settingsVersion=-1;engineError='';menuSync=new MenuSync(smokeTest||win.isVisible()&&!win.isMinimized());
  const connection=bridge;try{const loaded=await request('/settings');if(engine!==child||bridge!==connection)return;settings=patchSettings(structuredClone(defaults.settings),loaded);const handle=win.getNativeWindowHandle().readBigUInt64LE().toString();await request('/command','POST',{Action:'ui-window',Argument:handle});if(engine!==child||bridge!==connection)return;requestMenu(menuSync.visible);await tellVisibility();fallbackShortcut();broadcast();}catch(e){if(bridge===connection){engineError=e.message;broadcast();}}
 });
 child.stderr.on('data',data=>{engineError=data.toString().trim().slice(0,250);});
 child.on('error',()=>{if(engine===child){engine=undefined;bridge=undefined;engineError='Could not start the engine.';fallbackShortcut();broadcast();}});
 child.on('exit',()=>{lines.close();if(engine!==child)return;engine=undefined;bridge=undefined;state={...state,connected:false,fps:0,players:0};if(!quitting){engineError=engineError||'Open CS2 to connect the engine.';fallbackShortcut();broadcast();}});
}
async function poll(){
 if(pollBusy||!bridge||quitting)return;pollBusy=true;
 const connection=bridge;try{const next=await request('/state');if(bridge!==connection||next.settingsVersion<settingsVersion)return;if(next.settings){settings=next.settings;settingsVersion=next.settingsVersion;}state=mergeEngineState(state,next,profileName);if(win&&!win.isDestroyed()&&menuSync.receive(next.menuRequested,next.menuVersion)){if(menuSync.visible&&(!win.isVisible()||win.isMinimized()))show(false);else if(!menuSync.visible&&win.isVisible())hide(false);}fallbackShortcut();menuDiagnostics();broadcast();}
 catch(e){if(bridge===connection){engineError=e.message;broadcast();}}finally{pollBusy=false;}
}
function trusted(event){if(!win||event.sender!==win.webContents||event.senderFrame!==win.webContents.mainFrame)throw Error('Request not allowed.');}
async function stopEngine(){
 if(bridge){try{await request('/command','POST',{Action:'unload',Argument:''});}catch{}}
 if(engine){const child=engine;await Promise.race([new Promise(resolve=>child.once('exit',resolve)),new Promise(resolve=>setTimeout(resolve,2500))]);if(child.exitCode===null)child.kill();}
 engine=undefined;bridge=undefined;
}
async function shutdown(){
 if(quitting)return;quitting=true;globalShortcut.unregisterAll();updates?.dispose();await stopEngine();
 tray?.destroy();app.exit(exitCode);
}
if(!app.requestSingleInstanceLock()){app.quit();}else{
 app.on('second-instance',(_,argv)=>{if(argv.includes('--quit'))shutdown();else if(win)show();});
 app.whenReady().then(async()=>{
  if(!startupCheckMode()){const waitIcon=path.join(app.getAppPath(),'assets','nexo.png');tray=new Tray(nativeImage.createFromPath(waitIcon));tray.setToolTip('Nexo · Waiting for CS2');tray.setContextMenu(Menu.buildFromTemplate([{label:'Waiting for CS2',enabled:false},{label:'Quit Nexo',click:shutdown}]));if(!await waitForCs2({cancelled:()=>quitting}))return;gameRunning=true;tray.destroy();tray=undefined;}
  if(cloudCheck||uiAudit)console.log('Check: Electron ready');
  try{profileName=displayName(JSON.parse(fs.readFileSync(path.join(app.getPath('userData'),'user-profile.json'),'utf8')).name);}catch{}
  if(uiAudit||smokeTest||commercialCheck||menuCheck||cloudCheck){profileEntered=true;if(!profileName)profileName='Preview';}
  root=findRoot();const runtime=initializeRuntime(root,app.getPath('userData'),defaults.settings);dataRoot=runtime.dataRoot;installed=runtime.installed;settings=loadSettings();configs=configNames();for(const n of configs){try{configAuthors[n]=displayName(JSON.parse(fs.readFileSync(path.join(dataRoot,'configs',n+'.meta.json'),'utf8')).author);}catch{}}let deviceId;if(channel.channel==='commercial'&&!preview){const identity=await promisify(execFile)(path.join(root,'engine-v7','Nexo.exe'),['--installation-id'],{cwd:root,windowsHide:true,timeout:8000});deviceId=identity.stdout.trim();if(!/^[a-f0-9]{64}$/.test(deviceId))throw Error('No se pudo validar la identidad nativa');}licensing=new LicenseClient({config:channel,userData:app.getPath('userData'),safeStorage,deviceId});if(!preview)await licensing.renew();
  protocol.handle('nexo-asset',async request=>{try{if(uiAudit&&process.argv.includes('--missing-model'))return new Response('Test: no model',{status:404});return await net.fetch(pathToFileURL(resolveAsset(root,request.url)).toString());}catch{return new Response('Not found',{status:404});}});
  win=new BrowserWindow({width:1040,height:680,minWidth:1040,minHeight:680,resizable:false,show:false,skipTaskbar:true,frame:false,transparent:true,backgroundColor:'#00000000',title:'Nexo',icon:path.join(app.getAppPath(),'assets','nexo.png'),autoHideMenuBar:true,webPreferences:{preload:path.join(__dirname,'preload.cjs'),contextIsolation:true,nodeIntegration:false,sandbox:true}});
  win.setAlwaysOnTop(true,'screen-saver');win.webContents.setWindowOpenHandler(()=>({action:'deny'}));win.webContents.on('will-navigate',(event,url)=>{if(url!==win.webContents.getURL())event.preventDefault();});win.webContents.session.setPermissionRequestHandler((_,__,callback)=>callback(false));
  applyMenuProtection();
  updates=new UpdateService({updater:installed&&!preview?require('electron-updater').autoUpdater:null,enabled:installed&&app.isPackaged&&!preview,userData:app.getPath('userData'),installDir:path.dirname(app.getPath('exe')),version:app.getVersion(),onState:()=>broadcast(),prepareInstall:async()=>{quitting=true;globalShortcut.unregisterAll();await stopEngine();tray?.destroy();},quit:()=>{updates?.dispose();app.exit(exitCode);}});
  ipcMain.handle('nexo:initial',event=>{trusted(event);return{settings,aimDefaults:defaults.aim,state:{...state,updates:updates.status(),userName:profileName,menuCaptureProtected:win.isContentProtected(),configs,configAuthors},userProfile:{name:profileName,showWelcome:!profileEntered},preview,license:licensing.status()};});
  privateSync=preview&&!cloudCheck?undefined:new PrivateSync({userData:app.getPath('userData'),safeStorage});
  ipcMain.handle('nexo:cloud',async(event,action,payload)=>{trusted(event);if(action==='status')return preview&&!cloudCheck?{available:true,linked:false,preview:true}:privateSync.status();if(preview&&!cloudCheck)throw Error('Private sync is disabled in preview.');switch(action){case 'create':return privateSync.create();case 'link':return privateSync.link(payload);case 'disconnect':return privateSync.disconnect();case 'pairingCode':return privateSync.pairingCode();case 'list':return privateSync.list();case 'get':return privateSync.get(payload);case 'upload':return privateSync.upload(payload);case 'remove':if(!payload||typeof payload!=='object')throw Error('Invalid cloud request.');return privateSync.remove(payload.id,payload.expectedRevision);default:throw Error('Private sync action not allowed.');}});
  ipcMain.handle('nexo:update',async(event,action)=>{trusted(event);try{if(action==='check')await updates.check();else if(action==='install')await updates.install();else if(action==='rollback')await updates.rollback();else throw Error('Update action not allowed.');broadcast();return updates.status();}catch(e){if(quitting){quitting=false;startEngine();}throw e;}});
  ipcMain.handle('nexo:profile',(event,action,value)=>{trusted(event);if(action!=='enter'&&action!=='rename')throw Error('Invalid profile action.');const name=displayName(value);if(!preview||welcomeCheck){const file=path.join(app.getPath('userData'),'user-profile.json');fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file+'.tmp',JSON.stringify({version:1,name}));fs.renameSync(file+'.tmp',file);}profileName=name;profileEntered=true;state={...state,userName:name};broadcast();startEngine();return{name};});
  ipcMain.handle('nexo:share',async(event,action,value)=>{trusted(event);if(action==='export')return encodeConfig({name:displayName(value),author:profileName||'Local user',settings});if(action==='inspect')return decodeConfig(value);if(action!=='apply')throw Error('Invalid sharing action.');const cfg=decodeConfig(value);const task=queue.then(async()=>{if(bridge){const result=await request('/settings','PUT',cfg.settings);settings=result.settings;settingsVersion=result.settingsVersion;if(!result.settingsSaved)throw Error(result.saveError||'Could not save imported config.');}else{if(!preview){const file=path.join(dataRoot,'esp-settings.json');fs.writeFileSync(file+'.ui-tmp',JSON.stringify(cfg.settings,null,2));fs.renameSync(file+'.ui-tmp',file);}settings=cfg.settings;}state={...state,importedConfig:{name:cfg.name,author:cfg.author},settingsSaved:true,saveError:''};broadcast();return cfg;});queue=task.catch(()=>{});return task;});
  ipcMain.handle('nexo:patch',(event,patch)=>{trusted(event);const task=queue.then(async()=>{const value=patchSettings(settings,patch);if(bridge){const connection=bridge;const result=await request('/settings','PUT',value);if(bridge!==connection)throw Error('The engine reconnected. Apply the change again.');settings=result.settings;settingsVersion=result.settingsVersion;state={...state,settingsSaved:result.settingsSaved,saveError:result.saveError};broadcast();if(!result.settingsSaved)throw Error(result.saveError);}else{if(!preview){const file=path.join(dataRoot,'esp-settings.json');fs.writeFileSync(file+'.ui-tmp',JSON.stringify(value,null,2));fs.renameSync(file+'.ui-tmp',file);}settings=value;state={...state,settingsSaved:true,saveError:''};}broadcast();return settings;});queue=task.catch(()=>{});return task;});
  const commands=new Set(['config-save','config-load','grenade-save','grenade-delete','web-local','web-internet','web-copy','web-stop','sound-test','unload']);let modelImportBusy=false;
  ipcMain.handle('nexo:command',async(event,action,argument)=>{trusted(event);if(action==='connect'){startEngine();return'ok';}if(action==='import-model'){if(modelImportBusy)throw Error('Import already in progress');modelImportBusy=true;try{await promisify(execFile)('powershell.exe',['-NoProfile','-NonInteractive','-ExecutionPolicy','RemoteSigned','-File',path.join(root,'Preparar-Modelo.ps1')],{cwd:root,windowsHide:true,timeout:240000,maxBuffer:1048576,env:powershellEnv()});if(engine){await stopEngine();startEngine();}return'ok';}catch{throw Error('Model import failed. Run Preparar-Modelo.ps1 for details.');}finally{modelImportBusy=false;}}if(!commands.has(action)||typeof argument!=='string'||argument.length>1000)throw Error('Action not allowed.');if(action==='unload'){shutdown();return'ok';}if(action==='config-save'||action==='config-load')argument=canonicalConfigName(argument);await request('/command','POST',{Action:action,Argument:argument});if(action==='config-load')settings=await request('/settings');if(action==='config-save'){configs=configNames();if(configs.includes(argument)){configAuthors[argument]=profileName;fs.writeFileSync(path.join(dataRoot,'configs',argument+'.meta.json'),JSON.stringify({version:1,author:profileName}));}}if(action==='config-load')state={...state,importedConfig:{name:argument,author:configAuthors[argument]||'Unknown author'}};broadcast();return'ok';});
  let licenseBusy=false;
  ipcMain.handle('nexo:license',async(event,action,payload)=>{trusted(event);if(action==='vac-info'){await shell.openExternal('https://help.steampowered.com/en/faqs/view/571A-97DA-70E9-FF74');return{};}if(preview)throw Error('Modo diseño: no se realizan pagos ni activaciones.');if(licenseBusy)throw Error('Esperá a que termine la operación anterior.');if(!payload||typeof payload!=='object'||JSON.stringify(payload).length>2048)throw Error('Solicitud inválida');licenseBusy=true;try{let result;if(action==='activate'){result=await licensing.activate(payload.key);startEngine();}else if(action==='checkout')result=await licensing.checkout(payload.coin,payload.acceptedRisk);else if(action==='order'){if(typeof payload.id!=='string')throw Error('Orden inválida');result=await licensing.order(payload.id);}else if(action==='open-payment'){await shell.openExternal(licensing.payment(payload.id));result={};}else throw Error('Acción no permitida');broadcast();return result;}finally{licenseBusy=false;}});
  ipcMain.handle('nexo:window',(event,action)=>{trusted(event);if(action==='hide')hide();else if(action==='minimize'){requestMenu(false);applyingMenu=true;win.minimize();applyingMenu=false;tellVisibility();}else if(action==='quit')shutdown();else throw Error('Action not allowed.');});
  ipcMain.handle('nexo:external',async(event,action,target)=>{trusted(event);if(action==='info')return externalInfo();if(action!=='open')throw Error('Action not allowed.');const url=resolveExternal(target);if(preview)return{preview:true,target};await shell.openExternal(url);return{opened:true,target};});
  win.on('close',event=>{if(!quitting){event.preventDefault();hide();}});win.on('restore',()=>{if(!applyingMenu&&win.isVisible()&&!win.isMinimized())requestMenu(true);tellVisibility();});win.on('show',()=>tellVisibility());win.on('minimize',()=>{requestMenu(false);tellVisibility();});win.on('focus',fallbackShortcut);win.on('blur',fallbackShortcut);
  if(cloudCheck||uiAudit)console.log('Check: loading menu');
  await win.loadFile(path.join(__dirname,'..','dist','index.html'));
  if(cloudCheck||uiAudit)console.log('Check: menu loaded');
  if(cloudCheck){try{await require('./cloud-check.cjs').check(win,root,()=>JSON.stringify(settings));}catch(e){exitCode=1;console.error(e.stack);}finally{await shutdown();}return;}
  if(welcomeCheck){try{show();await new Promise(r=>setTimeout(r,400));if(!await win.webContents.executeJavaScript("!!document.querySelector('[data-welcome]')"))throw Error('Welcome missing');await win.webContents.executeJavaScript("document.getAnimations().forEach(a=>a.finish())");await win.webContents.executeJavaScript("new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))");await new Promise(r=>setTimeout(r,150));const screenshot=await win.webContents.capturePage();fs.writeFileSync(path.join(root,'work','welcome-current.png'),screenshot.toPNG());await win.webContents.executeJavaScript("(()=>{const e=document.querySelector('#display-name');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,'Preview');e.dispatchEvent(new Event('input',{bubbles:true}));})()");await new Promise(r=>setTimeout(r,100));await win.webContents.executeJavaScript("document.querySelector('[data-welcome] form').requestSubmit()");await new Promise(r=>setTimeout(r,400));if(!await win.webContents.executeJavaScript("!!document.querySelector('[data-ui]')"))throw Error('Welcome did not enter menu');const saved=JSON.parse(fs.readFileSync(path.join(app.getPath('userData'),'user-profile.json'),'utf8'));if(saved.name!=='Preview')throw Error('Profile not persisted');console.log('Welcome: 1040x680, username persisted, menu entered; no engine started.');}catch(e){exitCode=1;console.error(e.stack);}finally{await shutdown();}return;}
  if(commercialCheck){try{await new Promise(resolve=>setTimeout(resolve,350));if(channel.channel!=='commercial'||licensing.allowed())throw Error('No es una edición comercial bloqueada');startEngine();if(engine)throw Error('Se inició el motor sin licencia');const initial=await win.webContents.executeJavaScript('window.nexo.initial()');if(initial.license.status!=='inactive')throw Error('Estado de licencia incorrecto');await win.webContents.executeJavaScript("window.nexo.command('connect')");if(engine)throw Error('Conectar eludió la licencia');console.log('Commercial package: unlicensed engine blocked in main and native, account flow reachable.');}catch(e){exitCode=1;console.error(e.message);}finally{await shutdown();}return;}
  if(uiAudit){
   try{if(process.argv.includes('--missing-model'))await require('./ui-audit.cjs').errorAudit(win);else await require('./ui-audit.cjs').audit(win,root);console.log('UI audit passed.');}catch(e){exitCode=1;console.error(e.stack);}finally{await shutdown();}return;
  }
  if(menuCheck){
   try{
    const wait=ms=>new Promise(resolve=>setTimeout(resolve,ms));startEngine();const deadline=Date.now()+9000;
    while(!state.connected&&Date.now()<deadline){await poll();await wait(100);}if(!state.connected)throw Error('No se conectó el motor para probar el menú.');
    show();await visibilityQueue;await wait(150);await poll();if(!win.isVisible()||win.isMinimized())throw Error('El menú no se abrió.');
    for(let i=0;i<5;i++){hide();await wait(180);await poll();if(win.isVisible())throw Error('Una respuesta atrasada reabrió el menú.');show();await wait(180);await poll();if(!win.isVisible())throw Error('Una respuesta atrasada ocultó el menú.');}
    await win.webContents.executeJavaScript("window.nexo.window('minimize')");await wait(200);await poll();if(!win.isMinimized())throw Error('El menú no quedó minimizado.');
    show();await wait(200);await poll();if(win.isMinimized()||!win.isVisible())throw Error('El menú no se restauró.');
    for(let i=0;i<12;i++){hide();show();}await visibilityQueue;await wait(200);await poll();
    if(!win.isVisible()||!state.menuRequested||!state.externalVisible||menuSync.intent!==menuSync.settled)throw Error('Las alternancias rápidas desincronizaron el menú.');
    if(state.gameMinimized)throw Error('CS2 quedó minimizado al abrir el menú.');
    if(settings.ExcludeFromCapture&&(!state.overlayCaptureProtected||!win.isContentProtected()))throw Error('Capture protection did not survive menu transitions.');
    console.log('Menu: 5 ciclos, minimizar/restaurar y 12 alternancias rápidas verificados con el motor real; CS2 no minimizado. Entradas automáticas bloqueadas.');
   }catch(e){exitCode=1;console.error(e.stack);}finally{await shutdown();}return;
  }
  if(smokeTest){
   try{startEngine();const deadline=Date.now()+9000;while(!state.connected&&Date.now()<deadline){await poll();await new Promise(resolve=>setTimeout(resolve,100));}if(!state.connected)throw Error('No se conectó el motor para la prueba.');
    const initial=await win.webContents.executeJavaScript('window.nexo.initial()');if(typeof initial.settings.Enabled!=='boolean'||typeof initial.settings.GlowStrength!=='number')throw Error('El contrato de configuración no coincide.');
    const result=await win.webContents.executeJavaScript(`window.nexo.patch({GlowStrength:${initial.settings.GlowStrength}})`);if(result.GlowStrength!==initial.settings.GlowStrength)throw Error('La configuración no completó el recorrido.');
    const checked=await request('/state');if(!checked.menuRequested||!checked.settingsSaved)throw Error('Input is not blocked or settings were not saved.');if(settings.ExcludeFromCapture&&(!checked.overlayCaptureProtected||!win.isContentProtected()))throw Error('Capture protection check: '+JSON.stringify({menu:win.isContentProtected(),overlay:checked.overlayCaptureProtected,requested:settings.ExcludeFromCapture,affinity:checked.captureAffinity,code:checked.captureError,readMs:checked.readMs,age:checked.ageMs}));
    if(typeof checked.weaponId!=='number'||typeof checked.triggerStatus!=='string')throw Error('Live weapon / trigger state is missing.');
    console.log('Electron IPC verificado: preload aislado, motor conectado, ajuste persistido, menú bloquea entradas.');
   }catch(error){exitCode=1;console.error(error.message);}finally{await shutdown();}return;
  }
  const capture=process.argv.find(a=>a.startsWith('--capture='));
  if(capture){await new Promise(resolve=>setTimeout(resolve,700));const shot=await win.webContents.capturePage();fs.writeFileSync(capture.slice(10),shot.toPNG());await shutdown();return;}
  show();const iconFile=path.join(app.getAppPath(),'assets','nexo.png');if(fs.existsSync(iconFile)){tray=new Tray(nativeImage.createFromPath(iconFile));tray.setToolTip('Nexo');tray.setContextMenu(Menu.buildFromTemplate([{label:'Open Nexo',click:()=>show()},{type:'separator'},{label:'Quit Nexo',click:shutdown}]));tray.on('double-click',()=>show());}
  if(!startupCheckMode())setInterval(async()=>{if(gameProbeBusy||quitting)return;gameProbeBusy=true;try{const running=await cs2Running();if(running===gameRunning)return;gameRunning=running;if(!running){hide(false);await stopEngine();tray?.setToolTip('Nexo · Waiting for CS2');}else{tray?.setToolTip('Nexo');show();startEngine();}}finally{gameProbeBusy=false;}},1500);
  fallbackShortcut();startEngine();setInterval(poll,100);setInterval(()=>{if(!engine)startEngine();},10000);if(!preview)setInterval(async()=>{await licensing.renew();broadcast();},240000);
  if(installed&&!preview){updates.check().catch(()=>{});setInterval(()=>{if(!quitting)updates.check().catch(()=>{});},15*60*1000);}
 }).catch(error=>{console.error(error.message);app.exit(1);});
 app.on('before-quit',event=>{if(!quitting){event.preventDefault();shutdown();}});
 app.on('window-all-closed',()=>{});
}

