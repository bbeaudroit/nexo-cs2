#include "render_refresh.hpp"
#include <cstdio>
struct FakeRefresh {int order{},markOrder{},consumeOrder{};bool allowed=true,failNative=false;bool ready(){return allowed;}bool nativeRegen(){++order;return !failNative;}bool mark(){markOrder=++order;return true;}bool consume(){consumeOrder=++order;return true;}};
int main(){FakeRefresh b;if(!localtest::regenerateAndRefresh(b)||b.markOrder!=2||b.consumeOrder!=3){std::puts("FAIL refresh must follow native regeneration");return 1;}FakeRefresh f;f.failNative=true;if(localtest::regenerateAndRefresh(f)||f.markOrder||f.consumeOrder)return 2;FakeRefresh blocked;blocked.allowed=false;if(localtest::regenerateAndRefresh(blocked)||blocked.order)return 3;std::puts("PASS 3 render refresh checks");}
