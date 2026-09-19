@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo ==============================================
echo Nitemare 3D IMG Editor - .NET 8 x64 + x86
echo ==============================================
echo.

call build-win64.bat
if errorlevel 1 exit /b 1

call build-win32.bat
if errorlevel 1 exit /b 1

echo.
echo BOTH BUILDS COMPLETED SUCCESSFULLY.
echo x64: %~dp0publish\win-x64\
echo x86: %~dp0publish\win-x86\
pause
exit /b 0
