#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <bcrypt.h>
#include <atomic>
#include <cstring>
#include "nexo_bridge.h"
#include "observation_core.hpp"
#include "owned_slot.hpp"
static_assert(sizeof(void*)==8,"Windows x64 required");
static_assert(sizeof(NexoBridgeOptions)==16&&sizeof(NexoObservation)==24&&sizeof(NexoBridgeSnapshot)==320,"ABI layout changed");
namespace {
 HMODULE selfModule{};
 SRWLOCK control=SRWLOCK_INIT;
 nexo::ObservationCore core;
 std::atomic<bool> initialized{},observing{},pinned{},everEnabled{},sessionClosed{},hookInstalled{};
 std::atomic<uint32_t> status{},lastThread{};
 std::atomic<int32_t> lastStage{-1};
 std::atomic<void*> instance{};
 void** slot{};void* original{};
 using MainThreadFn=bool(__cdecl*)();MainThreadFn mainThread{};
 constexpr uintptr_t InstanceRva=0x25613A0,TableRva=0x1C65030,StageRva=0xB6E540,BuildRva=0x6BA02C;
 struct Guard{Guard(){AcquireSRWLockExclusive(&control);}~Guard(){ReleaseSRWLockExclusive(&control);}};
 uint32_t result(uint32_t value){status.store(value);return value;}
 bool imageRange(HMODULE m,uintptr_t rva,size_t count,DWORD require,DWORD forbid=0){
  __try {
   auto base=reinterpret_cast<uint8_t*>(m);auto dos=reinterpret_cast<IMAGE_DOS_HEADER*>(base);
   if(dos->e_magic!=IMAGE_DOS_SIGNATURE||dos->e_lfanew<sizeof(IMAGE_DOS_HEADER)||dos->e_lfanew>0x100000)return false;
   auto nt=reinterpret_cast<IMAGE_NT_HEADERS64*>(base+dos->e_lfanew);
   if(nt->Signature!=IMAGE_NT_SIGNATURE||nt->FileHeader.Machine!=IMAGE_FILE_MACHINE_AMD64||nt->OptionalHeader.Magic!=IMAGE_NT_OPTIONAL_HDR64_MAGIC||nt->FileHeader.NumberOfSections>96)return false;
   if(rva>nt->OptionalHeader.SizeOfImage||count>nt->OptionalHeader.SizeOfImage-rva)return false;
   auto sections=IMAGE_FIRST_SECTION(nt);
   for(unsigned i=0;i<nt->FileHeader.NumberOfSections;++i){const auto& s=sections[i];auto extent=static_cast<uintptr_t>(s.Misc.VirtualSize);if(rva>=s.VirtualAddress&&rva-s.VirtualAddress<=extent&&count<=extent-(rva-s.VirtualAddress)&&(s.Characteristics&require)==require&&(s.Characteristics&forbid)==0)return true;}
   return false;
  }__except(EXCEPTION_EXECUTE_HANDLER){return false;}
 }
 bool readPointer(void* address,void*& value){__try{value=*reinterpret_cast<void**>(address);return true;}__except(EXCEPTION_EXECUTE_HANDLER){return false;}}
 bool readBuild(HMODULE module,int& value){__try{value=*reinterpret_cast<int*>(reinterpret_cast<uint8_t*>(module)+BuildRva);return true;}__except(EXCEPTION_EXECUTE_HANDLER){return false;}}
 bool fingerprint(void* address){
  static constexpr uint8_t expected[]={0x48,0x89,0x5c,0x24,0x18,0x48,0x89,0x6c,0x24,0x20,0x57,0x48,0x83,0xec,0x40};
  __try{return std::memcmp(address,expected,sizeof(expected))==0;}__except(EXCEPTION_EXECUTE_HANDLER){return false;}
 }
 bool fileHashMatches(HMODULE module){
  static constexpr uint8_t expected[]={0xcc,0xaf,0x4e,0x49,0x26,0xba,0xbc,0x05,0x29,0x94,0x50,0x2b,0x19,0xec,0x61,0x19,0x8a,0x11,0x00,0x8a,0x39,0xdc,0xe7,0x12,0x92,0x1b,0x93,0xdd,0x99,0x3b,0xe9,0xf5};
  wchar_t path[32768];DWORD n=GetModuleFileNameW(module,path,32768);if(n==0||n>=32768)return false;
  HANDLE file=CreateFileW(path,GENERIC_READ,FILE_SHARE_READ|FILE_SHARE_DELETE,nullptr,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,nullptr);if(file==INVALID_HANDLE_VALUE)return false;
  BCRYPT_ALG_HANDLE algorithm{};BCRYPT_HASH_HANDLE hash{};PUCHAR object{};DWORD objectBytes{},returned{},read{};UCHAR digest[32]{},buffer[65536];bool ok=false;
  if(BCryptOpenAlgorithmProvider(&algorithm,BCRYPT_SHA256_ALGORITHM,nullptr,0)<0)goto cleanup;
  if(BCryptGetProperty(algorithm,BCRYPT_OBJECT_LENGTH,reinterpret_cast<PUCHAR>(&objectBytes),sizeof(objectBytes),&returned,0)<0||objectBytes>1048576)goto cleanup;
  object=static_cast<PUCHAR>(HeapAlloc(GetProcessHeap(),0,objectBytes));if(!object)goto cleanup;
  if(BCryptCreateHash(algorithm,&hash,object,objectBytes,nullptr,0,0)<0)goto cleanup;
  for(;;){if(!ReadFile(file,buffer,sizeof(buffer),&read,nullptr))goto cleanup;if(read==0)break;if(BCryptHashData(hash,buffer,read,0)<0)goto cleanup;}
  if(BCryptFinishHash(hash,digest,sizeof(digest),0)>=0)ok=std::memcmp(digest,expected,sizeof(expected))==0;
 cleanup:
  if(hash)BCryptDestroyHash(hash);if(object)HeapFree(GetProcessHeap(),0,object);if(algorithm)BCryptCloseAlgorithmProvider(algorithm,0);CloseHandle(file);return ok;
 }
 uint32_t validate(){
  HMODULE client=GetModuleHandleW(L"client.dll"),engine=GetModuleHandleW(L"engine2.dll"),tier=GetModuleHandleW(L"tier0.dll");if(!client||!engine||!tier)return NexoMissingModule;
  int build{};if(!imageRange(engine,BuildRva,sizeof(build),IMAGE_SCN_MEM_READ)||!readBuild(engine,build))return NexoInvalidImage;if(build!=14192)return NexoUnsupportedBuild;
  if(!fileHashMatches(client))return NexoHashMismatch;
  constexpr uintptr_t offset=36*sizeof(void*);
  if(!imageRange(client,InstanceRva,sizeof(void*),IMAGE_SCN_MEM_READ)||!imageRange(client,TableRva+offset,sizeof(void*),IMAGE_SCN_MEM_READ,IMAGE_SCN_MEM_EXECUTE)||!imageRange(client,StageRva,15,IMAGE_SCN_MEM_EXECUTE))return NexoInvalidImage;
  auto base=reinterpret_cast<uint8_t*>(client);void* table{};void* function{};
  if(!readPointer(base+InstanceRva,table)||table!=base+TableRva||!readPointer(base+TableRva+offset,function)||function!=base+StageRva||!fingerprint(function))return NexoInterfaceMismatch;
  auto symbol=GetProcAddress(tier,"ThreadInMainThread");
  if(!imageRange(tier,0x173B50,1,IMAGE_SCN_MEM_EXECUTE)||symbol!=reinterpret_cast<FARPROC>(reinterpret_cast<uint8_t*>(tier)+0x173B50))return NexoInterfaceMismatch;
  mainThread=reinterpret_cast<MainThreadFn>(symbol);slot=reinterpret_cast<void**>(base+TableRva+offset);original=function;instance.store(base+InstanceRva);return NexoOk;
 }
 // Same-process, one-pointer replacement only. Never overwrites someone else's hook.
 struct WindowsSlot {
  void** address;
  bool makeWritable(uint32_t& protection) noexcept {DWORD old{};if(!VirtualProtect(address,sizeof(void*),PAGE_READWRITE,&old))return false;protection=old;return true;}
  void* compareExchange(void* desired,void* expected) noexcept {return InterlockedCompareExchangePointer(address,desired,expected);}
  bool restore(uint32_t protection) noexcept {DWORD ignored{};return VirtualProtect(address,sizeof(void*),protection,&ignored)!=0;}
 };
 uint32_t replaceOwned(void* expected,void* desired){
  WindowsSlot backend{slot};auto code=nexo::replaceOwned(backend,expected,desired);
  return code==nexo::PatchResult::Ok?NexoOk:code==nexo::PatchResult::Foreign?NexoForeignHook:NexoProtectionFailure;
 }
}
extern "C" void NexoStageThunk();
extern "C" {void* NexoOriginalStage=nullptr;}
extern "C" void NexoObserveStage(void* current,int32_t stage) noexcept {
 if(!observing.load(std::memory_order_acquire)||current!=instance.load())return;
 lastThread.store(GetCurrentThreadId());lastStage.store(stage);
 // The observed engine export is called only inside an explicitly enabled hook.
 core.stage(static_cast<uint32_t>(stage),mainThread&&mainThread());
}
uint32_t NEXO_CALL NexoBridgeInitialize(const NexoBridgeOptions* options){
 if(!options||options->size!=sizeof(*options)||options->abiVersion!=NexoBridgeAbiVersion||options->mode!=NexoValidateOnly||options->reserved)return result(NexoBadArgument);
 Guard guard;if(sessionClosed.load()||everEnabled.load()||initialized.load())return result(NexoAlreadyUsed);auto code=validate();if(code==NexoOk)initialized.store(true);return result(code);
}
uint32_t NEXO_CALL NexoBridgeEnableObservation(){
 Guard guard;if(!initialized.load())return result(NexoNotInitialized);if(everEnabled.load())return result(NexoAlreadyUsed);
 auto code=validate();if(code!=NexoOk)return result(code);
 HMODULE pinnedModule{};if(!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_PIN,reinterpret_cast<LPCWSTR>(&NexoBridgeEnableObservation),&pinnedModule))return result(NexoProtectionFailure);
 pinned.store(true);everEnabled.store(true);if(pinnedModule!=selfModule)return result(NexoProtectionFailure);
 InterlockedExchangePointer(&NexoOriginalStage,original);observing.store(true,std::memory_order_release);
 code=replaceOwned(original,reinterpret_cast<void*>(&NexoStageThunk));hookInstalled.store(code==NexoOk);if(code!=NexoOk)observing.store(false);return result(code);
}
uint32_t NEXO_CALL NexoBridgeEnqueueObservation(const NexoObservation* command){
 if(!command||command->size!=sizeof(*command)||command->abiVersion!=NexoBridgeAbiVersion||command->reserved)return result(NexoBadArgument);
 if(!initialized.load())return result(NexoNotInitialized);return result(core.enqueue({command->id,command->stage})?NexoOk:NexoQueueRejected);
}
uint32_t NEXO_CALL NexoBridgeGetSnapshot(NexoBridgeSnapshot* snapshot){
 if(!snapshot||snapshot->size!=sizeof(*snapshot)||snapshot->abiVersion!=NexoBridgeAbiVersion)return NexoBadArgument;
 NexoBridgeSnapshot out{};out.size=sizeof(out);out.abiVersion=NexoBridgeAbiVersion;out.initialized=initialized.load();out.observing=observing.load();out.pinned=pinned.load();out.lastStatus=status.load();out.lastThreadId=lastThread.load();out.lastStage=lastStage.load();auto c=core.snapshot();for(unsigned i=0;i<16;++i){out.stages[i]=c.stages[i];out.mainThreadStages[i]=c.mainStages[i];}out.accepted=c.accepted;out.completed=c.completed;out.rejected=c.rejected;out.lastCompleted=c.lastCompleted;*snapshot=out;return NexoOk;
}
uint32_t NEXO_CALL NexoBridgeShutdown(){
 Guard guard;if(!initialized.load())return result(NexoNotInitialized);observing.store(false,std::memory_order_release);core.stop();initialized.store(false);sessionClosed.store(true);
 if(!hookInstalled.exchange(false))return result(NexoOk);return result(replaceOwned(reinterpret_cast<void*>(&NexoStageThunk),original));
}
BOOL WINAPI DllMain(HINSTANCE module,DWORD reason,LPVOID){if(reason==DLL_PROCESS_ATTACH)selfModule=module;return TRUE;}
