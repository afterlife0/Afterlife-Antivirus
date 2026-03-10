using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace AfterlifeWinUI.Services;

/// <summary>
/// Service for persisting detected threats with timestamps, actions, and history
/// </summary>
public class ThreatHistoryService
{
    private static ThreatHistoryService? _instance;
    private static readonly object _lock = new();
    
    /// <summary>
    /// Get the data folder path from ConfigurationManager
    /// </summary>
    private static string DataFolder => ConfigurationManager.IsInitialized
        ? ConfigurationManager.AppDataFolder
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AfterlifeWinUI");
    
    /// <summary>
    /// Get the threat history file path from ConfigurationManager
    /// </summary>
    private static string ThreatHistoryFile => ConfigurationManager.IsInitialized
        ? ConfigurationManager.ThreatHistoryFile
        : Path.Combine(DataFolder, "threat-history.json");

    /// <summary>
    /// Get the quarantine folder path from ConfigurationManager
    /// </summary>
    private static string QuarantineFolder => ConfigurationManager.IsInitialized
        ? ConfigurationManager.QuarantineFolder
        : Path.Combine(DataFolder, "quarantine");
    
    private readonly List<ThreatRecord> _allThreats = new();
    
    public static ThreatHistoryService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ThreatHistoryService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Observable collection of active (unresolved) threats for UI binding
    /// </summary>
    public ObservableCollection<ThreatRecord> ActiveThreats { get; } = new();

    /// <summary>
    /// All threats including resolved ones
    /// </summary>
    public IReadOnlyList<ThreatRecord> AllThreats => _allThreats.AsReadOnly();

    public int TotalCount => _allThreats.Count;
    public int ActiveCount => ActiveThreats.Count;
    public int QuarantinedCount => _allThreats.Count(t => t.Action == ThreatAction.Quarantined);
    public int DeletedCount => _allThreats.Count(t => t.Action == ThreatAction.Deleted);

    public event EventHandler? ThreatsChanged;

    private ThreatHistoryService()
    {
        EnsureDirectoriesExist();
        Load();
    }

