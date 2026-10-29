using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
public static partial class SkinBridgeObservationLoader {
 static byte[] LocalHeader(){byte[] b=Header(80);Buffer.BlockCopy(BitConverter.GetBytes(1171u),0,b,8,4);return b;}
 static uint LocalField(byte[] b,int n){return BitConverter.ToUInt32(b,n*4);}
 static string LocalReport(byte[] b,uint cleanup){
  return "{\"experimentalLocalTest\":true,\"visualChangeVerified\":false,\"state\":"+LocalField(b,3)+",\"status\":"+LocalField(b,4)+",\"dirty\":"+LocalField(b,5)+",\"hooked\":"+LocalField(b,7)+",\"attributeCount\":"+LocalField(b,8)+",\"paintKit\":"+LocalField(b,9)+",\"pendingCount\":"+LocalField(b,10)+",\"materialCount\":"+LocalField(b,11)+",\"stages\":"+BitConverter.ToUInt64(b,48)+",\"requestsObserved\":"+BitConverter.ToUInt64(b,56)+",\"cleanupStatus\":"+cleanup+",\"restartToUnload\":true}";
 }
 public static string RunLocalProbe(int pid,long created,string dll,string action){
  if(IntPtr.Size!=8||!(action=="Start"||action=="Poll"||action=="Restore"||action=="Shutdown"))throw new InvalidOperationException("Invalid local probe action or host");
  Process target=Process.GetProcessById(pid);if(target.ProcessName!="cs2"||target.StartTime.ToUniversalTime().ToFileTimeUtc()!=created)throw new InvalidOperationException("Expected local CS2 generation unavailable");
  string[] names={"NexoLocalStart","NexoLocalPoll","NexoLocalRequestRestore","NexoLocalShutdown"};long[] offsets=new long[4];
  IntPtr mapped=LoadLibraryExW(dll,IntPtr.Zero,1);if(mapped==IntPtr.Zero)throw Error("Local probe export mapping failed");
  try{for(int i=0;i<4;i++){IntPtr p=GetProcAddress(mapped,names[i]);if(p==IntPtr.Zero)throw new InvalidOperationException("Probe export missing");offsets[i]=p.ToInt64()-mapped.ToInt64();if(offsets[i]<0||offsets[i]>0x1000000)throw new InvalidOperationException("Probe export outside image");}}finally{FreeLibrary(mapped);}
  using(Session session=new Session(pid,created)){
   ProcessModule loaded=FindModule(target,dll);if(action=="Start"&&loaded!=null)throw new InvalidOperationException("Local probe already loaded. Restore/poll the existing session or restart local CS2.");
   if(loaded==null){if(action!="Start")throw new InvalidOperationException("No local probe session exists");session.Call(RemoteLoadLibrary(target),Encoding.Unicode.GetBytes(System.IO.Path.GetFullPath(dll)+"\0"),false);loaded=FindModule(target,dll);if(loaded==null)throw new InvalidOperationException("Local probe did not load");}
   IntPtr[] calls=new IntPtr[4];for(int i=0;i<4;i++){if(offsets[i]>=loaded.ModuleMemorySize)throw new InvalidOperationException("Probe export offset outside loaded module");calls[i]=new IntPtr(checked(loaded.BaseAddress.ToInt64()+offsets[i]));}
   byte[] snap=LocalHeader();uint cleanup=UInt32.MaxValue;
   try{
    uint code=0;if(action=="Start")code=session.Call(calls[0],snap,true);else if(action=="Restore")code=session.Call(calls[2],null,false);else if(action=="Shutdown")cleanup=code=session.Call(calls[3],null,false);
    if(code!=0)throw new InvalidOperationException("Local probe action rejected: "+code);
    Stopwatch watch=Stopwatch.StartNew();do{
     snap=LocalHeader();code=session.Call(calls[1],snap,true);if(code!=0)throw new InvalidOperationException("Local probe snapshot rejected: "+code);
     uint state=LocalField(snap,3),dirty=LocalField(snap,5);
     if(action=="Poll"||action=="Shutdown")break;
     if(action=="Start"&&(state==4||state==7||state==8))break;
     if(action=="Restore"&&state==6&&dirty==0)break;
     Thread.Sleep(100);
    }while(watch.ElapsedMilliseconds<10000);
    if(LocalField(snap,5)==0&&(action=="Restore"||action=="Start"&&LocalField(snap,3)==7)){
     cleanup=session.Call(calls[3],null,false);snap=LocalHeader();session.Call(calls[1],snap,true);
     if(cleanup!=0)throw new InvalidOperationException("Local probe hook cleanup not confirmed: "+cleanup);
    }
    return LocalReport(snap,cleanup);
   }catch(Exception e){
    // Never unload a pinned DLL or free live arguments after an uncertain call.
    // Ask the already-validated local module to restore if communication remains safe.
    if(!session.Uncertain){try{session.Call(calls[2],null,false);session.Call(calls[3],null,false);}catch{}}
    throw new InvalidOperationException(e.Message+". Restoration may still be pending; keep the original AK equipped and poll/restore, or close the local game before retrying.",e);
   }
  }
 }
}
