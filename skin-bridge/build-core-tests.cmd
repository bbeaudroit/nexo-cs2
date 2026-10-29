@echo off
call "%~dp0..\scripts\vs-env.cmd"
if errorlevel 1 exit /b %errorlevel%
if errorlevel 1 exit /b %errorlevel%
if not exist build mkdir build
cl /nologo /std:c++20 /W4 /WX /EHsc /O2 /Iinclude tests\core_tests.cpp /Febuild\core_tests.exe /Fobuild\core_tests.obj
if errorlevel 1 exit /b %errorlevel%
build\core_tests.exe

