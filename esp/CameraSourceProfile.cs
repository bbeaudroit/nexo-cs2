using System.Diagnostics.CodeAnalysis;
namespace CsEsp;
internal sealed record CameraSourceProfile(int Build,long GlobalsRva)
{
 internal static bool TryResolve(int build,string hash,int wrapper,int scene,int origin,[NotNullWhen(true)]out CameraSourceProfile? profile)
 {
  profile=null;if(wrapper!=0xf60||scene!=0x330||origin!=0xc8)return false;
  if(build==14192&&hash.Equals("CCAF4E4926BABC052994502B19EC61198A11008A39DCE712921B93DD993BE9F5",StringComparison.OrdinalIgnoreCase))profile=new(build,0x21c10a0);
  if(build==14193&&hash.Equals("08A741030C1E569D8B854C5886A87FCAFC21D0D0E2F05DFD380E8A351171501D",StringComparison.OrdinalIgnoreCase))profile=new(build,0x21c30a0);
  return profile is not null;
 }
}
