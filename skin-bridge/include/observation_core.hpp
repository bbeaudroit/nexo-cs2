#pragma once
#include <array>
#include <atomic>
#include <cstdint>

namespace nexo {
struct Command {std::uint64_t id; std::uint32_t stage;};
struct Counters {std::array<std::uint64_t,16> stages{},mainStages{};std::uint64_t accepted{},completed{},rejected{},lastCompleted{};};
class ObservationCore {
 std::array<Command,32> queue_{};
 std::array<bool,32> occupied_{};
 std::array<std::atomic<std::uint64_t>,16> stages_{},mainStages_{};
 std::atomic<std::uint64_t> accepted_{},completed_{},rejected_{},lastCompleted_{};
 std::atomic_flag queueLock_=ATOMIC_FLAG_INIT;
 std::atomic<bool> stopped_{};
public:
 bool enqueue(Command command) noexcept {
  if(command.id==0||command.stage<3||command.stage>12||stopped_.load(std::memory_order_acquire)){++rejected_;return false;}
  if(queueLock_.test_and_set(std::memory_order_acquire)){++rejected_;return false;}
  bool added=false;
  if(!stopped_.load(std::memory_order_acquire))for(std::size_t i=0;i<queue_.size();++i)if(!occupied_[i]){queue_[i]=command;occupied_[i]=true;added=true;++accepted_;break;}
  queueLock_.clear(std::memory_order_release);
  if(!added)++rejected_;return added;
 }
 void stage(std::uint32_t stage,bool mainThread) noexcept {
  if(stage>=stages_.size())return;
  ++stages_[stage];if(mainThread)++mainStages_[stage];
  if(!mainThread||stopped_.load(std::memory_order_acquire)||queueLock_.test_and_set(std::memory_order_acquire))return;
  if(!stopped_.load(std::memory_order_acquire))for(std::size_t i=0;i<queue_.size();++i)if(occupied_[i]&&queue_[i].stage==stage){lastCompleted_.store(queue_[i].id,std::memory_order_relaxed);++completed_;occupied_[i]=false;}
  queueLock_.clear(std::memory_order_release);
 }
 void stop() noexcept {stopped_.store(true,std::memory_order_release);}
 Counters snapshot() const noexcept {
  Counters c;for(std::size_t i=0;i<stages_.size();++i){c.stages[i]=stages_[i].load();c.mainStages[i]=mainStages_[i].load();}
  c.accepted=accepted_.load();c.completed=completed_.load();c.rejected=rejected_.load();c.lastCompleted=lastCompleted_.load();return c;
 }
};
}
