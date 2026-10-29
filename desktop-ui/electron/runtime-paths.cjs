const fs=require('node:fs'),path=require('node:path');
function isInstalledRoot(root){try{const file=path.join(root,'nexo-install.json');if(fs.statSync(file).size>1024)return false;const marker=JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,''));return marker.format===1&&marker.channel==='stable';}catch{return false;}}
function initializeRuntime(resourceRoot,userData,defaults){const root=path.resolve(resourceRoot),installed=isInstalledRoot(root),dataRoot=installed?path.resolve(userData):root;fs.mkdirSync(dataRoot,{recursive:true});const file=path.join(dataRoot,'esp-settings.json');if(installed&&!fs.existsSync(file))fs.writeFileSync(file,JSON.stringify(defaults,null,2),{flag:'wx'});return{resourceRoot:root,dataRoot,installed};}
module.exports={isInstalledRoot,initializeRuntime};
