# Compilar

Windows x64, Node.js24, npm y .NET SDK10. El código se publica en modo gratuito (`desktop-ui/build-channel.json`: `owner`); no pide una licencia de pago. El backend comercial anterior no forma parte de este repo.

Desde la raíz:

```powershell
npm ci --prefix desktop-ui
npm ci --prefix sites/nexo-vercel
npm run sync:menu --prefix sites/nexo-vercel
npm run build --prefix desktop-ui
npm run build --prefix sites/nexo-vercel
dotnet run --project input-checks
npm test --prefix desktop-ui
npm test --prefix sites/nexo-vercel
node --test tools/tests/*.test.cjs
```

Para generar el instalador, descargá `cloudflared-windows-amd64.exe` de los releases oficiales de [Cloudflare](https://github.com/cloudflare/cloudflared/releases) y guardalo como `tools/cloudflared.exe`. Luego:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/prepare-release.ps1
npm run build --prefix desktop-ui
node desktop-ui/scripts/prepare-package.cjs
Push-Location desktop-ui
node node_modules/electron-builder/cli.js --config release-config.json --publish never
Pop-Location
```

El resultado queda en `release/`. Los modelos y mapas no se distribuyen: `Preparar-Modelo.ps1` los importa de tu instalación de CS2. Nexo espera a que el juego esté abierto.

Los offsets incluidos corresponden a14193. Si CS2 cambia, descargá [cs2-dumper](https://github.com/a2x/cs2-dumper/releases), dejá su ejecutable en la raíz y usá `Ejecutar-Dumper.ps1`. Ese script genera un volcado separado; verificá la build y las lecturas antes de reemplazar `dumps/manual-01`. No mezcles offsets de builds distintas.

Para la web: `npm run dev --prefix sites/nexo-vercel`. Las integraciones de Discord y almacenamiento se configuran con variables de entorno propias; el ejemplo no contiene credenciales.

Los experimentos de `skin-bridge/` requieren Visual Studio2022 con C++/MASM y Windows SDK. Sus scripts detectan Build Tools con `vswhere`, o aceptan `VSDEVCMD` con la ruta de `VsDevCmd.bat`. Sus ensayos de escritura están restringidos a práctica local con `-insecure`; no están integrados al menú. Los manifiestos de ensayos revisados no se distribuyen: compilar no equivale a validar un DLL para ejecutarlo en el juego.

Para abrir el menú desde el checkout: `powershell -NoProfile -File scripts/dev.ps1`. Crea una CFG predeterminada ignorada por Git y compila el motor antes de abrir Electron; no inicia CS2.

Las pruebas que necesitan modelos o física extraídos de tu CS2 se omiten explícitamente hasta que los importes. Las pruebas de la API web requieren el frontend compilado con `npm run build --prefix sites/nexo-vercel`.
