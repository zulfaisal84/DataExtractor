using DocumentExtractor.Data.Context;
using DocumentExtractor.Core.Interfaces;
using DocumentExtractor.Services;
using Microsoft.EntityFrameworkCore;
using DocumentExtractor.Web.Data;
using DocumentExtractor.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Shared SQLite database location (both app data & identity tables)
var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var appFolder = Path.Combine(appDataPath, "DocumentExtractor");
Directory.CreateDirectory(appFolder);
var dbPath = Path.Combine(appFolder, "document_extraction.db");

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
builder.Services.AddRazorPages();

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

// Add Entity Framework Core with SQLite for application data
builder.Services.AddDbContext<DocumentExtractionContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
    
    // Enable detailed logging in development
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Add Identity database context (shares the same SQLite file)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
});

// Configure ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    options.Lockout.MaxFailedAccessAttempts = 5;
}).AddEntityFrameworkStores<ApplicationDbContext>()
  .AddDefaultTokenProviders();

// Configure JWT authentication for API / desktop-client usage
var jwtKey = builder.Configuration["Jwt:Key"] ?? "SuperSecretDevelopmentKey!123";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
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
app.UseAuthentication(); // handles both cookie & JWT schemes
app.UseAuthorization();

// Ensure database is created and display startup information
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<DocumentExtractionContext>();
    context.EnsureCreated();

    // Apply Identity migrations / create tables
    var identityContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    identityContext.Database.Migrate();

    // Seed default administrator account
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    const string adminRole = "Admin";
    const string adminEmail = "admin@example.com";
    const string adminPassword = "P@ssw0rd!";

    // Ensure Roles
    if (!roleManager.RoleExistsAsync(adminRole).Result)
    {
        roleManager.CreateAsync(new IdentityRole(adminRole)).Wait();
    }

    // Ensure Admin User
    var adminUser = userManager.FindByEmailAsync(adminEmail).Result;
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };
        userManager.CreateAsync(adminUser, adminPassword).Wait();
    }

    // Assign Role
    if (!userManager.IsInRoleAsync(adminUser, adminRole).Result)
    {
        userManager.AddToRoleAsync(adminUser, adminRole).Wait();
    }
    
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

// Map Razor Pages for Identity UI
app.MapRazorPages();

app.Run();
