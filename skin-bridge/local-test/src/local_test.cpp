#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <bcrypt.h>
#include <atomic>
#include <cstring>
#include "nexo_bridge.h"
#include "transaction.hpp"
#include "local_test.h"
#include <shellapi.h>
#include "owned_slot.hpp"
static_assert(sizeof(NexoLocalSnapshot)==80,"Local ABI layout changed");
static_assert(sizeof(void*)==8,"Windows x64 required");
static_assert(sizeof(NexoBridgeOptions)==16&&sizeof(NexoObservation)==24&&sizeof(NexoBridgeSnapshot)==320,"ABI layout changed");
namespace {
 HMODULE selfModule{};
 SRWLOCK control=SRWLOCK_INIT;

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
  if(!GetModuleHandleW(L"server.dll"))return NexoMissingModule;
  int argc{};auto argv=CommandLineToArgvW(GetCommandLineW(),&argc);bool insecure=false;if(argv){for(int i=1;i<argc;++i)if(wcscmp(argv[i],L"-insecure" )==0)insecure=true;LocalFree(argv);}if(!insecure)return NexoBadArgument;
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

namespace {
 localtest::Transaction tx;uint64_t stages{},requestObservations{};localtest::View latest{};
 uintptr_t base() {return reinterpret_cast<uintptr_t>(GetModuleHandleW(L"client.dll"));}
 template<class T>T load(uintptr_t p){return *reinterpret_cast<T*>(p);}
 bool bounded(uintptr_t p,size_t n){MEMORY_BASIC_INFORMATION m{};return p>=0x10000&&VirtualQuery(reinterpret_cast<void*>(p),&m,sizeof(m))==sizeof(m)&&m.State==MEM_COMMIT&&!(m.Protect&(PAGE_NOACCESS|PAGE_GUARD))&&p+n>=p&&p+n<=reinterpret_cast<uintptr_t>(m.BaseAddress)+m.RegionSize;}
 uintptr_t resolve(uintptr_t list,uint32_t h,uintptr_t& slotOut){if(!h||h>=0xfffffffe)return 0;auto i=h&0x7fff;auto chunk=load<uintptr_t>(list+0x10+8*(i>>9));slotOut=chunk+0x70*(i&511);if(!bounded(slotOut,0x70)||load<uint32_t>(slotOut+0x10)!=h)return 0;return load<uintptr_t>(slotOut);}
  bool ownerType(uintptr_t b,uintptr_t owner){
  auto table=load<uintptr_t>(owner);if(table!=b+0x1BE0940)return false;
  auto col=load<uintptr_t>(table-8);if(!bounded(col,24)||load<uint32_t>(col)!=1||load<uint32_t>(col+4)!=0x610||b+load<uint32_t>(col+20)!=col)return false;
  auto type=b+load<uint32_t>(col+12);static constexpr char expected[]=".?AVC_AK47@@";
  return bounded(type+16,sizeof(expected))&&std::memcmp(reinterpret_cast<void*>(type+16),expected,sizeof(expected))==0&&load<uintptr_t>(table+0x10)==b+0x955B00&&load<uintptr_t>(table+0x18)==b+0x955A00;
 }
 bool inspect(localtest::View& v){
  __try {
   auto b=base();if(!b||!GetModuleHandleW(L"server.dll"))return false;
   localtest::Identity id{};id.list=load<uintptr_t>(b+41011384);id.controller=load<uintptr_t>(b+39055096);id.pawn=load<uintptr_t>(b+39220952);
   if(!bounded(id.list,0x210)||!bounded(id.controller,0x940)||!bounded(id.pawn,0x1300))return false;
   id.pawnHandle=load<uint32_t>(id.controller+2348);uintptr_t pawnSlot{};if(resolve(id.list,id.pawnHandle,pawnSlot)!=id.pawn)return false;
   id.services=load<uintptr_t>(id.pawn+4848);if(!bounded(id.services,104))return false;id.weaponHandle=load<uint32_t>(id.services+96);id.weapon=resolve(id.list,id.weaponHandle,id.slot);
   if(!bounded(id.weapon,0x1b30)||load<uintptr_t>(id.weapon+0x10)!=id.slot||load<uint32_t>(id.weapon+1312)!=id.pawnHandle)return false;
   if(load<int>(id.weapon+0x18a8)!=0)return false; // Default fallback only; never write fallback fields.
   id.creation=load<uint32_t>(id.weapon+1488);auto item=id.weapon+0x12e0,owner=id.weapon+0x610;
   auto manager=load<uintptr_t>(item+0x278);if(manager!=id.weapon+0x1290||!bounded(manager,0x40)||load<uintptr_t>(manager)!=b+0x1D120F0)return false;
   if(load<uint16_t>(item+442)!=7||load<uint8_t>(item+488)!=1||load<uintptr_t>(item)!=b+0x1D11F50||!ownerType(b,owner))return false;
   v={};v.id=id;v.attrs=load<int>(item+0x210);v.pending=load<int>(owner+0x4b8);v.materials=load<int>(owner+0x4a0);
   if(v.attrs<0||v.attrs>1||v.pending<0||v.pending>64||v.materials<0||v.materials>64)return false;
   if(v.pending&&!bounded(load<uintptr_t>(owner+0x4c0),8ULL*v.pending))return false;
   if(v.materials&&!bounded(load<uintptr_t>(owner+0x4a8),8ULL*v.materials))return false;
   if(v.attrs==1){auto a=load<uintptr_t>(item+0x218);if(!bounded(a,0x48))return false;v.exactPaint=load<uint16_t>(a+0x30)==6&&load<float>(a+0x34)==1171.0f&&load<uint8_t>(a+0x40)==0;}
   // Getter only after fixed class and local ownership gates.
   v.paint=reinterpret_cast<int(__fastcall*)(uintptr_t)>(b+0x118ECD0)(item);v.valid=true;return true;
  }__except(EXCEPTION_EXECUTE_HANDLER){return false;}
 }
 struct Backend {
  bool read(localtest::View& v){return inspect(v);}
  bool ready(){localtest::View v{};return inspect(v)&&v.id==tx.saved&&v.pending==0;}
  bool set(){if(!ready())return false;__try{reinterpret_cast<void(__fastcall*)(uintptr_t,const char*,float)>(base()+0x11A3080)(tx.saved.weapon+0x12e0,"set item texture prefab",1171.0f);return true;}__except(EXCEPTION_EXECUTE_HANDLER){return false;}}
  bool remove(){if(!ready())return false;__try{auto item=tx.saved.weapon+0x12e0;auto attr=load<uintptr_t>(item+0x218);if(load<int>(item+0x210)!=1||!bounded(attr,0x48)||load<uint16_t>(attr+0x30)!=6||load<float>(attr+0x34)!=1171.0f||load<uint8_t>(attr+0x40))return false;
    reinterpret_cast<void(__fastcall*)(uintptr_t,int)>(base()+0x11A0CF0)(item+0x210,0);
    if(!ready())return false;auto manager=load<uintptr_t>(item+0x208+0x70);if(manager!=tx.saved.weapon+0x1290||!bounded(manager,0x40)||load<uintptr_t>(manager)!=base()+0x1D120F0)return false;
    reinterpret_cast<void(__fastcall*)(uintptr_t)>(base()+0x11194F0)(manager); // RCX confirmed at11A3009/11A3012.
    if(!ready())return false;reinterpret_cast<void(__fastcall*)(uintptr_t)>(base()+0x119EBB0)(item);return true;
   }__except(EXCEPTION_EXECUTE_HANDLER){return false;}}
  bool clear(){if(!ready())return false;__try{reinterpret_cast<void(__fastcall*)(uintptr_t,bool)>(base()+0x14E9D00)(tx.saved.weapon+0x610,true);return true;}__except(EXCEPTION_EXECUTE_HANDLER){return false;}}
  bool regen(){if(!ready())return false;__try{reinterpret_cast<void(__fastcall*)(uintptr_t,uint8_t)>(base()+0x803780)(tx.saved.weapon,0);return true;}__except(EXCEPTION_EXECUTE_HANDLER){return false;}}
 };
 bool idle(void* p){__try{return load<uintptr_t>(reinterpret_cast<uintptr_t>(p)+0x4b0)==0;}__except(EXCEPTION_EXECUTE_HANDLER){return false;}}
 bool validSnapshot(NexoLocalSnapshot* p){return p&&p->size==sizeof(*p)&&p->version==1&&p->kit==1171&&p->reserved[0]==0&&p->reserved[1]==0&&p->reserved[2]==0&&p->reserved[3]==0;}
 void snapshot(NexoLocalSnapshot* p){NexoLocalSnapshot s{};s.size=sizeof(s);s.version=1;s.kit=1171;s.state=static_cast<uint32_t>(tx.state);s.status=status.load();s.dirty=tx.dirty;s.pinned=pinned.load();s.hooked=hookInstalled.load();s.attributeCount=latest.attrs;s.paint=latest.paint;s.pendingCount=latest.pending;s.materialCount=latest.materials;s.stages=stages;s.requestsObserved=requestObservations;*p=s;}
}
extern "C" void NexoStageThunk();
extern "C" {void* NexoOriginalStage=nullptr;}
extern "C" void NexoObserveStage(void* current,int32_t stage) noexcept {
 if(!observing.load()||current!=instance.load()||stage!=8||!mainThread||!mainThread()||!idle(current))return;
 if(!TryAcquireSRWLockExclusive(&control))return;
 Backend b;++stages;tx.stage(b,true,true,GetTickCount64());if(inspect(latest)&&latest.pending>0)++requestObservations;ReleaseSRWLockExclusive(&control);
}
uint32_t __cdecl NexoLocalStart(NexoLocalSnapshot* p){if(!validSnapshot(p))return NexoBadArgument;Guard g;if(everEnabled.load())return result(NexoAlreadyUsed);auto code=validate();if(code!=NexoOk)return result(code);
 HMODULE pin{};if(!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_PIN,reinterpret_cast<LPCWSTR>(&NexoLocalStart),&pin)||pin!=selfModule)return result(NexoProtectionFailure);
 pinned=true;everEnabled=true;InterlockedExchangePointer(&NexoOriginalStage,original);tx.state=localtest::State::Queued;observing=true;code=replaceOwned(original,reinterpret_cast<void*>(&NexoStageThunk));hookInstalled=code==NexoOk;if(code!=NexoOk)observing=false;result(code);snapshot(p);return code;
}
uint32_t __cdecl NexoLocalPoll(NexoLocalSnapshot* p){if(!validSnapshot(p))return NexoBadArgument;Guard g;snapshot(p);return 0;}
uint32_t __cdecl NexoLocalRequestRestore(){Guard g;if(!hookInstalled.load())return NexoNotInitialized;tx.restore=true;return 0;}
uint32_t __cdecl NexoLocalShutdown(){Guard g;if(tx.dirty){tx.restore=true;tx.state=localtest::State::NeedsRestore;return result(12);}observing=false;if(!hookInstalled.load())return 0;auto code=replaceOwned(reinterpret_cast<void*>(&NexoStageThunk),original);if(code==NexoOk)hookInstalled=false;return result(code);}
BOOL WINAPI DllMain(HINSTANCE m,DWORD reason,LPVOID){if(reason==DLL_PROCESS_ATTACH)selfModule=m;return TRUE;}




