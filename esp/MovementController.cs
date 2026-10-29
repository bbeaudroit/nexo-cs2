using System.Diagnostics;
namespace CsEsp;

// Physical key state is supplied separately from injected state. Never polls its own key-downs.
internal sealed class MovementController(Func<int,bool> held,Func<Native.Input[],bool> inject,Func<long>? clock=null):IDisposable
{
    readonly Func<long> now=clock??Stopwatch.GetTimestamp;
    int jumpOwned,sideOwned,previousJump,previousHold;
    long jumpAt,lastSnapshot;
    bool latched,hasYaw,rearm;
    float lastYaw;
    internal bool OwnsInput=>jumpOwned!=0||sideOwned!=0;
    internal bool OwnsKey(int key)=>key!=0&&(key==jumpOwned||key==sideOwned);
    internal string HopStatus {get;private set;}="Bunny hop off";
    internal string StrafeStatus {get;private set;}="Auto-strafe off";
    internal string Status=>HopStatus+" · "+StrafeStatus;
    internal bool Active {get;private set;}

    bool Key(int key,bool down)=>inject([new Native.Input{Type=1,KeyCode=(ushort)key,KeyFlags=down?0u:2u}]);
    bool Release(ref int owned)
    {
        if(owned==0)return true;
        if(held(owned)){owned=0;return true;}
        if(!Key(owned,false)){HopStatus=StrafeStatus="Windows rejected release; retrying";return false;}
        owned=0;return true;
    }
    bool ReleaseAll(){bool jump=Release(ref jumpOwned),side=Release(ref sideOwned);return jump&&side;}
    void Pause(string reason){Active=false;hasYaw=false;latched=false;HopStatus=StrafeStatus=reason;ReleaseAll();}
    internal void Update(Frame frame,EspSettings settings,bool foreground)
    {
        long tick=now();
        if(previousJump!=0&&(previousJump!=settings.JumpKey||previousHold!=settings.MovementKey))
        {previousJump=settings.JumpKey;previousHold=settings.MovementKey;rearm=true;Pause("Binding changed; release assist key");return;}
        previousJump=settings.JumpKey;previousHold=settings.MovementKey;
        if(!settings.BunnyHop&&!settings.AutoStrafe){Pause("Movement off");return;}
        if(!foreground){Pause("Paused: menu or focus");return;}
        if(Stopwatch.GetElapsedTime(frame.Timestamp,tick).TotalMilliseconds is <0 or >=120){Pause("Waiting for fresh movement data");return;}
        var local=frame.Local;
        if(!local.Alive){Pause("Waiting for a live player");return;}
        if(!local.MovementValid||!float.IsFinite(local.Angles.Y)||!float.IsFinite(local.Velocity.X)||!float.IsFinite(local.Velocity.Y)||!float.IsFinite(local.Velocity.Z)||local.Velocity.LengthSquared()>36000000||!float.IsFinite(local.WaterLevel)||local.WaterLevel is <0 or >1){Pause("Movement data unavailable");return;}
        if(local.MoveType!=2){Pause("Paused: ladder / noclip / other movement");return;}
        if(local.WaterLevel>.1f||(local.MovementFlags&(4u|32u))!=0){Pause("Paused: water or frozen player");return;}
        if(settings.MovementKey==0||settings.JumpKey==0||settings.MovementKey==settings.JumpKey){Pause("Assign distinct assist and CS2 jump keys");return;}
        if(rearm){if(held(settings.MovementKey)){Pause("Release assist key to rearm");return;}rearm=false;}
        Active=true;
        // Hand ownership to a real physical press; its eventual physical key-up will release it.
        if(jumpOwned!=0&&held(jumpOwned))jumpOwned=0;
        if(sideOwned!=0&&held(sideOwned))sideOwned=0;
        if(jumpOwned!=0&&Stopwatch.GetElapsedTime(jumpAt,tick).TotalMilliseconds>=20&&!Release(ref jumpOwned))return;
        if(!held(settings.MovementKey)){hasYaw=false;latched=false;HopStatus=StrafeStatus="Hold assist key";ReleaseAll();return;}
        bool grounded=(local.MovementFlags&1)!=0;
        if(!settings.BunnyHop){HopStatus="Bunny hop off";if(!Release(ref jumpOwned))return;}
        else if(held(settings.JumpKey)){latched=false;HopStatus="Manual jump takes priority";}
        else if(!grounded){latched=false;HopStatus="Airborne · waiting for landing";}
        else if(!latched&&jumpOwned==0)
        {
            if(!Key(settings.JumpKey,true)){HopStatus="Windows rejected jump";return;}
            jumpOwned=settings.JumpKey;jumpAt=tick;latched=true;HopStatus="Jump pulse sent";
        }
        else HopStatus="Waiting for takeoff";

        string? noStrafe=!settings.AutoStrafe?"Auto-strafe off":grounded?"On ground":held(65)||held(68)||held(87)||held(83)?"Manual movement takes priority":local.Velocity.X*local.Velocity.X+local.Velocity.Y*local.Velocity.Y<400?"Build some speed first":null;
        if(noStrafe is not null){StrafeStatus=noStrafe;hasYaw=false;Release(ref sideOwned);return;}
        if(frame.Timestamp==lastSnapshot&&hasYaw)return;
        float turn=hasYaw?AimMath.Wrap(local.Angles.Y-lastYaw):0;hasYaw=true;lastYaw=local.Angles.Y;lastSnapshot=frame.Timestamp;
        int desired=turn>.1f?65:turn<-.1f?68:0;
        if(desired!=sideOwned)
        {
            if(!Release(ref sideOwned))return;
            if(desired!=0){if(!Key(desired,true)){StrafeStatus="Windows rejected strafe";return;}sideOwned=desired;}
        }
        StrafeStatus=desired==65?"Air strafe left":desired==68?"Air strafe right":"Turn the view to strafe";
    }
    public void Dispose(){Active=false;for(int i=0;i<3&&OwnsInput;i++)ReleaseAll();}
}
