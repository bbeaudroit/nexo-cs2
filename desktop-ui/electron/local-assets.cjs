const path=require('node:path');const fs=require('node:fs');
function resolveAsset(root,url){
 const u=new URL(url);if(u.protocol!=='nexo-asset:'||u.hostname!=='local'||u.search||u.hash)throw Error('Asset denied');
 const name=decodeURIComponent(u.pathname.slice(1));if(!/^[a-zA-Z0-9_.-]+\.(glb|png)$/.test(name))throw Error('Asset denied');
 const folder=path.join(root,'local-assets'),manifest=JSON.parse(fs.readFileSync(path.join(folder,'manifest.json'),'utf8').replace(/^\uFEFF/,''));
 if(!Array.isArray(manifest.files)||!manifest.files.includes(name))throw Error('Asset denied');
 const resolved=path.resolve(folder,name),realFolder=fs.realpathSync(folder),real=fs.realpathSync(resolved);if(path.dirname(real)!==realFolder)throw Error('Asset denied');return real;
}
module.exports={resolveAsset};
