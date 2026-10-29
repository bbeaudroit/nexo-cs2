import React,{useState} from 'react';
import {api} from './use-control.jsx';
import './updates.css';

const labels={disabled:'Installed edition',idle:'Updates',checking:'Checking',current:'Up to date',available:'Update available',downloading:'Downloading', 'backing-up':'Saving backup',ready:'Ready to install',installing:'Installing',restoring:'Restoring',ignored:'Version skipped',error:'Needs attention'};
export function Updates({state,action,notify=()=>{}}){
 const updates=state?.updates||state?.update||state||{};
 const [busy,setBusy]=useState(false),[confirm,setConfirm]=useState(false);
 const phase=updates.phase||'disabled',enabled=updates.enabled!==false&&phase!=='disabled';
 const working=busy||['checking','downloading','backing-up','installing','restoring'].includes(phase);
 const progress=Number.isFinite(updates.progress)?Math.max(0,Math.min(100,updates.progress)):0;
 async function run(name){if(busy)return;setBusy(true);try{const execute=action||api.update;if(typeof execute!=='function')throw Error('Updates are not available in this preview.');await execute(name);if(name==='check')notify('Update check completed.');}catch(e){notify(String(e.message||'Update operation failed.').replace(/^Error invoking.*?: Error: /,''),true);}finally{setBusy(false);}}
 return <section className="updates-panel" aria-labelledby="updates-heading">
  <div className="updates-heading"><div><h2 id="updates-heading">Updates</h2><p>Version {updates.currentVersion||'—'}</p></div><span className={'updates-badge'+(phase==='error'?' is-error':'')} role="status">{labels[phase]||'Updates'}</span></div>
  <p className="updates-message" role="status">{updates.message||'Automatic update checks are available in the installed edition.'}</p>
  {updates.version&&['available','downloading','backing-up','ready','installing','ignored'].includes(phase)&&<p className="updates-version">Update: {updates.version}</p>}
  {phase==='downloading'&&<div className="updates-progress"><progress max="100" value={progress} aria-label="Update download progress"/><span>{Math.round(progress)}%</span></div>}
  <div className="button-row updates-actions"><button disabled={!enabled||working} onClick={()=>run('check')}>Check for updates</button><button className="primary" disabled={!enabled||busy||phase!=='ready'} onClick={()=>run('install')}>Install and restart</button></div>
  {enabled&&<p className="updates-help">Updates download automatically. Installing restarts Nexo after closing the engine.</p>}
  <div className="updates-restore"><div><h3>Previous version</h3><p>{updates.canRollback?'Saved backup: '+(updates.backupVersion||'previous installation'):'A backup is saved before installing an update.'}</p></div><button disabled={!enabled||working||!updates.canRollback} onClick={()=>setConfirm(true)}>Restore backup</button></div>
  {confirm&&<div className="updates-confirm" role="alertdialog" aria-modal="false" aria-labelledby="restore-heading" aria-describedby="restore-description"><h3 id="restore-heading">Restore version {updates.backupVersion||'from backup'}?</h3><p id="restore-description">Nexo and the engine will close. Installed program files will be replaced with the saved backup. Your local settings remain in your data folder. The current version will be skipped by automatic updates.</p><div className="button-row"><button disabled={busy} onClick={()=>setConfirm(false)}>Cancel</button><button className="primary" disabled={working||!updates.canRollback} onClick={()=>{setConfirm(false);run('rollback');}}>Restore and restart</button></div></div>}
 </section>;
}
