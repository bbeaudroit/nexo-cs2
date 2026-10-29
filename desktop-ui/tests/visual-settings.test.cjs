const {test}=require('node:test');const assert=require('node:assert/strict');const {defaults,patchSettings}=require('../electron/settings.cjs');
test('ESP layout validates coordinates and rejects unknown fields and invalid objects',()=>{
 const result=patchSettings(defaults.settings,{EspLayout:{NameX:999,NameY:-999,ArmorX:12},EspMaxDistance:999});
 assert.equal(result.EspMaxDistance,500);assert.equal(result.EspLayout.NameX,150);assert.equal(result.EspLayout.NameY,-150);assert.equal(result.EspLayout.ArmorX,12);
 for(const value of [null,[],{NameX:1.5},{NameX:NaN},{NameX:'10'},{Other:3}])assert.throws(()=>patchSettings(defaults.settings,{EspLayout:value}));
});
test('privacy shortcut collisions are disabled without changing menu or visuals keys',()=>{
 for(const key of [35,45,119,1]){const result=patchSettings(defaults.settings,{PrivacyKey:key});assert.equal(result.PrivacyKey,0);assert.equal(result.MenuKey,45);assert.equal(result.ToggleKey,119);}
 assert.equal(patchSettings(defaults.settings,{PrivacyKey:118}).PrivacyKey,118);
});
test('external mesh settings preserve opacity endpoints and clamp width/range',()=>{
 const result=patchSettings(defaults.settings,{ChamsOpacity:0,ChamsOutlineOpacity:100,ChamsOutlineWidth:999,ChamsDistance:-1});
 assert.equal(result.ChamsOpacity,0);assert.equal(result.ChamsOutlineOpacity,100);assert.equal(result.ChamsOutlineWidth,4);assert.equal(result.ChamsDistance,0);
});
test('search reaches the specialized layout editor and supports capture aliases',async()=>{
 const {searchSettings}=await import('../src/catalog.mjs');assert(searchSettings('drag').some(x=>x.key==='EspLayout'&&x.tab==='layout'));assert(searchSettings('stream proof').some(x=>x.key==='ExcludeFromCapture'));
});
