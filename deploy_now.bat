@echo off
echo === DXTnavis Manual Deploy ===

set DEST=C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\DXTnavis
set SRC=C:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\dxtnavis

if not exist "%DEST%" mkdir "%DEST%"

echo Copying DXTnavis.dll...
copy /Y "%SRC%\bin\Debug\DXTnavis.dll" "%DEST%\"

echo Copying Newtonsoft.Json.dll...
copy /Y "%SRC%\packages\Newtonsoft.Json.13.0.4\lib\net45\Newtonsoft.Json.dll" "%DEST%\"

echo Copying System.Text.Json.dll...
copy /Y "%SRC%\packages\System.Text.Json.7.0.0\lib\net462\System.Text.Json.dll" "%DEST%\"

echo Copying System.Text.Encodings.Web.dll...
copy /Y "%SRC%\packages\System.Text.Encodings.Web.7.0.0\lib\net462\System.Text.Encodings.Web.dll" "%DEST%\"

echo Copying Microsoft.Bcl.AsyncInterfaces.dll...
copy /Y "%SRC%\packages\Microsoft.Bcl.AsyncInterfaces.7.0.0\lib\net462\Microsoft.Bcl.AsyncInterfaces.dll" "%DEST%\"

echo Copying System.Buffers.dll...
copy /Y "%SRC%\packages\System.Buffers.4.5.1\lib\net461\System.Buffers.dll" "%DEST%\"

echo Copying System.Memory.dll...
copy /Y "%SRC%\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll" "%DEST%\"

echo Copying System.Numerics.Vectors.dll...
copy /Y "%SRC%\packages\System.Numerics.Vectors.4.5.0\lib\net46\System.Numerics.Vectors.dll" "%DEST%\"

echo Copying System.Runtime.CompilerServices.Unsafe.dll...
copy /Y "%SRC%\packages\System.Runtime.CompilerServices.Unsafe.6.0.0\lib\net461\System.Runtime.CompilerServices.Unsafe.dll" "%DEST%\"

echo Copying System.Threading.Tasks.Extensions.dll...
copy /Y "%SRC%\packages\System.Threading.Tasks.Extensions.4.5.4\lib\net461\System.Threading.Tasks.Extensions.dll" "%DEST%\"

echo Copying System.ValueTuple.dll...
copy /Y "%SRC%\packages\System.ValueTuple.4.5.0\lib\net47\System.ValueTuple.dll" "%DEST%\"

echo.
echo === Deploy Complete ===
echo.
dir "%DEST%"
pause
