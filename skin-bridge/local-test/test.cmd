@echo off
call "%~dp0..\..\scripts\vs-env.cmd"
if errorlevel 1 exit /b %errorlevel%
if not exist build mkdir build
cl /nologo /std:c++20 /W4 /WX /EHsc /O2 /Iinclude tests\transaction_tests.cpp /Febuild\transaction_tests.exe /Fobuild\transaction_tests.obj
if errorlevel 1 exit /b %errorlevel%
build\transaction_tests.exe

