const {contextBridge,ipcRenderer}=require('electron');
contextBridge.exposeInMainWorld('nexo',{
 initial:()=>ipcRenderer.invoke('nexo:initial'),
 profile:(action,value)=>ipcRenderer.invoke('nexo:profile',action,value),
 external:(action,target)=>ipcRenderer.invoke('nexo:external',action,target),
 cloud:(action,payload)=>ipcRenderer.invoke('nexo:cloud',action,payload),
 update:action=>ipcRenderer.invoke('nexo:update',action),
 share:(action,value)=>ipcRenderer.invoke('nexo:share',action,value),
 patch:value=>ipcRenderer.invoke('nexo:patch',value),
 command:(action,argument='')=>ipcRenderer.invoke('nexo:command',action,argument),
 license:(action,payload={})=>ipcRenderer.invoke('nexo:license',action,payload),
 window:action=>ipcRenderer.invoke('nexo:window',action),
 subscribe:callback=>{const listener=(_,data)=>callback(data);ipcRenderer.on('nexo:state',listener);return()=>ipcRenderer.removeListener('nexo:state',listener);}
});
