const catalog=require('../skin-catalog.json');const compatible=new Map(catalog.weapons.map(w=>[String(w.id),new Set(w.paints.map(p=>p.id))]));
function plain(value){return value&&Object.getPrototypeOf(value)===Object.prototype&&!Reflect.ownKeys(value).some(k=>typeof k!=='string'||!Object.hasOwn(Object.getOwnPropertyDescriptor(value,k),'value'));}
function validateSkinLoadout(input){
 if(input===undefined)return {version:1,weapons:{}};
 if(!plain(input)||Reflect.ownKeys(input).length!==2||!Object.hasOwn(input,'version')||!Object.hasOwn(input,'weapons')||input.version!==1||!plain(input.weapons)||Reflect.ownKeys(input.weapons).length>35)throw Error('Invalid skin loadout');
 const weapons={};for(const [key,paint]of Object.entries(input.weapons)){if(!/^[1-9]\d{0,4}$/.test(key)||!Number.isInteger(paint)||!compatible.get(key)?.has(paint))throw Error('Invalid weapon/paint pair');weapons[key]=paint;}return {version:1,weapons};
}
module.exports={validateSkinLoadout};
