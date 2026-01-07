@echo off
chcp 65001 >nul
REM UTF-8 encoding for proper Korean display

REM ========================================
REM Navisworks DXnavis Manual Deployment
REM ========================================
REM This script is ONLY needed when:
REM   1. Navisworks is running (build fails)
REM   2. Manual redeployment needed
REM
REM Normal workflow: Just build in Visual Studio!
REM   PostBuildEvent auto-deploys to Plugins\DXnavis
REM ========================================

echo.
echo ========================================
echo Navisworks DXnavis Manual Deployment
echo ========================================
echo.
echo [INFO] This script is only needed when:
echo   1. Navisworks is running (build fails)
echo   2. Manual redeployment needed
echo.
echo [INFO] Normal workflow: Build in Visual Studio
echo   PostBuildEvent auto-deploys everything!
echo.

REM Check if Navisworks is running
tasklist /FI "IMAGENAME eq Roamer.exe" 2>NUL | find /I /N "Roamer.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo.
    echo [WARNING] Navisworks is currently running!
    echo           Close Navisworks before deployment.
    echo.
    pause
    exit /b 1
)

echo [OK] Navisworks is not running. Proceeding...
echo.

set PLUGINS_ROOT=C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins
set DXNAVIS_FOLDER=%PLUGINS_ROOT%\DXnavis
set DXBASE_SOURCE=C:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\DXBase\bin\Debug\netstandard2.0
set DXNAVIS_SOURCE=C:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\DXnavis\bin\Debug

echo [1/3] Copying DXBase and dependencies...
if exist "%DXBASE_SOURCE%" (
    xcopy /Y /Q "%DXBASE_SOURCE%\DXBase.dll" "%DXNAVIS_FOLDER%\"
    xcopy /Y /Q "%DXBASE_SOURCE%\DXBase.pdb" "%DXNAVIS_FOLDER%\"
    xcopy /Y /Q "%DXBASE_SOURCE%\System.*.dll" "%DXNAVIS_FOLDER%\"
    xcopy /Y /Q "%DXBASE_SOURCE%\Microsoft.*.dll" "%DXNAVIS_FOLDER%\"
    echo   [OK] DXBase and dependencies copied
) else (
    echo   [ERROR] DXBase source not found: %DXBASE_SOURCE%
    pause
    exit /b 1
)

echo.
echo [2/3] Copying DXnavis plugin...
if exist "%DXNAVIS_SOURCE%" (
    xcopy /Y /Q "%DXNAVIS_SOURCE%\DXnavis.dll" "%DXNAVIS_FOLDER%\"
    xcopy /Y /Q "%DXNAVIS_SOURCE%\DXnavis.pdb" "%DXNAVIS_FOLDER%\"
    xcopy /Y /Q "%DXNAVIS_SOURCE%\DXnavis.dll.config" "%DXNAVIS_FOLDER%\"
    echo   [OK] DXnavis plugin copied
) else (
    echo   [ERROR] DXnavis source not found: %DXNAVIS_SOURCE%
    pause
    exit /b 1
)

echo.
echo [3/3] Verifying deployed files...
echo.
if exist "%DXNAVIS_FOLDER%\DXnavis.dll" (
    echo   DXnavis.dll     [OK]
) else (
    echo   DXnavis.dll     [MISSING]
)

if exist "%DXNAVIS_FOLDER%\DXBase.dll" (
    echo   DXBase.dll      [OK]
) else (
    echo   DXBase.dll      [MISSING]
)

if exist "%DXNAVIS_FOLDER%\System.Text.Json.dll" (
    echo   System.Text.Json.dll [OK]
) else (
    echo   System.Text.Json.dll [MISSING]
)

if exist "%DXNAVIS_FOLDER%\System.Text.Encodings.Web.dll" (
    echo   System.Text.Encodings.Web.dll [OK]
) else (
    echo   System.Text.Encodings.Web.dll [MISSING]
)

echo.
echo ========================================
echo Deployment Completed!
echo ========================================
echo.
echo Target: %DXNAVIS_FOLDER%
echo.
echo Next steps:
echo   1. Start Navisworks
echo   2. Open DXnavis plugin
echo   3. Test project detection
echo   4. Check logs: %APPDATA%\DX_Platform\Logs\DX_yyyyMMdd.log
echo.
pause
