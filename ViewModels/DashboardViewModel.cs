using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;
using AfterlifeWinUI.Services;
using Serilog;

namespace AfterlifeWinUI.ViewModels;

/// <summary>
/// ViewModel for the Dashboard view with scan functionality
/// </summary>
public class DashboardViewModel : ObservableObject
{
    private static bool _hasLoggedStartup = false; // Static to persist across page navigations
    
    private DispatcherQueueTimer? _refreshTimer;
    private DispatcherQueue? _dispatcherQueue;
    private readonly ScanService _scanService;

    private ulong _filesScanned = 0;
    public ulong FilesScanned
    {
        get => _filesScanned;
        set => SetProperty(ref _filesScanned, value);
    }

    private ulong _threatsDetected = 0;
    public ulong ThreatsDetected
    {
        get => _threatsDetected;
        set => SetProperty(ref _threatsDetected, value);
    }

    private ulong _suspiciousDetected = 0;
    public ulong SuspiciousDetected
    {
        get => _suspiciousDetected;
        set => SetProperty(ref _suspiciousDetected, value);
    }

    private uint _signatureCount = 0;
    public uint SignatureCount
    {
        get => _signatureCount;
        set => SetProperty(ref _signatureCount, value);
    }

    private uint _yaraRuleCount = 0;
    public uint YaraRuleCount
    {
        get => _yaraRuleCount;
        set => SetProperty(ref _yaraRuleCount, value);
    }

    private int _aiModelCount = 0;
    public int AIModelCount
    {
        get => _aiModelCount;
        set => SetProperty(ref _aiModelCount, value);
    }

    private bool _isBrainOnline = false;
    public bool IsBrainOnline
    {
        get => _isBrainOnline;
        set => SetProperty(ref _isBrainOnline, value);
    }

    private string _scanPath = string.Empty;
    public string ScanPath
    {
        get => _scanPath;
        set => SetProperty(ref _scanPath, value);
    }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    private string _scanStatus = "Ready to scan";
    public string ScanStatus
    {
        get => _scanStatus;
        set => SetProperty(ref _scanStatus, value);
    }

    private int _scanProgress;
    public int ScanProgress
    {
        get => _scanProgress;
        set => SetProperty(ref _scanProgress, value);
    }

    private string _currentScanFile = string.Empty;
    public string CurrentScanFile
    {
        get => _currentScanFile;
        set => SetProperty(ref _currentScanFile, value);
    }

    /// <summary>
    /// List of monitored directories
    /// </summary>
    public ObservableCollection<string> MonitoredPaths { get; } = new();

    /// <summary>
    /// Activity log entries - bound to shared service
    /// </summary>
    public ObservableCollection<ActivityLogEntry> ActivityLog => ActivityLogService.Instance.Entries;

    // Commands
    public IAsyncRelayCommand QuickScanCommand { get; }
    public IAsyncRelayCommand<IEnumerable<string>> ScanFilesCommand { get; }
    public IRelayCommand StopScanCommand { get; }
    public IAsyncRelayCommand BrowseForFolderCommand { get; }
    public IAsyncRelayCommand BrowseForFileCommand { get; }
    public IAsyncRelayCommand AddMonitoredFolderCommand { get; }
    public IRelayCommand<string> RemoveMonitoredFolderCommand { get; }
    public IRelayCommand ClearActivityLogCommand { get; }

    public DashboardViewModel()
    {
        Log.Debug("DashboardViewModel constructor starting");
        
        _scanService = new ScanService();
        _scanService.ProgressChanged += OnScanProgressChanged;
        _scanService.ThreatFound += OnThreatFound;
        _scanService.ScanCompleted += OnScanCompleted;
        
        // Initialize commands
        QuickScanCommand = new AsyncRelayCommand(QuickScanAsync);
        ScanFilesCommand = new AsyncRelayCommand<IEnumerable<string>>(ScanDroppedFilesAsync);
        StopScanCommand = new RelayCommand(StopScan);
        BrowseForFolderCommand = new AsyncRelayCommand(BrowseForFolderAsync);
        BrowseForFileCommand = new AsyncRelayCommand(BrowseForFileAsync);
        AddMonitoredFolderCommand = new AsyncRelayCommand(AddMonitoredFolderAsync);
        RemoveMonitoredFolderCommand = new RelayCommand<string>(RemoveMonitoredFolder);
        ClearActivityLogCommand = new RelayCommand(ClearActivityLog);

        // Load settings
        var settings = AppSettingsService.Instance;
        ScanPath = settings.DefaultScanPath;
        
        // Load monitored paths from settings
        MonitoredPaths.Clear();
        foreach (var path in settings.MonitoredPaths)
        {
            MonitoredPaths.Add(path);
        }
        
        Log.Debug("DashboardViewModel constructor completed");
    }

