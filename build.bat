@echo off
:: Build script for BK7231GUIFlashTool
:: Locates MSBuild from the newest Visual Studio install (2022, 2026, Build Tools...) via vswhere.

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set MSBUILD_PATH=

if exist "%VSWHERE%" (
    for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
        if not defined MSBUILD_PATH set "MSBUILD_PATH=%%i"
    )
)

if not defined MSBUILD_PATH (
    echo [ERROR] MSBuild.exe not found.
    echo Please install Visual Studio 2022 or newer with the ".NET desktop development" workload.
    exit /b 1
)

echo [INFO] Using MSBuild: %MSBUILD_PATH%
set MSBUILD_PATH="%MSBUILD_PATH%"

:: Set version from argument or default to 1.0.0
set FLASHERVERSION=1.0.0
if not "%~1"=="" set FLASHERVERSION=%~1

echo [INFO] Building BK7231Flasher version %FLASHERVERSION%...
echo [INFO] Target: Release (Any CPU)

%MSBUILD_PATH% BK7231Flasher.sln /p:Configuration=Release /p:Platform="Any CPU" /p:FLASHERVERSION=%FLASHERVERSION% /t:Restore;Build

if %ERRORLEVEL% equ 0 (
    echo.
    echo [SUCCESS] Build completed successfully!
    echo [INFO] Executable path: BK7231Flasher\bin\Release\BK7231Flasher.exe
) else (
    echo.
    echo [ERROR] Build failed with error code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)
