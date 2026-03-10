using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace AfterlifeWinUI.Services;

/// <summary>
/// Async wrapper for MalwareScanner operations
/// </summary>
public class ScanService : IDisposable
{
    private CancellationTokenSource? _scanCts;
    private bool _disposed;

    public event EventHandler<ScanProgressEventArgs>? ProgressChanged;
    public event EventHandler<ThreatFoundEventArgs>? ThreatFound;
    public event EventHandler<ScanCompletedEventArgs>? ScanCompleted;

    public bool IsScanning => _scanCts != null && !_scanCts.IsCancellationRequested;

    /// <summary>
    /// Scan a single file asynchronously using MalwareScanner
    /// </summary>
    public async Task<ScanResult> ScanFileAsync(string filePath)
    {
        Log.Debug("[SCAN] Scanning file: {FilePath}", filePath);
        
        var result = await Task.Run(() => MalwareScanner.Instance.ScanFile(filePath));
        
        if (result.IsThreat)
        {
            Log.Warning("[SCAN] Threat detected in {FilePath}: {ThreatName} | DetectedBy: {DetectedBy} | Confidence: {Confidence:F0}%", 
                filePath, result.ThreatName, result.DetectedBy, result.Confidence * 100);
            
            ThreatFound?.Invoke(this, new ThreatFoundEventArgs
            {
                FilePath = filePath,
                ThreatName = result.ThreatName,
                ThreatType = result.ThreatType,
                DetectedBy = result.DetectedBy,
                Confidence = result.Confidence
            });
        }
        else
        {
            Log.Debug("[SCAN] File clean: {FilePath}", filePath);
        }
        
        return result;
    }

    /// <summary>
    /// Scan multiple files asynchronously - no file size limit
    /// </summary>
    public async Task<ScanSummary> ScanFilesAsync(
        IEnumerable<string> filePaths,
        IProgress<ScanProgressEventArgs>? progress = null)
    {
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;
        var summary = new ScanSummary();
        var files = new List<string>(filePaths);

        summary.TotalFiles = files.Count;
        Log.Information("[SCAN] Starting scan of {FileCount} files", files.Count);

        try
        {
            int scanned = 0;
            foreach (var file in files)
            {
                if (token.IsCancellationRequested)
                {
                    summary.WasCancelled = true;
                    Log.Information("[SCAN] Scan cancelled by user");
                    break;
                }

                try
                {
                    if (!File.Exists(file))
                    {
                        summary.SkippedFiles++;
                        continue;
                    }

                    var result = await Task.Run(() => MalwareScanner.Instance.ScanFile(file), token);
                    scanned++;
                    summary.ScannedFiles = scanned;

                    if (result.IsThreat)
                    {
                        // Determine if this is a suspicious (AI-only low confidence) or confirmed threat
                        // Note: confidence = score * agreement (~0.5), so threshold 0.25 = score 0.50
                        bool isAIOnly = result.DetectedBy?.Equals("AI", StringComparison.OrdinalIgnoreCase) == true;
                        bool isLowConfidence = result.Confidence < 0.25f;
                        
                        if (isAIOnly && isLowConfidence)
                        {
                            summary.SuspiciousFound++;
                        }
                        else
                        {
                            summary.ThreatsFound++;
                        }
                        
                        summary.Threats.Add(new ThreatInfo
                        {
                            FilePath = file,
                            ThreatName = result.ThreatName,
                            ThreatType = result.ThreatType,
                            DetectedBy = result.DetectedBy,
                            Confidence = result.Confidence
                        });

                        Log.Warning("[SCAN] THREAT DETECTED: {File} | {ThreatName} | DetectedBy: {DetectedBy} | Confidence: {Confidence:F0}%",
                            file, result.ThreatName, result.DetectedBy, result.Confidence * 100);

                        ThreatFound?.Invoke(this, new ThreatFoundEventArgs
                        {
                            FilePath = file,
                            ThreatName = result.ThreatName,
                            ThreatType = result.ThreatType,
                            DetectedBy = result.DetectedBy,
                            Confidence = result.Confidence
                        });
                    }



                    var progressArgs = new ScanProgressEventArgs
                    {
                        CurrentFile = file,
                        ScannedCount = scanned,
                        TotalCount = files.Count,
                        ProgressPercent = (int)((scanned / (float)files.Count) * 100)
                    };

                    progress?.Report(progressArgs);
                    ProgressChanged?.Invoke(this, progressArgs);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[SCAN] Error scanning file: {File}", file);
                    summary.ErrorFiles++;
                }
            }

            summary.EndTime = DateTime.Now;
            
            Log.Information("[SCAN] Scan completed: {Scanned}/{Total} files | Threats: {Threats} | Errors: {Errors} | Duration: {Duration:F1}s",
                summary.ScannedFiles, summary.TotalFiles, summary.ThreatsFound, summary.ErrorFiles, summary.Duration.TotalSeconds);
            
            ScanCompleted?.Invoke(this, new ScanCompletedEventArgs { Summary = summary });
        }
        catch (OperationCanceledException)
        {
            summary.WasCancelled = true;
            Log.Information("[SCAN] Scan operation was cancelled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SCAN] Scan operation failed");
            summary.Error = ex.Message;
        }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
        }

