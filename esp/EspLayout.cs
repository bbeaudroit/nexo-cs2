using System.Numerics;
using System.Drawing;
namespace CsEsp;
public sealed record EspLayout
{
    public int NameX {get;init;}
    public int NameY {get;init;}
    public int WeaponX {get;init;}
    public int WeaponY {get;init;}
    public int DistanceX {get;init;}
    public int DistanceY {get;init;}
    public int HealthX {get;init;}
    public int HealthY {get;init;}
    public int ArmorX {get;init;}
    public int ArmorY {get;init;}
    static int Limit(int n)=>Math.Clamp(n,-150,150);
    public EspLayout Normalize()=>this with{NameX=Limit(NameX),NameY=Limit(NameY),WeaponX=Limit(WeaponX),WeaponY=Limit(WeaponY),DistanceX=Limit(DistanceX),DistanceY=Limit(DistanceY),HealthX=Limit(HealthX),HealthY=Limit(HealthY),ArmorX=Limit(ArmorX),ArmorY=Limit(ArmorY)};
    internal Vector2 Offset(string item,RectangleF box)
    {
        var (x,y)=item switch{"Name"=>(NameX,NameY),"Weapon"=>(WeaponX,WeaponY),"Distance"=>(DistanceX,DistanceY),"Health"=>(HealthX,HealthY),"Armor"=>(ArmorX,ArmorY),_=>(0,0)};
        return new(box.Width*x/100f,box.Height*y/100f);
    }
}
