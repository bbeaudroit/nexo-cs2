#include "transaction.hpp"
#include "render_refresh.hpp"
#include <cstdio>
using namespace localtest;
struct Fake {View v{};int writes{},removes{},clears{},regens{},marks{},consumes{},absenceRefresh{};bool failSet{},failRegen{};Fake(){v.valid=true;v.materials=1;v.id.weaponHandle=123;}
 bool read(View& o){o=v;return v.valid;} bool set(){++writes;if(failSet)return false;v.attrs=1;v.paint=1171;v.exactPaint=true;return true;} bool remove(){++removes;v.attrs=0;v.paint=0;v.exactPaint=false;return true;}bool clear(){++clears;return true;}bool ready(){return v.valid&&v.pending==0;}bool nativeRegen(){++regens;return !failRegen;}bool mark(){++marks;if(v.attrs==0)++absenceRefresh;return true;}bool consume(){++consumes;return true;}bool regen(){return localtest::regenerateAndRefresh(*this);}};
int main(){int n=0;auto check=[&](bool b){++n;if(!b){std::printf("FAIL %d\n",n);return false;}return true;};
 if(!check(!restoreDue(100,60099)))return 1;if(!check(restoreDue(100,60100)))return 1;if(!check(restoreDue(100,60101)))return 1;
 Fake b;Transaction t;t.state=State::Queued;t.stage(b,true,true);if(!check(t.dirty&&b.writes==1&&b.regens==1))return 1;
 t.restore=true;t.stage(b,true,true);t.stage(b,true,true);if(!check(!t.dirty&&t.state==State::Restored&&b.removes==1&&b.v.attrs==0))return 1;
 for(int gate=0;gate<4;++gate){Fake f;Transaction x;x.state=State::Queued;if(gate==2)f.v.pending=1;if(gate==3)f.v.attrs=1;x.stage(f,gate!=0,gate!=1);if(!check(f.writes==0))return 1;}
 Fake f;Transaction x;x.state=State::Queued;x.stage(f,true,true);f.v.id.weaponHandle++;x.restore=true;x.stage(f,true,true);if(!check(x.dirty&&f.removes==0&&x.state==State::NeedsRestore))return 1;
 Fake p;Transaction y;y.state=State::Queued;y.stage(p,true,true);p.v.pending=1;y.restore=true;y.stage(p,true,true);if(!check(y.dirty&&p.removes==0))return 1;p.v.pending=0;y.stage(p,true,true);y.stage(p,true,true);if(!check(!y.dirty))return 1;
 Fake foreign;Transaction z;z.state=State::Queued;z.stage(foreign,true,true);foreign.v.exactPaint=false;z.restore=true;z.stage(foreign,true,true);if(!check(z.dirty&&foreign.removes==0))return 1;
 Fake fail;fail.failSet=true;Transaction q;q.state=State::Queued;q.stage(fail,true,true);if(!check(q.dirty&&q.state==State::NeedsRestore))return 1;
 Fake r;r.failRegen=true;Transaction rt;rt.state=State::Queued;rt.stage(r,true,true);if(!check(rt.dirty&&rt.state==State::NeedsRestore))return 1;rt.restore=true;r.failRegen=false;rt.stage(r,true,true);if(!check(rt.dirty&&rt.restoreSubmitted&&r.removes==1))return 1;r.v.pending=2;rt.stage(r,true,true);if(!check(rt.dirty))return 1;r.v.pending=0;rt.stage(r,true,true);if(!check(!rt.dirty&&rt.state==State::Restored))return 1;
 Fake no;Transaction nt;nt.state=State::Queued;nt.stage(no,true,true);nt.restore=true;nt.stage(no,true,true);no.v.materials=0;nt.stage(no,true,true);if(!check(nt.dirty&&nt.state==State::NeedsRestore))return 1;
 Fake absent;Transaction at;at.state=State::Queued;at.restore=true;at.stage(absent,true,true);if(!check(!at.dirty&&absent.writes==0&&absent.removes==0))return 1;
 Fake invalid;invalid.v.valid=false;Transaction it;it.state=State::Queued;it.stage(invalid,true,true);if(!check(it.state==State::Blocked&&invalid.writes==0))return 1;
 Fake changed;Transaction ct;ct.state=State::Queued;ct.stage(changed,true,true);changed.v.attrs=0;changed.v.paint=0;ct.restore=true;ct.stage(changed,true,true);if(!check(ct.dirty&&changed.removes==0))return 1;
 Fake blocked;blocked.v.attrs=1;Transaction bt;bt.state=State::Queued;bt.stage(blocked,true,true);blocked.v.attrs=0;bt.stage(blocked,true,true);if(!check(bt.state==State::Blocked&&blocked.writes==0))return 1;
 Fake waiting;Transaction wt;wt.state=State::Queued;wt.stage(waiting,true,true);waiting.v.pending=1;wt.stage(waiting,true,true);if(!check(wt.state==State::Waiting))return 1;waiting.v.pending=0;wt.stage(waiting,true,true);if(!check(wt.state==State::RegenSubmitted&&waiting.writes==1))return 1;
 Fake timer;Transaction tt;tt.state=State::Queued;tt.stage(timer,true,true,100);tt.stage(timer,true,true,60099);if(!check(!tt.restore&&timer.removes==0))return 1;tt.stage(timer,true,true,60100);if(!check(tt.restore&&timer.removes==1&&tt.dirty))return 1;tt.stage(timer,true,true,60101);if(!check(!tt.dirty&&tt.state==State::Restored))return 1;
 Fake timedForeign;Transaction ft;ft.state=State::Queued;ft.stage(timedForeign,true,true,500);timedForeign.v.id.creation++;ft.stage(timedForeign,true,true,60500);if(!check(ft.restore&&ft.dirty&&timedForeign.removes==0&&ft.state==State::NeedsRestore))return 1;
 if(!check(b.regens==2&&b.marks==2&&b.consumes==2&&b.absenceRefresh==1))return 1;
 Fake zero;zero.v.materials=0;Transaction zt;zt.state=State::Queued;zt.stage(zero,true,true);zt.restore=true;zt.stage(zero,true,true);zt.stage(zero,true,true);if(!check(zt.state==State::Restored&&zero.absenceRefresh==1&&zero.marks==2&&zero.consumes==2))return 1;
 std::printf("PASS %d transaction checks\n",n);return 0;}









