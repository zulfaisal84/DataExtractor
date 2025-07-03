#!/bin/bash

# Document Intelligence Web Application Startup Script
# This script starts the web application with proper configuration for macOS

echo "🚀 Starting Document Intelligence Web Application..."
echo "📍 This will open the web interface in your browser"
echo ""

# Navigate to the web project directory
cd "$(dirname "$0")/DocumentExtractor.Web"

# Check if project file exists
if [ ! -f "DocumentExtractor.Web.csproj" ]; then
    echo "❌ Error: DocumentExtractor.Web.csproj not found"
    echo "   Make sure you're running this script from the correct directory"
    exit 1
fi

# Set environment variables for better macOS compatibility
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="http://localhost:5286;http://127.0.0.1:5286;http://0.0.0.0:5286"

echo "⚙️  Environment Configuration:"
echo "   Environment: $ASPNETCORE_ENVIRONMENT"
echo "   URLs: $ASPNETCORE_URLS"
echo ""

# Build and run the application
echo "🔨 Building application..."
dotnet build --configuration Debug

if [ $? -ne 0 ]; then
    echo "❌ Build failed. Please check the errors above."
    exit 1
fi

echo "✅ Build successful!"
echo ""
echo "🌐 Starting web server..."
echo "   The application will be available at:"
echo "   • http://localhost:5286"
echo "   • http://127.0.0.1:5286"
echo ""
echo "💡 Tips:"
echo "   • If one URL doesn't work, try the other"
echo "   • The application will automatically open your default browser"
echo "   • Press Ctrl+C to stop the server"
echo ""

# Run the application
dotnet run --configuration Debug --no-build

echo ""
echo "✅ Web application stopped"