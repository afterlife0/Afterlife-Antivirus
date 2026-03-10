using System;
using System.IO;
using System.Text.Json;
using Windows.Graphics;
using Microsoft.UI.Windowing;
using Serilog;

namespace AfterlifeWinUI.Services;

/// <summary>
/// Service for persisting window settings like size and position
/// </summary>
public class WindowSettingsService
{
    /// <summary>
    /// Get the settings file path from ConfigurationManager
    /// </summary>
    private static string SettingsFile => ConfigurationManager.IsInitialized 
        ? ConfigurationManager.WindowSettingsFile 
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AfterlifeWinUI", "window-settings.json");

    /// <summary>
    /// Get the settings folder path
    /// </summary>
    private static string SettingsFolder => ConfigurationManager.IsInitialized
        ? ConfigurationManager.AppDataFolder
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AfterlifeWinUI");

    public class WindowSettings
    {
        public int Width { get; set; } = 1280;
        public int Height { get; set; } = 800;
        public int X { get; set; } = -1;
        public int Y { get; set; } = -1;
        public bool IsMaximized { get; set; } = false;
    }

    /// <summary>
    /// Load window settings from disk
    /// </summary>
    public static WindowSettings Load()
    {
        try
        {
            var filePath = SettingsFile;
            Log.Debug("[WindowSettings] Loading from: {Path}", filePath);

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                
                if (string.IsNullOrWhiteSpace(json))
                {
                    Log.Debug("[WindowSettings] File is empty, using defaults");
                    return new WindowSettings();
                }

                // Use source-generated serializer for AOT compatibility
                var settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.WindowSettings);
                
                if (settings != null)
                {
                    Log.Information("[WindowSettings] Loaded - Size: {W}x{H}, Position: ({X},{Y}), Maximized: {Max}",
                        settings.Width, settings.Height, settings.X, settings.Y, settings.IsMaximized);
                    return settings;
                }
            }
            else
            {
                Log.Debug("[WindowSettings] File not found, using defaults");
            }
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "[WindowSettings] Invalid JSON in settings file, using defaults");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WindowSettings] Failed to load window settings");
        }
        
        return new WindowSettings();
    }

    /// <summary>
    /// Save window settings to disk
    /// </summary>
    public static void Save(WindowSettings settings)
    {
        try
        {
            var folder = SettingsFolder;
            var filePath = SettingsFile;

            // Ensure directory exists
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                Log.Debug("[WindowSettings] Created settings folder: {Path}", folder);
            }

            // Use source-generated serializer for AOT compatibility
            var json = JsonSerializer.Serialize(settings, AppJsonContext.Default.WindowSettings);
            
            // Write to temp file first, then rename for atomic operation
            var tempFile = filePath + ".tmp";
            File.WriteAllText(tempFile, json);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            File.Move(tempFile, filePath);
            
            Log.Debug("[WindowSettings] Saved - Size: {W}x{H}, Position: ({X},{Y}), Maximized: {Max}",
                settings.Width, settings.Height, settings.X, settings.Y, settings.IsMaximized);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WindowSettings] Failed to save window settings");
        }
    }

    /// <summary>
    /// Save window settings from AppWindow
    /// </summary>
    public static void Save(AppWindow appWindow)
    {
        if (appWindow == null) 
        {
            Log.Warning("[WindowSettings] Cannot save - AppWindow is null");
            return;
        }

        try
        {
            var presenter = appWindow.Presenter as OverlappedPresenter;
            var isMaximized = presenter?.State == OverlappedPresenterState.Maximized;

            // Don't save position/size while maximized - save the restored size
            int width = appWindow.Size.Width;
            int height = appWindow.Size.Height;
            int x = appWindow.Position.X;
            int y = appWindow.Position.Y;

            // Validate values - don't save invalid dimensions
            if (width < 100 || height < 100)
            {
                Log.Warning("[WindowSettings] Invalid window size ({W}x{H}), skipping save", width, height);
                return;
            }

            var settings = new WindowSettings
            {
                Width = width,
                Height = height,
                X = x,
                Y = y,
                IsMaximized = isMaximized
            };

            Save(settings);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WindowSettings] Error saving from AppWindow");
        }
    }

    /// <summary>
    /// Apply window settings to AppWindow
    /// </summary>
    public static void Apply(AppWindow appWindow, WindowSettings settings)
    {
        if (appWindow == null) 
        {
            Log.Warning("[WindowSettings] Cannot apply - AppWindow is null");
            return;
        }

        try
        {
            Log.Debug("[WindowSettings] Applying - Size: {W}x{H}, Position: ({X},{Y}), Maximized: {Max}",
                settings.Width, settings.Height, settings.X, settings.Y, settings.IsMaximized);

            // Validate dimensions
            int width = Math.Max(settings.Width, 800);
            int height = Math.Max(settings.Height, 600);

            // Set size first
            appWindow.Resize(new SizeInt32(width, height));

            // Set position (if valid and not maximized)
            if (!settings.IsMaximized && settings.X >= 0 && settings.Y >= 0)
            {
                // Verify position is on screen
                var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    // Ensure window is at least partially visible
                    int maxX = displayArea.WorkArea.Width - 100;
                    int maxY = displayArea.WorkArea.Height - 100;
                    int adjustedX = Math.Min(Math.Max(settings.X, 0), maxX);
                    int adjustedY = Math.Min(Math.Max(settings.Y, 0), maxY);

                    appWindow.Move(new PointInt32(adjustedX, adjustedY));
                }
            }
            else if (settings.X < 0 || settings.Y < 0)
            {
                // Center on screen
                var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var centerX = (displayArea.WorkArea.Width - width) / 2;
                    var centerY = (displayArea.WorkArea.Height - height) / 2;
                    appWindow.Move(new PointInt32(centerX, centerY));
                    Log.Debug("[WindowSettings] Centered window at ({X},{Y})", centerX, centerY);
                }
            }

            // Set maximized state last
            if (settings.IsMaximized)
            {
                var presenter = appWindow.Presenter as OverlappedPresenter;
                presenter?.Maximize();
                Log.Debug("[WindowSettings] Maximized window");
            }

            Log.Information("[WindowSettings] Applied successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WindowSettings] Error applying window settings");
        }
    }
}