    private void OnScanProgressChanged(object? sender, ScanProgressEventArgs e)
    {
        DispatchToUI(() =>
        {
            CurrentScanFile = Path.GetFileName(e.CurrentFile);
            ScanProgress = e.ProgressPercent;
        });
    }

    private void OnThreatFound(object? sender, ThreatFoundEventArgs e)
    {
        // Add to threat history
        ThreatHistoryService.Instance.AddThreat(
            e.FilePath, 
            e.ThreatName, 
            e.DetectedBy, 
            e.ThreatType, 
            e.Confidence);
        
        // Format detection systems display (e.g., "SIG", "YARA", "SIG+YARA", "AI")
        string detectionLabel = FormatDetectionSystems(e.DetectedBy);
        
        // Use "SUSPICIOUS" type for AI-only detections with lower confidence
        // These are heuristic detections that need user verification
        // Note: confidence = score * agreement (~0.5), so threshold 0.25 = score 0.50
        bool isAIOnly = e.DetectedBy?.Equals("AI", StringComparison.OrdinalIgnoreCase) == true;
        bool isLowConfidence = e.Confidence < 0.25f;
        
        string message = $"[{detectionLabel}] {e.ThreatName} - {Path.GetFileName(e.FilePath)}";
        
        if (isAIOnly && isLowConfidence)
        {
            // AI-only detection with low confidence = Suspicious
            ActivityLogService.Instance.Suspicious(message);
        }
        else
        {
            // High confidence or multi-engine detection = Threat
            ActivityLogService.Instance.Threat(message);
        }
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
            // Use the original value if no known pattern
            return detectedBy.ToUpperInvariant();
        }