        return summary;
    }

    /// <summary>
    /// Scan a directory asynchronously with progress reporting - no file size limit
    /// </summary>
    public async Task<ScanSummary> ScanDirectoryAsync(
        string directoryPath,
        bool recursive = true,
        IProgress<ScanProgressEventArgs>? progress = null)
    {
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;
        var summary = new ScanSummary();

        Log.Information("[SCAN] Starting directory scan: {Directory} (recursive: {Recursive})", directoryPath, recursive);

        try
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = await Task.Run(() => Directory.GetFiles(directoryPath, "*.*", searchOption), token);

            summary.TotalFiles = files.Length;
            int scanned = 0;

            Log.Information("[SCAN] Found {FileCount} files to scan in {Directory}", files.Length, directoryPath);

            foreach (var file in files)
            {
                if (token.IsCancellationRequested)
                {
                    summary.WasCancelled = true;
                    Log.Information("[SCAN] Scan cancelled by user");
                    break;
                }

                try
                {
                    var result = await Task.Run(() => MalwareScanner.Instance.ScanFile(file), token);
                    scanned++;
                    summary.ScannedFiles = scanned;

                    if (result.IsThreat)
                    {
                        // Determine if this is a suspicious (AI-only low confidence) or confirmed threat
                        // Note: confidence = score * agreement (~0.5), so threshold 0.25 = score 0.50
                        bool isAIOnly = result.DetectedBy?.Equals("AI", StringComparison.OrdinalIgnoreCase) == true;
                        bool isLowConfidence = result.Confidence < 0.25f;
                        
                        if (isAIOnly && isLowConfidence)
                        {
                            summary.SuspiciousFound++;
                        }
                        else
                        {
                            summary.ThreatsFound++;
                        }
                        
                        summary.Threats.Add(new ThreatInfo
                        {
                            FilePath = file,
                            ThreatName = result.ThreatName,
                            ThreatType = result.ThreatType,
                            DetectedBy = result.DetectedBy,
                            Confidence = result.Confidence
                        });

                        Log.Warning("[SCAN] THREAT DETECTED: {File} | {ThreatName} | DetectedBy: {DetectedBy} | Confidence: {Confidence:F0}%",
                            file, result.ThreatName, result.DetectedBy, result.Confidence * 100);

                        ThreatFound?.Invoke(this, new ThreatFoundEventArgs
                        {
                            FilePath = file,
                            ThreatName = result.ThreatName,
                            ThreatType = result.ThreatType,
                            DetectedBy = result.DetectedBy,
                            Confidence = result.Confidence
                        });
                    }

                    var progressArgs = new ScanProgressEventArgs
                    {
                        CurrentFile = file,
                        ScannedCount = scanned,
                        TotalCount = files.Length,
                        ProgressPercent = (int)((scanned / (float)files.Length) * 100)
                    };

                    progress?.Report(progressArgs);
                    ProgressChanged?.Invoke(this, progressArgs);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[SCAN] Error scanning file: {File}", file);
                    summary.ErrorFiles++;
                }
            }

            summary.EndTime = DateTime.Now;
            
            Log.Information("[SCAN] Scan completed: {Scanned}/{Total} files | Threats: {Threats} | Errors: {Errors} | Duration: {Duration:F1}s",
                summary.ScannedFiles, summary.TotalFiles, summary.ThreatsFound, summary.ErrorFiles, summary.Duration.TotalSeconds);
            
            ScanCompleted?.Invoke(this, new ScanCompletedEventArgs { Summary = summary });
        }
        catch (OperationCanceledException)
        {
            summary.WasCancelled = true;
            Log.Information("[SCAN] Scan operation was cancelled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SCAN] Scan operation failed");
            summary.Error = ex.Message;
        }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
        }

        return summary;
    }

    /// <summary>
    /// Cancel the current scan operation
    /// </summary>
    public void CancelScan()
    {
        _scanCts?.Cancel();
        Log.Information("[SCAN] Scan cancellation requested");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _disposed = true;
    }
}

#region Event Args and Models

public class ScanProgressEventArgs : EventArgs
{
    public string CurrentFile { get; set; } = string.Empty;
    public int ScannedCount { get; set; }
    public int TotalCount { get; set; }
    public int ProgressPercent { get; set; }
}

public class ThreatFoundEventArgs : EventArgs
{
    public string FilePath { get; set; } = string.Empty;
    public string ThreatName { get; set; } = string.Empty;
    public string ThreatType { get; set; } = string.Empty;
    public string DetectedBy { get; set; } = string.Empty;
    public float Confidence { get; set; }
}

public class ScanCompletedEventArgs : EventArgs
{
    public ScanSummary Summary { get; set; } = new();
}

public class ScanSummary
{
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime EndTime { get; set; }
    public int TotalFiles { get; set; }
    public int ScannedFiles { get; set; }
    public int SkippedFiles { get; set; }
    public int ErrorFiles { get; set; }
    public int ThreatsFound { get; set; }
    public int SuspiciousFound { get; set; }
    public bool WasCancelled { get; set; }
    public string? Error { get; set; }
    public List<ThreatInfo> Threats { get; } = new();

    public TimeSpan Duration => EndTime - StartTime;
}

public class ThreatInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string ThreatName { get; set; } = string.Empty;
    public string ThreatType { get; set; } = string.Empty;
    public string DetectedBy { get; set; } = string.Empty;
    public float Confidence { get; set; }
}

#endregion
