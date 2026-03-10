using System;
using System.IO;
using Serilog;

namespace AfterlifeWinUI.Services;

/// <summary>
/// Centralized configuration manager that handles all application data paths
/// and ensures they exist on startup. All services should use this for path resolution.
/// </summary>
public static class ConfigurationManager
{
    private static bool _initialized = false;
    private static readonly object _lock = new();

    /// <summary>
    /// Base application data folder in LocalApplicationData
    /// </summary>
    public static string AppDataFolder { get; private set; } = string.Empty;

    /// <summary>
    /// Folder for log files
    /// </summary>
    public static string LogsFolder { get; private set; } = string.Empty;

    /// <summary>
    /// Folder for quarantined files
    /// </summary>
    public static string QuarantineFolder { get; private set; } = string.Empty;

    /// <summary>
    /// Path to app settings JSON file
    /// </summary>
    public static string AppSettingsFile { get; private set; } = string.Empty;

    /// <summary>
    /// Path to window settings JSON file
    /// </summary>
    public static string WindowSettingsFile { get; private set; } = string.Empty;

    /// <summary>
    /// Path to threat history JSON file
    /// </summary>
    public static string ThreatHistoryFile { get; private set; } = string.Empty;

    /// <summary>
    /// Path to activity log JSON file
    /// </summary>
    public static string ActivityLogFile { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the configuration manager has been initialized
    /// </summary>
    public static bool IsInitialized => _initialized;

    /// <summary>
    /// Initialize all configuration paths and ensure directories exist.
    /// This should be called at application startup before any other services.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            try
            {
                // Build all paths using Environment.GetFolderPath for cross-user compatibility
                AppDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AfterlifeWinUI");

                LogsFolder = Path.Combine(AppDataFolder, "logs");
                QuarantineFolder = Path.Combine(AppDataFolder, "quarantine");

                // Config files
                AppSettingsFile = Path.Combine(AppDataFolder, "app-settings.json");
                WindowSettingsFile = Path.Combine(AppDataFolder, "window-settings.json");
                ThreatHistoryFile = Path.Combine(AppDataFolder, "threat-history.json");
                ActivityLogFile = Path.Combine(AppDataFolder, "activity-log.json");

                // Ensure all directories exist
                EnsureDirectoriesExist();

                _initialized = true;

                Log.Information("[ConfigurationManager] Initialized successfully");
                Log.Information("[ConfigurationManager] AppData folder: {Path}", AppDataFolder);
                Log.Debug("[ConfigurationManager] Settings file: {Path}", AppSettingsFile);
                Log.Debug("[ConfigurationManager] Window settings file: {Path}", WindowSettingsFile);
                Log.Debug("[ConfigurationManager] Threat history file: {Path}", ThreatHistoryFile);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ConfigurationManager] Failed to initialize configuration paths");
                throw;
            }
        }
    }

    /// <summary>
    /// Ensure all required directories exist
    /// </summary>
    private static void EnsureDirectoriesExist()
    {
        var directories = new[]
        {
            AppDataFolder,
            LogsFolder,
            QuarantineFolder
        };

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                Log.Debug("[ConfigurationManager] Created directory: {Path}", dir);
            }
        }
    }

    /// <summary>
    /// Verify all configuration files exist and are accessible
    /// </summary>
    public static ConfigurationStatus VerifyConfiguration()
    {
        var status = new ConfigurationStatus();

        try
        {
            // Check directories
            status.AppDataFolderExists = Directory.Exists(AppDataFolder);
            status.LogsFolderExists = Directory.Exists(LogsFolder);
            status.QuarantineFolderExists = Directory.Exists(QuarantineFolder);

            // Check settings files (they may not exist yet, which is OK)
            status.AppSettingsFileExists = File.Exists(AppSettingsFile);
            status.WindowSettingsFileExists = File.Exists(WindowSettingsFile);
            status.ThreatHistoryFileExists = File.Exists(ThreatHistoryFile);

            // Test write access by creating a temp file
            var testFile = Path.Combine(AppDataFolder, ".writetest");
            try
            {
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                status.HasWriteAccess = true;
            }
            catch
            {
                status.HasWriteAccess = false;
            }

            status.IsValid = status.AppDataFolderExists && status.HasWriteAccess;

            Log.Debug("[ConfigurationManager] Verification complete - Valid: {IsValid}, WriteAccess: {WriteAccess}",
                status.IsValid, status.HasWriteAccess);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ConfigurationManager] Error during verification");
            status.IsValid = false;
        }

        return status;
    }

    /// <summary>
    /// Get the default user Downloads folder path
    /// </summary>
    public static string GetDefaultDownloadsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
    }

    /// <summary>
    /// Get the default user Documents folder path
    /// </summary>
    public static string GetDefaultDocumentsPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }
}

/// <summary>
/// Status of configuration verification
/// </summary>
public class ConfigurationStatus
{
    public bool IsValid { get; set; }
    public bool HasWriteAccess { get; set; }
    public bool AppDataFolderExists { get; set; }
    public bool LogsFolderExists { get; set; }
    public bool QuarantineFolderExists { get; set; }
    public bool AppSettingsFileExists { get; set; }
    public bool WindowSettingsFileExists { get; set; }
    public bool ThreatHistoryFileExists { get; set; }
}
