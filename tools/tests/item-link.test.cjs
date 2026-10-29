const {test}=require('node:test'),assert=require('node:assert/strict');
const api=require('../item-link.cjs');
test('repeated items blocks are combined and default is not an item ID',()=>{
 const r=api.linkItems('"items_game" {"items" {"default" {"name" "base"} "4" {"name" "weapon_glock"}} "items" {"7" {"name" "weapon_ak47"}}}',{weapons:{weapon_glock:{},weapon_ak47:{}}});
 assert.equal(r.counts.linked,2);assert.equal(r.byItemId['7'].weaponName,'weapon_ak47');assert.equal(r.byItemId.default,undefined);
});
test('item IDs link by explicit item name, never by display label or class',()=>{
 assert.equal(typeof api.linkItems,'function');
 const source='"items_game" { "items" { "4" { "name" "weapon_glock" "item_class" "weapon_pistol" } "60" { "name" "weapon_m4a1_silencer" } "900" { "name" "collectible" } } }';
 const result=api.linkItems(source,{weapons:{weapon_glock:{stats:{}},weapon_m4a1_silencer:{stats:{}}}});
 assert.equal(result.byItemId['4'].weaponName,'weapon_glock');assert.equal(result.byItemId['60'].weaponName,'weapon_m4a1_silencer');assert.equal(result.byItemId['900'],undefined);assert.equal(result.damageComputationEnabled,false);
});
test('unknown names remain unmatched and ambiguous IDs/names are rejected',()=>{
 assert.equal(typeof api.linkItems,'function');
 const weapons={weapons:{weapon_test:{}}};
 assert.equal(api.linkItems('"items_game" { "items" { "7" { "name" "unknown" } } }',weapons).counts.linked,0);
 for(const inner of ['"7" {"name" "weapon_test"} "7" {"name" "weapon_test"}','"07" {"name" "weapon_test"}','"65536" {"name" "weapon_test"}','"7" {"name" "weapon_test" "name" "other"}'])assert.throws(()=>api.linkItems('"items_game" { "items" { '+inner+' } }',weapons));
 assert.throws(()=>api.linkItems('"items_game" {"items" {"7" {"name" "weapon_test"}} "items" {"7" {"name" "weapon_test"}}}',weapons));
});
test('KV1 comments/escapes work, directives and truncated documents fail',()=>{
 assert.equal(typeof api.linkItems,'function');
 const weapons={weapons:{weapon_test:{}}};
 assert.equal(api.linkItems('//header\n"items_game" {"items" {"1" {"name" "weapon_test" "label" "say \\"hi\\""}}}',weapons).counts.linked,1);
 for(const text of ['#base "elsewhere"','"items_game" {','"items_game" {"items" {"1" {"name" "weapon_test"}}} garbage','"items_game" {"items" {}} [$WIN32]'])assert.throws(()=>api.linkItems(text,weapons));
});
test('non-weapon names may have spaces and punctuation without becoming weapon matches',()=>{
 assert.equal(api.linkItems('"items_game" {"items" {"62" {"name" "Recipe Trade Up"}}}',{weapons:{weapon_test:{}}}).counts.unmatched,1);
});
test('local CS2 item schema joins Glock, AK and silenced variants to source stats',{skip:!require('node:fs').existsSync(require('node:path').resolve(__dirname,'../../work/items_game.txt'))},()=>{
 const fs=require('node:fs'),path=require('node:path'),root=path.resolve(__dirname,'../..');
 const weapons=JSON.parse(fs.readFileSync(path.join(root,'local-assets/collision/weapons.json'))),r=api.linkItems(fs.readFileSync(path.join(root,'work/items_game.txt'),'utf8'),weapons);
 assert.equal(r.counts.items,2059);assert.equal(r.counts.linked,71);
 for(const [id,name] of [[4,'weapon_glock'],[7,'weapon_ak47'],[16,'weapon_m4a1'],[60,'weapon_m4a1_silencer'],[61,'weapon_usp_silencer']])assert.equal(r.byItemId[id].weaponName,name);
 assert.equal(weapons.weapons[r.byItemId['4'].weaponName].stats.m_flPenetration,1);
 assert.equal(weapons.weapons[r.byItemId['7'].weaponName].stats.m_flPenetration,2);
 assert.equal(Object.values(r.byItemId).some(x=>x.weaponName==='weapon_knife_twinblade'),false);
});
