const {test}=require('node:test');const assert=require('node:assert/strict');
const {MenuSync,accelerator,menuBounds,globalMenuKey}=require('../electron/menu-state.cjs');
test('a delayed poll cannot undo a newer menu intent',()=>{
 const menu=new MenuSync(true);assert.equal(menu.receive(true,0),true);
 const hide=menu.request(false);assert.equal(menu.receive(true,0),false);
 menu.ack(hide,1);assert.equal(menu.receive(true,0),false);assert.equal(menu.visible,false);
 const show=menu.request(true);menu.ack(show,2);assert.equal(menu.receive(false,1),false);assert.equal(menu.visible,true);
 assert.equal(menu.receive(false,3),true);assert.equal(menu.visible,false);
});
test('rapid toggles keep the last intent until its acknowledgement',()=>{
 const menu=new MenuSync(true),first=menu.request(false),last=menu.request(true);
 menu.ack(first,1);assert.equal(menu.receive(false,1),false);menu.ack(last,2);
 assert.equal(menu.receive(true,2),true);assert.equal(menu.visible,true);
});
test('configured menu keys map without silently falling back to Insert',()=>{
 assert.equal(accelerator(45),'Insert');assert.equal(accelerator(119),'F8');assert.equal(accelerator(65),'A');assert.equal(accelerator(1),null);
});
test('typing keys cannot be captured globally outside the menu',()=>{for(const key of [65,90,48,57,13,32,8,9])assert.equal(globalMenuKey(key),false);assert.equal(globalMenuKey(45),true);assert.equal(globalMenuKey(119),true);});
test('menu fits a scaled monitor and stays on the game display',()=>{
 const area={x:-1280,y:0,width:1280,height:650},bounds=menuBounds(area,{x:-1000,y:20,width:1040,height:680});
 assert.ok(bounds.x>=area.x&&bounds.x+bounds.width<=0);assert.ok(bounds.y>=0&&bounds.y+bounds.height<=650);assert.ok(bounds.scale<1);
 const full=menuBounds({x:1920,y:0,width:1920,height:1040},null);assert.equal(full.scale,1);assert.ok(full.x>=1920);
});
