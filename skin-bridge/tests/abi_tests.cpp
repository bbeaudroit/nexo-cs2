#include <cstdint>
#include <cstdio>
#include <cstdlib>
extern "C" void NexoStageThunk();
extern "C" {void* NexoOriginalStage=nullptr;}
static bool correct=false;
static std::uint64_t integers(std::uint64_t a,std::uint64_t b,std::uint64_t c,std::uint64_t d,std::uint64_t e,std::uint64_t f,std::uint64_t g,std::uint64_t h){correct=a==11&&b==22&&c==33&&d==44&&e==55&&f==66&&g==77&&h==88;return UINT64_C(0xFEDCBA9876543210);}
static std::uint64_t mixed(void* a,int b,double c,float d,std::uint64_t e,double f){correct=a==reinterpret_cast<void*>(0x12345678)&&b==6&&c==3.25&&d==4.5f&&e==UINT64_C(0x123456789ABCDEF0)&&f==9.75;return 91;}
static double floats(double a,double b,double c,double d,double e){correct=a==1.25&&b==2.5&&c==3.75&&d==4.25&&e==5.5;return 1234.125;}
static void require(bool ok,const char* text){if(!ok){std::fprintf(stderr,"FAIL: %s\n",text);std::exit(1);}}
int main(){
 NexoOriginalStage=reinterpret_cast<void*>(&integers);
 auto i=reinterpret_cast<decltype(&integers)>(&NexoStageThunk);
 require(i(11,22,33,44,55,66,77,88)==UINT64_C(0xFEDCBA9876543210)&&correct,"GPR register/stack arguments and RAX return preserved");
 NexoOriginalStage=reinterpret_cast<void*>(&mixed);
 auto m=reinterpret_cast<decltype(&mixed)>(&NexoStageThunk);
 require(m(reinterpret_cast<void*>(0x12345678),6,3.25,4.5f,UINT64_C(0x123456789ABCDEF0),9.75)==91&&correct,"mixed XMM/GPR and stack arguments preserved");
 NexoOriginalStage=reinterpret_cast<void*>(&floats);
 auto f=reinterpret_cast<decltype(&floats)>(&NexoStageThunk);
 require(f(1.25,2.5,3.75,4.25,5.5)==1234.125&&correct,"floating-point arguments and original XMM0 return preserved");
 std::puts("3 offline thunk ABI checks passed with deliberately clobbered volatile registers.");
}
