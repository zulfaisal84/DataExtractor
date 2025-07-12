using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using DocumentExtractor.Desktop.ViewModels;
using DocumentExtractor.Desktop.Views;
using System;
using System.Net.Http;
using DocumentExtractor.Desktop.Services;
using Avalonia.Controls;

namespace DocumentExtractor.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Create shared HttpClient and AuthService
            var httpClient = new HttpClient { BaseAddress = new Uri("https://localhost:7133/") };
            var authService = new Services.AuthService(httpClient);

            // Show login if user is not already authenticated
            if (!authService.IsAuthenticated)
            {
                var loginVm = new ViewModels.LoginViewModel(authService);
                var loginWindow = new Views.LoginWindow(loginVm);
                loginWindow.ShowDialog((Window?)null);
            }

            // Pass authService to MainWindowViewModel as needed (not wired yet)

            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            
            // Configure desktop lifetime to prevent auto-shutdown
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
            
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
            
            // Ensure window is shown and activated
            desktop.MainWindow.Show();
            desktop.MainWindow.Activate();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}