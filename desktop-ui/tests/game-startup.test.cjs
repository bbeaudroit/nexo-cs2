const {test}=require('node:test');const assert=require('node:assert/strict');
const {parseCs2Processes,waitForCs2}=require('../electron/game-startup.cjs');
test('exact tasklist names only; unavailable response fails closed',()=>{
 assert.equal(parseCs2Processes('"cs2.exe","123","Console","1","123 K"'),true);
 for(const value of ['"notcs2.exe","123"','INFO: No tasks are running','',null,'"cs2.exe.bak","123"'])assert.equal(parseCs2Processes(value),false);
});
test('startup waits and rechecks without opening the menu early',async()=>{
 let checks=0,pauses=0;await waitForCs2({running:async()=>++checks===3,pause:async()=>pauses++,cancelled:()=>false});assert.equal(checks,3);assert.equal(pauses,2);
});
test('cancel waiting on quit and reject probe errors',async()=>{
 let checks=0;assert.equal(await waitForCs2({running:async()=>{checks++;return false;},pause:async()=>{},cancelled:()=>checks===2}),false);
 await assert.rejects(waitForCs2({running:async()=>{throw Error('process query unavailable');},pause:async()=>{},cancelled:()=>false}));
});
