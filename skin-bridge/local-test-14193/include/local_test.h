#pragma once
#include <stdint.h>
#ifdef __cplusplus
extern "C" {
#endif
typedef struct NexoLocalSnapshot {uint32_t size,version,kit,state,status,dirty,pinned,hooked,attributeCount,paint,pendingCount,materialCount;uint64_t stages,requestsObserved;uint32_t reserved[4];} NexoLocalSnapshot;
__declspec(dllexport) uint32_t __cdecl NexoLocalStart(NexoLocalSnapshot* snapshot);
__declspec(dllexport) uint32_t __cdecl NexoLocalPoll(NexoLocalSnapshot* snapshot);
__declspec(dllexport) uint32_t __cdecl NexoLocalRequestRestore(void);
__declspec(dllexport) uint32_t __cdecl NexoLocalShutdown(void);
#ifdef __cplusplus
}
#endif
