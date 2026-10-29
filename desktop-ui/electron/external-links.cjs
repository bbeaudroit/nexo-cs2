const bundled=require('../social-links.json');
function resolveExternal(target,links=bundled){
 if(!['website','discord'].includes(target)||typeof links[target]!=='string')throw Error('Unknown external link.');
 const url=new URL(links[target]);
 if(url.protocol!=='https:'||url.username||url.password)throw Error('Invalid external link.');
 if(target==='discord'&&!(url.hostname==='discord.gg'||url.hostname==='discord.com'&&url.pathname.startsWith('/invite/')))throw Error('Invalid Discord invitation.');
 return url.href;
}
function externalInfo(){return{website:resolveExternal('website'),discord:resolveExternal('discord')};}
module.exports={resolveExternal,externalInfo};
