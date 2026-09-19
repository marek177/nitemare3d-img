@echo off
setlocal
cd /d "%~dp0"

echo Building Nitemare 3D IMG Editor x86...
dotnet publish "Nitemare3D.ImgEditor.csproj" -c Release -r win-x86 --self-contained false -p:Platform=x86

if errorlevel 1 (
    echo.
    echo BUILD FAILED
    pause
    exit /b 1
)

echo.
echo BUILD SUCCESSFUL
echo Output: %~dp0bin\Release\net8.0-windows\win-x86\publish\
pause
