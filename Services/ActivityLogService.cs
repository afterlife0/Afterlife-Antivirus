using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Serilog;

namespace AfterlifeWinUI.Services;

/// <summary>
/// Singleton service for app-wide activity logging
/// Shared between Dashboard, Activity Page, and other components
/// </summary>
public class ActivityLogService
{
    private static ActivityLogService? _instance;
    private static readonly object _lock = new();
    private DispatcherQueue? _dispatcherQueue;

    public static ActivityLogService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ActivityLogService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Observable collection of activity log entries for UI binding
    /// </summary>
    public ObservableCollection<ActivityLogEntry> Entries { get; } = new();

    // Statistics
    public int TotalEvents => Entries.Count;
    public int ThreatCount => Entries.Count(e => e.Type == ActivityType.Threat);
    public int SuspiciousCount => Entries.Count(e => e.Type == ActivityType.Suspicious);
    public int ScanCount => Entries.Count(e => e.Type == ActivityType.Scan);
    public int WarningCount => Entries.Count(e => e.Type == ActivityType.Warning);

    public event EventHandler? StatsChanged;

    private ActivityLogService()
    {
    }

    /// <summary>
    /// Set the dispatcher queue for UI updates (call from UI thread)
    /// </summary>
    public void SetDispatcher(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    /// <summary>
    /// Add an activity log entry
    /// </summary>
    public void AddEntry(string message, ActivityType type)
    {
        var entry = new ActivityLogEntry
        {
            Time = DateTime.Now,
            Message = message,
            Type = type
        };

        // Log to Serilog as well
        switch (type)
        {
            case ActivityType.Error:
            case ActivityType.Threat:
                Log.Warning("[Activity] {Type}: {Message}", type, message);
                break;
            case ActivityType.Warning:
                Log.Information("[Activity] {Type}: {Message}", type, message);
                break;
            default:
                Log.Debug("[Activity] {Type}: {Message}", type, message);
                break;
        }

        DispatchToUI(() =>
        {
            // Limit log size
            while (Entries.Count >= 500)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }

            Entries.Insert(0, entry);
            StatsChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>
    /// Add info entry
    /// </summary>
    public void Info(string message) => AddEntry(message, ActivityType.Info);

    /// <summary>
    /// Add scan entry
    /// </summary>
    public void Scan(string message) => AddEntry(message, ActivityType.Scan);

    /// <summary>
    /// Add success entry
    /// </summary>
    public void Success(string message) => AddEntry(message, ActivityType.Success);

    /// <summary>
    /// Add warning entry
    /// </summary>
    public void Warning(string message) => AddEntry(message, ActivityType.Warning);

    /// <summary>
    /// Add error entry
    /// </summary>
    public void Error(string message) => AddEntry(message, ActivityType.Error);

    /// <summary>
    /// Add threat entry
    /// </summary>
    public void Threat(string message) => AddEntry(message, ActivityType.Threat);

    /// <summary>
    /// Add suspicious entry (lower confidence AI detection)
    /// </summary>
    public void Suspicious(string message) => AddEntry(message, ActivityType.Suspicious);

    /// <summary>
    /// Add monitor entry
    /// </summary>
    public void Monitor(string message) => AddEntry(message, ActivityType.Monitor);

    /// <summary>
    /// Clear all entries
    /// </summary>
    public void Clear()
    {
        DispatchToUI(() =>
        {
            Entries.Clear();
            StatsChanged?.Invoke(this, EventArgs.Empty);
            Log.Debug("[ActivityLog] Cleared");
        });
    }

    private void DispatchToUI(Action action)
    {
        if (_dispatcherQueue == null)
        {
            // No dispatcher yet, try to execute directly
            action();
            return;
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
    }
}

/// <summary>
/// Represents a single activity log entry
/// </summary>
public class ActivityLogEntry
{
    public DateTime Time { get; set; }
    public string Message { get; set; } = string.Empty;
    public ActivityType Type { get; set; }

    public string TimeString => Time.ToString("HH:mm:ss");
    public string TypeString => Type.ToString().ToUpper();

    public string TypeColor => Type switch
    {
        ActivityType.Success => "#00ff9d",
        ActivityType.Warning => "#ffaa00",
        ActivityType.Error => "#ff2a6d",
        ActivityType.Threat => "#ff2a6d",
        ActivityType.Suspicious => "#ff8800",  // Orange for suspicious
        ActivityType.Scan => "#00f3ff",
        ActivityType.Monitor => "#b026ff",
        _ => "#888888"
    };
}

public enum ActivityType
{
    Info,
    Scan,
    Success,
    Warning,
    Error,
    Threat,
    Suspicious,
    Monitor
}
