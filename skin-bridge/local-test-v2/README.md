# Candidato local experimental v2 — kit1171, AK-47

Artefacto separado `build/Nexo.SkinBridge.LocalTestV2.dll`. No se cargó ni ejecutó
CS2 durante su desarrollo. No es una publicación ni una implementación visual
validada. El observador congelado y su manifiesto no se modifican.

Desde este directorio: `cmd /c build.cmd`. Compila MSVC x64 /std:c++20 /W4 /WX
más MASM. Pruebas offline:31 transacción,9 host DLL,8 propiedad de slot,3 ABI y3 refresh.
El primer test de transacción produjo FAIL1 antes de implementar el núcleo.
Los tests simulados no prueban lifetimes, consumidor renderer o GPU del juego.

## C ABI

`include/local_test.h`, cuatro exports __cdecl uint32, ABI1. Snapshot80 bytes:
12 uint32 (size/version/kit/state/status/dirty/pinned/hooked/attributeCount/paint/
pendingCount/materialCount),2 uint64 (stages/requestsObserved),4 uint32 reserved.
Entrada size80,version1,kit1171,reserved cero. No acepta direcciones/callbacks.

- NexoLocalStart(snapshot*): valida -insecure como argumento exacto, server.dll,
  hash/build14192, interfaz y slot; pin permanente, CAS propio y cola única.
- NexoLocalPoll(snapshot*): copia coherente bajo lock del adaptador. No lee juego
  desde el caller; counters son la última observación de fase8 válida.
- NexoLocalRequestRestore(): solicita eliminación real del único atributo propio.
- NexoLocalShutdown():12 NeedsRestore mientras dirty; conserva hook y pide restore.
  Si limpio, CAS retira solamente el hook propio; DLL sigue pin hasta salida.

States0 Idle,1 Queued,2 Waiting,3 AttributeWritten,4 RegenSubmitted,5 RestoreQueued,
6 Restored,7 Blocked,8 NeedsRestore. No existe SkinApplied. requestsObserved cuenta
callbacks que encontraron pending>0, no cantidad de requests ni confirmación visual.

## Secuencia y límites

Callback preserva ABI por copia del thunk revisado. Sólo fase8, export tier0
ThreadInMainThread y Source2Client+4B0 null, antes del original. No espera/cancela
un job pendiente. El callback usa TryAcquireSRWLockExclusive y se omite si ocupado.

Identidad se obtiene de globals del proceso actual, full handle+serial del slot,
controller/pawn coherentes, arma activa local, owner, creation tick, ItemView
inicializado def7. Se compara toda la identidad antes de cada llamada. Cambio de
arma/owner no autoriza escribir un target nuevo. owner weapon+610 exige vtable1BE0940, RTTI COL signature1/self válido,
subobjectOffset610 y tipo C_AK47, más callbacks955B00/955A00 en slots+10/+18.
ItemView exige1D11F50. Otros propietarios se bloquean.

Baseline exige atributos0/getter0, fallbackPaint0 y pending0. Blocked sin escritura es terminal para esta instancia; no apunta automáticamente a otra AK posterior. Esta versión exige m_pManager exactamente weapon+1290 con vtable1D120F0
C_AttributeContainer, tanto antes de aplicar como durante restauración. El
callsite11A3009/11A3012 confirma RCX=manager para11194F0. No se permiten otros tipos.

Set11A3080(ItemView,nombre,float1171); valida exactamente un atributo def6,
valor1171,setBonusfalse; clear14E9D00(owner,true) sólo pending0; regen803780(weapon,0).
Restauración elimina índice0 con11A0CF0(ItemView+210,0), invalida manager11194F0(manager), borra descripción119EBB0,
y repite clear/regen. dirty se establece ANTES del setter para cubrir fallos
parciales; no se confunde ausencia con pintura0. Una excepción/cambio ajeno puede
mantener NeedsRestore: no se fuerza rollback sobre identidad o atributo desconocido.

Restored exige callback posterior: atributo ausente,getter0,pending0 y cantidad de
materiales igual a baseline. Es una comprobación de contadores y atributos; no
prueba igualdad de recursos, finalización GPU ni instalación/consumo en renderer o
viewmodel. Si el contador no vuelve a baseline, el hook permanece para observar.
El usuario todavía debe comprobar visualmente aplicación y retorno a default.

Fuente material:803780→8004B0 construye solicitudes y14EA420 las envía al manager;
14E9D00 libera materiales. Que el vector se llene/vacíe no demuestra que el
consumidor renderer haya instalado el resultado. No se cancela pending ni se
interpreta cancelación como join. No hay loader, shellcode, llamadas arbitrarias,
WPM externo, UI, cambios de ESP ni ejecución live en esta carpeta.


## Revisión de propietario observado y plazo

La evidencia runtime acotada aportada por el coordinador identificó el subobjeto
C_AK47 real. `work/skin-actual-owner-callbacks.c`955A00 resta610 y llama93BE00;
`work/skin-owner-render-consumer.c` conecta ese consumidor con RenderComponent+338.
Esto permite reemplazar el gate base imposible por la identidad exacta observada;
no valida todavía la apariencia ni todos los lifetimes de la ruta live.

`GetTickCount64` se registra al primer dirty antes del setter. Al cumplirse60000ms,
la siguiente fase8 válida solicita restauración automáticamente. La prueba pura
comprueba59999/60000/60001ms y cambio de identidad al vencer. No crea un hilo/timer
que escriba fuera de main thread; si no hay fase válida, pending no drena o cambia
identidad, no puede garantizar restauración a los60s. Mantiene dirty/NeedsRestore
y requiere cerrar la práctica en error. La CAPI80 y los cuatro exports no cambian.


## Cambio experimental V2: restauración visual sin soltar el arma

La prueba live de V1, realizada por el coordinador, confirmó pintura1171 visible,
y después ausencia de atributo/getter0/pending0/materials0 con Shutdown exitoso.
La apariencia modificada persistió: cambiar cuchillo/AK no la restauró. Soltar y
recoger la misma AK sí devolvió la apariencia original, confirmado por el usuario.
V1 por tanto restauró los datos, pero no completó restauración visual inmediata.
Este módulo V2 es otra DLL y conserva la V1 pinneada y su archivo sin modificación.

Hipótesis mínima: el callback955A00 sólo llama AED800(weapon,true) cuando el owner
tiene materiales>0; al restaurar default con count0 omite esa marca. V2 realiza
803780(weapon,0), revalida identidad/pending0, AED800(weapon,true), revalida de nuevo,
y93BE00(weapon). AED800 marca bit0 en entity+3BC; no se atribuye un nombre de estado
no probado.93BE00 recorre RenderComponent+338 y prepara reemplazos materiales con
14E9E90(owner). La firma y efecto provienen de work/skin-owner-render-consumer.c.
No se llama full OnDataChanged8209D0 ni se agrega otro contrato de lifecycle.

La secuencia adicional sucede en aplicación y restauración, incluso con materials0;
el fake backend prueba ese caso. Si native regen deja pending>0, la revalidación
falla cerrada: no se marca ni consume bajo pending y dirty permanece para restore.
V2 requiere nueva comprobación visual; State6 sigue describiendo datos/contadores,
no prueba apariencia. No se ejecutó CS2 para construir o verificar esta revisión.
