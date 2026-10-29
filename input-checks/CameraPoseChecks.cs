using System.Buffers.Binary;
using System.Numerics;
using CsEsp;
internal static class CameraPoseChecks
{
 internal static void Run()
 {
  int count=0;void Check(bool value,string why){if(!value)throw new Exception(why);count++;}
  Check(CameraSourceProfile.TryResolve(14193,"08A741030C1E569D8B854C5886A87FCAFC21D0D0E2F05DFD380E8A351171501D",0xf60,0x330,0xc8,out var profile)&&profile.GlobalsRva==0x21c30a0,"14193 requires its separately audited clock globals");
  Check(!CameraSourceProfile.TryResolve(14193,"CCAF4E4926BABC052994502B19EC61198A11008A39DCE712921B93DD993BE9F5",0xf60,0x330,0xc8,out _),"Old source hash must not authorize a newer build");
  Check(!CameraSourceProfile.TryResolve(14193,"08A741030C1E569D8B854C5886A87FCAFC21D0D0E2F05DFD380E8A351171501D",0xf64,0x330,0xc8,out _),"Changed wrapper schema must reject the profile");
  Check(!CameraSourceProfile.TryResolve(14194,"08A741030C1E569D8B854C5886A87FCAFC21D0D0E2F05DFD380E8A351171501D",0xf60,0x330,0xc8,out _),"Unknown builds must not inherit camera support");
  static void I(byte[] b,int o,int n)=>BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(o,4),n);
  static void F(byte[] b,int o,float n)=>I(b,o,BitConverter.SingleToInt32Bits(n));
  static void V(byte[] b,int o,Vector3 v){F(b,o,v.X);F(b,o+4,v.Y);F(b,o+8,v.Z);}
  var g=new byte[16];var w=new byte[0xc1];var s=new byte[0x114];
  // Real observed base-mode fixture: mode0/mode2=0/flag192, yaw-only transform.
  w[0x98]=192;w[0xc0]=1;V(w,0,new(0,0,64));V(s,0xc8,new(-22.865917f,-1611.9657f,-134.01562f));F(s,0x1c,1);F(s,0x28,-.8808355f);F(s,0x2c,.4734225f);
  bool Decode(byte[] ga,byte[] wa,byte[] sa,byte[] gb,byte[] wb,byte[] sb,bool verified,out Vector3 eye)=>CameraPose14192.TryDecode(ga,wa,sa,gb,wb,sb,verified,out eye);
  bool Same(out Vector3 eye)=>Decode(g,w,s,g,w,s,true,out eye);
  Check(Same(out var eye)&&Vector3.Distance(eye,new(-22.865917f,-1611.9657f,-70.015625f))<.001,"Observed base fixture must decode without inventing eye height");
  Check(!Decode(g,w,s,g,w,s,false,out eye)&&eye==default,"Unverified source contract must remain unknown");
  foreach(int which in new[]{0,1,2}){var gb=(byte[])g.Clone();var wb=(byte[])w.Clone();var sb=(byte[])s.Clone();(which==0?gb:which==1?wb:sb)[0]^=1;Check(!Decode(g,w,s,gb,wb,sb,true,out eye)&&eye==default,"Mixed repeated captures must remain unknown");}
  Check(!Decode(g[..15],w,s,g,w,s,true,out eye),"Incomplete globals must be rejected");Check(!Decode(g,w[..0xc0],s,g,w,s,true,out eye),"Missing interpolation flag must be rejected");Check(!Decode(g,w,s[..0x110],g,w,s,true,out eye),"Missing dirty byte must be rejected");
  s[0x110]=1;Check(!Same(out eye),"Dirty scene requires engine recomputation and must remain unknown");s[0x110]=0;
  F(s,0x1c,0);Check(!Same(out eye),"Zero scale must fail");F(s,0x1c,1);
  F(s,0x2c,2);Check(!Same(out eye),"Unnormalized quaternion must fail");F(s,0x2c,.4734225f);
  // Cache0 selected by mode=-1; exact clock chosen by flag bit7.
  I(g,0,-1);F(g,8,461.78125f);F(g,12,461.796875f);V(w,0x30,new(0,0,63.5f));F(w,0x80,461.796875f);
  Check(Same(out eye)&&Math.Abs(eye.Z-(-70.515625f))<.001,"Matching cached time must use cached offset, not base");
  F(w,0x80,461.80966f);Check(!Same(out eye),"Observed stale cache time must remain unknown");F(w,0x80,float.NaN);Check(!Same(out eye),"NaN cache time must fail");
  F(g,12,-1);F(w,0x80,-1);Check(!Same(out eye),"Uninitialized negative clock/cache sentinels must fail even when equal");F(g,12,461.796875f);
  F(w,0x80,461.796875f);I(g,0,1);I(g,4,1);w[0x98]=224;V(w,0x58,new(0,0,62));F(w,0x84,461.796875f);
  Check(Same(out eye)&&Math.Abs(eye.Z-(-72.015625f))<.001,"Mode1 bit5 set must select cache1");
  w[0x98]=192;Check(Same(out eye)&&Math.Abs(eye.Z-(-70.515625f))<.001,"Mode1 bit5 clear must select cache0");
  w[0xc0]=0;F(w,0x80,float.NaN);Check(Same(out eye)&&Math.Abs(eye.Z-(-70.015625f))<.001,"Disabled interpolation uses base without requiring unused cache timestamp");
  V(w,0,new(float.NaN,0,64));Check(!Same(out eye)&&eye==default,"Nonfinite offset must fail");
  // Pitch rotation proves transform is reproduced rather than merely origin+offset.
  V(w,0,new(0,0,10));F(s,0x20,0);F(s,0x24,MathF.Sqrt(.5f));F(s,0x28,0);F(s,0x2c,MathF.Sqrt(.5f));
  Check(Same(out eye)&&Vector3.Distance(eye,new(-12.865917f,-1611.9657f,-134.01562f))<.001,"Quaternion rotation must transform camera offset");
  Console.WriteLine($"{count} pure camera-pose checks passed; repeated equality is not atomic capture.");
 }
}

