const {validateSkinLoadout}=require('./skin-loadout.cjs');
const defaults=require('../defaults.json');
const groups=new Set(['Rifles','SMG','Pistolas','Francotiradores','Escopetas','Pesadas','Otras']);
const ranges={ChamsHistoryDelay:[50,500],ChamsHistoryOpacity:[0,60],MovementKey:[0,254],JumpKey:[0,254],ChamsOpacity:[0,100],ChamsOutlineOpacity:[0,100],ChamsOutlineWidth:[1,4],ChamsDistance:[0,500],EspMaxDistance:[0,500],PrivacyKey:[0,254],Frames:[30,240],Thickness:[1,4],FontSize:[7,16],GlowStrength:[0,100],GlowRadius:[2,14],LabelOpacity:[0,100],RadarOpacity:[0,100],RadarSize:[140,320],RadarRange:[500,5000],RadarX:[0,7000],RadarY:[0,4000],SoundTime:[1,10],MenuKey:[7,254],ToggleKey:[7,254],GrenadeKey:[1,254],Sensitivity:[.01,20],GameFov:[60,140],HitVolume:[0,100],HitSoundType:[0,2],GrenadeDistance:[100,10000]};
function scalar(key,value,template,range){
 if(typeof value!==typeof template||value===null)throw Error('Invalid value: '+key);
 if(typeof value==='number'){if(!Number.isFinite(value))throw Error('Invalid number');if(range){if(!['Sensitivity','Fov','Smooth','MaxDistance','TriggerDistance','TriggerFov','TriggerSmooth','RecoilX','RecoilY'].includes(key)&&!Number.isInteger(value))throw Error('Expected an integer: '+key);return Math.max(range[0],Math.min(range[1],value));}if(!Number.isInteger(value)||value< -2147483648||value>2147483647)throw Error('Number out of range');}
 if(typeof value==='string'&&value.length>128)throw Error('Text too long');return value;
}
function profiles(input){
 if(input===null)return null;if(typeof input!=='object'||Array.isArray(input)||Object.keys(input).length>128)throw Error('Invalid profiles');
 const result={};for(const [key,p]of Object.entries(input)){if(!(groups.has(key)||/^arma:\d{1,5}$/.test(key))||!p||typeof p!=='object'||Array.isArray(p))throw Error('Invalid profile');const next={...defaults.aim};for(const [k,v]of Object.entries(p)){if(!Object.hasOwn(defaults.aim,k))throw Error('Unknown setting');const r={Fov:[.1,30],Smooth:[1,50],TriggerFov:[.1,30],TriggerSmooth:[1,50],Key:[1,254],TriggerKey:[1,254],TriggerAimKey:[1,254],ShootDelay:[0,1000],TriggerInterval:[30,1000],MaxDistance:[0,500],TriggerDistance:[0,500],RecoilX:[0,3],RecoilY:[0,3],Hitboxes:[0,31],TriggerHitbox:[0,31]}[k];next[k]=scalar(k,v,defaults.aim[k],r);}result[key]=next;}return result;
}
function layout(input){
 if(!input||typeof input!=='object'||Array.isArray(input))throw Error('Invalid ESP layout');
 const result={...defaults.settings.EspLayout};
 for(const [key,value]of Object.entries(input)){if(!Object.hasOwn(result,key)||!Number.isInteger(value))throw Error('Invalid layout coordinate');result[key]=Math.max(-150,Math.min(150,value));}
 return result;
}
function movementBindings(result){
 const direction=k=>[65,68,83,87].includes(k),letter=k=>k>=65&&k<=90&&!direction(k),digit=k=>k>=48&&k<=57,fn=k=>k>=112&&k<=135,collision=k=>[result.MenuKey,result.ToggleKey,result.PrivacyKey].includes(k);
 if(!(result.JumpKey===32||letter(result.JumpKey)||digit(result.JumpKey)||fn(result.JumpKey))||collision(result.JumpKey))result.JumpKey=0;
 if(!([4,5,6,16,32].includes(result.MovementKey)||letter(result.MovementKey)||digit(result.MovementKey)||fn(result.MovementKey))||collision(result.MovementKey)||result.MovementKey===result.JumpKey)result.MovementKey=0;
 return result;
}
function patchSettings(current,patch){
 if(!patch||typeof patch!=='object'||Array.isArray(patch)||JSON.stringify(patch).length>200000)throw Error('Invalid changes');
 const result={...current};delete result.SnapTap;for(const [key,value]of Object.entries(patch)){if(key==='SnapTap'){if(typeof value!=='boolean')throw Error('Invalid legacy setting');continue;}if(!Object.hasOwn(defaults.settings,key))throw Error('Unknown setting: '+key);result[key]=key==='SkinLoadout'?validateSkinLoadout(value):key==='AimProfiles'?profiles(value):key==='EspLayout'?layout(value):scalar(key,value,defaults.settings[key],ranges[key]);}
 if(result.MenuKey===35)result.MenuKey=45;if(result.ToggleKey===35)result.ToggleKey=119;if(result.MenuKey===result.ToggleKey)result.ToggleKey=result.MenuKey===119?120:119;if(result.PrivacyKey===35||(result.PrivacyKey>0&&result.PrivacyKey<7)||result.PrivacyKey===result.MenuKey||result.PrivacyKey===result.ToggleKey)result.PrivacyKey=0;return movementBindings(result);
}
module.exports={defaults,patchSettings};
