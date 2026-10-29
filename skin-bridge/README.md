# Puente experimental de skins

Código de observación y ensayos locales para builds concretas de CS2. No es el backend del menú y no soporta un skinchanger completo.

- `src/`, `include/`, `tests/`: núcleo de observación y pruebas offline.
- `local-test/`, `local-test-v2/`: ensayos anteriores para14192.
- `local-test-14193/`: adaptación para AK-47/Inheritance en14193; la validación visual quedó pendiente.

Requisitos: Visual Studio2022 Build Tools con C++/MASM y Windows SDK. `scripts/vs-env.cmd` detecta la instalación; `VSDEVCMD` permite indicar otra ruta de `VsDevCmd.bat`.

Desde esta carpeta, `build.cmd` compila y ejecuta las comprobaciones offline. Compilar no valida la ejecución en el juego. No se incluyen manifiestos de artefactos revisados; los loaders rechazan un ensayo sin su manifiesto. Los ensayos mantienen restricciones de versión, hash, identidad, propietario y práctica local `-insecure`. Los DLL de los ensayos permanecen residentes hasta cerrar CS2; no deben reconstruirse mientras estén cargados.
