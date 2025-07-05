using DocumentExtractor.Data.Context;
using DocumentExtractor.Core.Interfaces;
using DocumentExtractor.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to bind to IPv4 localhost for macOS compatibility
builder.WebHost.ConfigureKestrel(options =>
{
    // Listen on IPv4 localhost for HTTP
    options.ListenLocalhost(5286);
    // Listen on IPv4 localhost for HTTPS (optional)
    options.ListenLocalhost(7133, listenOptions =>
    {
        listenOptions.UseHttps();
    });
});

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add CORS policy for local development
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDevelopment",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// Add Entity Framework Core with SQLite
builder.Services.AddDbContext<DocumentExtractionContext>(options =>
{
    // Use the same database path as the console application
    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var appFolder = Path.Combine(appDataPath, "DocumentExtractor");
    Directory.CreateDirectory(appFolder);
    var dbPath = Path.Combine(appFolder, "document_extraction.db");
    
    options.UseSqlite($"Data Source={dbPath}");
    
    // Enable detailed logging in development
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Register OCR and document processing services
builder.Services.AddScoped<ITextExtractor, TesseractTextExtractor>();
builder.Services.AddScoped<DocumentUploadService>();
builder.Services.AddScoped<RealDocumentProcessor>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseCors("LocalDevelopment");
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Disable HTTPS redirection for local development to avoid certificate issues
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Ensure database is created and display startup information
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<DocumentExtractionContext>();
    context.EnsureCreated();
    
    // Display startup information
    Console.WriteLine("🚀 Document Intelligence Web Application Starting...");
    Console.WriteLine("📊 Database initialized successfully");
    Console.WriteLine("🌐 Web server is starting on:");
    Console.WriteLine("   HTTP:  http://localhost:5286");
    Console.WriteLine("   HTTPS: https://localhost:7133");
    Console.WriteLine("🔗 Alternative access URLs:");
    Console.WriteLine("   HTTP:  http://127.0.0.1:5286");
    Console.WriteLine("   HTTP:  http://0.0.0.0:5286");
    Console.WriteLine("💡 Try opening any of these URLs in your browser");
    Console.WriteLine("✅ Ready to accept connections!");
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Document}/{action=Index}/{id?}");

app.Run();
