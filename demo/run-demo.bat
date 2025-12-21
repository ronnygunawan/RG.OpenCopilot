@echo off
REM Demo runner script for RG.OpenCopilot
REM This script demonstrates the core capabilities without requiring GitHub setup

echo ╔══════════════════════════════════════════════════════════════╗
echo ║         RG.OpenCopilot - Quick Start Demo                    ║
echo ╚══════════════════════════════════════════════════════════════╝
echo.

REM Check if we're in the right directory
if not exist "RG.OpenCopilot.slnx" (
    echo ❌ Error: Please run this script from the RG.OpenCopilot root directory
    exit /b 1
)

REM Check if dotnet is installed
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ❌ Error: .NET 10.0 SDK is required but not installed
    echo    Download from: https://dotnet.microsoft.com/download/dotnet/10.0
    exit /b 1
)

echo 🔧 Building demo application...
dotnet build RG.OpenCopilot.Demo\RG.OpenCopilot.Demo.csproj --configuration Release -v quiet

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ❌ Build failed. Please ensure all dependencies are installed.
    exit /b 1
)

echo.
echo 🚀 Running demo...
echo.

REM Run the demo
dotnet run --project RG.OpenCopilot.Demo\RG.OpenCopilot.Demo.csproj --configuration Release --no-build

exit /b %ERRORLEVEL%
