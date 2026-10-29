using System.Numerics;
namespace CsEsp;
internal static class CameraRay
{
 internal static bool TryBuild(LocalState local,float distance,out Vector3 start,out Vector3 end)
 {
  start=end=default;
  if(!local.Alive||!local.EyeValid||!local.AnglesValid||!float.IsFinite(distance)||distance<=0||distance>10000000||!float.IsFinite(local.Eye.X)||!float.IsFinite(local.Eye.Y)||!float.IsFinite(local.Eye.Z)||!float.IsFinite(local.Angles.X)||!float.IsFinite(local.Angles.Y)||Math.Abs(local.Angles.X)>90||Math.Abs(local.Angles.Y)>360)return false;
  float pitch=local.Angles.X*MathF.PI/180,yaw=local.Angles.Y*MathF.PI/180;
  var direction=new Vector3(MathF.Cos(pitch)*MathF.Cos(yaw),MathF.Cos(pitch)*MathF.Sin(yaw),-MathF.Sin(pitch));
  start=local.Eye;end=start+direction*distance;
  if(!float.IsFinite(end.X)||!float.IsFinite(end.Y)||!float.IsFinite(end.Z)){start=end=default;return false;}return true;
 }
}
