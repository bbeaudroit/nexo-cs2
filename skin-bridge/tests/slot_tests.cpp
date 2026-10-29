#include "owned_slot.hpp"
#include <cstdio>
#include <cstdlib>
struct Slot {
 void* value;bool writable{},allow=true,failRestore{},injectForeign{};unsigned protects{},restores{},writes{};std::uint32_t restoredToken{};
 bool makeWritable(std::uint32_t& old){++protects;if(!allow)return false;old=0x20;writable=true;return true;}
 void* compareExchange(void* desired,void* expected){void* found=value;if(value==expected){value=desired;++writes;}return found;}
 bool restore(std::uint32_t old){++restores;restoredToken=old;if(failRestore&&restores==1){if(injectForeign)value=reinterpret_cast<void*>(3);return false;}writable=false;return true;}
};
static unsigned checks=0;static void check(bool ok,const char* why){++checks;if(!ok){std::fprintf(stderr,"FAIL: %s\n",why);std::exit(1);}}
int main(){
 auto original=reinterpret_cast<void*>(1),hook=reinterpret_cast<void*>(2),foreign=reinterpret_cast<void*>(3);
 Slot normal{original};check(nexo::replaceOwned(normal,original,hook)==nexo::PatchResult::Ok&&normal.value==hook,"owned original installs hook");check(!normal.writable&&normal.restoredToken==0x20,"original page protection restored exactly");
 check(nexo::replaceOwned(normal,hook,original)==nexo::PatchResult::Ok&&normal.value==original,"owned hook restores original");
 Slot other{foreign};check(nexo::replaceOwned(other,original,hook)==nexo::PatchResult::Foreign&&other.value==foreign&&other.writes==0,"foreign hook never overwritten");check(!other.writable,"foreign-hook failure still restores protection");
 Slot denied{original};denied.allow=false;check(nexo::replaceOwned(denied,original,hook)==nexo::PatchResult::ProtectionFailure&&denied.writes==0,"protection denial performs no pointer write");
 Slot rollback{original};rollback.failRestore=true;check(nexo::replaceOwned(rollback,original,hook)==nexo::PatchResult::ProtectionFailure&&rollback.value==original&&rollback.restores==2,"protection restore failure rolls back owned pointer and retries protection");
 Slot raced{original};raced.failRestore=true;raced.injectForeign=true;check(nexo::replaceOwned(raced,original,hook)==nexo::PatchResult::ProtectionFailure&&raced.value==foreign,"rollback preserves third-party replacement");
 std::printf("%u fake slot ownership checks passed; no executable memory or game module used.\n",checks);
}
