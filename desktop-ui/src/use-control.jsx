import {useEffect,useRef,useState} from 'react';
import defaults from '../defaults.json';
export const api=window.nexo||{initial:async()=>({settings:defaults.settings,state:{connected:false,spots:[]},userProfile:{name:"Preview",showWelcome:false},preview:true}),patch:async p=>({...defaults.settings,...p}),subscribe:()=>()=>{},external:async()=>({preview:true}),command:async()=>{throw Error('Engine offline');},window:()=>{}};
export function useControl(){
 const [settings,setSettings]=useState(defaults.settings),[state,setState]=useState({connected:false,spots:[]}),[preview,setPreview]=useState(false),[save,setSave]=useState('saved'),[error,setError]=useState('');
 const current=useRef(settings),pending=useRef({}),failed=useRef({}),running=useRef(false),timer=useRef();current.current=settings;
 const flush=async()=>{if(running.current){timer.current=setTimeout(flush,80);return;}const patch={...failed.current,...pending.current};pending.current={};failed.current={};if(!Object.keys(patch).length)return;running.current=true;try{const result=await api.patch(patch);if(!Object.keys(pending.current).length)setSettings(result);setError('');setSave(Object.keys(pending.current).length?'saving':'saved');}catch(e){failed.current={...patch,...failed.current};setSave('error');setError(e.message.replace(/^Error invoking.*?: Error: /,''));}finally{running.current=false;if(Object.keys(pending.current).length)timer.current=setTimeout(flush,80);}};
 function change(patch){current.current={...current.current,...patch};setSettings(current.current);pending.current={...pending.current,...patch};setSave('saving');clearTimeout(timer.current);timer.current=setTimeout(flush,120);}
 useEffect(()=>{api.initial().then(data=>{setSettings(data.settings);setState(data.state);setPreview(data.preview);}).catch(e=>setError(e.message));const off=api.subscribe(data=>{setState(data);setPreview(data.preview);if(data.settings&&!running.current&&!Object.keys(pending.current).length&&!Object.keys(failed.current).length)setSettings(old=>JSON.stringify(old)===JSON.stringify(data.settings)?old:data.settings);if(data.settingsSaved===false&&!running.current){setSave('error');setError(data.saveError||'Could not save settings');}});return()=>{off();clearTimeout(timer.current);};},[]);
 return {settings,state,preview,change,save,error,retry:flush};
}

