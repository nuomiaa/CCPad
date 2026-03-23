@echo off
echo [1/2] Publishing...
dotnet publish CCPad/CCPad.csproj -c Release -r win-x64 --self-contained
if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo [2/2] Packaging installer...
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
if %errorlevel% neq 0 (
    echo Inno Setup compile failed!
    pause
    exit /b 1
)

echo Done! Output: installer_output\CCPad-Setup-x64.exe
pause
