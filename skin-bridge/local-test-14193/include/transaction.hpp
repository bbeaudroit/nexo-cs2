#pragma once
#include <cstdint>
namespace localtest {
inline bool restoreDue(uint64_t started,uint64_t now){return now>=started&&now-started>=60000;}
enum class State:unsigned {Idle,Queued,Waiting,AttributeWritten,RegenSubmitted,RestoreQueued,Restored,Blocked,NeedsRestore};
struct Identity {uintptr_t list{},controller{},pawn{},services{},weapon{},slot{};uint32_t pawnHandle{},weaponHandle{},creation{};bool operator==(const Identity&)const=default;};
struct View {Identity id;int attrs{},paint{},pending{},materials{};bool exactPaint{},valid{};};
struct Transaction {State state=State::Idle;bool dirty=false,restore=false,removed=false,restoreSubmitted=false;Identity saved{};int baselineMaterials{};uint64_t dirtySince{};
 template<class B> bool verify(B& b,View& v){return b.read(v)&&v.valid&&v.id==saved&&v.pending==0;}
 template<class B> void stage(B& b,bool main,bool jobIdle,uint64_t now=0){
  if(dirty&&restoreDue(dirtySince,now))restore=true;
  if(!main||!jobIdle||state==State::Idle||state==State::Restored||(!dirty&&state==State::Blocked))return;
  View v{};if(!b.read(v)||!v.valid){state=dirty?State::NeedsRestore:State::Blocked;return;}
  if(dirty&&!(v.id==saved)){state=State::NeedsRestore;return;}
  if(v.pending!=0){state=restore?State::RestoreQueued:State::Waiting;return;}
  if(!dirty){
   if(restore){state=State::Restored;return;}
   if(v.attrs!=0||v.paint!=0){state=State::Blocked;return;}
   saved=v.id;baselineMaterials=v.materials;if(!verify(b,v)){state=State::Blocked;return;}
   dirty=true;dirtySince=now; // Set before entering engine: exceptions/partial changes require restore.
   if(!b.set()){state=State::NeedsRestore;return;}
   if(!verify(b,v)||v.attrs!=1||v.paint!=1171||!v.exactPaint){state=State::NeedsRestore;return;}
   state=State::AttributeWritten;
   if(!verify(b,v)||!b.clear()||!verify(b,v)||!b.regen()){state=State::NeedsRestore;return;}
   state=State::RegenSubmitted;return;
  }
  if(!restore){if(state==State::Waiting&&v.attrs==1&&v.paint==1171&&v.exactPaint)state=State::RegenSubmitted;return;}
  if(restoreSubmitted){if(v.attrs==0&&v.paint==0&&v.materials==baselineMaterials){dirty=false;state=State::Restored;}else state=State::NeedsRestore;return;}
  if(!removed){if(v.attrs!=1||v.paint!=1171||!v.exactPaint){state=State::NeedsRestore;return;}
   if(!verify(b,v)||!b.remove()){state=State::NeedsRestore;return;}removed=true;}
  if(!verify(b,v)||v.attrs!=0||v.paint!=0){state=State::NeedsRestore;return;}
  if(!verify(b,v)||!b.clear()||!verify(b,v)||!b.regen()){state=State::NeedsRestore;return;}
  restoreSubmitted=true;state=State::RestoreQueued;
 }
};
}







