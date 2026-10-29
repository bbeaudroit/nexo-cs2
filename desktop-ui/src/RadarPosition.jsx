import React,{useRef} from 'react';
import {radarGeometry,dragRadar} from './radar-position.mjs';
import './radar-position.css';
export function RadarPosition({settings:s,state,change}){
 const drag=useRef(null),screen=useRef(null);
 const g=radarGeometry(state.gameBounds?.width,state.gameBounds?.height,s.RadarSize,s.RadarHudPosition?24:s.RadarX,s.RadarHudPosition?70:s.RadarY);
 function move(e){if(!drag.current)return;const d=drag.current;change(dragRadar(d.g,e.clientX-d.x,e.clientY-d.y,d.width));}
 return <div className="radar-editor"><p>Drag the radar on this screen preview. Coordinates above stay in sync.</p><div ref={screen} className="radar-screen" style={{aspectRatio:`${g.width}/${g.height}`}}><span className="radar-resolution">{g.width} × {g.height}{!state.gameBounds?' · preview':''}</span><button aria-label="Drag radar position" title="Drag radar position" className="radar-handle" style={{left:`${g.x/g.width*100}%`,top:`${g.y/g.height*100}%`,width:`${g.size/g.width*100}%`,height:`${(g.size+34)/g.height*100}%`}} onPointerDown={e=>{if(e.button!==0)return;e.preventDefault();e.currentTarget.setPointerCapture(e.pointerId);drag.current={g,x:e.clientX,y:e.clientY,width:screen.current.getBoundingClientRect().width};}} onPointerMove={move} onPointerUp={e=>{move(e);drag.current=null;}} onPointerCancel={()=>{drag.current=null;}} onLostPointerCapture={()=>{drag.current=null;}} onKeyDown={e=>{const delta={ArrowLeft:[-1,0],ArrowRight:[1,0],ArrowUp:[0,-1],ArrowDown:[0,1]}[e.key];if(!delta)return;e.preventDefault();change(dragRadar(g,delta[0]*(e.shiftKey?10:1),delta[1]*(e.shiftKey?10:1),g.width));}}><span aria-hidden="true">✥</span></button></div><small>Arrow keys: 1 px · Shift: 10 px. Dragging disables Align with HUD.</small></div>;
}
