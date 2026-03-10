using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace AfterlifeWinUI.Services;

/// <summary>
/// Service for monitoring directories for file changes and triggering real-time scans
/// </summary>
public sealed class DirectoryMonitorService : IDisposable
{
    private static DirectoryMonitorService? _instance;
    private static readonly object _instanceLock = new();

    public static DirectoryMonitorService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= new DirectoryMonitorService();
                }
            }
            return _instance;
        }
    }

    private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
    private readonly ConcurrentQueue<string> _pendingScans = new();
    private readonly ConcurrentDictionary<string, DateTime> _recentlyScanned = new();
    private readonly object _lock = new();
    private CancellationTokenSource? _scanCts;
    private Task? _scanTask;
    private bool _isDisposed;

    /// <summary>
    /// Event raised when a file change is detected
    /// </summary>
    public event Action<string, WatcherChangeTypes>? FileChanged;

    /// <summary>
    /// Event raised when a threat is detected during real-time monitoring
    /// </summary>
    public event Action<string, ScanResult>? ThreatDetected;

    /// <summary>
    /// Debounce delay in milliseconds before scanning a changed file
    /// </summary>
    public int DebounceDelayMs { get; set; } = 500;

    /// <summary>
    /// Whether the monitor is currently active
    /// </summary>
    public bool IsMonitoring => _watchers.Count > 0;

    /// <summary>
    /// Number of currently monitored directories
    /// </summary>
    public int MonitoredCount
    {
        get
        {
            lock (_lock)
            {
                return _watchers.Count;
            }
        }
    }

    /// <summary>
    /// List of currently monitored paths
    /// </summary>
    public IReadOnlyCollection<string> MonitoredPaths
    {
        get
        {
            lock (_lock)
            {
                return new List<string>(_watchers.Keys);
            }
        }
    }

    private DirectoryMonitorService()
    {
        StartScanProcessor();
    }

    /// <summary>
    /// Adds a directory to monitor for file changes
    /// </summary>
    public bool AddDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            Log.Warning("[Monitor] Cannot monitor invalid path: {Path}", path);
            return false;
        }

        // Normalize path
        path = Path.GetFullPath(path);

        lock (_lock)
        {
            if (_watchers.ContainsKey(path))
            {
                Log.Debug("[Monitor] Path already being monitored: {Path}", path);
                return true;
            }

            try
            {
                var watcher = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.FileName | 
                                   NotifyFilters.LastWrite | 
                                   NotifyFilters.CreationTime |
                                   NotifyFilters.Size,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                    InternalBufferSize = 65536 // 64KB buffer for high-volume directories
                };

                watcher.Created += OnFileCreated;
                watcher.Changed += OnFileChanged;
                watcher.Renamed += OnFileRenamed;
                watcher.Error += OnWatcherError;

                _watchers[path] = watcher;

                Log.Information("[Monitor] Started monitoring: {Path}", path);
                ActivityLogService.Instance.Monitor($"Started real-time monitoring: {path}");

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Monitor] Failed to start monitoring: {Path}", path);
                return false;
            }
        }
    }

    /// <summary>
    /// Removes a directory from monitoring
    /// </summary>
    public bool RemoveDirectory(string path)
    {
        path = Path.GetFullPath(path);

        lock (_lock)
        {
            if (_watchers.TryGetValue(path, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Created -= OnFileCreated;
                watcher.Changed -= OnFileChanged;
                watcher.Renamed -= OnFileRenamed;
                watcher.Error -= OnWatcherError;
                watcher.Dispose();
                _watchers.Remove(path);

                Log.Information("[Monitor] Stopped monitoring: {Path}", path);
                ActivityLogService.Instance.Monitor($"Stopped monitoring: {path}");

                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Stops all directory monitoring
    /// </summary>
    public void StopAll()
    {
        lock (_lock)
        {
            foreach (var watcher in _watchers.Values)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch { }
            }
            _watchers.Clear();
            Log.Information("[Monitor] Stopped all directory monitoring");
        }
    }

    /// <summary>
    /// Refresh monitoring for all configured paths
    /// </summary>
    public void RefreshFromSettings()
    {
        var settings = AppSettingsService.Instance;
        var currentPaths = new HashSet<string>(MonitoredPaths, StringComparer.OrdinalIgnoreCase);
        var configuredPaths = new HashSet<string>(settings.MonitoredPaths, StringComparer.OrdinalIgnoreCase);

        // Remove paths no longer in settings
        foreach (var path in currentPaths)
        {
            if (!configuredPaths.Contains(path))
            {
                RemoveDirectory(path);
            }
        }

        // Add new paths from settings
        foreach (var path in configuredPaths)
        {
            if (!currentPaths.Contains(path) && Directory.Exists(path))
            {
                AddDirectory(path);
            }
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (ShouldScanFile(e.FullPath))
        {
            Log.Debug("[Monitor] File created: {Path}", e.FullPath);
            QueueFileScan(e.FullPath);
            FileChanged?.Invoke(e.FullPath, WatcherChangeTypes.Created);
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (ShouldScanFile(e.FullPath))
        {
            Log.Debug("[Monitor] File changed: {Path}", e.FullPath);
            QueueFileScan(e.FullPath);
            FileChanged?.Invoke(e.FullPath, WatcherChangeTypes.Changed);
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (ShouldScanFile(e.FullPath))
        {
            Log.Debug("[Monitor] File renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
            QueueFileScan(e.FullPath);
            FileChanged?.Invoke(e.FullPath, WatcherChangeTypes.Renamed);
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        Log.Error(ex, "[Monitor] FileSystemWatcher error");

        // Try to restart the watcher
        if (sender is FileSystemWatcher watcher && !string.IsNullOrEmpty(watcher.Path))
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                Thread.Sleep(100);
                watcher.EnableRaisingEvents = true;
                Log.Information("[Monitor] Restarted watcher for: {Path}", watcher.Path);
            }
            catch (Exception restartEx)
            {
                Log.Error(restartEx, "[Monitor] Failed to restart watcher");
            }
        }
    }

    private bool ShouldScanFile(string path)
    {
        try
        {
            // Skip directories
            if (Directory.Exists(path)) return false;

            // Skip if file doesn't exist
            if (!File.Exists(path)) return false;

            // Skip certain extensions
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var skipExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".tmp", ".log", ".lock", ".partial", ".crdownload", ".part",
                ".downloading", ".temp", ".bak", ".swp", ".cache"
            };
            if (skipExtensions.Contains(ext)) return false;

            // Use FileTypeFilter if available
            if (!FileTypeFilter.ShouldScanFile(path))
            {
                return false;
            }

            // Skip very small files (likely temp or incomplete)
            var info = new FileInfo(path);
            if (info.Length < 100) return false;

            // Skip if recently scanned (within 5 seconds)
            var now = DateTime.UtcNow;
            if (_recentlyScanned.TryGetValue(path, out var lastScan))
            {
                if ((now - lastScan).TotalSeconds < 5)
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void QueueFileScan(string path)
    {
        _recentlyScanned[path] = DateTime.UtcNow;
        _pendingScans.Enqueue(path);
    }

    private void StartScanProcessor()
    {
        _scanCts = new CancellationTokenSource();
        _scanTask = Task.Run(async () =>
        {
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (!_scanCts.Token.IsCancellationRequested)
            {
                try
                {
                    // Wait for debounce period
                    await Task.Delay(DebounceDelayMs, _scanCts.Token);

                    // Collect all pending files (deduplicating)
                    processed.Clear();
                    while (_pendingScans.TryDequeue(out var path))
                    {
                        if (!processed.Contains(path))
                        {
                            processed.Add(path);
                        }
                    }

                    if (processed.Count == 0) continue;

                    // Scan each unique file
                    foreach (var path in processed)
                    {
                        if (_scanCts.Token.IsCancellationRequested) break;

                        try
                        {
                            if (!File.Exists(path)) continue;

                            // Wait a bit for file to be fully written
                            await Task.Delay(200, _scanCts.Token);

                            var result = MalwareScanner.Instance.ScanFile(path);

                            if (result.IsThreat)
                            {
                                Log.Warning("[Monitor] Real-time detection: {Path} - {Threat}", 
                                    path, result.ThreatName);

                                // Format detection systems display (e.g., "SIG", "YARA", "SIG+YARA", "AI")
                                string detectionLabel = FormatDetectionSystems(result.DetectedBy);

                                // Determine if suspicious or threat
                                // Note: confidence = score * agreement (~0.5), so threshold 0.25 = score 0.50
                                bool isAIOnly = result.DetectedBy?.Equals("AI", StringComparison.OrdinalIgnoreCase) == true;
                                bool isLowConfidence = result.Confidence < 0.25f;

                                string message = $"[{detectionLabel}] {result.ThreatName} - {Path.GetFileName(path)}";

                                if (isAIOnly && isLowConfidence)
                                {
                                    ActivityLogService.Instance.Suspicious(message);
                                }
                                else
                                {
                                    ActivityLogService.Instance.Threat(message);
                                }

                                // Add to threat history
                                ThreatHistoryService.Instance.AddThreat(
                                    path,
                                    result.ThreatName,
                                    result.DetectedBy ?? "Unknown",
                                    result.ThreatType ?? "unknown",
                                    result.Confidence);

                                ThreatDetected?.Invoke(path, result);
                            }
                            else
                            {
                                Log.Debug("[Monitor] File clean: {Path}", path);
                            }
                        }
                        catch (IOException)
                        {
                            // File is still being written, requeue
                            _pendingScans.Enqueue(path);
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "[Monitor] Failed to scan file: {Path}", path);
                        }
                    }

                    // Clean up old entries from recently scanned cache
                    var cutoff = DateTime.UtcNow.AddMinutes(-1);
                    foreach (var kvp in _recentlyScanned)
                    {
                        if (kvp.Value < cutoff)
                        {
                            _recentlyScanned.TryRemove(kvp.Key, out _);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[Monitor] Scan processor error");
                    await Task.Delay(1000, _scanCts.Token);
                }
            }
        }, _scanCts.Token);
    }

    /// <summary>
    /// Format detection system names for display (e.g., "SIG", "YARA", "SIG+YARA+AI")
    /// </summary>
    private static string FormatDetectionSystems(string? detectedBy)
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

    public void Dispose()
    {
        if (_isDisposed) return;

        _scanCts?.Cancel();
        
        try
        {
            _scanTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch { }

        _scanCts?.Dispose();
        StopAll();
        
        _isDisposed = true;
        Log.Debug("[Monitor] DirectoryMonitorService disposed");
    }
}
