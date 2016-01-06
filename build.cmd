@echo off
cls

build\tools\nuget\nuget.exe restore build\tools\build.project.json -OutputDirectory "artifacts\#build_deps"
artifacts\#build_deps\FAKE\4.12.0\tools\fake.exe build.fsx %*
