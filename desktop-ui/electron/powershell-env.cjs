// Let Windows PowerShell discover its own modules instead of inheriting PowerShell 7 paths.
function powershellEnv(source=process.env){return Object.fromEntries(Object.entries(source).filter(([key])=>key.toLowerCase()!=='psmodulepath'));}
module.exports={powershellEnv};
