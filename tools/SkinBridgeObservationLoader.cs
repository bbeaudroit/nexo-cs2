// Explicit local-practice observation test only. No skin mutation commands.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
public static partial class SkinBridgeObservationLoader {
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool CloseHandle(IntPtr handle);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetProcessTimes(IntPtr handle,out long created,out long exited,out long kernel,out long user);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryExW(string file,IntPtr reserved,uint flags);
 [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandleW(string name);
 [DllImport("kernel32.dll",CharSet=CharSet.Ansi,ExactSpelling=true)] static extern IntPtr GetProcAddress(IntPtr module,string name);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool GetModuleHandleExW(uint flags,IntPtr address,out IntPtr module);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern uint GetModuleFileNameW(IntPtr module,StringBuilder path,int size);
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr VirtualAllocEx(IntPtr process,IntPtr address,UIntPtr size,uint allocation,uint protection);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool VirtualFreeEx(IntPtr process,IntPtr address,UIntPtr size,uint kind);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool WriteProcessMemory(IntPtr process,IntPtr address,byte[] data,UIntPtr size,out UIntPtr written);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(IntPtr process,IntPtr address,byte[] data,UIntPtr size,out UIntPtr read);
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr CreateRemoteThread(IntPtr process,IntPtr attributes,UIntPtr stack,IntPtr entry,IntPtr argument,uint flags,IntPtr threadId);
 [DllImport("kernel32.dll",SetLastError=true)] static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetExitCodeThread(IntPtr thread,out uint code);
 static Exception Error(string message){return new InvalidOperationException(message+" (Win32 "+Marshal.GetLastWin32Error()+")");}
 static bool SamePath(string a,string b){return String.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase);}
 static ProcessModule FindModule(Process p,string file){p.Refresh();foreach(ProcessModule m in p.Modules)if(SamePath(m.FileName,file))return m;return null;}
 static byte[] Header(int size){byte[] b=new byte[size];Buffer.BlockCopy(BitConverter.GetBytes((uint)size),0,b,0,4);Buffer.BlockCopy(BitConverter.GetBytes(1u),0,b,4,4);return b;}
 public static int ReadBuild(int pid,long expectedCreated,long engineBase,long offset){
  IntPtr h=OpenProcess(0x1010,false,pid);if(h==IntPtr.Zero)throw Error("Read access denied");
  try{VerifyGeneration(h,expectedCreated);byte[] b=new byte[4];UIntPtr n;if(!ReadProcessMemory(h,new IntPtr(checked(engineBase+offset)),b,(UIntPtr)4,out n)||n.ToUInt64()!=4)throw Error("Build read failed");return BitConverter.ToInt32(b,0);}finally{CloseHandle(h);}
 }
 static void VerifyGeneration(IntPtr h,long expected){long created,exited,kernel,user;if(!GetProcessTimes(h,out created,out exited,out kernel,out user)||created!=expected)throw Error("Process generation changed");}
 sealed class Session:IDisposable {
  internal IntPtr Handle;internal bool Uncertain;readonly List<IntPtr> retained=new List<IntPtr>();
  internal Session(int pid,long created){Handle=OpenProcess(0x43A,false,pid);if(Handle==IntPtr.Zero)throw Error("Local test process access denied");try{VerifyGeneration(Handle,created);}catch{CloseHandle(Handle);Handle=IntPtr.Zero;throw;}}
  internal uint Call(IntPtr entry,byte[] data,bool readBack){
   IntPtr argument=IntPtr.Zero,thread=IntPtr.Zero;bool keep=false;
   try{
    if(data!=null){argument=VirtualAllocEx(Handle,IntPtr.Zero,(UIntPtr)data.Length,0x3000,4);if(argument==IntPtr.Zero)throw Error("Argument allocation failed");UIntPtr n;if(!WriteProcessMemory(Handle,argument,data,(UIntPtr)data.Length,out n)||n.ToUInt64()!=(ulong)data.Length)throw Error("Argument write failed");}
    thread=CreateRemoteThread(Handle,IntPtr.Zero,UIntPtr.Zero,entry,argument,0,IntPtr.Zero);if(thread==IntPtr.Zero)throw Error("Observation entry could not start");
    uint wait=WaitForSingleObject(thread,10000);if(wait!=0){Uncertain=true;keep=true;if(argument!=IntPtr.Zero)retained.Add(argument);throw new InvalidOperationException("Entry completion is unknown; restart the local game before another attempt. Arguments retained to avoid freeing live memory.");}
    uint code;if(!GetExitCodeThread(thread,out code))throw Error("Entry exit status unavailable");
    if(readBack&&data!=null){UIntPtr n;if(!ReadProcessMemory(Handle,argument,data,(UIntPtr)data.Length,out n)||n.ToUInt64()!=(ulong)data.Length)throw Error("Observation readback failed");}
    return code;
   }finally{if(thread!=IntPtr.Zero)CloseHandle(thread);if(argument!=IntPtr.Zero&&!keep)VirtualFreeEx(Handle,argument,UIntPtr.Zero,0x8000);}
  }
  public void Dispose(){if(Handle!=IntPtr.Zero){CloseHandle(Handle);Handle=IntPtr.Zero;}}
 }
 static IntPtr RemoteLoadLibrary(Process target){
  IntPtr entry=GetProcAddress(GetModuleHandleW("kernel32.dll"),"LoadLibraryW"),actual;
  if(entry==IntPtr.Zero||!GetModuleHandleExW(6,entry,out actual))throw Error("System loader resolution failed");
  StringBuilder file=new StringBuilder(32768);if(GetModuleFileNameW(actual,file,file.Capacity)==0)throw Error("System loader module unavailable");
  string name=Path.GetFileName(file.ToString());if(!name.Equals("kernel32.dll",StringComparison.OrdinalIgnoreCase)&&!name.Equals("kernelbase.dll",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unexpected system loader module");
  ProcessModule local=FindModule(Process.GetCurrentProcess(),file.ToString()),remote=FindModule(target,file.ToString());
  if(local==null||remote==null)throw new InvalidOperationException("Matching system module unavailable");
  long rva=entry.ToInt64()-local.BaseAddress.ToInt64();if(rva<0||rva>=local.ModuleMemorySize||rva>=remote.ModuleMemorySize)throw new InvalidOperationException("System loader offset outside matching image");
  return new IntPtr(checked(remote.BaseAddress.ToInt64()+rva));
 }
 public static string Run(int pid,long expectedCreated,string dll){
  if(IntPtr.Size!=8)throw new InvalidOperationException("64-bit host required");
  Process target=Process.GetProcessById(pid);if(target.ProcessName!="cs2"||target.StartTime.ToUniversalTime().ToFileTimeUtc()!=expectedCreated)throw new InvalidOperationException("Expected CS2 generation unavailable");
  if(FindModule(target,dll)!=null)throw new InvalidOperationException("Observation DLL already loaded; restart local CS2 before another attempt");
  string[] names={"NexoBridgeInitialize","NexoBridgeEnableObservation","NexoBridgeEnqueueObservation","NexoBridgeGetSnapshot","NexoBridgeShutdown"};long[] offsets=new long[names.Length];
  IntPtr mapped=LoadLibraryExW(dll,IntPtr.Zero,1);if(mapped==IntPtr.Zero)throw Error("Local export-only mapping failed");
  try{for(int i=0;i<names.Length;i++){IntPtr address=GetProcAddress(mapped,names[i]);if(address==IntPtr.Zero)throw new InvalidOperationException("Observation export missing");offsets[i]=address.ToInt64()-mapped.ToInt64();if(offsets[i]<0||offsets[i]>0x1000000)throw new InvalidOperationException("Invalid observation export offset");}}finally{FreeLibrary(mapped);}
  using(Session session=new Session(pid,expectedCreated)){
   bool initialized=false,enableAttempted=false;uint shutdown=UInt32.MaxValue;byte[] snapshot=Header(320);Exception failure=null;IntPtr[] calls=new IntPtr[5];
   try{
    session.Call(RemoteLoadLibrary(target),Encoding.Unicode.GetBytes(Path.GetFullPath(dll)+"\0"),false);
    ProcessModule loaded=FindModule(target,dll);if(loaded==null)throw new InvalidOperationException("Observation DLL was not found after loader completion");
    for(int i=0;i<calls.Length;i++){if(offsets[i]>=loaded.ModuleMemorySize)throw new InvalidOperationException("Export offset outside loaded observation image");calls[i]=new IntPtr(checked(loaded.BaseAddress.ToInt64()+offsets[i]));}
    uint code=session.Call(calls[0],Header(16),false);if(code!=0)throw new InvalidOperationException("Observation initialization rejected context: "+code);initialized=true;
    enableAttempted=true;code=session.Call(calls[1],null,false);if(code!=0)throw new InvalidOperationException("Observation hook was not enabled: "+code);
    byte[] observation=Header(24);Buffer.BlockCopy(BitConverter.GetBytes(1UL),0,observation,8,8);Buffer.BlockCopy(BitConverter.GetBytes(6u),0,observation,16,4);
    code=session.Call(calls[2],observation,false);if(code!=0)throw new InvalidOperationException("Observation queue rejected request: "+code);
    Stopwatch time=Stopwatch.StartNew();do{snapshot=Header(320);code=session.Call(calls[3],snapshot,true);if(code!=0)throw new InvalidOperationException("Snapshot rejected: "+code);if(BitConverter.ToUInt64(snapshot,296)>0)break;Thread.Sleep(100);}while(time.ElapsedMilliseconds<5000);
   }catch(Exception e){failure=e;}
   finally{if(initialized&&!session.Uncertain){try{shutdown=session.Call(calls[4],null,false);}catch(Exception e){if(failure==null)failure=e;}}}
   if(failure!=null)throw new InvalidOperationException(failure.Message+"; shutdownStatus="+shutdown+"; if initialization/hook was attempted, restart local CS2 before retrying.",failure);
   if(shutdown!=0)throw new InvalidOperationException("Observation cleanup was not confirmed; shutdownStatus="+shutdown+". Restart local CS2 before another attempt.");
   return "{\"observationOnly\":true,\"skinApplied\":false,\"completed\":"+BitConverter.ToUInt64(snapshot,296)+",\"mainThreadStage6\":"+BitConverter.ToUInt64(snapshot,32+128+6*8)+",\"shutdownStatus\":"+shutdown+",\"restartToUnload\":"+(enableAttempted?"true":"false")+"}";
  }
 }
}
