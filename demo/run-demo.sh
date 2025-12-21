#!/bin/bash
# Demo runner script for RG.OpenCopilot
# This script demonstrates the core capabilities without requiring GitHub setup

set -e

echo "╔══════════════════════════════════════════════════════════════╗"
echo "║         RG.OpenCopilot - Quick Start Demo                    ║"
echo "╚══════════════════════════════════════════════════════════════╝"
echo ""

# Check if we're in the right directory
if [ ! -f "RG.OpenCopilot.slnx" ]; then
    echo "❌ Error: Please run this script from the RG.OpenCopilot root directory"
    exit 1
fi

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ Error: .NET 10.0 SDK is required but not installed"
    echo "   Download from: https://dotnet.microsoft.com/download/dotnet/10.0"
    exit 1
fi

echo "🔧 Building demo application..."
dotnet build RG.OpenCopilot.Demo/RG.OpenCopilot.Demo.csproj --configuration Release -v quiet

if [ $? -ne 0 ]; then
    echo ""
    echo "❌ Build failed. Please ensure all dependencies are installed."
    exit 1
fi

echo ""
echo "🚀 Running demo..."
echo ""

# Run the demo
dotnet run --project RG.OpenCopilot.Demo/RG.OpenCopilot.Demo.csproj --configuration Release --no-build

exit $?
