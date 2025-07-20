using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DocumentExtractor.Desktop.Services;
using DocumentExtractor.Desktop.ViewModels;

namespace DocumentExtractor.Desktop.Services;

/// <summary>
/// Service configuration for dependency injection
/// Registers all the hybrid AI architecture services
/// </summary>
public static class ServiceConfiguration
{
    /// <summary>
    /// Configure all services for the hybrid document processing architecture
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Configured service collection</returns>
    public static IServiceCollection ConfigureDocumentServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Core services for hybrid architecture
        services.AddSingleton<DocumentSimilarityService>();
        services.AddSingleton<LocalPatternMatchingService>();
        services.AddSingleton<AIService>();
        services.AddSingleton<HybridProcessingEngine>();
        
        // Existing services
        services.AddSingleton<GlobalAIAssistantService>();
        services.AddSingleton<ExcelDataService>();
        services.AddSingleton<HtmlTemplateService>();
        services.AddSingleton<SimpleDocumentPreviewService>();
        services.AddSingleton<SimpleTableGeneratorService>();
        
        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ConversationalLearningViewModel>();
        services.AddTransient<TemplateMappingViewModel>();
        services.AddTransient<FieldMappingDialogViewModel>();
        
        return services;
    }
    
    /// <summary>
    /// Configure logging for the application
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection with logging</returns>
    public static IServiceCollection ConfigureLogging(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        return services;
    }
    
    /// <summary>
    /// Configure application settings
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection with configuration</returns>
    public static IServiceCollection ConfigureAppSettings(this IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(System.IO.Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
            
        services.AddSingleton<IConfiguration>(configuration);
        
        return services;
    }
    
    /// <summary>
    /// Create and configure the complete service provider
    /// </summary>
    /// <returns>Configured service provider</returns>
    public static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        
        // Configure all services
        services
            .ConfigureAppSettings()
            .ConfigureLogging()
            .ConfigureDocumentServices(services.BuildServiceProvider().GetRequiredService<IConfiguration>());
        
        return services.BuildServiceProvider();
    }
}