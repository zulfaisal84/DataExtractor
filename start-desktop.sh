#!/bin/bash

# Document Intelligence Desktop Application Launcher
# This script starts the desktop application with proper configuration for macOS

echo "🚀 Starting Document Intelligence Desktop Application..."
echo "🖥️  This will open a persistent desktop window"
echo ""

# Navigate to the desktop project directory
cd "$(dirname "$0")/DocumentExtractor.Desktop"

# Check if project file exists
if [ ! -f "DocumentExtractor.Desktop.csproj" ]; then
    echo "❌ Error: DocumentExtractor.Desktop.csproj not found"
    echo "   Make sure you're running this script from the correct directory"
    exit 1
fi

echo "⚙️  Environment Configuration:"
echo "   Platform: Avalonia UI (Cross-platform)"
echo "   Database: SQLite with Entity Framework Core"
echo "   UI Framework: .NET 9 Desktop"
echo ""

# Build the application
echo "🔨 Building desktop application..."
dotnet build --configuration Debug

if [ $? -ne 0 ]; then
    echo "❌ Build failed. Please check the errors above."
    exit 1
fi

echo "✅ Build successful!"
echo ""
echo "🖥️  Starting desktop application..."
echo "   Features:"
echo "   • Window stays open permanently"
echo "   • Minimize and Close buttons provided"
echo "   • Database statistics displayed"
echo "   • Professional UI with real-time data"
echo ""
echo "💡 Tips:"
echo "   • The window will NOT close automatically"
echo "   • Use the 'Minimize' button to minimize to taskbar"
echo "   • Use the 'Close App' button to exit completely"
echo "   • Window should appear on your screen within 5 seconds"
echo ""

# Run the application
dotnet run --configuration Debug --no-build

echo ""
echo "✅ Desktop application closed"