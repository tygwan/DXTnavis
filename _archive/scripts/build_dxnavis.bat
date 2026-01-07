@echo off
echo ========================================
echo DXnavis Build and Deploy Script
echo ========================================
echo.

REM Visual Studio 2022 Build Tools 경로
set MSBUILD="C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

REM 프로젝트 경로
set PROJECT="c:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\DXnavis\DXnavis.csproj"

REM MSBuild가 있는지 확인
if not exist %MSBUILD% (
    echo [ERROR] MSBuild not found at %MSBUILD%
    echo Please install Visual Studio 2022 or modify the path
    pause
    exit /b 1
)

echo [1/2] Building DXnavis...
%MSBUILD% %PROJECT% /p:Configuration=Debug /p:Platform=AnyCPU /t:Rebuild /v:minimal

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed!
    pause
    exit /b 1
)

echo.
echo [2/2] Build successful!
echo PostBuild event will automatically deploy to Navisworks.
echo.
echo ========================================
echo Deployment completed!
echo ========================================
echo.
echo You can now run DXnavis from Navisworks 2025
echo Add-ins ^> DXnavis 속성 확인기
echo.
pause
