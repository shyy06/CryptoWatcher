@echo off
setlocal
title CryptoWatcher (WPF / .NET 8) Release Builder

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] .NET SDK not found. Install .NET 8 SDK first:
    echo         https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

set "PROJ=%~dp0CryptoWatcher\CryptoWatcher.csproj"
set "OUT=%~dp0publish"

if not exist "%PROJ%" (
    echo [ERROR] Project file not found: %PROJ%
    pause
    exit /b 1
)

if exist "%OUT%" rmdir /s /q "%OUT%"

if /i "%~1"=="portable" goto :portable

echo ============================================
echo  Mode: FRAMEWORK-DEPENDENT single file
echo  Needs .NET 8 Desktop Runtime on the target PC
echo ============================================
echo.
dotnet publish "%PROJ%" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "%OUT%\win-x64"
if errorlevel 1 goto :fail

echo.
echo [OK] Output: %OUT%\win-x64\CryptoWatcher.exe
echo [HINT] For a portable build that needs no runtime, run:
echo        build-release.bat portable
start "" explorer "%OUT%\win-x64"
pause
exit /b 0

:portable
echo ============================================
echo  Mode: SELF-CONTAINED single file (portable)
echo  No runtime needed, larger exe (~150 MB)
echo ============================================
echo.
dotnet publish "%PROJ%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%OUT%\portable"
if errorlevel 1 goto :fail

echo.
echo [OK] Output: %OUT%\portable\CryptoWatcher.exe
start "" explorer "%OUT%\portable"
pause
exit /b 0

:fail
echo.
echo [ERROR] Publish failed. See errors above.
pause
exit /b 1
