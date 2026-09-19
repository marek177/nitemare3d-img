@echo off
setlocal EnableExtensions
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK was not found in PATH.
    echo Install the .NET 8 SDK and run this script again.
    pause
    exit /b 1
)

dotnet --list-sdks | findstr /R /B "8\.0\." >nul
if errorlevel 1 (
    echo ERROR: .NET 8 SDK is not installed.
    echo Install the .NET 8 SDK and run this script again.
    pause
    exit /b 1
)

echo Building Nitemare 3D IMG Editor for Windows x64 using .NET 8...

if exist "publish\win-x64" rmdir /s /q "publish\win-x64"

dotnet restore "Nitemare3D.ImgEditor.csproj" -r win-x64
if errorlevel 1 goto :failed

dotnet publish "Nitemare3D.ImgEditor.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained false ^
  -p:Platform=x64 ^
  --no-restore ^
  -o "publish\win-x64"
if errorlevel 1 goto :failed

echo.
echo BUILD SUCCESSFUL
 echo Output: %~dp0publish\win-x64\
pause
exit /b 0

:failed
echo.
echo BUILD FAILED
pause
exit /b 1
