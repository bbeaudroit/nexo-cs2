@echo off
call "%~dp0..\scripts\vs-env.cmd"
if errorlevel 1 exit /b %errorlevel%
if errorlevel 1 exit /b %errorlevel%
if not exist build mkdir build
cl /nologo /std:c++20 /W4 /WX /EHsc /O2 /Iinclude tests\slot_tests.cpp /Febuild\slot_tests.exe /Fobuild\slot_tests.obj
if errorlevel 1 exit /b %errorlevel%
build\slot_tests.exe

