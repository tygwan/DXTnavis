@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ============================================
echo   DXTnavis Build and Deploy Script
echo ============================================
echo.

:: Paths
set "PROJECT_DIR=%~dp0.."
set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
set "TARGET_DIR=C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\DXTnavis"
set "OUTPUT_DLL=%PROJECT_DIR%\bin\Debug\DXTnavis.dll"
set "OUTPUT_PDB=%PROJECT_DIR%\bin\Debug\DXTnavis.pdb"

:: Check Navisworks
echo [1/4] Checking Navisworks status...
tasklist /FI "IMAGENAME eq Roamer.exe" 2>nul | find /I "Roamer.exe" >nul
if %ERRORLEVEL% EQU 0 (
    echo ⚠️  WARNING: Navisworks is running!
    echo    Please close Navisworks before deploying.
    echo    Or restart Navisworks after deployment.
    echo.
)

:: Build
echo [2/4] Building DXTnavis...
cd /d "%PROJECT_DIR%"
"%MSBUILD%" DXTnavis.csproj -p:Configuration=Debug -v:minimal -nologo
if %ERRORLEVEL% NEQ 0 (
    echo ❌ Build failed!
    exit /b 1
)
echo ✅ Build succeeded
echo.

:: Deploy
echo [3/4] Deploying to Navisworks...
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"
copy /Y "%OUTPUT_DLL%" "%TARGET_DIR%\" >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo ⚠️  Standard copy failed, trying with elevated permissions...
    powershell -Command "Start-Process cmd -ArgumentList '/c copy /Y \"%OUTPUT_DLL%\" \"%TARGET_DIR%\\\"' -Verb RunAs -Wait"
)
copy /Y "%OUTPUT_PDB%" "%TARGET_DIR%\" >nul 2>&1
echo ✅ Deploy completed
echo.

:: Verify
echo [4/4] Verifying deployment...
for %%F in ("%OUTPUT_DLL%") do set "SRC_SIZE=%%~zF"
for %%F in ("%TARGET_DIR%\DXTnavis.dll") do set "DST_SIZE=%%~zF"

if "%SRC_SIZE%"=="%DST_SIZE%" (
    echo ✅ Verification passed (Size: %SRC_SIZE% bytes)
) else (
    echo ⚠️  Size mismatch: Source=%SRC_SIZE%, Deployed=%DST_SIZE%
)

echo.
echo ============================================
echo   Deployment Complete!
echo   Remember to restart Navisworks.
echo ============================================

endlocal
