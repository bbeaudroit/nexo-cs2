const t=(key,label,color,help,aliases='')=>({type:'toggle',key,label,color,help,aliases});
const r=(key,label,min,max,step=1,unit='',help)=>({type:'range',key,label,min,max,step,unit,help});
const k=(key,label)=>({type:'key',key,label});
const c=(key,label)=>({type:'color',key,label});
const select=(key,label,options,numeric=false)=>({type:'select',key,label,options,numeric});
const tab=(id,label,rows,extra,editors=[])=>({id,label,rows,extra,editors});
const profile=rows=>rows.map(row=>({...row,scope:'profile'}));
export const catalog=[
 {id:'visuals',label:'Visuals',icon:'eye',tabs:[
  tab('players','Players',[t('Enabled','Enable visuals'),t('ShowTeammates','Show teammates',null,'Off by default. Includes same-team players in ESP and radar for free-for-all DeathMatch. Does not enable teammate targeting.'),t('Boxes','Boxes','ColorArgb'),t('Skeleton','Skeleton','BoneColorArgb'),t('HeadCircle','Head circle','HeadColor'),t('Names','Name','NameColor'),t('Weapons','Weapon & ammo','WeaponColor'),t('WeaponIcons','Weapon icon'),t('Distance','Distance'),r('EspMaxDistance','Max distance',0,500,1,' m','0 = unlimited. Applies to player ESP and radar dots.')]),
  tab('layout','Layout',[],'esp-layout',[{key:'EspLayout',label:'ESP layout',aliases:'drag position labels bars'}]),
  tab('chams','Chams',[t('ChamsEnabled','External SAS silhouette','ChamsColor','Reconstructed mesh with approximate pose. Does not replace live game materials or classify visibility.'),r('ChamsOpacity','Fill opacity',0,100,1,'%'),t('ChamsOutline','Contour','ChamsOutlineColor'),r('ChamsOutlineOpacity','Contour opacity',0,100,1,'%'),r('ChamsOutlineWidth','Contour width',1,4,1,' px'),r('ChamsDistance','Max distance',0,500,1,' m','0 = unlimited.')],'chams-status'),
  tab('history','History',[t('ChamsHistoryEnabled','Historical silhouette','ChamsHistoryColor','Draw one recorded pose behind a moving player. External visual only; does not modify lag compensation or targeting.'),r('ChamsHistoryDelay','Pose delay',50,500,50,' ms'),r('ChamsHistoryOpacity','History opacity',0,60,1,'%')],'history-status'),
  tab('bars','Bars',[t('Health','Health','HealthColor'),t('Armor','Armor','ArmorColor'),t('LabelBackground','Label backgrounds'),r('LabelOpacity','Opacity',0,100,1,'%'),r('FontSize','Text size',7,16,1,' pt')]),
  tab('style','Style',[t('CornerBoxes','Corner boxes'),t('GlowEnabled','Glow','GlowColor'),r('GlowStrength','Intensity',0,100,1,'%'),r('GlowRadius','Radius',2,14,1,' px'),r('Thickness','Line width',1,4,1,' px')],'presets'),
  tab('sound','Sound',[t('SoundEsp','Shot markers',null,'Uses changes in shot counters, not footstep audio.'),r('SoundTime','Duration',1,10,1,' s')])]},
 {id:'aim',label:'Aimbot',icon:'aim',tabs:[
  tab('general','General',profile([t('Enabled','Enable aimbot'),k('Key','Aim key'),r('Fov','FOV',.1,30,.1,'°'),r('Smooth','Smoothing',1,50),r('MaxDistance','Max distance',0,500,1,' m','0 = unlimited. Also applies to Trigger Aim.'),t('Linear','Linear smoothing'),t('Closest','Closest hitbox'),{type:'hitboxes',key:'Hitboxes',label:'Hitboxes'}])),
  tab('fov','FOV',profile([t('ShowFov','Show FOV'),c('FovColor','FOV color'),t('FollowRecoil','Follow recoil'),c('RecoilColor','Recoil FOV color')])),
  tab('recoil','Recoil',profile([t('Rcs','Recoil control'),r('RecoilX','Horizontal',0,3,.05),r('RecoilY','Vertical',0,3,.05)])),
  tab('input','Input',[t('AutoSensitivity','Auto-detect sensitivity',null,'Reads the active Steam account’s saved CS2 config every 2 seconds. Console changes appear after CS2 saves them. Falls back to the manual value if unavailable.'),r('Sensitivity','Manual sensitivity / fallback',.01,20,.01)],'input-status')]},
 {id:'trigger',label:'Trigger',icon:'crosshair',tabs:[
  tab('shot','Firing',profile([t('Trigger','Enable trigger'),t('TriggerAlways','Always active',null,'When off, hold the trigger key.'),k('TriggerKey','Trigger key'),r('ShootDelay','Shot delay',0,1000,10,' ms'),r('TriggerInterval','Shot interval',30,1000,10,' ms'),r('TriggerDistance','Max distance',0,500,1,' m','0 = unlimited.')]),'trigger-status'),
  tab('aim','Trigger Aim',profile([t('TriggerAim','Enable Trigger Aim'),k('TriggerAimKey','Aim key'),t('TriggerAimRecoil','Compensate recoil'),r('TriggerFov','FOV',.1,30,.1,'°'),r('TriggerSmooth','Smoothing',1,50),select('TriggerHitbox','Hitbox',[[1,'Head'],[2,'Neck'],[4,'Chest'],[8,'Pelvis'],[16,'Legs']],true)]))]},
 {id:'radar',label:'Radar',icon:'radar',tabs:[
  tab('local','Local',[t('Radar','Show radar'),t('RadarOutlines','Guides & outlines'),t('RadarDots','Player dots','DotColor'),t('RadarHudPosition','Align with HUD'),t('RadarWeapons','Dropped weapons'),t('RadarGrenades','Dropped grenades')]),
  tab('position','Position',[r('RadarSize','Size',140,320,1,' px'),r('RadarRange','Range',500,5000,100,' u'),r('RadarOpacity','Opacity',0,100,1,'%'),r('RadarX','Horizontal',0,7000,1,' px'),r('RadarY','Vertical',0,4000,1,' px')],'radar-position'),
  tab('web','Sharing',[],'web')]},
 {id:'world',label:'World',icon:'world',tabs:[
  tab('items','Items',[t('WorldWeapons','Weapons'),t('WorldItems','Other items'),t('WorldGrenades','Grenades'),t('WorldFire','Molotov fire'),c('WorldColor','Color')]),
  tab('bomb','Bomb',[t('Bomb','Location & timer',null,'Requires a validated game clock.'),c('BombColor','Color')])]},
 {id:'grenades',label:'Grenades',icon:'grenade',tabs:[
  tab('general','Lineups',[t('GrenadeHelper','Show lineups'),{type:'text',key:'Map',label:'Map'},select('GrenadeFilter','Type',[['Todas','All'],['Humo','Smoke'],['Flash','Flash'],['HE','HE'],['Molotov','Molotov'],['Incendiaria','Incendiary'],['Señuelo','Decoy']]),r('GrenadeDistance','Max distance',100,10000,100,' u'),c('GrenadeColor','Color')]),
  tab('align','Alignment',[t('GrenadeAlign','Align aim',null,'Only near a saved spot. Does not throw the grenade.'),k('GrenadeKey','Key')]),
  tab('library','Library',[],'grenades')]},
 {id:'skins',label:'Skins',icon:'spark',tabs:[tab('weapons','Weapons',[],'skins-weapons',[{key:'SkinLoadout',label:'Skin selections',aliases:'paint finish loadout original'}]),tab('knives','Knives',[],'skins-knives'),tab('gloves','Gloves',[],'skins-gloves')]},
 {id:'misc',label:'Misc',icon:'spark',tabs:[
  tab('hud','HUD',[t('Status','Status watermark'),t('DetailedStatus','FPS & timings'),t('Spectators','Spectator list'),t('VerboseSpectators','Spectator details'),t('RecoilCrosshair','Recoil crosshair')]),
  tab('hits','Hits',[t('Hitmarker','Hitmarker',null,'Requires damage attributed to the local player.'),t('HitSound','Hit sound'),r('HitVolume','Volume',0,100,1,'%'),select('HitSoundType','Sound',[[0,'Click'],[1,'Bell'],[2,'Low']],true)],'sound-test'),
  tab('camera','Camera',[t('ChangeFov','Custom camera FOV'),r('GameFov','Camera FOV',60,140,1,'°')],'camera-status'),
  tab('movement','Movement',[t('BunnyHop','Bunny hop',null,'Hold the assist key to send one jump pulse on landing. Match the jump key to CS2. External sampling does not guarantee perfect hops.'),t('AutoStrafe','Mouse-steered auto-strafe',null,'While airborne and holding assist, turn the view to strafe. Manual W/A/S/D takes priority.'),k('MovementKey','Hold movement assist'),k('JumpKey','CS2 jump key')],'movement-status')]},
 {id:'system',label:'System',icon:'settings',tabs:[
  tab('performance','Performance',[r('Frames','Overlay limit',30,240,1,' FPS'),t('VSync','Sync with DWM',null,'DWM = Desktop Window Manager, the Windows component that combines windows on screen. Synchronizing the overlay can reduce tearing, but waiting for the compositor may add latency. This does not change CS2’s V-Sync.'),t('ChamsSupersampling','Smoother chams edges',null,'Off = faster single-sample silhouettes. On = 2× sampling on small silhouettes, using more CPU. Geometry and opacity stay the same.')],'performance-status'),
  tab('keys','Shortcuts',[k('MenuKey','Menu'),k('ToggleKey','Visuals')]),
  tab('updates','Updates',[],'updates'),
  tab('capture','Capture',[t('ExcludeFromCapture','Exclude from capture',null,'Supported Windows capture methods only.','Stream proof capture protection'),t('HideOnCaptureFailure','Hide overlay if protection fails'),k('PrivacyKey','Hold to hide overlay')],'capture-status')]},
 {id:'configs',label:'Configs',icon:'folder',tabs:[tab('files','Saved configs',[],'configs')]}
];
const normalize=s=>s.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase();
export function searchSettings(query){const q=normalize(query.trim());if(!q)return[];return catalog.flatMap(page=>page.tabs.flatMap(tab=>[...tab.rows,...tab.editors].filter(row=>normalize(row.label+' '+row.key+' '+tab.label+' '+(row.aliases||'')).includes(q)).map(row=>({...row,page:page.id,pageLabel:page.label,tab:tab.id,tabLabel:tab.label}))));}
export const weapons=[['Rifles','Rifles'],['SMG','SMGs'],['Pistolas','Pistols'],['Francotiradores','Snipers'],['Escopetas','Shotguns'],['Pesadas','Heavy'],['Otras','Other'],...[[7,'AK-47'],[16,'M4A4'],[60,'M4A1-S'],[8,'AUG'],[39,'SG 553'],[10,'FAMAS'],[13,'Galil AR'],[9,'AWP'],[40,'SSG 08'],[11,'G3SG1'],[38,'SCAR-20'],[1,'Desert Eagle'],[4,'Glock-18'],[61,'USP-S'],[32,'P2000'],[36,'P250'],[3,'Five-SeveN'],[30,'Tec-9'],[2,'Dual Berettas'],[63,'CZ75-Auto'],[64,'Revolver'],[17,'MAC-10'],[34,'MP9'],[33,'MP7'],[23,'MP5-SD'],[24,'UMP-45'],[19,'P90'],[26,'PP-Bizon'],[25,'XM1014'],[27,'MAG-7'],[29,'Sawed-Off'],[35,'Nova'],[14,'M249'],[28,'Negev']].map(([id,label])=>['arma:'+id,label])];
