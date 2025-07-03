using Avalonia;
using System;

namespace DocumentExtractor.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("🚀 Starting Document Intelligence Desktop Application...");
            Console.WriteLine("📍 Application should appear in a new window");
            Console.WriteLine("⏳ Initializing...");
            
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
                
            Console.WriteLine("✅ Application closed normally");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Application startup failed: {ex}");
            Console.WriteLine($"📋 Stack trace: {ex.StackTrace}");
            Console.WriteLine("\n🔧 Press any key to exit...");
            Console.ReadKey();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
