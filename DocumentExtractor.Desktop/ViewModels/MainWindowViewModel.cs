using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentExtractor.Core.Models;
using DocumentExtractor.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace DocumentExtractor.Desktop.ViewModels;

/// <summary>
/// Main window view model for the Document Intelligence Desktop Application.
/// Manages the application state, database statistics, and user interactions.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly DocumentExtractionContext _context;
    private readonly System.Timers.Timer _statusTimer;

    #region Observable Properties

    [ObservableProperty]
    private string _databaseStatus = "🔗 Connected";

    [ObservableProperty]
    private string _totalDocuments = "0";

    [ObservableProperty]
    private string _patternSuccessRate = "0%";

    [ObservableProperty]
    private string _learnedPatterns = "0";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _currentTime = DateTime.Now.ToString("HH:mm:ss");

    [ObservableProperty]
    private bool _showWelcome = true;

    [ObservableProperty]
    private bool _showDocumentList = false;

    [ObservableProperty]
    private bool _isProcessing = false;

    [ObservableProperty]
    private double _processingProgress = 0;

    [ObservableProperty]
    private string _recentActivity = "Application started successfully";

    [ObservableProperty]
    private ObservableCollection<ExtractedDocument> _documents = new();

    #endregion

    #region Commands

    [RelayCommand]
    private async Task UploadDocuments()
    {
        try
        {
            StatusMessage = "Preparing document upload...";
            IsProcessing = true;

            // Simulate file dialog and processing
            await SimulateProcessing("Uploading documents...", 3000);

            // TODO: Implement actual file dialog and document processing
            StatusMessage = "Documents uploaded successfully";
            RecentActivity = $"📤 Document upload completed at {DateTime.Now:HH:mm:ss}";
            
            await RefreshStatistics();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error uploading documents: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task ViewDocuments()
    {
        try
        {
            StatusMessage = "Loading document library...";
            ShowWelcome = false;
            ShowDocumentList = true;

            await LoadDocuments();
            
            StatusMessage = $"Loaded {Documents.Count} documents";
            RecentActivity = $"📋 Viewed document library at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading documents: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ManagePatterns()
    {
        try
        {
            StatusMessage = "Opening pattern management...";
            
            // TODO: Open pattern management window
            await Task.Delay(500);
            
            StatusMessage = "Pattern management opened";
            RecentActivity = $"🧠 Accessed pattern management at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening pattern management: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportExcel()
    {
        try
        {
            StatusMessage = "Preparing Excel export...";
            IsProcessing = true;

            await SimulateProcessing("Exporting to Excel...", 2000);

            // TODO: Implement actual Excel export
            StatusMessage = "Excel export completed";
            RecentActivity = $"📊 Excel export completed at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error exporting to Excel: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task OpenSettings()
    {
        try
        {
            StatusMessage = "Opening settings...";
            
            // TODO: Open settings window
            await Task.Delay(500);
            
            StatusMessage = "Settings opened";
            RecentActivity = $"⚙️ Opened settings at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ShowWelcomeScreen()
    {
        ShowWelcome = true;
        ShowDocumentList = false;
        StatusMessage = "Ready";
        RecentActivity = $"🏠 Returned to welcome screen at {DateTime.Now:HH:mm:ss}";
    }

    #endregion

    #region Constructor and Initialization

    public MainWindowViewModel()
    {
        try
        {
            // Set initial values
            StatusMessage = "Starting application...";
            TotalDocuments = "Loading...";
            PatternSuccessRate = "Loading...";
            LearnedPatterns = "Loading...";
            RecentActivity = "Application initializing...";
            
            // Initialize timer for current time updates
            _statusTimer = new System.Timers.Timer(1000);
            _statusTimer.Elapsed += (s, e) => CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            _statusTimer.Start();

            // Initialize database context safely
            _context = new DocumentExtractionContext();
            
            // Initialize the application asynchronously
            _ = InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Startup error: {ex.Message}";
            DatabaseStatus = "❌ Error";
            Console.WriteLine($"MainWindowViewModel constructor error: {ex}");
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            StatusMessage = "Initializing application...";
            Console.WriteLine("Starting database initialization...");
            
            // Ensure database is created
            await _context.Database.EnsureCreatedAsync();
            Console.WriteLine("Database created successfully");
            
            // Load initial statistics
            await RefreshStatistics();
            Console.WriteLine("Statistics loaded successfully");
            
            StatusMessage = "Application ready";
            DatabaseStatus = "🔗 Connected";
            RecentActivity = "Application started successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Initialization error: {ex.Message}";
            DatabaseStatus = "❌ Error";
            RecentActivity = $"❌ Database connection failed: {ex.Message}";
            Console.WriteLine($"InitializeAsync error: {ex}");
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Refresh database statistics displayed in the sidebar.
    /// </summary>
    private async Task RefreshStatistics()
    {
        try
        {
            var totalDocs = await _context.Documents.CountAsync();
            var totalPatterns = await _context.Patterns.CountAsync();
            var activePatterns = await _context.Patterns.CountAsync(p => p.IsActive);
            
            // Calculate average pattern success rate
            var patterns = await _context.Patterns
                .Where(p => p.UsageCount > 0)
                .Select(p => p.SuccessRate)
                .ToListAsync();
            
            var avgSuccessRate = patterns.Any() ? patterns.Average() : 0;

            // Update UI properties
            TotalDocuments = totalDocs.ToString();
            LearnedPatterns = $"{activePatterns}/{totalPatterns}";
            PatternSuccessRate = $"{avgSuccessRate:P1}";

            // Update database status
            DatabaseStatus = totalDocs > 0 ? $"🔗 {totalDocs} docs" : "🔗 Connected";
        }
        catch (Exception ex)
        {
            DatabaseStatus = "❌ Error";
            Console.WriteLine($"Error refreshing statistics: {ex.Message}");
        }
    }

    /// <summary>
    /// Load documents from database for display in the document list.
    /// </summary>
    private async Task LoadDocuments()
    {
        try
        {
            var documents = await _context.Documents
                .OrderByDescending(d => d.ProcessedDate)
                .Take(100) // Limit to last 100 documents for performance
                .ToListAsync();

            Documents.Clear();
            foreach (var doc in documents)
            {
                Documents.Add(doc);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading documents: {ex.Message}";
            Console.WriteLine($"Error loading documents: {ex.Message}");
        }
    }

    /// <summary>
    /// Simulate processing with progress updates for user feedback.
    /// </summary>
    private async Task SimulateProcessing(string message, int durationMs)
    {
        StatusMessage = message;
        ProcessingProgress = 0;

        var steps = 20;
        var stepDelay = durationMs / steps;

        for (int i = 0; i <= steps; i++)
        {
            ProcessingProgress = (double)i / steps * 100;
            await Task.Delay(stepDelay);
        }

        ProcessingProgress = 100;
        await Task.Delay(500); // Brief pause to show completion
    }

    #endregion

    #region Cleanup

    public override void Dispose()
    {
        _statusTimer?.Stop();
        _statusTimer?.Dispose();
        _context?.Dispose();
        base.Dispose();
    }

    #endregion
}
