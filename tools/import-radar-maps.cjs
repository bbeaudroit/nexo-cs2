// Re-run extraction with Source2Viewer before this script after CS2 map updates.
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'..'),source=path.join(root,'work/map-extract'),output=path.join(root,'local-assets/maps');fs.mkdirSync(output,{recursive:true});
const maps=[];
for(const file of fs.readdirSync(path.join(source,'resource/overviews'))){
 const name=path.basename(file,'.txt');if(!/^(de|cs|ar)_[a-z0-9_]+$/.test(name))continue;
 const text=fs.readFileSync(path.join(source,'resource/overviews',file),'utf8').replace(/\/\/[^\r\n]*/g,'');
 const value=key=>{const m=text.match(new RegExp('"'+key+'"\\s+"([^"\\r\\n]+)"','i'));return m?.[1];};
 const x=Number(value('pos_x')),y=Number(value('pos_y')),scale=Number(value('scale'));if(![x,y,scale].every(Number.isFinite)||scale<=0)continue;
 const material=value('material')?.split('/').pop()?.replace(/_radar$/,'')||name;
 function image(base){const files=[base+'_radar_psd.png',base+'_radar_tga.png'];const found=files.find(f=>fs.existsSync(path.join(source,'panorama/images/overheadmaps',f)));if(!found)return null;const target=base+'.png';fs.copyFileSync(path.join(source,'panorama/images/overheadmaps',found),path.join(output,target));return target;}
 const main=image(name)||image(material);if(!main)continue;
 const layers=[];
 for(const m of text.matchAll(/"(default|lower|[a-z0-9_]+)"\s*\{([^{}]*"AltitudeM[^{}]+)\}/gi)){
   const min=Number(m[2].match(/"AltitudeMin"\s*"([^"]+)"/i)?.[1]),max=Number(m[2].match(/"AltitudeMax"\s*"([^"]+)"/i)?.[1]);const img=m[1]==='default'?main:image(name+'_'+m[1]);
   if(img&&Number.isFinite(min)&&Number.isFinite(max))layers.push({name:m[1],min,max,image:img});
 }
 maps.push({name,x,y,scale,width:1024,height:1024,image:main,layers});
}
fs.writeFileSync(path.join(output,'maps.json'),JSON.stringify(maps,null,2)+'\n');
fs.writeFileSync(path.join(output,'SOURCE.txt'),'Extracted from the locally installed CS2 pak01_dir.vpk on 2026-10-26 using Source2Viewer 20.1. Logical overview coordinates are 1024 square. Re-extract after map layout changes. Valve game assets; not Nexo-original artwork.\n');
console.log('Imported '+maps.length+' map overviews with calibrated origins/scales and available floors.');
