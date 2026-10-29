const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const {buildSkinCatalog}=require('../../tools/skin-catalog.cjs');
const {validateSkinLoadout}=require('../electron/skin-loadout.cjs');
const catalog=require('../skin-catalog.json');
const fixture='"items_game" { "items" { "7" { "name" "weapon_ak47" } } "paint_kits" { "180" { "name" "cu_fireserpent" } } "item_sets" { "set" { "items" { "[cu_fireserpent]weapon_ak47" "1" } } } }';
test('ordered importer resolves explicit pairs only, including repeated blocks',()=>{
 const result=buildSkinCatalog(fixture,{featured:{7:180}});
 assert.equal(result.weapons[0].featuredPaintId,180);
 assert.deepEqual(result.weapons[0].paints,[{id:180,name:'cu_fireserpent',label:'cu_fireserpent'}]);
 assert.match(result.sourceHash,/^[a-f0-9]{64}$/);
 const repeated=fixture.replace('"item_sets"','"paint_kits" { "181" { "name" "other" } } "item_sets"');
 assert.equal(buildSkinCatalog(repeated,{featured:{7:180}}).weapons[0].paints.length,1);
});
test('ambiguous IDs/names, missing mappings, invalid featured pair and oversized source fail closed',()=>{
 for(const text of [fixture.replace('"paint_kits"','"items" { "7" { "name" "weapon_other" } } "paint_kits"'),fixture.replace('"item_sets"','"paint_kits" { "181" { "name" "cu_fireserpent" } } "item_sets"'),fixture.replace('[cu_fireserpent]','[unknown]')])assert.throws(()=>buildSkinCatalog(text));
 assert.throws(()=>buildSkinCatalog(fixture,{featured:{7:999}}));
 assert.throws(()=>buildSkinCatalog(' '.repeat(16*1024*1024+1)));
});
test('legacy absence defaults empty; valid choices roundtrip and Original is absence',()=>{
 assert.deepEqual(validateSkinLoadout(undefined),{version:1,weapons:{}});
 assert.deepEqual(validateSkinLoadout({version:1,weapons:{7:180}}),{version:1,weapons:{7:180}});
 assert.deepEqual(validateSkinLoadout({version:1,weapons:{}}),{version:1,weapons:{}});
});
test('strict bounded loadout rejects malformed, prototype and unsupported pairs',()=>{
 const bad=[null,[],{}, {version:2,weapons:{}},{version:1,weapons:[]},{version:1,weapons:{7:'180'}},{version:1,weapons:{7:0}},{version:1,weapons:{7:NaN}},{version:1,weapons:{7:Infinity}},{version:1,weapons:{7:180.5}},{version:1,weapons:{7:344}},{version:1,weapons:{500:180}},{version:1,weapons:{'07':180}},{version:1,weapons:{7:180},extra:1},{version:1,weapons:Object.assign(Object.create({x:1}),{7:180})},JSON.parse('{"version":1,"weapons":{"__proto__":180}}'),{version:1,weapons:Object.fromEntries(Array.from({length:36},(_,i)=>[i+1,180]))}];
 for(const input of bad)assert.throws(()=>validateSkinLoadout(input));
 const symbol={version:1,weapons:{}};symbol[Symbol('extra')]=1;assert.throws(()=>validateSkinLoadout(symbol));
 const getter={version:1,get weapons(){throw Error('getter must not run');}};assert.throws(()=>validateSkinLoadout(getter),/Invalid/);
});
test('generated catalog exactly covers audited pairs and editorial first batch',()=>{
 const audit=require('./fixtures/skin-catalog-audit.json');
 assert.equal(catalog.format,1);assert.equal(catalog.weapons.length,35);assert.equal(catalog.knives.length,20);assert.equal(catalog.gloves.length,8);
 const pairs=new Set(catalog.weapons.flatMap(w=>w.paints.map(p=>`${w.id}:${p.id}`)));
 assert.equal(pairs.size,1456);assert.equal(catalog.weapons.reduce((n,w)=>n+w.paints.length,0),1456);
 for(const p of audit.associations)assert.ok(pairs.has(`${p.itemId}:${p.paintId}`));
 for(const p of audit.firstBatch)assert.equal(catalog.weapons.find(w=>w.id===p.itemId).featuredPaintId,p.paintId);
 for(const w of catalog.weapons)for(const p of w.paints)assert.equal(validateSkinLoadout({version:1,weapons:{[w.id]:p.id}}).weapons[w.id],p.id);
 const local=path.resolve(__dirname,'../../work/items_game.txt');
 if(fs.existsSync(local))assert.deepEqual(buildSkinCatalog(fs.readFileSync(local,'utf8'),{localization:fs.existsSync(path.resolve(__dirname,'../../work/skin-localization'))?fs.readFileSync(path.resolve(__dirname,'../../work/skin-localization'),'utf8'):undefined}),catalog);
});
