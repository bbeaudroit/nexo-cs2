export function radarGeometry(width,height,size,x,y){
 width=Number.isFinite(width)&&width>=320?width:1920;height=Number.isFinite(height)&&height>=240?height:1080;
 size=Math.max(100,Math.min(size,width-16,height-50));
 return {width,height,size,x:Math.max(8,Math.min(x,width-size-8)),y:Math.max(8,Math.min(y,height-size-42))};
}
export function dragRadar(start,dx,dy,previewWidth){
 const scale=start.width/previewWidth;
 const next=radarGeometry(start.width,start.height,start.size,start.x+dx*scale,start.y+dy*scale);
 return {RadarX:Math.min(7000,Math.round(next.x)),RadarY:Math.min(4000,Math.round(next.y)),RadarHudPosition:false};
}
