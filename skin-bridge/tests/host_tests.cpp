#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <cstdio>
#include <cstdlib>
#include "nexo_bridge.h"
static unsigned checks=0;
static void check(bool ok,const char* why){++checks;if(!ok){std::fprintf(stderr,"FAIL: %s\n",why);std::exit(1);}}
int main(){
 check(!GetModuleHandleW(L"client.dll")&&!GetModuleHandleW(L"engine2.dll"),"offline host has no game modules");
 auto dll=LoadLibraryW(L"build\\Nexo.SkinBridge.Observation.dll");check(dll!=nullptr,"locally built observation DLL loads in offline host");
 auto init=reinterpret_cast<uint32_t(__cdecl*)(const NexoBridgeOptions*)>(GetProcAddress(dll,"NexoBridgeInitialize"));
 auto enable=reinterpret_cast<uint32_t(__cdecl*)()>(GetProcAddress(dll,"NexoBridgeEnableObservation"));
 auto enqueue=reinterpret_cast<uint32_t(__cdecl*)(const NexoObservation*)>(GetProcAddress(dll,"NexoBridgeEnqueueObservation"));
 auto snapshot=reinterpret_cast<uint32_t(__cdecl*)(NexoBridgeSnapshot*)>(GetProcAddress(dll,"NexoBridgeGetSnapshot"));
 auto stop=reinterpret_cast<uint32_t(__cdecl*)()>(GetProcAddress(dll,"NexoBridgeShutdown"));
 check(init&&enable&&enqueue&&snapshot&&stop,"five versioned observation exports exist");
 check(init(nullptr)==NexoBadArgument,"null initialization rejected");
 NexoBridgeOptions options{sizeof(NexoBridgeOptions),NexoBridgeAbiVersion,NexoValidateOnly,0};
 auto invalid=options;invalid.mode=1;check(init(&invalid)==NexoBadArgument,"unreviewed initialization mode rejected");
 invalid=options;invalid.abiVersion=99;check(init(&invalid)==NexoBadArgument,"unknown ABI rejected");
 check(init(&options)==NexoMissingModule,"default validation fails closed without game modules");
 check(enable()==NexoNotInitialized,"observation cannot hook before validated initialization");
 NexoObservation command{sizeof(NexoObservation),NexoBridgeAbiVersion,1,6,0};
 check(enqueue(&command)==NexoNotInitialized,"observations cannot queue before initialization");
 check(snapshot(nullptr)==NexoBadArgument,"null snapshot rejected");
 NexoBridgeSnapshot state{};state.size=sizeof(state);state.abiVersion=NexoBridgeAbiVersion;
 check(snapshot(&state)==NexoOk,"snapshot available to diagnose failed initialization");
 check(!state.initialized&&!state.observing&&!state.pinned&&state.accepted==0&&state.completed==0,"default host creates no hook, pin or observation");
 check(stop()==NexoNotInitialized,"shutdown of unopened session is harmless");
 check(init(&options)==NexoMissingModule,"unopened shutdown does not poison later validation attempts");
 check(FreeLibrary(dll)!=0,"never-hooked offline module can unload normally");
 std::printf("%u offline DLL-host checks passed; no CS2 process accessed.\n",checks);
}
