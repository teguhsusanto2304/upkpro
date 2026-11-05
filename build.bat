
@echo off
setlocal

echo =====================================
echo        UPKPro - BUILD PROJECT
echo =====================================

REM Set DOTNET_ROOT
set "DOTNET_ROOT=%APPDATA%\Code\User\globalStorage\ms-dotnettools.vscode-dotnet-runtime\.dotnet"

REM Tentukan lokasi dotnet.exe
set "DOTNET_EXE=%DOTNET_ROOT%\dotnet.exe"

REM Validasi dotnet.exe
if not exist "%DOTNET_EXE%" (
    echo ERROR: dotnet.exe tidak ditemukan!
    echo Lokasi: %DOTNET_EXE%
    pause
    exit /b 1
)

REM Build project
"%DOTNET_EXE%" build "UPKPro\UPKPro.csproj"

if errorlevel 1 (
    echo.
    echo BUILD FAILED!
) else (
    echo.
    echo BUILD SUCCESS!
)

pause
endlocal
