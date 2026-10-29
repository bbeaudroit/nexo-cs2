using System.Buffers.Binary;
using System.Numerics;
namespace CsEsp;

// Valid only for the locally audited build/source contract. No process access occurs here.
internal static class CameraPose14192
{
 internal const int GlobalsLength=16,WrapperLength=0xc1,SceneLength=0x114;
 // globals selected by verified CameraSourceProfile; wrapper: pawn+0xF60; scene: scene-node+0.
 // A and B must be separately acquired complete reads of the same addresses/identity.
 // Equality detects observed changes; it does not establish an atomic capture.
 // Shared layout audited in 14192 and 14193. Caller verifies selected build/hash/schema and identities.
 internal static bool TryDecode(ReadOnlySpan<byte> globalsA,ReadOnlySpan<byte> wrapperA,ReadOnlySpan<byte> sceneA,
  ReadOnlySpan<byte> globalsB,ReadOnlySpan<byte> wrapperB,ReadOnlySpan<byte> sceneB,
  bool verifiedBuildSchemaAndSource,out Vector3 eye)
 {
  eye=default;
  if(!verifiedBuildSchemaAndSource||globalsA.Length<GlobalsLength||wrapperA.Length<WrapperLength||sceneA.Length<SceneLength||
   !globalsA.SequenceEqual(globalsB)||!wrapperA.SequenceEqual(wrapperB)||!sceneA.SequenceEqual(sceneB)||sceneA[0x110]!=0)return false;
  int mode=Int(globalsA,0),mode2=Int(globalsA,4),flag=wrapperA[0x98],index=-1;
  if(mode is <-1 or >1)return false;
  if(wrapperA[0xc0]!=0){
   if(mode==-1)index=0;
   else if(mode==0)index=mode2==1?0:((flag>>5)&1)*2-1;
   else if(mode2==1)index=1;
  }
  if(index==1&&((flag>>5)&1)==0)index=0;
  int vectorOffset=0;
  if(index>=0){
   int clockOffset=8+(flag>>7)*4,cacheTimeOffset=0x80+index*4;
   float clock=Float(globalsA,clockOffset),cacheTime=Float(wrapperA,cacheTimeOffset);
   // Compare bits, not a tolerance: this reader must never advance/recompute the engine cache.
   if(!float.IsFinite(clock)||!float.IsFinite(cacheTime)||clock<0||cacheTime<0||Int(globalsA,clockOffset)!=Int(wrapperA,cacheTimeOffset))return false;
   vectorOffset=0x30+index*40;
  }
  Vector3 offset=Vector(wrapperA,vectorOffset),origin=Vector(sceneA,0xc8);
  float scale=Float(sceneA,0x1c);
  var rotation=new Quaternion(Float(sceneA,0x20),Float(sceneA,0x24),Float(sceneA,0x28),Float(sceneA,0x2c));
  float norm=rotation.LengthSquared();
  if(!Finite(offset)||!Finite(origin)||!float.IsFinite(scale)||scale<=0||
   !float.IsFinite(norm)||MathF.Abs(norm-1)>.002f)return false;
  // Same quaternion cross-product transform and uniform scale as audited helper 0x16A3B0.
  // Do not normalize a malformed source transform or substitute a fixed eye height.
  Vector3 q=new(rotation.X,rotation.Y,rotation.Z),twiceCross=2*Vector3.Cross(q,offset);
  Vector3 result=origin+scale*(offset+rotation.W*twiceCross+Vector3.Cross(q,twiceCross));
  if(!Finite(result))return false;
  eye=result;return true;
 }
 static int Int(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset,4));
 static float Float(ReadOnlySpan<byte> bytes,int offset)=>BitConverter.Int32BitsToSingle(Int(bytes,offset));
 static Vector3 Vector(ReadOnlySpan<byte> bytes,int offset)=>new(Float(bytes,offset),Float(bytes,offset+4),Float(bytes,offset+8));
 static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}

