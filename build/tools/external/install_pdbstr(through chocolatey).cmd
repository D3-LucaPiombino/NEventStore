@echo off

echo This script will install the SourceLink that contains pdbstr.exe (and its dependencies).
echo Note that chocolately will be installed in the system.

pause

net session >nul 2>&1
if %errorLevel% == 0 (
    echo Success: Administrative permissions confirmed.
) else (
    echo Failure: Administrative permissions required. 
    echo Please relaunch the script with administrative privileges.
    goto exit
)


@powershell -NoProfile -ExecutionPolicy Bypass -Command "iex ((new-object net.webclient).DownloadString('https://chocolatey.org/install.ps1'))" && SET PATH=%PATH%;%ALLUSERSPROFILE%\chocolatey\bin
cmd choco install SourceLink

:exit
pause