@echo off
cls

echo %CD%
dir /s

pushd src

dir %CD%\..\build\tools\ /s
echo %CD%

%CD%\..\build\tools\nuget\nuget.exe restore
echo %CD%
popd


rem build\tools\nuget\nuget.exe restore build\tools\build.project.json -OutputDirectory "build\tools"
rem build\tools\FAKE\4.12.0\tools\fake.exe build.fsx %*
