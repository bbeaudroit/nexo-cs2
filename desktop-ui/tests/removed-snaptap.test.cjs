const{test}=require('node:test'),assert=require('node:assert/strict');
const{defaults,patchSettings}=require('../electron/settings.cjs');
test('old SnapTap settings are ignored without resetting other user settings',()=>{const result=patchSettings(defaults.settings,{SnapTap:true,Frames:144,Boxes:true,BunnyHop:true});assert.equal(result.Frames,144);assert.equal(result.Boxes,true);assert.equal(result.BunnyHop,true);assert.equal(Object.hasOwn(result,'SnapTap'),false);assert.throws(()=>patchSettings(defaults.settings,{SnapTap:'true'}));});
test('SnapTap no longer has a visible menu control',async()=>{const{searchSettings}=await import('../src/catalog.mjs');assert.equal(searchSettings('SnapTap').length,0);assert.equal(Object.hasOwn(defaults.settings,'SnapTap'),false);});
