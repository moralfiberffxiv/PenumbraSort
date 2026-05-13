#!/bin/bash

# Build script for PenumbraSort Dalamud plugin

set -e

echo "Building PenumbraSort..."
dotnet build -c Release

echo ""
echo "Build complete!"
echo "Plugin DLL: bin/x64/Release/net8.0-windows/PenumbraSort.dll"
