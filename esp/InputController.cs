using System.Diagnostics;
using System.Numerics;
namespace CsEsp;
internal sealed class InputController : IDisposable
{
    Vector2 fractional,lastPunch;
    long last,candidateAt,shotAt;
    readonly Func<int,bool> held;
    readonly Func<Native.Input[],bool> inject;
    readonly Func<long> clock;
    internal InputController(Func<int,bool>? held=null,Func<Native.Input[],bool>? inject=null,Func<long>? clock=null){this.held=held??(key=>(Native.GetAsyncKeyState(key)&0x8000)!=0);this.inject=inject??(inputs=>Native.SendInput((uint)inputs.Length,inputs,40)==inputs.Length);this.clock=clock??Stopwatch.GetTimestamp;last=this.clock();}
    int candidate=-1;
    ushort candidateWeapon;
    bool mouseDown;
    internal string Status {get;private set;}="Disabled";
    internal string TriggerStatus {get;private set;}="Disabled";
    bool Held(int key)=>held(key);
    bool Inject(params Native.Input[] inputs)=>inject(inputs);
    void Move(Vector2 angles,float sensitivity)
    {
        if(!float.IsFinite(angles.X)||!float.IsFinite(angles.Y))return;
        fractional+=new Vector2(-angles.Y,angles.X)/(.022f*sensitivity);
        int x=(int)Math.Clamp(fractional.X,-150,150),y=(int)Math.Clamp(fractional.Y,-150,150);fractional-=new Vector2(x,y);
        if((x!=0||y!=0)&&!Inject(new Native.Input{Type=0,X=x,Y=y,Flags=1}))Status="Mouse input blocked by Windows";
    }
    internal void Update(Frame frame,EspSettings settings,bool foreground,GrenadeSpot? grenade)
    {
        long now=clock();float dt=(float)Stopwatch.GetElapsedTime(last,now).TotalSeconds;last=now;
        if(mouseDown&&Stopwatch.GetElapsedTime(shotAt,now).TotalMilliseconds>=20&&!ReleaseShot()){TriggerStatus="Mouse input blocked by Windows";return;}
        bool fresh=Stopwatch.GetElapsedTime(frame.Timestamp,now).TotalMilliseconds<120;
        if(!foreground||!fresh||!frame.Local.Alive){Release();Status=TriggerStatus=!foreground?"Paused: menu or focus":!fresh?"Waiting for fresh data":"Waiting for a live player";return;}
        if(settings.GrenadeAlign&&grenade is not null&&Held(settings.GrenadeKey))
        {
            ReleaseShot();candidate=-1;Move(AimMath.Step(AimMath.Delta(grenade.Angles,frame.Local.Angles),8,dt,true),settings.Sensitivity*frame.Local.SensitivityScale);Status="Grenade alignment";TriggerStatus="Paused: grenade alignment";return;
        }
        var p=settings.Profile(frame.Local.Weapon);
        if(AimMath.Group(frame.Local.Weapon)=="Otras"){Release();Status=TriggerStatus="Unsupported weapon";return;}
        bool triggerAim=p.TriggerAim&&Held(p.TriggerAimKey),aim=p.Enabled&&Held(p.Key);
        if(aim||triggerAim)
        {
            Hitboxes hitboxes=triggerAim?p.TriggerHitbox:p.Hitboxes;float fov=triggerAim?p.TriggerFov:p.Fov,smooth=triggerAim?p.TriggerSmooth:p.Smooth;Vector2? best=null;float distance=fov;
            foreach(var enemy in frame.Players)
            {
                if(enemy.IsTeammate||enemy.Bones.Length==0||!AimMath.InRange(enemy.Distance,p.MaxDistance))continue;
                foreach(int joint in AimMath.Joints(hitboxes,enemy.Bones.Length))
                {
                    var desired=AimMath.Angles(frame.Local.Eye,enemy.Bones[joint]);
                    if((triggerAim?p.TriggerAimRecoil:p.Rcs)&&frame.Local.RecoilValid)desired-=new Vector2(frame.Local.Recoil.X*p.RecoilY,frame.Local.Recoil.Y*p.RecoilX)*2;
                    var delta=AimMath.Delta(desired,frame.Local.Angles);float d=delta.Length();if(d>=distance)continue;distance=d;best=delta;if(!p.Closest)break;
                }
            }
            if(best is not null){Move(AimMath.Step(best.Value,smooth,dt,p.Linear),settings.Sensitivity*frame.Local.SensitivityScale);Status="Target acquired";}else Status="No target within FOV / distance";
        }
        else if(p.Rcs&&frame.Local.RecoilValid&&frame.Local.Shots>1&&Held(1))
        {
            Vector2 delta=frame.Local.Recoil-lastPunch;Move(-new Vector2(delta.X*p.RecoilY,delta.Y*p.RecoilX)*2,settings.Sensitivity*frame.Local.SensitivityScale);Status="Recoil control";
        }
        else{fractional=default;Status=p.Enabled?"Waiting for aim key":"Disabled";}
        lastPunch=frame.Local.RecoilValid?frame.Local.Recoil:default;
        int targetId=AimMath.EntityIndex(frame.Local.CrosshairEntity);
        var target=targetId>0?frame.Players.FirstOrDefault(e=>!e.IsTeammate&&e.EntityId==targetId):null;
        string? reason=!p.Trigger?"Disabled":!p.TriggerAlways&&!Held(p.TriggerKey)?"Waiting for trigger key":frame.Local.Ammo<=0?"No ammunition":target is null?"No enemy under crosshair":!AimMath.InRange(target.Distance,p.TriggerDistance)?"Target outside distance limit":null;
        if(reason is not null){candidate=-1;ReleaseShot();TriggerStatus=reason;return;}
        if(candidate!=targetId||candidateWeapon!=frame.Local.Weapon){if(!ReleaseShot()){TriggerStatus="Mouse input blocked by Windows";return;}candidate=targetId;candidateWeapon=frame.Local.Weapon;candidateAt=now;}
        TriggerStatus=mouseDown?"Firing":Held(1)?"Manual fire": "Waiting for shot delay";
        if(!mouseDown&&!Held(1)&&Stopwatch.GetElapsedTime(candidateAt,now).TotalMilliseconds>=p.ShootDelay&&Stopwatch.GetElapsedTime(shotAt,now).TotalMilliseconds>=p.TriggerInterval)
        {
            if(Inject(new Native.Input{Type=0,Flags=2})){mouseDown=true;shotAt=now;candidateAt=now;TriggerStatus="Firing";}else TriggerStatus="Mouse input blocked by Windows";
        }
    }
    bool ReleaseShot(){if(!mouseDown)return true;if(!Inject(new Native.Input{Type=0,Flags=4}))return false;mouseDown=false;return true;}
    void Release(){ReleaseShot();candidate=-1;fractional=default;lastPunch=default;}
    public void Dispose()=>Release();
}
