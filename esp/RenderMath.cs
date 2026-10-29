using System.Numerics;
namespace CsEsp;
public static class RenderMath
{
    // Visual skull marker, not a hitbox or an aim target. head_0 is a joint pivot.
    // Place the marker between that pivot and the existing five-unit box crown.
    public static bool TryHeadMarker(Vector3[] bones,out Vector3 center)
    {
        center=default;if(bones.Length<7)return false;
        Vector3 axis=bones[6]-bones[5];float length=axis.Length();
        if(!float.IsFinite(length)||length<.5f||length>20)return false;
        center=bones[6]+axis*(2.5f/length);
        return float.IsFinite(center.X)&&float.IsFinite(center.Y)&&float.IsFinite(center.Z);
    }
    public static bool IsEnemy(byte local, byte other) => local is 2 or 3 && other is 2 or 3 && local!=other;
    public static Vector2 Radar(Vector3 local, Vector3 target, float yaw, float radius, float range)
    {
        if(range<=0 || radius<=0 || !float.IsFinite(yaw)) return default;
        float a=yaw*MathF.PI/180,dx=target.X-local.X,dy=target.Y-local.Y;
        var p=new Vector2(dx*MathF.Sin(a)-dy*MathF.Cos(a),-dx*MathF.Cos(a)-dy*MathF.Sin(a))*(radius/range);
        if(!float.IsFinite(p.X)||!float.IsFinite(p.Y))return default;
        return p.Length()>radius?Vector2.Normalize(p)*radius:p;
    }
    public static bool ValidBones(Vector3[] bones, Vector3 origin)
    {
        if(bones.Length is not(23 or 28))return false;
        int[] joints=bones.Length==23?[1,2,4,5,6,8,9,10,11,12,13,14,15,17,18,19,20,21,22]:[0,2,4,5,6,8,9,10,13,14,15,22,23,24,25,26,27];
        foreach(int i in joints)
            if(!float.IsFinite(bones[i].X)||!float.IsFinite(bones[i].Y)||!float.IsFinite(bones[i].Z)||Vector3.Distance(bones[i],origin)>160)return false;
        int pelvis=bones.Length==23?1:0,leftFoot=bones.Length==23?19:24,rightFoot=bones.Length==23?22:27;
        return bones[6].Z-origin.Z is >20 and <100 && Vector3.Distance(bones[6],bones[pelvis]) is >10 and <90 &&
            bones[leftFoot].Z-origin.Z<35 && bones[rightFoot].Z-origin.Z<35 &&
            bones[leftFoot].Z<bones[pelvis].Z && bones[rightFoot].Z<bones[pelvis].Z;
    }
    public static readonly (int,int)[] Links=[(6,5),(5,4),(4,2),(2,0),(5,8),(8,9),(9,10),(5,13),(13,14),(14,15),(0,22),(22,23),(23,24),(0,25),(25,26),(26,27)];
    public static readonly (int,int)[] CompactLinks=[(6,5),(5,4),(4,2),(2,1),(5,8),(8,9),(9,10),(10,11),(5,12),(12,13),(13,14),(14,15),(1,17),(17,18),(18,19),(1,20),(20,21),(21,22)];
    public static (int,int)[] LinksFor(int count)=>count==23?CompactLinks:Links;
    static readonly int[] BoxJoints=Links.SelectMany(link=>new[]{link.Item1,link.Item2}).Distinct().ToArray();
    static readonly int[] CompactBoxJoints=CompactLinks.SelectMany(link=>new[]{link.Item1,link.Item2}).Distinct().ToArray();
    public static int[] BoxJointsFor(int count)=>count==23?CompactBoxJoints:BoxJoints;
    public static Vector4 RadarBounds(int width,int height,int requested,int x,int y)
    {
        float size=Math.Min(requested,Math.Min(width-16,height-50));if(size<100)return default;
        return new(Math.Clamp(x,8,width-size-8),Math.Clamp(y,8,height-size-42),size,size+34);
    }
}
