using CsEsp;
using System.Diagnostics;
using System.Numerics;

internal static class MovementChecks
{
    internal static void Run()
    {
        int passed=0;void Check(bool ok,string text){if(!ok)throw new Exception("Movement: "+text);passed++;}
        var settings=new EspSettings{BunnyHop=true,MovementKey=5,JumpKey=32};
        using(var h=new Harness()){h.Held.Add(5);h.Step(settings,0);Check(h.Down(32)==1,"held assist must jump on valid ground");h.Step(settings,10);Check(h.Down(32)==1,"duplicate grounded snapshot must not spam jump");h.Step(settings,21);Check(h.Up(32)==1,"jump pulse must release");h.Step(settings,40,ground:false);h.Step(settings,100);Check(h.Down(32)==2,"new landing must allow another jump");h.Held.Clear();h.Step(settings,105);Check(h.Up(32)==2,"releasing activation must release an owned jump");}
        foreach(string pause in new[]{"disabled","focus","stale","dead","invalid","ladder","water","frozen"})using(var h=new Harness()){
            h.Held.Add(5);h.Step(settings,0);h.Step(pause=="disabled"?new EspSettings():settings,5,focused:pause!="focus",age:pause=="stale"?121:0,alive:pause!="dead",valid:pause!="invalid",mode:pause=="ladder"?(byte)9:(byte)2,water:pause=="water"?.5f:0,extraFlags:pause=="frozen"?32u:0);
            Check(h.Up(32)==1&&!h.Controller.OwnsInput,"pause must release keys: "+pause);
        }
        foreach(string pause in new[]{"disabled","focus","stale","dead","invalid","binding","dispose"})using(var h=new Harness()){
            h.Held.Add(5);h.Step(settings,0);h.Held.Add(32);
            if(pause=="dispose")h.Controller.Dispose();else h.Step(pause=="disabled"?new EspSettings():pause=="binding"?settings with{JumpKey=74}:settings,5,focused:pause!="focus",age:pause=="stale"?121:0,alive:pause!="dead",valid:pause!="invalid");
            Check(h.Up(32)==0&&!h.Controller.OwnsInput,"physical key takeover must survive every release path: "+pause);
        }
        using(var h=new Harness()){h.Held.Add(5);h.Held.Add(32);h.Step(settings,0);Check(h.Sent.Count==0,"manual jump must retain control");}
        using(var h=new Harness()){h.Held.Add(5);h.Step(settings,0);h.Held.Add(32);h.Step(settings,5);Check(h.Up(32)==0&&!h.Controller.OwnsInput,"physical jump takeover must not be cancelled by synthetic key-up");}
        using(var h=new Harness()){h.Held.Add(5);h.Step(settings,0);h.Accept=false;h.Step(settings,25);Check(h.Controller.OwnsInput,"failed release must retain ownership for retry");h.Accept=true;h.Step(settings,30);Check(h.Up(32)==1&&!h.Controller.OwnsInput,"failed release must be retried");}
        using(var h=new Harness()){h.Held.Add(5);h.Accept=false;h.Step(settings,0);Check(!h.Controller.OwnsInput&&h.Down(32)==0,"rejected key down must never acquire ownership");}
        using(var h=new Harness()){h.Held.Add(5);h.Step(settings,0);h.Step(settings with{JumpKey=74},5);Check(h.Up(32)==1&&h.Down(74)==0,"binding changes must release old keys before starting again");}
        var strafe=settings with{BunnyHop=false,AutoStrafe=true};
        using(var h=new Harness()){h.Held.Add(5);h.Step(strafe,0,ground:false,yaw:0);h.Step(strafe,10,ground:false,yaw:2);Check(h.Down(65)==1,"left view turn must strafe left in the air");h.Step(strafe,20,ground:false,yaw:0);Check(h.Up(65)==1&&h.Down(68)==1,"direction reversal must release previous side first");h.Step(strafe,30);Check(h.Up(68)==1,"landing must release generated strafe");}
        using(var h=new Harness()){h.Held.Add(5);h.Step(strafe,0,ground:false,yaw:179);h.Step(strafe,10,ground:false,yaw:-179);Check(h.Down(65)==1&&h.Down(68)==0,"yaw wrap must use the short signed turn");}
        using(var h=new Harness()){h.Held.Add(5);h.Step(strafe,0,ground:false,yaw:0);h.Step(strafe,10,ground:false,yaw:2);h.Held.Add(65);h.Step(strafe,20,ground:false,yaw:3);Check(h.Up(65)==0&&!h.Controller.OwnsInput,"manual same-side takeover must not cancel the user's held key");}
        foreach(int manual in new[]{65,68,87,83})using(var h=new Harness()){h.Held.UnionWith([5,manual]);h.Step(strafe,0,ground:false,yaw:0);h.Step(strafe,10,ground:false,yaw:5);Check(h.Sent.Count==0,"manual direction must take priority: "+manual);}
        using(var h=new Harness()){h.Held.Add(5);h.Step(strafe,0,ground:false);h.Step(strafe,10,ground:false,yaw:5);Check(h.Down(65)==1,"auto-strafe remains available without direction arbitration");}
        using(var h=new Harness()){h.Held.Add(5);h.Step(strafe,0,ground:false,speed:1);h.Step(strafe,10,ground:false,yaw:5,speed:1);Check(h.Sent.Count==0,"stationary or invalid velocity must not produce strafe");}
        foreach(float bad in new[]{float.NaN,float.PositiveInfinity,7000f})using(var h=new Harness()){h.Held.Add(5);h.Step(settings,0,speed:bad);Check(h.Sent.Count==0,"invalid movement speed must never jump");}
        using(var h=new Harness()){h.Held.Add(5);h.Step(settings,0,mode:7);Check(h.Sent.Count==0,"noclip must not trigger movement assistance");}
        Check(!new EspSettings().BunnyHop&&!new EspSettings().AutoStrafe,"new defaults must keep movement off");
        foreach(int key in new[]{0,1,2,65,68,83,87,91,92,45,119,118})Check((new EspSettings{JumpKey=key}).Normalize().JumpKey==0,"native jump binding validation "+key);
        foreach(int key in new[]{1,2,17,18,65,68,83,87,91,92,45,119,118,32})Check((new EspSettings{MovementKey=key}).Normalize().MovementKey==0,"native assist binding validation "+key);
        Console.WriteLine($"{passed} movement checks passed using a fake keyboard transport; no game input sent.");
    }
    sealed class Harness:IDisposable
    {
        public readonly HashSet<int> Held=[];public readonly List<Native.Input> Sent=[];public bool Accept=true;long tick=Stopwatch.Frequency;public readonly MovementController Controller;
        public Harness()=>Controller=new(k=>Held.Contains(k),items=>{if(!Accept)return false;Sent.AddRange(items);return true;},()=>tick);
        public int Down(int key)=>Sent.Count(i=>i.KeyCode==key&&i.KeyFlags==0);public int Up(int key)=>Sent.Count(i=>i.KeyCode==key&&i.KeyFlags==2);
        public void Step(EspSettings settings,int ms,bool ground=true,bool focused=true,int age=0,bool alive=true,bool valid=true,byte mode=2,float water=0,uint extraFlags=0,float yaw=0,float speed=200){tick=Stopwatch.Frequency+(long)(ms*Stopwatch.Frequency/1000d);var f=Frame.Empty with{Timestamp=tick-(long)(age*Stopwatch.Frequency/1000d),Local=new(){Alive=alive,MovementValid=valid,MovementFlags=(ground?1u:0)|extraFlags,MoveType=mode,WaterLevel=water,Velocity=new(speed,0,0),Angles=new(0,yaw)}};Controller.Update(f,settings,focused);}
        public void Dispose()=>Controller.Dispose();
    }
}
