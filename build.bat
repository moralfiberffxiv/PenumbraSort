@echo off
REM Build script for PenumbraSort Dalamud plugin

echo Building PenumbraSort...
dotnet build -c Release

if errorlevel 1 (
    echo Build failed!
    exit /b 1
)

echo.
echo Build complete!
echo Plugin DLL: bin\x64\Release\net8.0-windows\PenumbraSort.dll
