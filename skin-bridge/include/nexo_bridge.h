#pragma once
#include <stdint.h>
#ifdef _WIN32
#define NEXO_CALL __cdecl
#ifdef NEXO_BRIDGE_BUILD
#define NEXO_API __declspec(dllexport)
#else
#define NEXO_API __declspec(dllimport)
#endif
#else
#error Windows x64 only
#endif
#ifdef __cplusplus
extern "C" {
#endif
enum NexoBridgeStatus {NexoOk=0,NexoBadArgument=1,NexoMissingModule=2,NexoUnsupportedBuild=3,NexoHashMismatch=4,NexoInvalidImage=5,NexoInterfaceMismatch=6,NexoForeignHook=7,NexoProtectionFailure=8,NexoNotInitialized=9,NexoQueueRejected=10,NexoAlreadyUsed=11};
enum {NexoBridgeAbiVersion=1,NexoValidateOnly=0};
typedef struct NexoBridgeOptions {uint32_t size,abiVersion,mode,reserved;} NexoBridgeOptions;
typedef struct NexoObservation {uint32_t size,abiVersion;uint64_t id;uint32_t stage,reserved;} NexoObservation;
typedef struct NexoBridgeSnapshot {
 uint32_t size,abiVersion,initialized,observing,pinned,lastStatus,lastThreadId;
 int32_t lastStage;
 uint64_t stages[16],mainThreadStages[16];
 uint64_t accepted,completed,rejected,lastCompleted;
} NexoBridgeSnapshot;
NEXO_API uint32_t NEXO_CALL NexoBridgeInitialize(const NexoBridgeOptions* options);
NEXO_API uint32_t NEXO_CALL NexoBridgeEnableObservation(void);
NEXO_API uint32_t NEXO_CALL NexoBridgeEnqueueObservation(const NexoObservation* command);
NEXO_API uint32_t NEXO_CALL NexoBridgeGetSnapshot(NexoBridgeSnapshot* snapshot);
NEXO_API uint32_t NEXO_CALL NexoBridgeShutdown(void);
#ifdef __cplusplus
}
#endif
