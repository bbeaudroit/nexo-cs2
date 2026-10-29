using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
namespace CsEsp;
internal sealed partial class GameReader
{
 bool cameraSourceVerified;long cameraGlobalsRva;
 internal string CameraPoseStatus {get;private set;}="Camera source not verified";
 void InitializeCameraSource(ProcessModule module,int build,int viewOffset)
 {
  cameraSourceVerified=false;cameraGlobalsRva=0;
  try{
   using var file=File.OpenRead(module.FileName);
   cameraSourceVerified=CameraSourceProfile.TryResolve(build,Convert.ToHexString(SHA256.HashData(file)),viewOffset,sceneOffset,originOffset,out var profile);
   if(cameraSourceVerified)cameraGlobalsRva=profile!.GlobalsRva;
   CameraPoseStatus=cameraSourceVerified?"Audited camera source ready":"Client camera source changed; revalidation required";
  }catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception){CameraPoseStatus="Client camera source cannot be verified";}
 }
 bool TryReadCameraEye(long pawn,out Vector3 eye)
 {
  eye=default;if(!cameraSourceVerified)return false;
  for(int attempt=0;attempt<2;attempt++){
   long scene=Pointer(pawn+sceneOffset);if(scene==0)break;
   var ga=Bytes((long)client+cameraGlobalsRva,16);var wa=Bytes(pawn+0xf60,0xc1);var sa=Bytes(scene,0x114);
   var gb=Bytes((long)client+cameraGlobalsRva,16);var wb=Bytes(pawn+0xf60,0xc1);var sb=Bytes(scene,0x114);
   if(Pointer(pawn+sceneOffset)!=scene||ga is null||wa is null||sa is null||gb is null||wb is null||sb is null)continue;
   if(CameraPose14192.TryDecode(ga,wa,sa,gb,wb,sb,true,out eye)){CameraPoseStatus="Audited view pose from matching repeated reads";return true;}
  }
  CameraPoseStatus="View pose changed, dirty or interpolation cache stale";return false;
 }
}