        return string.Join("+", systems);
    }

    private void OnScanCompleted(object? sender, ScanCompletedEventArgs e)
    {
        DispatchToUI(() =>
        {
            var summary = e.Summary;
            ScanProgress = 100;
            
            // Build status with threats and suspicious counts
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
            
            ScanStatus = $"Scan complete: {summary.ScannedFiles} files, {detectionText}";
            
            var activityType = totalDetections > 0 ? ActivityType.Warning : ActivityType.Success;
            ActivityLogService.Instance.AddEntry(
                $"Scan complete: {summary.ScannedFiles} files scanned, {detectionText} ({summary.Duration.TotalSeconds:F1}s)",
                activityType);
            
            RefreshStats();
        });
    }

    public void OnLoaded()
    {
        Log.Debug("DashboardViewModel.OnLoaded called");
        
        // Setup refresh timer
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        
        if (_dispatcherQueue != null)
        {
            // Set dispatcher for activity log service
            ActivityLogService.Instance.SetDispatcher(_dispatcherQueue);
            
            _refreshTimer = _dispatcherQueue.CreateTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(2);
            _refreshTimer.Tick += (s, e) => RefreshStats();
            _refreshTimer.Start();
        }

        RefreshStats();

        // Log startup info only once per app session
        if (!_hasLoggedStartup)
        {
            _hasLoggedStartup = true;
            
            ActivityLogService.Instance.Info("Dashboard loaded");
            
            var stats = MalwareScanner.Instance.GetStats();
            if (stats.IsOnline && (stats.SignatureCount > 0 || stats.YaraRuleCount > 0 || stats.AIModelCount > 0))
            {
                ActivityLogService.Instance.Success(
                    $"Scanning engine online - {stats.SignatureCount:N0} signatures, {stats.YaraRuleCount:N0} YARA rules, {stats.AIModelCount} AI models");
            }
            else if (stats.IsOnline)
            {
                ActivityLogService.Instance.Warning("Scanning engine online - no detection databases loaded");
            }
            else
            {
                ActivityLogService.Instance.Error("Scanning engine offline - detection disabled");
            }
        }
        
        Log.Information("Dashboard loaded successfully");
    }

    public void OnUnloaded()
    {
        Log.Debug("DashboardViewModel.OnUnloaded called");
        _refreshTimer?.Stop();
        
        // Save monitored paths
        AppSettingsService.Instance.MonitoredPaths = MonitoredPaths.ToList();
        AppSettingsService.Instance.Save();
    }

    private void RefreshStats()
    {
        try
        {
            var stats = MalwareScanner.Instance.GetStats();

            FilesScanned = stats.FilesScanned;
            ThreatsDetected = stats.ThreatsDetected;
            SuspiciousDetected = stats.SuspiciousDetected;
            SignatureCount = stats.SignatureCount;
            YaraRuleCount = stats.YaraRuleCount;
            AIModelCount = stats.AIModelCount;
            IsBrainOnline = stats.IsOnline;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to refresh stats");
        }
    }

    /// <summary>
    /// Scan dropped files or folders
    /// </summary>
    public async Task ScanDroppedFilesAsync(IEnumerable<string>? paths)
    {
        if (paths == null || !paths.Any())
            return;

        if (IsScanning)
        {
            Log.Warning("Scan already in progress, ignoring dropped files");
            return;
        }

        var filesToScan = new List<string>();
        
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                    filesToScan.AddRange(files);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to enumerate files in dropped folder: {Path}", path);
                }
            }
            else if (File.Exists(path))
            {
                filesToScan.Add(path);
            }
        }

        if (filesToScan.Count == 0)
        {
            ActivityLogService.Instance.Warning("No valid files to scan");
            return;
        }

        IsScanning = true;
        ScanStatus = $"Scanning {filesToScan.Count} files...";
        ScanProgress = 0;

        ActivityLogService.Instance.Scan($"Started scan: {filesToScan.Count} dropped files");

        try
        {
            await _scanService.ScanFilesAsync(filesToScan);
        }
        finally
        {
            IsScanning = false;
            CurrentScanFile = string.Empty;
        }
    }

    private async Task QuickScanAsync()
    {
        if (IsScanning)
            return;

        bool isFile = File.Exists(ScanPath);
        bool isDirectory = Directory.Exists(ScanPath);
        
        if (!isFile && !isDirectory)
        {
            ActivityLogService.Instance.Error($"Invalid path: {ScanPath} does not exist");
            return;
        }

        IsScanning = true;
        ScanStatus = "Scanning...";
        ScanProgress = 0;

        ActivityLogService.Instance.Scan($"Started scan: {ScanPath}");

        try
        {
            if (isFile)
            {
                var result = await _scanService.ScanFileAsync(ScanPath);
                ScanProgress = 100;
                
                if (result.IsThreat)
                {
                    ScanStatus = $"Threat detected: {result.ThreatName}";
                }
                else
                {
                    ScanStatus = "File is clean";
                    ActivityLogService.Instance.Success($"Clean: {Path.GetFileName(ScanPath)}");
                }
            }
            else
            {
                await _scanService.ScanDirectoryAsync(ScanPath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            ScanStatus = "Scan failed";
            ActivityLogService.Instance.Error($"Scan error: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
            CurrentScanFile = string.Empty;
            RefreshStats();
        }
    }

    private void StopScan()
    {
        _scanService.CancelScan();
        IsScanning = false;
        ScanStatus = "Scan stopped";
        ActivityLogService.Instance.Warning("Scan stopped by user");
    }

    private async Task BrowseForFolderAsync()
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                ScanPath = folder.Path;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open folder picker");
        }
    }

    private async Task BrowseForFileAsync()
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                ScanPath = file.Path;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open file picker");
        }
    }


    private async Task AddMonitoredFolderAsync()
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                var path = folder.Path;
                if (!MonitoredPaths.Contains(path))
                {
                    MonitoredPaths.Add(path);
                    ActivityLogService.Instance.Monitor($"Added monitored folder: {path}");
                    
                    // Start real-time monitoring for this directory
                    DirectoryMonitorService.Instance.AddDirectory(path);
                    
                    AppSettingsService.Instance.MonitoredPaths = MonitoredPaths.ToList();
                    AppSettingsService.Instance.Save();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to add monitored folder");
        }
    }

    private void RemoveMonitoredFolder(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;

        if (MonitoredPaths.Remove(path))
        {
            ActivityLogService.Instance.Monitor($"Removed monitored folder: {path}");
            
            // Stop real-time monitoring for this directory
            DirectoryMonitorService.Instance.RemoveDirectory(path);
            
            AppSettingsService.Instance.MonitoredPaths = MonitoredPaths.ToList();
            AppSettingsService.Instance.Save();
        }
    }

    private void ClearActivityLog()
    {
        ActivityLogService.Instance.Clear();
    }

    private void DispatchToUI(Action action)
    {
        if (_dispatcherQueue?.HasThreadAccess == true)
        {
            action();
        }
        else
        {
            _dispatcherQueue?.TryEnqueue(() => action());
        }
    }
}
