using Microsoft.UI.Xaml;
using Serilog;
using AfterlifeWinUI.Services;

namespace AfterlifeWinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Gets the main application window
    /// </summary>
    public static MainWindow? MainWindow { get; private set; }

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();

        // FIRST: Initialize configuration manager to set up all paths
        ConfigurationManager.Initialize();

        // Setup logging with paths from ConfigurationManager
        var logPath = Path.Combine(ConfigurationManager.LogsFolder, "afterlife-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.File(logPath, 
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                flushToDiskInterval: TimeSpan.FromSeconds(1),
                buffered: false,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("=== AFTERLiFE Application Starting ===");
        Log.Information("AppData folder: {AppDataFolder}", ConfigurationManager.AppDataFolder);
        Log.Information("Log folder: {LogFolder}", ConfigurationManager.LogsFolder);
        Log.Information("Machine: {MachineName}, User: {User}", Environment.MachineName, Environment.UserName);
        Log.Information("Base directory: {BaseDir}", AppDomain.CurrentDomain.BaseDirectory);

        // Verify configuration paths
        var configStatus = ConfigurationManager.VerifyConfiguration();
        if (!configStatus.IsValid)
        {
            Log.Warning("Configuration verification failed - WriteAccess: {WriteAccess}", configStatus.HasWriteAccess);
        }
        else
        {
            Log.Debug("Configuration verified - all paths accessible");
        }

        // Initialize settings services (this triggers loading from disk)
        InitializeSettings();

        // Initialize the malware scanner
        InitializeMalwareScanner();

        // Handle application exit
        UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled exception occurred");
        Log.CloseAndFlush();
    }

    /// <summary>
    /// Initialize all settings services to load their data from disk
    /// </summary>
    private void InitializeSettings()
    {
        try
        {
            // Access AppSettingsService to trigger loading
            var appSettings = AppSettingsService.Instance;
            Log.Debug("AppSettingsService initialized - Theme: {Theme}", appSettings.Theme);

            // Access ThreatHistoryService to trigger loading
            var threatHistory = ThreatHistoryService.Instance;
            Log.Debug("ThreatHistoryService initialized - {Total} threats ({Active} active)",
                threatHistory.TotalCount, threatHistory.ActiveCount);

            // Note: WindowSettingsService is loaded when MainWindow is created
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize settings services");
        }
    }

    private void InitializeMalwareScanner()
    {
        try
        {
            var initialized = MalwareScanner.Instance.Initialize();
            
            var stats = MalwareScanner.Instance.GetStats();
            
            if (initialized && (stats.SignatureCount > 0 || stats.YaraRuleCount > 0 || stats.AIModelCount > 0))
            {
                Log.Information("MalwareScanner ready - {SigCount:N0} signatures, {YaraCount:N0} YARA rules, {AICount} AI models",
                    stats.SignatureCount, stats.YaraRuleCount, stats.AIModelCount);
            }
            else
            {
                Log.Warning("MalwareScanner initialized but no detection databases loaded");
                Log.Warning("Expected resources at: {Path}", 
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources"));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize MalwareScanner");
        }
    }

    /// <summary>
    /// Invoked when the application is launched normally by the end user.
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log.Debug("OnLaunched called");
        
        MainWindow = new MainWindow();
        MainWindow.Activate();

        Log.Information("Main window activated and ready");
    }

    /// <summary>
    /// Clean up resources when application exits
    /// </summary>
    public static void Shutdown()
    {
        try
        {
            // Save any pending settings
            Log.Debug("Saving final application state...");

            // Dispose MalwareScanner
            MalwareScanner.Instance.Dispose();
            
            // Finalize YARA library
            YaraEngine.FinalizeYara();
            
            Log.Information("=== AFTERLiFE Application Shutting Down ===");
            Log.CloseAndFlush();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during shutdown");
        }
    }
}
