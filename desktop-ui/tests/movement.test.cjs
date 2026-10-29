const {test}=require('node:test');const assert=require('node:assert/strict');const {defaults,patchSettings}=require('../electron/settings.cjs');
test('movement is opt-in and its controls remain reachable',async()=>{
 assert.equal(defaults.settings.BunnyHop,false);assert.equal(defaults.settings.AutoStrafe,false);
 const {searchSettings}=await import('../src/catalog.mjs');for(const key of ['BunnyHop','AutoStrafe','MovementKey','JumpKey'])assert(searchSettings(key).some(x=>x.key===key&&x.tab==='movement'));
});
test('movement key normalization rejects output collisions and system keys',()=>{
 const base=defaults.settings;
 for(const key of [0,1,2,65,68,83,87,91,92,45,119,118])assert.equal(patchSettings(base,{JumpKey:key}).JumpKey,0,'jump '+key);
 for(const key of [1,2,17,18,65,68,83,87,91,92,45,119,118,32])assert.equal(patchSettings(base,{MovementKey:key}).MovementKey,0,'hold '+key);
 assert.equal(patchSettings(base,{MovementKey:5,JumpKey:74}).JumpKey,74);
 assert.throws(()=>patchSettings(base,{BunnyHop:1}));assert.throws(()=>patchSettings(base,{MovementKey:5.5}));
});
