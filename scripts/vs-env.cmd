@echo off
if defined VSCMD_VER exit /b 0
if defined VSDEVCMD goto nexo_explicit
set "NEXO_VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%NEXO_VSWHERE%" (
  echo Visual Studio Build Tools not found. Set VSDEVCMD to VsDevCmd.bat.
  exit /b 1
)
for /f "usebackq tokens=*" %%i in (`"%NEXO_VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "NEXO_VSINSTALL=%%i"
if not defined NEXO_VSINSTALL exit /b 1
call "%NEXO_VSINSTALL%\Common7\Tools\VsDevCmd.bat" -arch=x64 -host_arch=x64 >nul
exit /b %errorlevel%
:nexo_explicit
call "%VSDEVCMD%" -arch=x64 -host_arch=x64 >nul
exit /b %errorlevel%
