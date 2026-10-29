const fs=require('node:fs'),path=require('node:path');
async function check(win,root,current){
 win.show();win.focus();
 const js=s=>win.webContents.executeJavaScript(s),pause=ms=>new Promise(r=>setTimeout(r,ms)),before=current();let saved;
 try{
  const status=await js("window.nexo.cloud('status')");if(!status.available||status.linked)throw Error('Expected fresh secure OS-backed vault');
  await js("window.nexo.cloud('create')");const empty=await js("window.nexo.cloud('list')");if(empty.length)throw Error('Fresh vault is not empty');
  const code=await js("window.nexo.share('export','Private sync QA')");saved=await js("window.nexo.cloud('upload',"+JSON.stringify({code,expectedRevision:null})+")");if(saved.revision!==0)throw Error('Unexpected first revision');
  const item=await js("window.nexo.cloud('get',"+JSON.stringify(saved.id)+")");if(item.code!==code||item.revision!==0)throw Error('Downloaded CFG differs');
  const conflict=await js("window.nexo.cloud('upload',"+JSON.stringify({code,expectedRevision:null})+").then(()=>false,()=>true)");if(!conflict)throw Error('Stale write accepted');
  await js("[...document.querySelectorAll('nav button')].find(x=>x.textContent==='Configs').click()");await pause(50);await js("[...document.querySelectorAll('.config-sections button')].find(x=>x.textContent==='Private sync').click()");await js("new Promise((resolve,reject)=>{const end=Date.now()+10000;function probe(){if(document.querySelector('.private-sync')?.textContent.includes('Private sync QA'))resolve();else if(Date.now()>end)reject(Error('Cloud list timeout'));else setTimeout(probe,50);}probe();})");
  await js("new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)))");if(!await js("document.querySelector('.private-sync')?.textContent.includes('Private sync QA')"))throw Error('Cloud list not rendered');
  const overflow=await js("({body:document.querySelector('.settings-body').scrollHeight>document.querySelector('.settings-body').clientHeight+2,doc:document.documentElement.scrollWidth>innerWidth})");if(overflow.body||overflow.doc)throw Error('Cloud panel overflow '+JSON.stringify(overflow));
  await js("document.getAnimations().forEach(a=>a.finish())");await pause(180);
  fs.mkdirSync(path.join(root,'work'),{recursive:true});fs.writeFileSync(path.join(root,'work','private-sync-current.png'),(await win.webContents.capturePage()).toPNG());
  await js("window.nexo.cloud('remove',"+JSON.stringify({id:saved.id,expectedRevision:saved.revision})+")");saved=null;if((await js("window.nexo.cloud('list')")).length)throw Error('Deleted CFG remains visible');
  if(current()!==before)throw Error('Cloud operations changed local settings');console.log('Private sync: secure OS storage, real API upload/list/get/conflict/delete and bounded panel passed; local settings unchanged; no engine started.');
 }finally{if(saved)await js("window.nexo.cloud('remove',"+JSON.stringify({id:saved.id,expectedRevision:saved.revision})+")").catch(()=>{});await js("window.nexo.cloud('disconnect')").catch(()=>{});}
}
module.exports={check};
