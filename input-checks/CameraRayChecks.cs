using System.Numerics;
using CsEsp;
internal static class CameraRayChecks
{
 internal static void Run(){int passed=0;void Check(bool ok,string why){if(!ok)throw new Exception(why);passed++;}
  var local=new LocalState{Alive=true,Eye=new(10,20,64),EyeValid=true,AnglesValid=true};
  Check(CameraRay.TryBuild(local,100,out var start,out var end)&&start==local.Eye&&Vector3.Distance(end,new(110,20,64))<.0001,"Zero angles point along positive X");
  Check(CameraRay.TryBuild(local with{Angles=new(0,90)},100,out start,out end)&&Vector3.Distance(end,new(10,120,64))<.0001,"Yaw ninety points along positive Y");
  Check(CameraRay.TryBuild(local with{Angles=new(30,0)},100,out start,out end)&&Math.Abs(end.Z-14)<.0001&&Math.Abs(Vector3.Distance(start,end)-100)<.0001,"Positive pitch points down and preserves source-unit length");
  foreach(var invalid in new[]{local with{Alive=false},local with{EyeValid=false},local with{AnglesValid=false},local with{Eye=new(float.NaN,0,0)},local with{Angles=new(float.NaN,0)},local with{Angles=new(100,0)}})Check(!CameraRay.TryBuild(invalid,100,out start,out end)&&start==default&&end==default,"Unknown or fallback camera must not create a trace");
  foreach(float distance in new[]{0f,-1,float.NaN,float.PositiveInfinity,10000001})Check(!CameraRay.TryBuild(local,distance,out start,out end),"Invalid distance must fail");
  Console.WriteLine($"{passed} source-camera ray checks passed.");
 }
}
