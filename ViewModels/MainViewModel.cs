using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using AfterlifeWinUI.Services;
using Serilog;

namespace AfterlifeWinUI.ViewModels;

/// <summary>
/// Main ViewModel for the application window - manages sidebar state and global stats
/// </summary>
public class MainViewModel : ObservableObject
{
    private DispatcherQueueTimer? _statsTimer;

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    private string _connectionStatus = "Connecting...";
    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }

    private string _engineVersion = "v2.0";
    public string EngineVersion
    {
        get => _engineVersion;
        set => SetProperty(ref _engineVersion, value);
    }

    private ulong _filesScanned;
    public ulong FilesScanned
    {
        get => _filesScanned;
        set => SetProperty(ref _filesScanned, value);
    }

    private ulong _threatsDetected;
    public ulong ThreatsDetected
    {
        get => _threatsDetected;
        set => SetProperty(ref _threatsDetected, value);
    }

    private uint _signatureCount;
    public uint SignatureCount
    {
        get => _signatureCount;
        set => SetProperty(ref _signatureCount, value);
    }

    private uint _yaraRuleCount;
    public uint YaraRuleCount
    {
        get => _yaraRuleCount;
        set => SetProperty(ref _yaraRuleCount, value);
    }

    private int _aiModelCount;
    public int AIModelCount
    {
        get => _aiModelCount;
        set => SetProperty(ref _aiModelCount, value);
    }

    private bool _isAIOnline;
    public bool IsAIOnline
    {
        get => _isAIOnline;
        set => SetProperty(ref _isAIOnline, value);
    }

    private bool _isBrainOnline;
    public bool IsBrainOnline
    {
        get => _isBrainOnline;
        set => SetProperty(ref _isBrainOnline, value);
    }

    private bool _isMonitoringActive;
    public bool IsMonitoringActive
    {
        get => _isMonitoringActive;
        set => SetProperty(ref _isMonitoringActive, value);
    }

    private string _currentView = "Dashboard";
    public string CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    private bool _isSidebarExpanded = true;
    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        set => SetProperty(ref _isSidebarExpanded, value);
    }

    private double _sidebarWidth = 260;
    public double SidebarWidth
    {
        get => _sidebarWidth;
        set => SetProperty(ref _sidebarWidth, value);
    }

    private const double ExpandedSidebarWidth = 260;
    private const double CollapsedSidebarWidth = 72;

    public IRelayCommand ToggleSidebarCommand { get; }
    public IRelayCommand RefreshStatsCommand { get; }
    public IRelayCommand<string> NavigateToCommand { get; }

    public MainViewModel()
    {
        ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
        RefreshStatsCommand = new RelayCommand(RefreshStats);
        NavigateToCommand = new RelayCommand<string>(NavigateTo);
    }

    public void Initialize()
    {
        try
        {
            Log.Information("MainViewModel initialized");

            // Setup periodic stats refresh
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            if (dispatcherQueue != null)
            {
                // Set dispatcher for activity log service
                ActivityLogService.Instance.SetDispatcher(dispatcherQueue);
                
                _statsTimer = dispatcherQueue.CreateTimer();
                _statsTimer.Interval = TimeSpan.FromSeconds(2);
                _statsTimer.Tick += (s, e) => RefreshStats();
                _statsTimer.Start();
            }

            RefreshStats();
            
            // Start real-time monitoring for configured directories
            StartRealTimeMonitoring();
            
            // Perform startup scan if enabled
            if (AppSettingsService.Instance.ScanOnStartup)
            {
                _ = ScanMonitoredDirectoriesAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize MainViewModel");
            IsConnected = false;
            ConnectionStatus = "OFFLINE";
        }
    }

    /// <summary>
    /// Start real-time file monitoring for all configured directories
    /// </summary>
    private void StartRealTimeMonitoring()
    {
        try
        {
            var settings = AppSettingsService.Instance;
            var monitoredPaths = settings.MonitoredPaths;

            if (monitoredPaths.Count == 0)
            {
                Log.Debug("[Monitor] No directories configured for real-time monitoring");
                return;
            }

            // Subscribe to threat detection events
            DirectoryMonitorService.Instance.ThreatDetected += OnRealTimeThreatDetected;

            // Add all configured directories to monitoring
            int successCount = 0;
            foreach (var path in monitoredPaths)
            {
                if (Directory.Exists(path))
                {
                    if (DirectoryMonitorService.Instance.AddDirectory(path))
                    {
                        successCount++;
                    }
                }
                else
                {
                    Log.Warning("[Monitor] Directory does not exist: {Path}", path);
                }
            }

            if (successCount > 0)
            {
                Log.Information("[Monitor] Real-time monitoring started for {Count} directories", successCount);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Monitor] Failed to start real-time monitoring");
        }
    }

    /// <summary>
    /// Handle threat detected during real-time monitoring
    /// </summary>
    private void OnRealTimeThreatDetected(string path, ScanResult result)
    {
        Log.Warning("[Monitor] Real-time threat: {Path} | {Threat}", path, result.ThreatName);
        
        // Refresh stats to update threat count
        RefreshStats();
    }

    /// <summary>
    /// Scan all monitored directories at startup
    /// </summary>
    private async Task ScanMonitoredDirectoriesAsync()
    {
        var monitoredPaths = AppSettingsService.Instance.MonitoredPaths;
        if (monitoredPaths.Count == 0)
        {
            Log.Debug("No monitored directories to scan");
            return;
        }

        ActivityLogService.Instance.Info("Starting startup scan of monitored directories...");
        
        var scanService = new ScanService();
        scanService.ThreatFound += (s, e) =>
        {
            ThreatHistoryService.Instance.AddThreat(
                e.FilePath,
                e.ThreatName,
                e.DetectedBy,
                e.ThreatType,
                e.Confidence);
            
            // Format detection systems display
            string detectionLabel = FormatDetectionSystems(e.DetectedBy);
            
            // Use "SUSPICIOUS" type for AI-only detections with lower confidence
            // Note: confidence = score * agreement (~0.5), so threshold 0.25 = score 0.50
            bool isAIOnly = e.DetectedBy?.Equals("AI", StringComparison.OrdinalIgnoreCase) == true;
            bool isLowConfidence = e.Confidence < 0.25f;
            
            string message = $"[{detectionLabel}] {e.ThreatName} - {Path.GetFileName(e.FilePath)}";
            
            if (isAIOnly && isLowConfidence)
            {
                ActivityLogService.Instance.Suspicious(message);
            }
            else
            {
                ActivityLogService.Instance.Threat(message);
            }
        };

        foreach (var path in monitoredPaths)
        {
            if (Directory.Exists(path))
            {
                ActivityLogService.Instance.Scan($"Scanning monitored folder: {path}");
                try
                {
                    var summary = await scanService.ScanDirectoryAsync(path, recursive: true);
                    
                    // Build completion message with threats and suspicious counts
                    int totalDetections = summary.ThreatsFound + summary.SuspiciousFound;
                    string detectionText;
                    
                    if (summary.ThreatsFound > 0 && summary.SuspiciousFound > 0)
                    {
                        detectionText = $"{summary.ThreatsFound} threats, {summary.SuspiciousFound} suspicious";
                    }
                    else if (summary.ThreatsFound > 0)
                    {
                        detectionText = $"{summary.ThreatsFound} threats";
                    }
                    else if (summary.SuspiciousFound > 0)
                    {
                        detectionText = $"{summary.SuspiciousFound} suspicious";
                    }
                    else
                    {
                        detectionText = "no threats";
                    }
                    
                    var resultType = totalDetections > 0 ? ActivityType.Warning : ActivityType.Success;
                    ActivityLogService.Instance.AddEntry(
                        $"Startup scan complete: {summary.ScannedFiles} files, {detectionText} in {path}",
                        resultType);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error scanning monitored directory: {Path}", path);
                    ActivityLogService.Instance.Error($"Failed to scan {path}: {ex.Message}");
                }
            }
        }
        
        RefreshStats();
    }

    /// <summary>
    /// Format detection system names for display
    /// </summary>
    private static string FormatDetectionSystems(string detectedBy)
    {
        if (string.IsNullOrEmpty(detectedBy))
            return "UNKNOWN";

        var systems = new List<string>();
        var lower = detectedBy.ToLowerInvariant();

        if (lower.Contains("signature") || lower.Contains("sig") || lower.Contains("md5") || lower.Contains("sha"))
            systems.Add("SIG");
        
        if (lower.Contains("yara"))
            systems.Add("YARA");
        
        if (lower.Contains("ai") || lower.Contains("brain") || lower.Contains("heuristic") || lower.Contains("ml"))
            systems.Add("AI");

        if (systems.Count == 0)
        {
            // Use original value if no known pattern
            return detectedBy.ToUpperInvariant();
        }

        return string.Join("+", systems);
    }

    public void Shutdown()
    {
        _statsTimer?.Stop();
        
        // Stop real-time monitoring
        DirectoryMonitorService.Instance.ThreatDetected -= OnRealTimeThreatDetected;
        DirectoryMonitorService.Instance.Dispose();
        
        // Save settings on shutdown
        AppSettingsService.Instance.Save();
        
        Log.Information("MainViewModel shutdown");
    }

    private void RefreshStats()
    {
        try
        {
            var stats = MalwareScanner.Instance.GetStats();
            
            FilesScanned = stats.FilesScanned;
            ThreatsDetected = stats.ThreatsDetected;
            SignatureCount = stats.SignatureCount;
            YaraRuleCount = stats.YaraRuleCount;
            AIModelCount = stats.AIModelCount;
            IsAIOnline = stats.AIModelCount > 0;
            IsBrainOnline = stats.IsOnline;
            
            IsConnected = stats.IsOnline;
            ConnectionStatus = IsConnected ? "PROTECTED" : "OFFLINE";
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to refresh stats");
            IsConnected = false;
            ConnectionStatus = "OFFLINE";
        }
    }

    private void NavigateTo(string? viewName)
    {
        if (viewName != null)
        {
            CurrentView = viewName;
            Log.Debug("Navigated to {View}", viewName);
        }
    }

    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
        SidebarWidth = IsSidebarExpanded ? ExpandedSidebarWidth : CollapsedSidebarWidth;
        Log.Debug("Sidebar toggled: {IsExpanded}", IsSidebarExpanded);
    }

    /// <summary>
    /// Formats large numbers with K/M suffix
    /// </summary>
    public string FormatNumber(ulong number)
    {
        return number switch
        {
            >= 1_000_000 => $"{number / 1_000_000.0:N1}M",
            >= 1_000 => $"{number / 1_000.0:N1}K",
            _ => number.ToString("N0")
        };
    }
}
