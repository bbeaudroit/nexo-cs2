#pragma once
#include <cstdint>
namespace nexo {
enum class PatchResult {Ok,Foreign,ProtectionFailure};
template<class Backend> PatchResult replaceOwned(Backend& backend,void* expected,void* desired) noexcept {
 std::uint32_t old{};if(!backend.makeWritable(old))return PatchResult::ProtectionFailure;
 void* found=backend.compareExchange(desired,expected);
 if(!backend.restore(old)){
  backend.compareExchange(expected,desired); // Roll back only our own replacement.
  backend.restore(old);return PatchResult::ProtectionFailure;
 }
 return found==expected?PatchResult::Ok:PatchResult::Foreign;
}
}
