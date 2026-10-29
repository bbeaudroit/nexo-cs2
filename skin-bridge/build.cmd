@echo off
call "%~dp0..\scripts\vs-env.cmd"
if errorlevel 1 exit /b %errorlevel%
if errorlevel 1 exit /b %errorlevel%
if not exist build mkdir build
cl /nologo /std:c++20 /W4 /WX /EHsc /O2 /Iinclude tests\core_tests.cpp /Febuild\core_tests.exe /Fobuild\core_tests.obj
if errorlevel 1 exit /b %errorlevel%
cl /nologo /std:c++20 /W4 /WX /EHsc /O2 /Iinclude tests\slot_tests.cpp /Febuild\slot_tests.exe /Fobuild\slot_tests.obj
if errorlevel 1 exit /b %errorlevel%
ml64 /nologo /c /Fobuild\stage_thunk.obj src\stage_thunk.asm
if errorlevel 1 exit /b %errorlevel%
cl /nologo /std:c++20 /W4 /WX /EHsc /O2 /DNEXO_BRIDGE_BUILD /Iinclude /LD src\bridge.cpp build\stage_thunk.obj /Febuild\Nexo.SkinBridge.Observation.dll /Fobuild\bridge.obj /link bcrypt.lib /DYNAMICBASE /NXCOMPAT
if errorlevel 1 exit /b %errorlevel%
cl /nologo /std:c++20 /W4 /WX /EHsc /O2 /Iinclude tests\host_tests.cpp /Febuild\host_tests.exe /Fobuild\host_tests.obj
if errorlevel 1 exit /b %errorlevel%
ml64 /nologo /c /Fobuild\clobber_observer.obj tests\clobber_observer.asm
if errorlevel 1 exit /b %errorlevel%
cl /nologo /std:c++20 /W4 /WX /EHsc /O2 tests\abi_tests.cpp build\stage_thunk.obj build\clobber_observer.obj /Febuild\abi_tests.exe /Fobuild\abi_tests.obj
if errorlevel 1 exit /b %errorlevel%
build\core_tests.exe
if errorlevel 1 exit /b %errorlevel%
build\slot_tests.exe
if errorlevel 1 exit /b %errorlevel%
build\host_tests.exe
if errorlevel 1 exit /b %errorlevel%
build\abi_tests.exe

