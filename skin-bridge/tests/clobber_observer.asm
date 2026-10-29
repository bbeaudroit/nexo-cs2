OPTION CASEMAP:NONE
PUBLIC NexoObserveStage
.code
NexoObserveStage PROC
 xor eax,eax
 xor ecx,ecx
 xor edx,edx
 xor r8d,r8d
 xor r9d,r9d
 xor r10d,r10d
 xor r11d,r11d
 pxor xmm0,xmm0
 pxor xmm1,xmm1
 pxor xmm2,xmm2
 pxor xmm3,xmm3
 pxor xmm4,xmm4
 pxor xmm5,xmm5
 ret
NexoObserveStage ENDP
END
