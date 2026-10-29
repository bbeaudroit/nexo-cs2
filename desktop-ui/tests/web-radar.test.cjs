const {test}=require('node:test'),assert=require('node:assert/strict');
test('full-map projection stays fixed when viewer moves or turns; off-map points are excluded',async()=>{
 const {project}=await import('../../esp/WebRadarMath.mjs');const map={x:-1000,y:2000,scale:4,width:1024,height:1024};
 assert.deepEqual(project(map,{x:1048,y:-48,yaw:0}),{x:.5,y:.5});assert.deepEqual(project(map,{x:1048,y:-48,yaw:180,local:{x:999}}),{x:.5,y:.5});
 assert.equal(project(map,{x:NaN,y:0}),null);assert.equal(project(map,{x:-1001,y:0}),null);assert.deepEqual(project(map,{x:-1000,y:2000}),{x:0,y:0});
});
test('floors use calibrated altitude thresholds, including boundary',async()=>{const {floorDirection}=await import('../../esp/WebRadarMath.mjs');const upper={min:-495,max:10000};assert.equal(floorDirection(upper,-496),-1);assert.equal(floorDirection(upper,-495),0);assert.equal(floorDirection(upper,10000),1);});
test('history settings remain off by default and survive configuration validation',()=>{const {defaults,patchSettings}=require('../electron/settings.cjs');assert.equal(defaults.settings.ChamsHistoryEnabled,false);const next=patchSettings(defaults.settings,{ChamsHistoryEnabled:true,ChamsHistoryDelay:999,ChamsHistoryOpacity:90});assert.equal(next.ChamsHistoryDelay,500);assert.equal(next.ChamsHistoryOpacity,60);assert.equal(next.ChamsHistoryEnabled,true);});
