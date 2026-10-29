const assert=require('node:assert/strict'),{test}=require('node:test');
const {parseKV3}=require('../kv3.cjs');
test('KV3 boilerplate, objects, optional commas, arrays and bytes',()=>{const v=parseKV3('viewer boilerplate\n<!-- kv3 test -->\n{foo=[1,2 true null "ok"] data=#[00 ff 12] nested={a=-2.5e2}}');assert.equal(v.nested.a,-250);assert.deepEqual([...v.data],[0,255,18]);assert.equal(v.foo[2],true);});
test('reject duplicate/prototype keys, bad blobs/numbers and depth/size limits',()=>{for(const text of ['{x=1 x=2}','{__proto__={}}','{constructor=1}','{x=#[0]}','{x=NaN}','{x=1e999}','{x=[1}'])assert.throws(()=>parseKV3(text));assert.throws(()=>parseKV3('{x={a={}}}',{maxDepth:1}));assert.throws(()=>parseKV3('{}',{maxBytes:1}));});
