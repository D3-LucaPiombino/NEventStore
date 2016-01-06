
cls

pushd src
..\build\tools\nuget\nuget.exe restore
popd

build\tools\nuget\nuget.exe restore build\tools\build.project.json -OutputDirectory "build\tools"
build\tools\FAKE\4.12.0\tools\fake.exe build.fsx %*
