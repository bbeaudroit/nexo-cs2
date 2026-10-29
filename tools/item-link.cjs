const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
// Keep ordered pairs: KV1 permits repeated keys in unrelated schema sections.
// Fields used for the identity join must be unique and are checked below.
function parseKV1(text){
 if(typeof text!=='string'||Buffer.byteLength(text)>16*1024*1024)throw Error('KV1 size limit');
 let pos=0,nodes=0;
 function skip(){for(;;){while(/\s/.test(text[pos]??'')&&pos<text.length)pos++;if(text.startsWith('//',pos)){const end=text.indexOf('\n',pos+2);pos=end<0?text.length:end+1;}else break;}}
 function string(){skip();if(text[pos++]!=='"')throw Error('Expected quoted KV1 string');let out='';while(pos<text.length){const c=text[pos++];if(c==='"')return out;if(c==='\\'){if(pos>=text.length)break;const e=text[pos++];out+=e==='"'||e==='\\'?e:'\\'+e;}else out+=c;if(out.length>1024*1024)throw Error('KV1 string limit');}throw Error('Unterminated KV1 string');}
 function object(depth,closed){if(depth>64)throw Error('KV1 depth limit');const entries=[];for(;;){skip();if(pos===text.length){if(closed)throw Error('Unclosed KV1 object');return entries;}if(text[pos]==='}'){if(!closed)throw Error('Unexpected KV1 close');pos++;return entries;}if(++nodes>1000000)throw Error('KV1 node limit');const key=string();skip();let value;if(text[pos]==='{'){pos++;value=object(depth+1,true);}else value=string();entries.push([key,value]);}}
 return object(0,false);
}
function field(entries,key,required=true){if(!Array.isArray(entries))throw Error('Expected KV1 object');const found=entries.filter(([name])=>name===key);if(found.length>1||required&&!found.length)throw Error('Missing or duplicate KV1 field: '+key);return found[0]?.[1];}
function linkItems(text,ballistics){
 if(!ballistics?.weapons||typeof ballistics.weapons!=='object'||Array.isArray(ballistics.weapons))throw Error('Invalid weapon catalogue');
 const root=field(parseKV1(text),'items_game');if(!Array.isArray(root))throw Error('Invalid items_game object');
 const blocks=root.filter(([key])=>key==='items').map(([,value])=>value);if(!blocks.length||blocks.some(value=>!Array.isArray(value)))throw Error('Invalid items object');const items=blocks.flat();
 const byItemId=Object.create(null),seen=new Set();let unmatched=0;
 let defaults=0;for(const [id,item] of items){
  if(id==='default'){if(++defaults>1||!Array.isArray(item))throw Error('Invalid default item');continue;}
  if(!/^(0|[1-9][0-9]{0,4})$/.test(id)||Number(id)>65535||seen.has(id))throw Error('Invalid or duplicate item definition ID');seen.add(id);
  const name=field(item,'name',false);if(name!==undefined&&(typeof name!=='string'||!name.length||name.length>512||/[\x00-\x1f\x7f]/.test(name)))throw Error('Invalid item name');
  if(!name||!Object.hasOwn(ballistics.weapons,name)){unmatched++;continue;}
  byItemId[id]={itemDefinitionId:Number(id),weaponName:name,sourceField:'items_game.items.'+id+'.name'};
 }
 return{format:1,localOnly:true,damageComputationEnabled:false,counts:{items:seen.size,linked:Object.keys(byItemId).length,unmatched},byItemId};
}
function importItemLinks({itemsFile,weaponsFile,output=path.resolve('local-assets/collision/item-links.json')}){
 for(const file of [itemsFile,weaponsFile])if(fs.statSync(file).size>16*1024*1024)throw Error('Input size limit');
 const items=fs.readFileSync(itemsFile),weapons=fs.readFileSync(weaponsFile),result=linkItems(items.toString('utf8'),JSON.parse(weapons));
 result.provenance={itemResource:'scripts/items/items_game.txt',itemsSHA256:crypto.createHash('sha256').update(items).digest('hex'),weaponsSHA256:crypto.createHash('sha256').update(weapons).digest('hex')};
 fs.mkdirSync(path.dirname(output),{recursive:true});fs.writeFileSync(output,JSON.stringify(result));return{...result.counts,output,provenance:result.provenance};
}
if(require.main===module){const [itemsFile,weaponsFile,output]=process.argv.slice(2);try{console.log(JSON.stringify(importItemLinks({itemsFile,weaponsFile,output}),null,2));}catch(e){console.error(e.message);process.exitCode=1;}}
module.exports={parseKV1,linkItems,importItemLinks};
