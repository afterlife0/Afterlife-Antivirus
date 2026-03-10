using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Serilog;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AfterlifeWinUI.Services;

/// <summary>
/// Theme options for the application
/// </summary>
public enum AppTheme
{
    System = 0,
    Dark = 1,
    Light = 2
}

/// <summary>
/// Service for persisting application settings
/// </summary>
public class AppSettingsService
{
    private static AppSettingsService? _instance;
    private static readonly object _lock = new();
    
    /// <summary>
    /// Get the settings folder path from ConfigurationManager
    /// </summary>
    private static string SettingsFolder => ConfigurationManager.IsInitialized
        ? ConfigurationManager.AppDataFolder
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AfterlifeWinUI");
    
    /// <summary>
    /// Get the settings file path from ConfigurationManager
    /// </summary>
    private static string SettingsFile => ConfigurationManager.IsInitialized
        ? ConfigurationManager.AppSettingsFile
        : Path.Combine(SettingsFolder, "app-settings.json");

    /// <summary>
    /// Event fired when theme changes
    /// </summary>
    public event EventHandler<AppTheme>? ThemeChanged;

    public static AppSettingsService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new AppSettingsService();
                }
            }
            return _instance;
        }
    }

    // Settings properties
    public string DefaultScanPath { get; set; }
    public List<string> MonitoredPaths { get; set; } = new();
    public bool ScanOnStartup { get; set; } = true;
    
    private bool _realTimeProtection = true;
    public bool RealTimeProtection 
    { 
        get => _realTimeProtection;
        set
        {
            if (_realTimeProtection != value)
            {
                _realTimeProtection = value;
                Save();
            }
        }
    }
    
    // Scan trigger settings
    private bool _scanOnCreation = true;
    public bool ScanOnCreation
    {
        get => _scanOnCreation;
        set
        {
            if (_scanOnCreation != value)
            {
                _scanOnCreation = value;
                Save();
            }
        }
    }
    
    private bool _scanOnModification = true;
    public bool ScanOnModification
    {
        get => _scanOnModification;
        set
        {
            if (_scanOnModification != value)
            {
                _scanOnModification = value;
                Save();
            }
        }
    }
    
    // Detection engine settings
    private bool _signatureDetection = true;
    public bool SignatureDetection
    {
        get => _signatureDetection;
        set
        {
            if (_signatureDetection != value)
            {
                _signatureDetection = value;
                Save();
            }
        }
    }
    
    private bool _yaraDetection = true;
    public bool YaraDetection
    {
        get => _yaraDetection;
        set
        {
            if (_yaraDetection != value)
            {
                _yaraDetection = value;
                Save();
            }
        }
    }
    
    private bool _aiHeuristic = true;
    public bool AiHeuristic
    {
        get => _aiHeuristic;
        set
        {
            if (_aiHeuristic != value)
            {
                _aiHeuristic = value;
                Save();
            }
        }
    }
    
    // Scheduled scan settings
    private bool _scheduledScansEnabled = true;
    public bool ScheduledScansEnabled
    {
        get => _scheduledScansEnabled;
        set
        {
            if (_scheduledScansEnabled != value)
            {
                _scheduledScansEnabled = value;
                Save();
            }
        }
    }
    
    public bool MinimizeToTray { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool AutoQuarantine { get; set; } = false;
    public int ScanThreads { get; set; } = 4;
    
    // Whitelist for trusted paths and files (reduces false positives)
    public List<string> WhitelistedPaths { get; set; } = new();
    public List<string> WhitelistedExtensions { get; set; } = new();
    
    // Theme setting
    private AppTheme _theme = AppTheme.Dark;
    public AppTheme Theme 
    { 
        get => _theme;
        set
        {
            if (_theme != value)
            {
                _theme = value;
                ThemeChanged?.Invoke(this, value);
                Save();
            }
        }
    }

    private AppSettingsService()
    {
        // Default scan path
        DefaultScanPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
            "Downloads");
        
        // Load saved settings first
        Load();
        
        // Only add default monitored path if none were loaded from settings
        if (MonitoredPaths.Count == 0)
        {
            MonitoredPaths.Add(DefaultScanPath);
        }
    }

    /// <summary>
    /// Get the glass panel background brush appropriate for the current theme.
    /// Both modes: Semi-transparent with frosted glass effect to let orbs show through
    /// </summary>
    public static LinearGradientBrush GetGlassPanelBrush()
    {
        var effectiveTheme = Instance.GetEffectiveTheme();
        
        if (effectiveTheme == AppTheme.Light)
        {
            // Light mode: Very transparent frosted glass - synced with sidebar transparency
            // Orbs should be clearly visible through panels
            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(100, 255, 255, 255), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(85, 250, 250, 252), Offset = 0.3 },
                    new GradientStop { Color = Color.FromArgb(90, 248, 248, 250), Offset = 0.7 },
                    new GradientStop { Color = Color.FromArgb(95, 252, 252, 255), Offset = 1 }
                }
            };
        }
        else
        {
            // Dark mode: Transparent glass with depth effect - orbs visible through
            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(35, 30, 30, 50), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(20, 255, 255, 255), Offset = 0.15 },
                    new GradientStop { Color = Color.FromArgb(12, 255, 255, 255), Offset = 0.5 },
                    new GradientStop { Color = Color.FromArgb(30, 20, 20, 40), Offset = 1 }
                }
            };
        }
    }

    /// <summary>
    /// Get glass panel border brush - subtle borders without fixed colored edge glow
    /// Orbs provide dynamic color through the transparent panels
    /// </summary>
    public static LinearGradientBrush GetGlassBorderBrush()
    {
        var effectiveTheme = Instance.GetEffectiveTheme();
        
        if (effectiveTheme == AppTheme.Light)
        {
            // Light mode: Very subtle black shadow borders - thinner appearance
            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(35, 0, 0, 0), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(20, 0, 0, 0), Offset = 0.5 },
                    new GradientStop { Color = Color.FromArgb(30, 0, 0, 0), Offset = 1 }
                }
            };
        }
        else
        {
            // Dark mode: Subtle white border only - no colored edge glow
            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(50, 255, 255, 255), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(30, 255, 255, 255), Offset = 0.5 },
                    new GradientStop { Color = Color.FromArgb(45, 255, 255, 255), Offset = 1 }
                }
            };
        }
    }

    /// <summary>
    /// Get sidebar background brush - synced transparency with panels
    /// </summary>
    public static LinearGradientBrush GetSidebarBrush()
    {
        var effectiveTheme = Instance.GetEffectiveTheme();
        
        if (effectiveTheme == AppTheme.Light)
        {
            // Light mode: Very transparent frosted white glass - synced with panels
            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(100, 255, 255, 255), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(85, 250, 250, 252), Offset = 0.3 },
                    new GradientStop { Color = Color.FromArgb(90, 248, 248, 250), Offset = 0.7 },
                    new GradientStop { Color = Color.FromArgb(95, 252, 252, 255), Offset = 1 }
                }
            };
        }
        else
        {
            // Dark mode: Transparent glass sidebar - synced with panel transparency
            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(35, 30, 30, 50), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(20, 255, 255, 255), Offset = 0.15 },
                    new GradientStop { Color = Color.FromArgb(12, 255, 255, 255), Offset = 0.5 },
                    new GradientStop { Color = Color.FromArgb(30, 20, 20, 40), Offset = 1 }
                }
            };
        }
    }

    /// <summary>
    /// Get shadow/depth brush to simulate drop shadow effect
    /// Creates a layered depth appearance
    /// </summary>
    public static LinearGradientBrush GetPanelShadowBrush()
    {
        var effectiveTheme = Instance.GetEffectiveTheme();
        
        if (effectiveTheme == AppTheme.Light)
        {
            // Light mode: Black shadow for depth
            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(0, 0, 0, 0), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(25, 0, 0, 0), Offset = 0.8 },
                    new GradientStop { Color = Color.FromArgb(40, 0, 0, 0), Offset = 1 }
                }
            };
        }
        else
        {
            // Dark mode: Subtle dark shadow for depth
            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(0, 0, 0, 0), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(30, 0, 0, 0), Offset = 0.85 },
                    new GradientStop { Color = Color.FromArgb(50, 0, 0, 0), Offset = 1 }
                }
            };
        }
    }

    /// <summary>
    /// Get text colors appropriate for the current theme
    /// </summary>
    public static SolidColorBrush GetPrimaryTextBrush()
    {
        var effectiveTheme = Instance.GetEffectiveTheme();
        
        if (effectiveTheme == AppTheme.Light)
        {
            // Dark text for light mode
            return new SolidColorBrush(Color.FromArgb(255, 30, 30, 35));
        }
        else
        {
            // Light text for dark mode
            return new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        }
    }

    /// <summary>
    /// Get secondary text colors appropriate for the current theme
    /// </summary>
    public static SolidColorBrush GetSecondaryTextBrush()
    {
        var effectiveTheme = Instance.GetEffectiveTheme();
        
        if (effectiveTheme == AppTheme.Light)
        {
            // Medium dark text for light mode
            return new SolidColorBrush(Color.FromArgb(255, 80, 80, 90));
        }
        else
        {
            // Medium light text for dark mode
            return new SolidColorBrush(Color.FromArgb(255, 136, 136, 153));
        }
    }

    /// <summary>
    /// Get tertiary/hint text colors appropriate for the current theme
    /// </summary>
    public static SolidColorBrush GetTertiaryTextBrush()
    {
        var effectiveTheme = Instance.GetEffectiveTheme();
        
        if (effectiveTheme == AppTheme.Light)
        {
            // Light gray text for light mode
            return new SolidColorBrush(Color.FromArgb(255, 100, 100, 110));
        }
        else
        {
            // Dark gray text for dark mode
            return new SolidColorBrush(Color.FromArgb(255, 85, 85, 102));
        }
    }

    /// <summary>
    /// Get section title text brush - white text that works on both glass panels
    /// </summary>
    public static SolidColorBrush GetSectionTitleBrush()
    {
        var effectiveTheme = Instance.GetEffectiveTheme();
        
        if (effectiveTheme == AppTheme.Light)
        {
            // Dark text for light mode panels
            return new SolidColorBrush(Color.FromArgb(255, 25, 25, 30));
        }
        else
        {
            // White text for dark mode
            return new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        }
    }

    /// <summary>
    /// Get label text brush for stat labels
    /// </summary>
    public static SolidColorBrush GetStatLabelBrush()
    {
        var effectiveTheme = Instance.GetEffectiveTheme();
        
        if (effectiveTheme == AppTheme.Light)
        {
            return new SolidColorBrush(Color.FromArgb(255, 70, 70, 80));
        }
        else
        {
            return new SolidColorBrush(Color.FromArgb(255, 136, 136, 153));
        }
    }

    /// <summary>
    /// Get edge glow brush for glass panels in dark mode
    /// Returns transparent for light mode
    /// </summary>
    public static LinearGradientBrush GetEdgeGlowBrush()
    {
        var effectiveTheme = Instance.GetEffectiveTheme();
        
        if (effectiveTheme == AppTheme.Light)
        {
            // No edge glow in light mode - return transparent
            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(0, 0, 0, 0), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(0, 0, 0, 0), Offset = 1 }
                }
            };
        }
        else
        {
            // Neon edge glow for dark mode
            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(30, 0, 243, 255), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(20, 0, 160, 255), Offset = 0.2 },
                    new GradientStop { Color = Color.FromArgb(0, 0, 0, 0), Offset = 0.4 },
                    new GradientStop { Color = Color.FromArgb(0, 0, 0, 0), Offset = 0.6 },
                    new GradientStop { Color = Color.FromArgb(20, 176, 38, 255), Offset = 0.8 },
                    new GradientStop { Color = Color.FromArgb(25, 0, 255, 157), Offset = 1 }
                }
            };
        }
    }

    /// <summary>
    /// Get inner card/list item background for theme
    /// </summary>
    public static SolidColorBrush GetCardItemBrush()
    {
        var effectiveTheme = Instance.GetEffectiveTheme();
        
        if (effectiveTheme == AppTheme.Light)
        {
            return new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
        }
        else
        {
            return new SolidColorBrush(Color.FromArgb(18, 255, 255, 255));
        }
    }

    /// <summary>
    /// Load settings from disk
    /// </summary>
    public void Load()
    {
        try
        {
            var filePath = SettingsFile;
            Log.Debug("[AppSettings] Loading from: {Path}", filePath);

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                
                if (string.IsNullOrWhiteSpace(json))
                {
                    Log.Debug("[AppSettings] File is empty, using defaults");
                    return;
                }

                // Use source-generated serializer for AOT compatibility
                var data = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettingsData);
                
                if (data != null)
                {
                    if (!string.IsNullOrEmpty(data.DefaultScanPath))
                        DefaultScanPath = data.DefaultScanPath;
                    
                    if (data.MonitoredPaths?.Count > 0)
                        MonitoredPaths = data.MonitoredPaths;
                    
                    ScanOnStartup = data.ScanOnStartup;
                    _realTimeProtection = data.RealTimeProtection;
                    _scanOnCreation = data.ScanOnCreation;
                    _scanOnModification = data.ScanOnModification;
                    _signatureDetection = data.SignatureDetection;
                    _yaraDetection = data.YaraDetection;
                    _aiHeuristic = data.AiHeuristic;
                    _scheduledScansEnabled = data.ScheduledScansEnabled;
                    MinimizeToTray = data.MinimizeToTray;
                    StartMinimized = data.StartMinimized;
                    AutoQuarantine = data.AutoQuarantine;
                    ScanThreads = data.ScanThreads > 0 ? data.ScanThreads : 4;
                    
                    // Load whitelist settings
                    if (data.WhitelistedPaths?.Count > 0)
                        WhitelistedPaths = data.WhitelistedPaths;
                    if (data.WhitelistedExtensions?.Count > 0)
                        WhitelistedExtensions = data.WhitelistedExtensions;
                    
                    _theme = (AppTheme)data.Theme;
                    
                    Log.Information("[AppSettings] Loaded settings from {Path} (Theme: {Theme})", filePath, _theme);
                }
            }
            else
            {
                Log.Debug("[AppSettings] Settings file not found, using defaults: {Path}", filePath);
            }
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "[AppSettings] Invalid JSON in settings file, using defaults");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AppSettings] Failed to load settings");
        }
    }

    /// <summary>
    /// Save settings to disk
    /// </summary>
    public void Save()
    {
        try
        {
            var folder = SettingsFolder;
            var filePath = SettingsFile;

            // Ensure directory exists
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                Log.Debug("[AppSettings] Created settings folder: {Path}", folder);
            }
            
            var data = new AppSettingsData
            {
                DefaultScanPath = DefaultScanPath,
                MonitoredPaths = MonitoredPaths,
                ScanOnStartup = ScanOnStartup,
                RealTimeProtection = _realTimeProtection,
                ScanOnCreation = _scanOnCreation,
                ScanOnModification = _scanOnModification,
                SignatureDetection = _signatureDetection,
                YaraDetection = _yaraDetection,
                AiHeuristic = _aiHeuristic,
                ScheduledScansEnabled = _scheduledScansEnabled,
                MinimizeToTray = MinimizeToTray,
                StartMinimized = StartMinimized,
                AutoQuarantine = AutoQuarantine,
                ScanThreads = ScanThreads,
                WhitelistedPaths = WhitelistedPaths,
                WhitelistedExtensions = WhitelistedExtensions,
                Theme = (int)_theme
            };
            
            // Use source-generated serializer for AOT compatibility
            var json = JsonSerializer.Serialize(data, AppJsonContext.Default.AppSettingsData);
            
            // Write to temp file first, then rename for atomic operation
            var tempFile = filePath + ".tmp";
            File.WriteAllText(tempFile, json);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            File.Move(tempFile, filePath);
            
            Log.Debug("[AppSettings] Saved settings to {Path}", filePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AppSettings] Failed to save settings");
        }
    }

    /// <summary>
    /// Get the effective theme (resolves System to actual theme)
    /// </summary>
    public AppTheme GetEffectiveTheme()
    {
        if (_theme == AppTheme.System)
        {
            // Check Windows system theme
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                if (value is int intValue)
                {
                    return intValue == 1 ? AppTheme.Light : AppTheme.Dark;
                }
            }
            catch
            {
                // Default to dark on error
            }
            return AppTheme.Dark;
        }
        return _theme;
    }

    /// <summary>
    /// Check if a file path is whitelisted (should not be scanned)
    /// </summary>
    public bool IsPathWhitelisted(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        try
        {
            var normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            // Check whitelisted extensions
            foreach (var ext in WhitelistedExtensions)
            {
                var normalizedExt = ext.StartsWith(".") ? ext.ToLowerInvariant() : "." + ext.ToLowerInvariant();
                if (extension == normalizedExt)
                {
                    return true;
                }
            }

            // Check whitelisted paths
            foreach (var path in WhitelistedPaths)
            {
                var normalizedWhitelistPath = Path.GetFullPath(path).ToLowerInvariant();
                
                // Check if file is in whitelisted directory or is the exact file
                if (normalizedPath.StartsWith(normalizedWhitelistPath) || 
                    normalizedPath == normalizedWhitelistPath)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Add a path to the whitelist
    /// </summary>
    public void AddWhitelistedPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        var normalizedPath = Path.GetFullPath(path);
        if (!WhitelistedPaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
        {
            WhitelistedPaths.Add(normalizedPath);
            Save();
            Log.Information("[AppSettings] Added to whitelist: {Path}", normalizedPath);
        }
    }

    /// <summary>
    /// Remove a path from the whitelist
    /// </summary>
    public void RemoveWhitelistedPath(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        var removed = WhitelistedPaths.RemoveAll(p => 
            string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase));
        
        if (removed > 0)
        {
            Save();
            Log.Information("[AppSettings] Removed from whitelist: {Path}", normalizedPath);
        }
    }

}

/// <summary>
/// Persistence data structure for app settings
/// </summary>
public class AppSettingsData
{
    public string DefaultScanPath { get; set; } = string.Empty;
    public List<string> MonitoredPaths { get; set; } = new();
    public bool ScanOnStartup { get; set; } = true;
    public bool RealTimeProtection { get; set; } = true;
    public bool ScanOnCreation { get; set; } = true;
    public bool ScanOnModification { get; set; } = true;
    public bool SignatureDetection { get; set; } = true;
    public bool YaraDetection { get; set; } = true;
    public bool AiHeuristic { get; set; } = true;
    public bool ScheduledScansEnabled { get; set; } = true;
    public bool MinimizeToTray { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool AutoQuarantine { get; set; } = false;
    public int ScanThreads { get; set; } = 4;
    public List<string> WhitelistedPaths { get; set; } = new();
    public List<string> WhitelistedExtensions { get; set; } = new();
    public int Theme { get; set; } = 1; // Default to Dark
}
