class MenuSync {
 constructor(visible){this.visible=visible;this.version=-1;this.intent=0;this.settled=0;}
 request(visible){this.visible=visible;return ++this.intent;}
 ack(intent,version){if(intent!==this.intent)return;this.settled=intent;this.version=Math.max(this.version,version);}
 fail(intent){if(intent===this.intent)this.settled=intent;}
 receive(visible,version){if(!Number.isSafeInteger(version)||this.intent!==this.settled||version<this.version)return false;this.version=version;this.visible=visible;return true;}
}
function accelerator(key){
 if(key>=112&&key<=135)return 'F'+(key-111);
 if(key>=48&&key<=57||key>=65&&key<=90)return String.fromCharCode(key);
 return ({8:'Backspace',9:'Tab',13:'Enter',19:'Pause',20:'Capslock',27:'Escape',32:'Space',33:'PageUp',34:'PageDown',35:'End',36:'Home',37:'Left',38:'Up',39:'Right',40:'Down',45:'Insert',46:'Delete',144:'Numlock',145:'Scrolllock'})[key]||null;
}
function globalMenuKey(key){return key>=112&&key<=135||[19,33,34,35,36,45,46,145].includes(key);}
function menuBounds(area,previous){
 const scale=Math.min(1,area.width/1040,area.height/680),width=Math.floor(1040*scale),height=Math.floor(680*scale);
 const fits=previous&&previous.x>=area.x&&previous.y>=area.y&&previous.x+width<=area.x+area.width&&previous.y+height<=area.y+area.height;
 return {x:fits?previous.x:area.x+Math.floor((area.width-width)/2),y:fits?previous.y:area.y+Math.floor((area.height-height)/2),width,height,scale};
}
module.exports={MenuSync,accelerator,menuBounds,globalMenuKey};
