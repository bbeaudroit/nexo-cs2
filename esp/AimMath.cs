using System.Numerics;
namespace CsEsp;
[Flags] public enum Hitboxes {Head=1,Neck=2,Chest=4,Pelvis=8,Legs=16}
public sealed record AimProfile
{
    public bool Enabled {get;init;}
    public int Key {get;init;}=2;
    public bool ShowFov {get;init;}
    public bool FollowRecoil {get;init;}
    public int FovColor {get;init;}=unchecked((int)0xff41d9e6);
    public int RecoilColor {get;init;}=unchecked((int)0xffffce73);
    public float Fov {get;init;}=3;
    public float Smooth {get;init;}=8;
    public float MaxDistance {get;init;}
    public bool Linear {get;init;}=true;
    public Hitboxes Hitboxes {get;init;}=Hitboxes.Head|Hitboxes.Neck|Hitboxes.Chest;
    public bool Closest {get;init;}=true;
    public bool Rcs {get;init;}
    public float RecoilX {get;init;}=1;
    public float RecoilY {get;init;}=1;
    public bool Trigger {get;init;}
    public int TriggerKey {get;init;}=6;
    public int ShootDelay {get;init;}=120;
    public int TriggerInterval {get;init;}=90;
    public bool TriggerAlways {get;init;}
    public float TriggerDistance {get;init;}
    public bool TriggerAim {get;init;}
    public int TriggerAimKey {get;init;}=6;
    public bool TriggerAimRecoil {get;init;}
    public float TriggerFov {get;init;}=2;
    public float TriggerSmooth {get;init;}=10;
    public Hitboxes TriggerHitbox {get;init;}=Hitboxes.Head;
    public AimProfile Normalize()=>this with{Key=Math.Clamp(Key,1,254),TriggerKey=Math.Clamp(TriggerKey,1,254),TriggerAimKey=Math.Clamp(TriggerAimKey,1,254),Fov=Math.Clamp(float.IsFinite(Fov)?Fov:3,.1f,30),Smooth=Math.Clamp(float.IsFinite(Smooth)?Smooth:8,1,50),MaxDistance=Math.Clamp(float.IsFinite(MaxDistance)?MaxDistance:0,0,500),TriggerDistance=Math.Clamp(float.IsFinite(TriggerDistance)?TriggerDistance:0,0,500),TriggerFov=Math.Clamp(float.IsFinite(TriggerFov)?TriggerFov:2,.1f,30),TriggerSmooth=Math.Clamp(float.IsFinite(TriggerSmooth)?TriggerSmooth:10,1,50),ShootDelay=Math.Clamp(ShootDelay,0,1000),TriggerInterval=Math.Clamp(TriggerInterval,30,1000),RecoilX=Math.Clamp(float.IsFinite(RecoilX)?RecoilX:1,0,3),RecoilY=Math.Clamp(float.IsFinite(RecoilY)?RecoilY:1,0,3),Hitboxes=Hitboxes& (Hitboxes)31,TriggerHitbox=TriggerHitbox&(Hitboxes)31};
}
public static class AimMath
{
    public static int EntityIndex(int id)=>id<=0||((id&0x7fff)==0x7fff)?-1:id&0x7fff;
    public static bool InRange(float distance,float limit)=>float.IsFinite(distance)&&distance>=0&&(limit<=0||distance<=limit);
    public static string Group(ushort id)=>id switch{7 or 8 or 10 or 13 or 16 or 39 or 60=>"Rifles",17 or 19 or 23 or 24 or 26 or 33 or 34=>"SMG",1 or 2 or 3 or 4 or 30 or 32 or 36 or 61 or 63 or 64=>"Pistolas",9 or 11 or 38 or 40=>"Francotiradores",25 or 27 or 29 or 35=>"Escopetas",14 or 28=>"Pesadas",_=>"Otras"};
    public static float Wrap(float degrees){if(!float.IsFinite(degrees))return 0;degrees%=360;if(degrees>180)degrees-=360;if(degrees< -180)degrees+=360;return degrees;}
    public static Vector2 Angles(Vector3 eye,Vector3 target){var d=target-eye;return new(-MathF.Atan2(d.Z,MathF.Sqrt(d.X*d.X+d.Y*d.Y))*180/MathF.PI,MathF.Atan2(d.Y,d.X)*180/MathF.PI);}
    public static Vector2 Delta(Vector2 desired,Vector2 current)=>new(Wrap(desired.X-current.X),Wrap(desired.Y-current.Y));
    public static Vector2 Step(Vector2 delta,float smooth,float dt,bool linear)
    {
        if(!float.IsFinite(delta.X)||!float.IsFinite(delta.Y)||dt<=0)return default;smooth=Math.Max(1,smooth);dt=Math.Clamp(dt,.001f,.05f);
        if(!linear)return delta*(1-MathF.Exp(-120*dt/smooth));
        float max=180*dt/smooth,length=delta.Length();return length>max?delta*(max/length):delta;
    }
    public static int[] Joints(Hitboxes boxes,int count)
    {
        var result=new List<int>();if(boxes.HasFlag(Hitboxes.Head))result.Add(6);if(boxes.HasFlag(Hitboxes.Neck))result.Add(5);if(boxes.HasFlag(Hitboxes.Chest))result.Add(4);if(boxes.HasFlag(Hitboxes.Pelvis))result.Add(count==23?1:0);if(boxes.HasFlag(Hitboxes.Legs))result.AddRange(count==23?[19,22]:[24,27]);return result.ToArray();
    }
}
