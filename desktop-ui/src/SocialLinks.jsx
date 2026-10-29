import React,{useEffect,useRef,useState} from 'react';
import {api} from './use-control.jsx';
import {Brand} from './controls.jsx';
import {Icon} from './icons.jsx';
import discord from '../assets/discord.svg';
import links from '../social-links.json';
import './social-links.css';
export function SocialLinks({notify}){
 const [target,setTarget]=useState(null),[busy,setBusy]=useState(false);const dialog=useRef(null);
 const label=target==='discord'?'Discord':'the Nexo website';
 useEffect(()=>{const node=dialog.current;if(target&&!node.open){node.showModal();node.querySelector('[data-cancel]')?.focus();}else if(!target&&node.open)node.close();},[target]);
 async function open(){if(busy||!target)return;setBusy(true);try{const result=await api.external('open',target);setTarget(null);if(result.preview)notify('Preview: external link was not opened.');}catch(e){notify(e.message,true);}finally{setBusy(false);}}
 return <><div className="social-links" aria-label="Nexo links"><button aria-label="Open Discord" title="Discord" onClick={()=>setTarget('discord')}><img src={discord} alt="" width="19" height="19"/></button><button aria-label="Open Nexo website" title="Nexo website" onClick={()=>setTarget('website')}><Brand size={21}/></button></div><dialog ref={dialog} className="social-dialog" aria-labelledby="social-dialog-title" onCancel={e=>{e.preventDefault();if(!busy)setTarget(null);}} onClose={()=>setTarget(null)}><button className="social-close" aria-label="Close redirect confirmation" disabled={busy} onClick={()=>setTarget(null)}><Icon name="close" size={15}/></button><div className="social-dialog-icon">{target==='discord'?<img src={discord} alt="" width="28" height="28"/>:<Brand size={30}/>}</div><h2 id="social-dialog-title">Open {label}?</h2><p>You will leave Nexo and open {label} in your browser.</p><span className="social-host">{target?new URL(links[target]).hostname:''}</span><div className="social-dialog-actions"><button data-cancel disabled={busy} onClick={()=>setTarget(null)}>Cancel</button><button className="primary" disabled={busy} onClick={open}>{busy?'Opening…':'Continue'}<Icon name="arrow" size={14}/></button></div></dialog></>;
}
