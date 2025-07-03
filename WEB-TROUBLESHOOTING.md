# Web Interface Troubleshooting Guide

## 🌐 Document Intelligence Web Application

### Quick Start Options (Try in Order):

#### Option 1: Use the Startup Script
```bash
cd /Users/muhamadzulfaisalsallehmustafa/DataExtractor
./start-web.sh
```

#### Option 2: Manual Start
```bash
cd /Users/muhamadzulfaisalsallehmustafa/DataExtractor/DocumentExtractor.Web
dotnet run
```

#### Option 3: Specific Profile
```bash
cd /Users/muhamadzulfaisalsallehmustafa/DataExtractor/DocumentExtractor.Web
dotnet run --launch-profile "macOS-Friendly"
```

### 🔗 URLs to Try (in order of preference):

1. **Primary**: http://127.0.0.1:5286
2. **Alternative**: http://localhost:5286
3. **Network**: http://0.0.0.0:5286

### 🚨 Common Issues & Solutions:

#### Issue: "This site can't be reached" or "Connection refused"
**Solutions:**
1. Make sure the application is running (check terminal output)
2. Try different URLs listed above
3. Check if another application is using port 5286:
   ```bash
   lsof -i :5286
   ```
4. Use a different port:
   ```bash
   export ASPNETCORE_URLS="http://localhost:5287"
   dotnet run
   ```

#### Issue: HTTP 403 "Access Denied"
**Solutions:**
1. The new configuration disables HTTPS redirection in development
2. Make sure you're using HTTP (not HTTPS) URLs
3. Try the 127.0.0.1 URL instead of localhost
4. Clear browser cache and try incognito mode

#### Issue: Port Already in Use
**Solutions:**
1. Kill existing processes:
   ```bash
   pkill -f "dotnet.*DocumentExtractor.Web"
   ```
2. Use different port:
   ```bash
   export ASPNETCORE_URLS="http://localhost:5287"
   dotnet run
   ```

#### Issue: Database Errors
**Solution:**
The application will automatically create the database. If you see database errors:
```bash
rm -rf ~/.local/share/DocumentExtractor/document_extraction.db
dotnet run
```

### 🔧 Configuration Changes Made:

1. **Network Binding**: Application now binds to all network interfaces
2. **CORS Policy**: Added for local development
3. **HTTPS Disabled**: In development mode to avoid certificate issues
4. **Multiple URLs**: Application listens on multiple URL formats
5. **Enhanced Logging**: Better error messages and startup information

### 📊 Expected Output:

When the application starts successfully, you should see:
```
🚀 Document Intelligence Web Application Starting...
📊 Database initialized successfully
🌐 Web server is starting on:
   HTTP:  http://localhost:5286
   HTTPS: https://localhost:7133
🔗 Alternative access URLs:
   HTTP:  http://127.0.0.1:5286
   HTTP:  http://0.0.0.0:5286
💡 Try opening any of these URLs in your browser
✅ Ready to accept connections!
```

### 🎯 What You Should See in Browser:

The web interface will show:
- Document Intelligence Dashboard
- Database statistics (documents, patterns, success rates)
- Professional UI with Bootstrap styling
- Functional navigation and document management

### 🆘 If Nothing Works:

1. Check firewall settings (System Preferences > Security & Privacy > Firewall)
2. Try a different browser
3. Restart your computer and try again
4. Move to Option C: Desktop Application (Avalonia)

### 📞 Success Indicators:

✅ Application builds without errors
✅ Database creates successfully  
✅ Web server starts and shows startup messages
✅ Browser can access the interface
✅ You can see the dashboard with statistics