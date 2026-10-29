export function project(map,point){
 if(!map||![map.x,map.y,map.scale,map.width,map.height,point.x,point.y].every(Number.isFinite)||map.scale<=0||map.width<=0||map.height<=0)return null;
 const x=(point.x-map.x)/(map.scale*map.width),y=(map.y-point.y)/(map.scale*map.height);
 return x>=0&&x<=1&&y>=0&&y<=1?{x,y}:null;
}
export function floorDirection(layer,z){return !layer||!Number.isFinite(z)?0:z<layer.min?-1:z>=layer.max?1:0;}
