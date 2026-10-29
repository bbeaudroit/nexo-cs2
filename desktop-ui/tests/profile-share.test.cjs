const {test}=require('node:test');const assert=require('node:assert/strict');
test('display names are normalized and bounded without accepting controls',()=>{
 const {displayName}=require('../electron/profile-share.cjs');
 assert.equal(displayName('  Papi   Nexo  '),'Papi Nexo');
 for(const value of ['',null,'a'.repeat(33),'a\nb','<script>'])assert.throws(()=>displayName(value));
});
test('config codes retain declared author and validated settings; reject corruption and version mismatch',()=>{
 const {encodeConfig,decodeConfig}=require('../electron/profile-share.cjs');const {defaults}=require('../electron/settings.cjs');
 const code=encodeConfig({name:'Deathmatch',author:'Papi',settings:{...defaults.settings,RadarX:247}});
 const cfg=decodeConfig(code);assert.equal(cfg.author,'Papi');assert.equal(cfg.name,'Deathmatch');assert.equal(cfg.settings.RadarX,247);
 assert.throws(()=>decodeConfig(code.slice(0,-3)+'xyz'));assert.throws(()=>decodeConfig('NEXO2.abc.def'));assert.throws(()=>decodeConfig('x'.repeat(100001)));
});
test('native config filenames and attribution use one canonical name',()=>{
 const {canonicalConfigName}=require('../electron/profile-share.cjs');
 assert.equal(canonicalConfigName(' my config '),'myconfig');assert.equal(canonicalConfigName('../Test_1-2'),'Test_1-2');assert.equal(canonicalConfigName('a'.repeat(80)),'a'.repeat(40));assert.throws(()=>canonicalConfigName('.. /'));
});
test('engine polling preserves local username and imported attribution',()=>{
 const {mergeEngineState}=require('../electron/profile-share.cjs');const importedConfig={name:'Deathmatch',author:'Papi'};
 const next=mergeEngineState({importedConfig},{connected:true,players:5},'Tester');assert.equal(next.userName,'Tester');assert.deepEqual(next.importedConfig,importedConfig);assert.equal(next.players,5);
});
