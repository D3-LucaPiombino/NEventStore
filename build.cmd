@echo off
cls
pushd src
..\build\tools\nuget\nuget.exe restore -OutputDirectory "..\artifacts\#build_deps"
popd

build\tools\nuget\nuget.exe restore build\tools\build.project.json -OutputDirectory "artifacts\#build_deps"
artifacts\#build_deps\FAKE\4.12.0\tools\fake.exe build.fsx %*
