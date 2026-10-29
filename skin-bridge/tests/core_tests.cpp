#include "observation_core.hpp"
#include <atomic>
#include <cstdio>
#include <cstdlib>
#include <thread>

static unsigned checks=0;
static void check(bool ok,const char* why){++checks;if(!ok){std::fprintf(stderr,"FAIL: %s\n",why);std::exit(1);}}
int main(){
 nexo::ObservationCore core;
 check(core.enqueue({7,6}),"valid observation is accepted");
 core.stage(5,true);check(core.snapshot().completed==0,"wrong stage retains command");
 core.stage(6,false);check(core.snapshot().completed==0,"non-main stage never drains command");
 core.stage(6,true);auto c=core.snapshot();check(c.completed==1&&c.lastCompleted==7,"matching main stage completes observation");
 check(c.stages[6]==2&&c.mainStages[6]==1,"stage and main-thread counters are distinct");
 check(!core.enqueue({0,6}),"zero command identity rejected");
 check(!core.enqueue({8,2})&&!core.enqueue({8,13}),"only audited stages3..12 accepted");
 for(unsigned i=0;i<32;++i)check(core.enqueue({100+i,8}),"bounded queue accepts32 commands");
 check(!core.enqueue({999,8}),"queue overflow fails without allocation");
 core.stage(8,true);c=core.snapshot();check(c.completed==33&&c.lastCompleted==131,"one matching stage drains bounded queue");
 check(core.enqueue({55,6})&&core.enqueue({56,8}),"different stage commands coexist");
 core.stage(8,true);check(core.snapshot().completed==34,"selective draining preserves other stages");
 core.stage(6,true);check(core.snapshot().completed==35,"preserved command eventually completes");
 core.stop();check(!core.enqueue({99,8}),"shutdown closes queue");
 core.stage(8,true);check(core.snapshot().completed==35,"shutdown prevents future completion");
 nexo::ObservationCore concurrent;std::atomic<bool> done=false;std::atomic<unsigned> accepted=0;
 std::thread producer([&]{for(unsigned i=1;i<=10000;++i)if(concurrent.enqueue({i,8}))++accepted;done=true;});
 while(!done.load())concurrent.stage(8,true);producer.join();concurrent.stage(8,true);
 check(concurrent.snapshot().completed==accepted.load(),"concurrent accepted observations are neither lost nor duplicated");
 std::printf("%u observation-core checks passed; no game module or process used.\n",checks);
}
