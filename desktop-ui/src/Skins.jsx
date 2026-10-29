import React,{useEffect,useState} from 'react';
import data from '../skin-catalog.json';
import {weapons as weaponNames} from './catalog.mjs';
import {chooseSkin} from './skin-selection.mjs';
import {Icon} from './icons.jsx';
import './skins.css';
const labels=new Map(weaponNames.filter(([id])=>id.startsWith('arma:')).map(([id,label])=>[Number(id.slice(5)),label]));
const weaponLabel=w=>labels.get(w.id)|| (w.id===31?'Zeus x27':w.label);
export function Skins({category='weapons',settings,state,change,notify}){
 const [weapon,setWeapon]=useState(7),[query,setQuery]=useState(''),[paint,setPaint]=useState(0);
 const selected=data.weapons.find(w=>w.id===weapon)||data.weapons[0],saved=settings.SkinLoadout?.weapons?.[selected.id]||0;
 useEffect(()=>{setPaint(saved);setQuery('');},[selected.id,saved]);
 if(category!=='weapons'){const rows=category==='knives'?data.knives:data.gloves;return <div className="skins-panel"><div className="runtime-status"><Icon name="info"/><span>Model changes are not available yet.<small>Catalog only · these models cannot be applied in this build.</small></span></div><div className="skin-models">{rows.map(row=><div key={row.id}><span>{row.label===row.name?row.name.replace(/^weapon_/,'').replaceAll('_',' '):row.label}</span><small>#{row.id}</small></div>)}</div></div>;}
 const paints=selected.paints.filter(p=>(p.label+' '+p.name+' '+p.id).toLowerCase().includes(query.trim().toLowerCase())).sort((a,b)=>(a.id===selected.featuredPaintId?-1:b.id===selected.featuredPaintId?1:a.label.localeCompare(b.label)));
 const selectedPaint=selected.paints.find(p=>p.id===paint);
 return <div className="skins-panel" data-setting="SkinLoadout"><div className="runtime-status" role="status"><Icon name="info"/><span>Loadout library<small>{state.skinStatus||'Selections save in your CFG. Applying them in CS2 is not available in this build.'}</small></span></div><div className="skin-fields"><label>Weapon<select aria-label="Skin weapon" value={selected.id} onChange={e=>setWeapon(Number(e.target.value))}>{data.weapons.map(w=><option key={w.id} value={w.id}>{weaponLabel(w)}{settings.SkinLoadout?.weapons?.[w.id]?' · Saved':''}</option>)}</select></label><label>Find a finish<input aria-label="Find skin finish" placeholder="Name or paint ID" value={query} onChange={e=>setQuery(e.target.value)} maxLength={80}/></label></div><div className="skin-finishes" role="group" aria-label="Skin finishes"><button aria-pressed={paint===0} onClick={()=>setPaint(0)}><span>Original</span><small>No override</small></button>{paints.map(p=><button key={p.id} aria-pressed={paint===p.id} onClick={()=>setPaint(p.id)}><span>{p.label}{p.id===selected.featuredPaintId&&<i>Featured</i>}</span><small>#{p.id}</small></button>)}{!paints.length&&<p>No matching finishes.</p>}</div><div className="skin-selection"><span>{selectedPaint?.label||'Original'}<small>{Object.keys(settings.SkinLoadout?.weapons||{}).length} weapon selections in this CFG</small></span><button className="primary" disabled={paint===saved} onClick={()=>{change({SkinLoadout:chooseSkin(settings.SkinLoadout,selected.id,paint)});notify?.('Selection queued for CFG save. Game appearance was not changed.');}}>Save selection</button></div></div>;
}