    /// <summary>
    /// Ensure all required directories exist
    /// </summary>
    private void EnsureDirectoriesExist()
    {
        try
        {
            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
                Log.Debug("[ThreatHistory] Created data folder: {Path}", DataFolder);
            }

            if (!Directory.Exists(QuarantineFolder))
            {
                Directory.CreateDirectory(QuarantineFolder);
                Log.Debug("[ThreatHistory] Created quarantine folder: {Path}", QuarantineFolder);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ThreatHistory] Failed to create directories");
        }
    }

    /// <summary>
    /// Add a newly detected threat
    /// </summary>
    public void AddThreat(string filePath, string threatName, string detectedBy, string threatType, float confidence)
    {
        var record = new ThreatRecord
        {
            Id = Guid.NewGuid().ToString(),
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            ThreatName = threatName,
            DetectedBy = detectedBy,
            ThreatType = threatType,
            Confidence = confidence,
            DetectedAt = DateTime.Now,
            Action = ThreatAction.Active
        };

        lock (_lock)
        {
            // Check if already exists
            var existing = _allThreats.FirstOrDefault(t => 
                t.FilePath == filePath && t.Action == ThreatAction.Active);
            
            if (existing != null)
            {
                Log.Debug("[ThreatHistory] Threat already recorded: {Path}", filePath);
                return;
            }

            _allThreats.Insert(0, record);
            ActiveThreats.Insert(0, record);
            
            Save();
            ThreatsChanged?.Invoke(this, EventArgs.Empty);
            
            Log.Information("[ThreatHistory] Added threat: {Name} at {Path}", threatName, filePath);
        }
    }

    /// <summary>
    /// Quarantine a threat
    /// </summary>
    public bool QuarantineThreat(string threatId)
    {
        lock (_lock)
        {
            var threat = _allThreats.FirstOrDefault(t => t.Id == threatId);
            if (threat == null) return false;

            try
            {
                // Ensure quarantine folder exists
                EnsureDirectoriesExist();
                
                var quarantinePath = Path.Combine(QuarantineFolder, $"{threat.Id}_{threat.FileName}");
                
                if (File.Exists(threat.FilePath))
                {
                    File.Move(threat.FilePath, quarantinePath);
                    threat.QuarantinePath = quarantinePath;
                    Log.Debug("[ThreatHistory] Moved file to quarantine: {Path}", quarantinePath);
                }

                threat.Action = ThreatAction.Quarantined;
                threat.ActionAt = DateTime.Now;
                
                ActiveThreats.Remove(threat);
                Save();
                ThreatsChanged?.Invoke(this, EventArgs.Empty);
                
                Log.Information("[ThreatHistory] Quarantined: {Path}", threat.FilePath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ThreatHistory] Failed to quarantine: {Path}", threat.FilePath);
                return false;
            }
        }
    }

    /// <summary>
    /// Delete a threat file
    /// </summary>
    public bool DeleteThreat(string threatId)
    {
        lock (_lock)
        {
            var threat = _allThreats.FirstOrDefault(t => t.Id == threatId);
            if (threat == null) return false;

            try
            {
                if (File.Exists(threat.FilePath))
                {
                    File.Delete(threat.FilePath);
                }

                threat.Action = ThreatAction.Deleted;
                threat.ActionAt = DateTime.Now;
                
                ActiveThreats.Remove(threat);
                Save();
                ThreatsChanged?.Invoke(this, EventArgs.Empty);
                
                Log.Information("[ThreatHistory] Deleted: {Path}", threat.FilePath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ThreatHistory] Failed to delete: {Path}", threat.FilePath);
                return false;
            }
        }
    }

    /// <summary>
    /// Ignore a threat (mark as allowed)
    /// </summary>
    public void IgnoreThreat(string threatId)
    {
        lock (_lock)
        {
            var threat = _allThreats.FirstOrDefault(t => t.Id == threatId);
            if (threat == null) return;

            threat.Action = ThreatAction.Ignored;
            threat.ActionAt = DateTime.Now;
            
            ActiveThreats.Remove(threat);
            Save();
            ThreatsChanged?.Invoke(this, EventArgs.Empty);
            
            Log.Information("[ThreatHistory] Ignored: {Path}", threat.FilePath);
        }
    }

    /// <summary>
    /// Quarantine all active threats
    /// </summary>
    public int QuarantineAll()
    {
        int count = 0;
        var activeIds = ActiveThreats.Select(t => t.Id).ToList();
        
        foreach (var id in activeIds)
        {
            if (QuarantineThreat(id))
                count++;
        }
        
        return count;
    }

    /// <summary>
    /// Clear resolved threats from history
    /// </summary>
    public void ClearResolved()
    {
        lock (_lock)
        {
            _allThreats.RemoveAll(t => t.Action != ThreatAction.Active);
            Save();
            ThreatsChanged?.Invoke(this, EventArgs.Empty);
            
            Log.Information("[ThreatHistory] Cleared resolved threats");
        }
    }

    /// <summary>
    /// Load threat history from disk
    /// </summary>
    private void Load()
    {
        try
        {
            var filePath = ThreatHistoryFile;
            Log.Debug("[ThreatHistory] Loading from: {Path}", filePath);

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                
                if (string.IsNullOrWhiteSpace(json))
                {
                    Log.Debug("[ThreatHistory] File is empty, starting fresh");
                    return;
                }

                // Use source-generated serializer for AOT compatibility
                var data = JsonSerializer.Deserialize(json, AppJsonContext.Default.ThreatHistoryData);
                
                if (data?.Threats != null)
                {
                    _allThreats.Clear();
                    ActiveThreats.Clear();
                    
                    foreach (var threat in data.Threats.OrderByDescending(t => t.DetectedAt))
                    {
                        _allThreats.Add(threat);
                        if (threat.Action == ThreatAction.Active)
                        {
                            ActiveThreats.Add(threat);
                        }
                    }
                    
                    Log.Information("[ThreatHistory] Loaded {Total} threats ({Active} active) from {Path}", 
                        _allThreats.Count, ActiveThreats.Count, filePath);
                }
            }
            else
            {
                Log.Debug("[ThreatHistory] History file not found, starting fresh: {Path}", filePath);
            }
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "[ThreatHistory] Invalid JSON in threat history file, starting fresh");
            _allThreats.Clear();
            ActiveThreats.Clear();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ThreatHistory] Failed to load threat history");
        }
    }

    /// <summary>
    /// Save threat history to disk
    /// </summary>
    private void Save()
    {
        try
        {
            // Ensure directory exists
            EnsureDirectoriesExist();

            var filePath = ThreatHistoryFile;
            var data = new ThreatHistoryData { Threats = _allThreats };
            
            // Use source-generated serializer for AOT compatibility
            var json = JsonSerializer.Serialize(data, AppJsonContext.Default.ThreatHistoryData);
            
            // Write to temp file first, then rename for atomic operation
            var tempFile = filePath + ".tmp";
            File.WriteAllText(tempFile, json);
            
            // Replace the original file
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            File.Move(tempFile, filePath);
            
            Log.Debug("[ThreatHistory] Saved {Count} threats to {Path}", _allThreats.Count, filePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ThreatHistory] Failed to save threat history");
        }
    }

    /// <summary>
    /// Force a reload of threat history from disk
    /// </summary>
    public void Reload()
    {
        lock (_lock)
        {
            Load();
            ThreatsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>
/// Persistence data structure
/// </summary>
public class ThreatHistoryData
{
    public List<ThreatRecord> Threats { get; set; } = new();
}

/// <summary>
/// Record of a detected threat
/// </summary>
public class ThreatRecord
{
    public string Id { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ThreatName { get; set; } = string.Empty;
    public string DetectedBy { get; set; } = string.Empty;
    public string ThreatType { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public DateTime DetectedAt { get; set; }
    public ThreatAction Action { get; set; }
    public DateTime? ActionAt { get; set; }
    public string? QuarantinePath { get; set; }

    [JsonIgnore]
    public string DetectedAtString => DetectedAt.ToString("yyyy-MM-dd HH:mm:ss");
    
    [JsonIgnore]
    public string ConfidenceString => $"{Confidence * 100:F0}%";
    
    [JsonIgnore]
    public string ActionString => Action.ToString();
}

public enum ThreatAction
{
    Active,
    Quarantined,
    Deleted,
    Ignored
}
