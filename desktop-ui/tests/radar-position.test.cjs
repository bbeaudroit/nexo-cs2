const {test}=require('node:test');const assert=require('node:assert/strict');
test('radar drag maps preview pixels to game pixels and clamps the whole panel',async()=>{
 const {radarGeometry,dragRadar}=await import('../src/radar-position.mjs');
 const g=radarGeometry(1920,1080,190,24,76);
 assert.deepEqual(dragRadar(g,100,50,480),{RadarX:424,RadarY:276,RadarHudPosition:false});
 assert.deepEqual(dragRadar(g,9999,9999,480),{RadarX:1722,RadarY:848,RadarHudPosition:false});
 assert.equal(dragRadar(g,-9999,-9999,480).RadarX,8);
 assert.equal(radarGeometry(1920,1080,190,9999,9999).y,848);
});
test('quality and sensitivity options survive the settings contract',()=>{
 const {defaults,patchSettings}=require('../electron/settings.cjs');
 const result=patchSettings(defaults.settings,{AutoSensitivity:true,ChamsSupersampling:false});
 assert.equal(result.AutoSensitivity,true);assert.equal(result.ChamsSupersampling,false);
 assert.throws(()=>patchSettings(result,{AutoSensitivity:'true'}));
});
