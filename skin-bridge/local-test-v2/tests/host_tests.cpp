#include <windows.h>
#include "local_test.h"
#include <cstdio>
int main(){auto m=LoadLibraryW(L"build\\Nexo.SkinBridge.LocalTestV2.dll");if(!m)return 1;using Fn=uint32_t(__cdecl*)(NexoLocalSnapshot*);using End=uint32_t(__cdecl*)();auto start=reinterpret_cast<Fn>(GetProcAddress(m,"NexoLocalStart"));auto poll=reinterpret_cast<Fn>(GetProcAddress(m,"NexoLocalPoll"));auto restore=reinterpret_cast<End>(GetProcAddress(m,"NexoLocalRequestRestore"));auto stop=reinterpret_cast<End>(GetProcAddress(m,"NexoLocalShutdown"));if(!start||!poll||!restore||!stop)return 2;NexoLocalSnapshot s{};s.size=sizeof(s);s.version=1;s.kit=1171;if(start(&s)!=2||poll(&s)!=0||s.dirty||s.hooked||s.pinned||restore()!=9||stop()!=0)return 3;s.kit=282;if(start(&s)!=1)return 4;if(!FreeLibrary(m))return 5;std::puts("PASS 9 DLL-host checks");}

