using CsEsp;
using System.Diagnostics;
using System.Numerics;
int passed=0;
void Check(bool value,string message){if(!value)throw new Exception(message);passed++;}
Frame Enemy(int id=64,float distance=10)=>new([new Player(new(100,0,0),100,3,"Bot",7,30,[new(100,0,64),new(100,0,64),new(100,0,64),new(100,0,64),new(100,0,64),new(100,0,64),new(100,0,64)],distance){EntityId=id}],new float[16],default,0,"Fixture",0,1,1){Local=new(){Alive=true,Eye=new(0,0,64),Weapon=7,Ammo=30,CrosshairEntity=id}};
EspSettings Config(AimProfile profile)=>new(){AimProfiles=new(){["Rifles"]=profile}};
var enabled=new AimProfile{Trigger=true,TriggerKey=6,ShootDelay=120};
try{
{var skinSettings=System.Text.Json.JsonSerializer.Deserialize<EspSettings>("{\"SkinLoadout\":{\"version\":1,\"weapons\":{\"7\":1171}}}")!.Normalize();
Check(skinSettings.SkinLoadout.Weapons["7"]==1171,"Native settings must preserve skin selections");
string skinFile=Path.Combine(Path.GetTempPath(),"nexo-skin-"+Guid.NewGuid().ToString("N")+".json");
try{skinSettings.Save(skinFile);Check(EspSettings.LoadStrict(skinFile).SkinLoadout.Weapons["7"]==1171,"Skin CFG must survive a native save/load cycle");}finally{if(File.Exists(skinFile))File.Delete(skinFile);}
bool rejected=false;try{System.Text.Json.JsonSerializer.Deserialize<EspSettings>("{\"SkinLoadout\":{\"version\":1,\"weapons\":{\"7\":344}}}")!.Normalize();}catch(System.Text.Json.JsonException){rejected=true;}
Check(rejected,"Native settings must reject a paint for another weapon");Check(new EspSettings().Normalize().SkinLoadout.Weapons.Count==0,"Old settings must retain original skins");}
{var headBones=new Vector3[7];headBones[5]=new(10,20,58);headBones[6]=new(10,20,64);
Check(RenderMath.TryHeadMarker(headBones,out var marker)&&marker.Z>64&&marker.Z<69,"Head marker must sit above the joint pivot");
headBones[6]=headBones[5];Check(!RenderMath.TryHeadMarker(headBones,out _),"Collapsed head/neck must not produce a guessed marker");
headBones[6]=new(float.NaN,0,0);Check(!RenderMath.TryHeadMarker(headBones,out _),"Invalid head coordinates must be rejected");}
if(args.Length==1&&args[0]=="--decoded-penetration-check"){DecodedPenetrationChecks.Run();return 0;}
if(args.Length==1&&args[0]=="--camera-ray-check"){CameraRayChecks.Run();return 0;}
if(args.Length==1&&args[0]=="--camera-pose-check"){CameraPoseChecks.Run();return 0;}
if(args.Length==1&&args[0]=="--collision-filter-check"){CollisionAttributePolicyChecks.Run();return 0;}
if(args.Length==1&&args[0]=="--skin-transaction-check"){SkinTransactionChecks.Run();return 0;}
if(args.Length==2&&args[0]=="--game-readonly"){
    using var reader=new GameReader(args[1]);Frame sample=Frame.Empty;int maxPlayers=0,maxBones=0,projectableHeads=0,validMatrices=0;
    for(int i=0;i<20;i++){
        sample=reader.Sample(skeleton:true,world:true,fullWorld:true,includeTeammates:true,movement:true);
        maxPlayers=Math.Max(maxPlayers,sample.Players.Length);maxBones=Math.Max(maxBones,sample.BonesValid);
        if(sample.Matrix.Length==16&&sample.Matrix.All(float.IsFinite)&&sample.Matrix.Any(v=>Math.Abs(v)>.001f))validMatrices++;
        projectableHeads=Math.Max(projectableHeads,sample.Players.Count(p=>RenderMath.TryHeadMarker(p.Bones,out var center)&&Projection.ToScreen(sample.Matrix,center,1920,1080,out _)));
        await Task.Delay(50);
    }
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{readOnly=true,gameWrites=false,inputSent=false,samples=20,alive=sample.Local.Alive,weapon=sample.Local.Weapon,ammo=sample.Local.Ammo,movementValid=sample.Local.MovementValid,validMatrices,maxPlayers,maxBones,projectableHeads,worldItems=sample.World.Length,cameraPoseVerified=sample.Local.EyeValid,cameraStatus=reader.CameraPoseStatus,status=sample.Status}));
    return sample.Local.Alive&&validMatrices==20&&maxPlayers>0&&maxBones>0?0:2;
}
if(args.Length==4&&args[0]=="--collision-ray-readonly"){
    var catalogue=WeaponSourceCatalog.Load(Path.Combine(args[3],"item-links.json"),Path.Combine(args[3],"weapons.json"));
    var geometry=CollisionGeometry.Load(args[2]);using var reader=new GameReader(args[1]);var frame=reader.Sample(skeleton:false,world:false,fullWorld:false);var source=catalogue.Find(frame.Local.Weapon);
    float range=(float)Math.Min(source?.Range??2048,8192);
    if(!CameraRay.TryBuild(frame.Local,range,out var from,out var to)){Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{readOnly=true,noInputSent=true,damagePredictionEnabled=false,status=frame.Status,reason=!frame.Local.Alive?"Local player is not alive; an active practice player is required":!frame.Local.EyeValid?"Source camera is unavailable; fallback positions cannot validate geometry":!frame.Local.AnglesValid?"View angles are unavailable":"Source camera or range failed numeric validation",alive=frame.Local.Alive,cameraStatus=reader.CameraPoseStatus,eyeValid=frame.Local.EyeValid,anglesValid=frame.Local.AnglesValid}));return 2;}
    var trace=geometry.TraceSegment(from,to);
    var policies=new Dictionary<int,CollisionAttributePolicy.Result>();
    foreach(var hull in trace.HullIntervals)policies.TryAdd(hull.CollisionAttributeIndex,CollisionAttributePolicy.Evaluate(hull.CollisionAttributes));
    foreach(var hit in trace.MeshHits)policies.TryAdd(hit.CollisionAttributeIndex,CollisionAttributePolicy.Evaluate(hit.CollisionAttributes));
    object Point(Vector3 p)=>new{x=p.X,y=p.Y,z=p.Z};
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{readOnly=true,noInputSent=true,damagePredictionEnabled=false,declaredMap=geometry.Map,mapNotAutomaticallyVerified=true,itemDefinitionId=frame.Local.Weapon,firearmSource=source?.IsBallisticFirearm??false,from=Point(from),to=Point(to),trace.CandidateTests,meshClosureKnown=trace.MeshClosureKnown,staticHullIntervals=trace.HullIntervals.Select(h=>new{h.EntryT,h.ExitT,h.Thickness,h.SurfaceHash,h.CollisionAttributeIndex,h.StartsInside,h.EndsInside,h.HasPhysicalExit,h.HullIndex,h.IsTangent,bulletQueryParticipation=policies[h.CollisionAttributeIndex].Decision.ToString()}),meshHits=trace.MeshHits.Select(h=>new{h.T,h.MeshIndex,h.TriangleIndex,h.SurfaceHash,h.CollisionAttributeIndex,h.FrontFacing,bulletQueryParticipation=policies[h.CollisionAttributeIndex].Decision.ToString()}),collisionPolicies=policies.Select(p=>new{attribute=p.Key,decision=p.Value.Decision.ToString(),p.Value.Reason}),classification="Unknown",reason="Live dynamic collision, numeric interaction flags and mesh closure are not yet validated",failedReads=reader.FailedReads}));return 0;
}
if(args.Length==3&&args[0]=="--weapon-source-readonly"){
    var catalogue=WeaponSourceCatalog.Load(Path.Combine(args[2],"item-links.json"),Path.Combine(args[2],"weapons.json"));
    using var reader=new GameReader(args[1]);var frame=reader.Sample(skeleton:false,world:false,fullWorld:false);
    var source=catalogue.Find(frame.Local.Weapon);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{readOnly=true,noInputSent=true,damagePredictionEnabled=false,status=frame.Status,alive=frame.Local.Alive,itemDefinitionId=frame.Local.Weapon,sourceMatched=source is not null,firearmSource=source?.IsBallisticFirearm??false,source,failedReads=reader.FailedReads}));
    return source is null?2:0;
}
if((args.Length==3&&args[0]=="--radar-live-readonly")||(args.Length==4&&args[0]=="--radar-live-gsi")){
    using var reader=new GameReader(args[1]);object readLock=new();
    Frame Snapshot(){lock(readLock)return reader.Sample(skeleton:false,world:false,fullWorld:false);}
    bool gsi=args[0]=="--radar-live-gsi";
    string gameFolder=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(reader.Process.MainModule!.FileName)!,"..","..","csgo"));
    using var feed=new RadarMapFeed(gameFolder,gsi?args[3]:Path.GetTempPath());
    using var radar=new WebRadar(Snapshot,mapsDirectory:args[2],mapSource:gsi?()=>feed.Current:null,startFeed:gsi?feed.Start:null,feedStatus:gsi?()=>feed.Status:()=>"Live read-only check · select the current CS2 map manually");
    await radar.Start();string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".."));
    File.WriteAllText(Path.Combine(root,"work","radar-live-readonly-url.txt"),radar.Link);
    Console.WriteLine(gsi?"Live read-only radar and authenticated GSI map feed on loopback for 15 minutes; managed GSI config only, no game memory writes, input or public tunnel.":"Live read-only radar on loopback for 15 minutes; no input, game writes or public tunnel. Select the current map manually.");
    await Task.Delay(TimeSpan.FromMinutes(15));return 0;
}
if(args.Contains("--radar-preview")){
    string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".."));
    Frame Preview(){long now=Stopwatch.GetTimestamp();return new([new(new(-1200,-500,0),100,3,"Preview enemy",7,30,[],10){EntityId=42},new(new(-1500,-900,0),84,2,"Preview teammate",16,20,[],10){IsTeammate=true,EntityId=43}],new float[16],new(-1000,-1200,0),0,"Synthetic map preview",0,now,0){Local=new(){Alive=true}};}
    using var radar=new WebRadar(Preview,mapsDirectory:Path.Combine(root,"local-assets","maps"),mapSource:()=>"de_mirage");radar.Start().GetAwaiter().GetResult();File.WriteAllText(Path.Combine(root,"work","radar-preview-url.txt"),radar.Link);Console.WriteLine("Synthetic radar preview running on loopback; no game process or input.");await Task.Delay(TimeSpan.FromMinutes(15));return 0;
}
if(args.Length==2&&args[0]=="--movement-readonly"){
    using var reader=new GameReader(args[1]);var frame=reader.Sample(skeleton:false,world:false,fullWorld:false,movement:true);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{readOnly=true,noInputSent=true,alive=frame.Local.Alive,movementValid=frame.Local.MovementValid,onGround=(frame.Local.MovementFlags&1)!=0,moveType=frame.Local.MoveType,water=frame.Local.WaterLevel,speed=new Vector2(frame.Local.Velocity.X,frame.Local.Velocity.Y).Length(),status=frame.Status}));
    return frame.Local.MovementValid?0:2;
}
using(var input=new Harness()){
 input.Keys.Add(6);input.Step(Enemy(),Config(enabled),0);input.Step(Enemy(),Config(enabled),119);Check(input.Down==0,"Trigger fired before the configured delay");input.Step(Enemy(),Config(enabled),120);Check(input.Down==1,"Trigger did not fire after the delay");input.Step(Enemy(),Config(enabled),141);Check(input.Up==1,"Trigger did not release the shot");
}
using(var input=new Harness()){
 input.Keys.Add(6);var frame=Enemy() with{Local=Enemy().Local with{CrosshairEntity=64|(9<<15)}};
 input.Step(frame,Config(enabled with{ShootDelay=0}),0);Check(input.Down==1,"Trigger must compare the entity index without handle serial bits");
}
using(var input=new Harness()){
 input.Keys.Add(6);input.Step(Enemy(),Config(enabled with{ShootDelay=0}),0);input.Keys.Remove(6);input.Step(Enemy(),Config(enabled),1);Check(input.Up==1,"Releasing the activation key must release the injected mouse button immediately");
}
foreach(var invalid in new[]{Enemy() with{Players=[]},Enemy() with{Local=Enemy().Local with{Ammo=0}},Enemy() with{Local=Enemy().Local with{CrosshairEntity=-1}},Enemy() with{Local=Enemy().Local with{Alive=false}}})using(var input=new Harness()){input.Keys.Add(6);input.Step(invalid,Config(enabled with{ShootDelay=0}),0);Check(input.Down==0,"Invalid target / ammo / life must not fire");}
using(var input=new Harness()){input.Keys.Add(6);input.Step(Enemy(),Config(enabled with{ShootDelay=0}),0,false);Check(input.Down==0,"Unfocused input must not fire");input.Step(Enemy(),Config(enabled with{ShootDelay=0}),500,true,150);Check(input.Down==0,"Stale data must not fire");}
using(var input=new Harness()){input.Step(Enemy(),Config(enabled with{ShootDelay=0}),0);Check(input.Down==0&&input.TriggerStatus=="Waiting for trigger key","Hold mode must report the missing activation key");input.Step(Enemy(),Config(enabled with{ShootDelay=0,TriggerAlways=true}),1);Check(input.Down==1,"Always mode must not require an activation key");}
using(var input=new Harness()){input.Keys.Add(6);input.Step(Enemy(),Config(enabled with{ShootDelay=0,Trigger=false}),0);Check(input.Down==0&&input.TriggerStatus=="Disabled","Disabled trigger must remain disabled");}
using(var input=new Harness()){input.Keys.Add(6);var cfg=Config(enabled with{ShootDelay=0,TriggerInterval=180});input.Step(Enemy(),cfg,0);input.Step(Enemy(),cfg,21);input.Step(Enemy(),cfg,179);Check(input.Down==1,"Shot interval must prevent early repeat");input.Step(Enemy(),cfg,180);Check(input.Down==2,"Shot interval must allow the next shot");}
using(var input=new Harness()){input.Keys.Add(6);input.Step(Enemy(),Config(enabled),0);var switched=Enemy() with{Local=Enemy().Local with{Weapon=16}};input.Step(switched,Config(enabled),120);Check(input.Down==0,"Switching weapons must restart the shot delay");input.Step(switched,Config(enabled),240);Check(input.Down==1,"Weapon switch delay must eventually complete");}
using(var input=new Harness()){input.Keys.Add(6);var cfg=Config(enabled with{ShootDelay=0,TriggerDistance=20});input.Step(Enemy(distance:25),cfg,0);Check(input.Down==0,"Trigger must respect its distance limit");input.Step(Enemy(distance:20),cfg,1);Check(input.Down==1,"Trigger distance boundary must be inclusive");}
using(var input=new Harness()){input.Keys.Add(2);var bones=Enumerable.Repeat(new Vector3(100,8.7f,64),7).ToArray();var frame=Enemy(distance:25) with{Players=[Enemy(distance:25).Players[0] with{Bones=bones}]};var cfg=Config(new(){Enabled=true,Key=2,Fov=30,Smooth=1,MaxDistance=20});input.Step(frame,cfg,16);Check(!input.Sent.Any(x=>x.Flags==1),"Aim must reject distant targets");input.Step(frame,cfg with{AimProfiles=new(){["Rifles"]=cfg.Profile(7) with{MaxDistance=0}}},32);Check(input.Sent.Any(x=>x.Flags==1),"Unlimited aim range must preserve target selection");}
using(var input=new Harness()){input.Keys.Add(6);input.Step(Enemy(),Config(enabled with{ShootDelay=0}),0);input.Keys.Remove(6);input.Accept=false;input.Step(Enemy(),Config(enabled),1);Check(input.Up==0,"Rejected releases must not be reported as successful");input.Accept=true;input.Step(Enemy(),Config(enabled),25);Check(input.Up==1,"Rejected release must be retried");}
Check(new AimProfile{MaxDistance=float.NaN,TriggerDistance=9999,TriggerInterval=0}.Normalize() is {MaxDistance:0,TriggerDistance:500,TriggerInterval:30},"Profile limits must normalize consistently");
foreach(bool changeWeapon in new[]{false,true})using(var input=new Harness()){input.Keys.Add(6);var cfg=Config(enabled with{ShootDelay=0});input.Step(Enemy(),cfg,0);var next=changeWeapon?Enemy() with{Local=Enemy().Local with{Weapon=16}}:Enemy(65);input.Step(next,cfg,1);Check(input.Up==1,"Changing target or weapon must release the previous shot immediately");}
using(var input=new Harness()){input.Keys.Add(6);input.Keys.Add(2);var friend=Enemy() with{Players=[Enemy().Players[0] with{IsTeammate=true}]};input.Step(friend,Config(enabled with{Enabled=true,Key=2,Fov=30,Smooth=1,ShootDelay=0}) with{ShowTeammates=true},20);Check(input.Sent.Count==0,"Showing teammates must not enable teammate aim or trigger");}
Check(!new EspSettings().ShowTeammates&&!new EspSettings().Enabled&&!new AimProfile().ShowFov,"New installations must start with visuals and FOV off");
Console.WriteLine($"{passed} input checks passed. All mouse/keyboard output was captured by a fake transport; no game input was sent.");
VisualChecks.Run();
MovementChecks.Run();
RuntimePathChecks.Run();
RadarHistoryChecks.Run();
CollisionGeometryChecks.Run();
WeaponSourceChecks.Run();
DecodedPenetrationChecks.Run();
CameraRayChecks.Run();
CameraPoseChecks.Run();
CollisionAttributePolicyChecks.Run();
SkinTransactionChecks.Run();
return 0;
}catch(Exception error){Console.Error.WriteLine(error);return 1;}
sealed class Harness:IDisposable{
 public HashSet<int> Keys=[];public List<Native.Input> Sent=[];public bool Accept=true;long tick=Stopwatch.Frequency;readonly InputController input;
 public string TriggerStatus=>input.TriggerStatus;
 public int Down=>Sent.Count(x=>x.Flags==2);public int Up=>Sent.Count(x=>x.Flags==4);
 public Harness(){input=new(key=>Keys.Contains(key),items=>{if(!Accept)return false;Sent.AddRange(items);return true;},()=>tick);}
 public void Step(Frame frame,EspSettings settings,int milliseconds,bool focused=true,int ageMs=0){tick=Stopwatch.Frequency+(long)(milliseconds*Stopwatch.Frequency/1000d);input.Update(frame with{Timestamp=tick-(long)(ageMs*Stopwatch.Frequency/1000d)},settings,focused,null);}
 public void Dispose()=>input.Dispose();
}
