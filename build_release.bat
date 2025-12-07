@echo off
echo ========================================
echo Prospect Server Overlay - Release Build
echo ========================================

echo.
echo Building Release version...
dotnet publish ProspectServerOverlay/ProspectServerOverlay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

echo.
echo Creating Release directory...
if exist Release rmdir /s /q Release
mkdir Release
copy "ProspectServerOverlay\bin\Release\net9.0-windows\win-x64\publish\*" Release\
copy "ProspectServerOverlay\README.md" "Release\README.txt"

echo.
echo Creating portable ZIP package...
powershell -command "Compress-Archive -Path 'Release\*' -DestinationPath 'ProspectServerOverlay_Portable.zip' -Force"

echo.
echo ========================================
echo Build Complete!
echo ========================================
echo.
echo Files created:
echo - Release\ directory (portable package)
echo - ProspectServerOverlay_Portable.zip
echo.
echo To create installer:
echo 1. Download Inno Setup from https://jrsoftware.org/isinfo.php
echo 2. Open installer.iss in Inno Setup Compiler
echo 3. Click Compile
echo 4. Output: ProspectServerOverlay_Installer.exe
echo.
echo Distribution options:
echo 1. Installer: ProspectServerOverlay_Installer.exe (recommended)
echo 2. Portable: ProspectServerOverlay_Portable.zip
echo.
pause