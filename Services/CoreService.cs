using System;
using System.IO;
using System.Runtime.InteropServices;
using Serilog;

namespace AfterlifeWinUI.Services;

/// <summary>
/// P/Invoke wrapper for AfterlifeCore.dll with error handling
/// </summary>
public static class CoreService
{
    private const string DllName = "AfterlifeCore.dll";
    private static bool _isInitialized;
    private static readonly object _lock = new();

    #region Structures

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct ScanResult
    {
        [MarshalAs(UnmanagedType.I1)]
        public bool IsThreat;
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ThreatName;
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string ThreatType;
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DetectedBy;
        
        public float Confidence;

        public static ScanResult Empty => new() 
        { 
            IsThreat = false, 
            ThreatName = string.Empty, 
            ThreatType = string.Empty, 
            DetectedBy = string.Empty,
            Confidence = 0 
        };
        
        /// <summary>
        /// Gets a formatted detection summary string
        /// </summary>
        public readonly string GetDetectionSummary()
        {
            if (!IsThreat) return "Clean";
            
            var confidence = (Confidence * 100).ToString("F0");
            return $"{ThreatName} [{DetectedBy}] ({confidence}% confidence)";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Stats
    {
        public ulong FilesScanned;
        public ulong ThreatsDetected;
        public uint SignatureCount;
        public uint YaraRuleCount;
        [MarshalAs(UnmanagedType.I1)]
        public bool BrainOnline;
        [MarshalAs(UnmanagedType.I1)]
        public bool MonitoringActive;

        public static Stats Empty => new();
    }

    #endregion

    #region Delegates

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ThreatCallback(string filePath, ref ScanResult result);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LogCallback(int level, string message);

    #endregion

    #region Native Imports

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Initialize")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool Native_Initialize(string configPath);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Shutdown")]
    private static extern void Native_Shutdown();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ScanFile")]
    private static extern ScanResult Native_ScanFile(string filePath);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ScanDirectory")]
    private static extern void Native_ScanDirectory(string dirPath, [MarshalAs(UnmanagedType.I1)] bool recursive);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "StartMonitoring")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool Native_StartMonitoring(string[] paths, int pathCount);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "StopMonitoring")]
    private static extern void Native_StopMonitoring();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IsMonitoring")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool Native_IsMonitoring();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetStats")]
    private static extern Stats Native_GetStats();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SetThreatCallback")]
    private static extern void Native_SetThreatCallback(ThreatCallback callback);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SetLogCallback")]
    private static extern void Native_SetLogCallback(LogCallback callback);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SetLogLevel")]
    private static extern void Native_SetLogLevel(int level);

    #endregion

    #region Public API with Error Handling

    public static bool IsInitialized => _isInitialized;

    public static bool Initialize(string? configPath = null)
    {
        lock (_lock)
        {
            if (_isInitialized)
            {
                Log.Warning("CoreService already initialized");
                return true;
            }

            try
            {
                // Check if DLL exists
                var dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DllName);
                if (!File.Exists(dllPath))
                {
                    Log.Warning("AfterlifeCore.dll not found at: {Path} - running in demo mode", dllPath);
                    return false;
                }

                // Use default config path if not specified
                if (string.IsNullOrEmpty(configPath))
                {
                    configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                }

                // Check if config exists
                if (!File.Exists(configPath))
                {
                    Log.Warning("Config file not found at: {Path} - using defaults", configPath);
                    // Continue without config - the native library may have defaults
                }

                _isInitialized = Native_Initialize(configPath);
                
                if (_isInitialized)
                {
                    Log.Information("CoreService initialized successfully");
                }
                else
                {
                    Log.Warning("CoreService initialization returned false");
                }

                return _isInitialized;
            }
            catch (DllNotFoundException ex)
            {
                Log.Warning(ex, "AfterlifeCore.dll not found - running in demo mode");
                return false;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize CoreService - running in demo mode");
                return false;
            }
        }
    }

    public static void Shutdown()
    {
        lock (_lock)
        {
            if (!_isInitialized) return;

            try
            {
                Native_Shutdown();
                _isInitialized = false;
                Log.Information("CoreService shut down");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during CoreService shutdown");
            }
        }
    }

    public static ScanResult ScanFile(string filePath)
    {
        if (!_isInitialized)
        {
            Log.Verbose("CoreService not initialized, returning empty result");
            return ScanResult.Empty;
        }

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return ScanResult.Empty;
        }

        try
        {
            return Native_ScanFile(filePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error scanning file: {Path}", filePath);
            return ScanResult.Empty;
        }
    }

    public static void ScanDirectory(string dirPath, bool recursive)
    {
        if (!_isInitialized)
        {
            Log.Warning("CoreService not initialized, cannot scan directory");
            return;
        }

        try
        {
            Native_ScanDirectory(dirPath, recursive);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error scanning directory: {Path}", dirPath);
        }
    }

    public static bool StartMonitoring(string[] paths, int pathCount)
    {
        if (!_isInitialized)
        {
            Log.Warning("CoreService not initialized, cannot start monitoring");
            return false;
        }

        try
        {
            return Native_StartMonitoring(paths, pathCount);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error starting monitoring");
            return false;
        }
    }

    public static void StopMonitoring()
    {
        if (!_isInitialized) return;

        try
        {
            Native_StopMonitoring();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error stopping monitoring");
        }
    }

    public static bool IsMonitoring()
    {
        if (!_isInitialized) return false;

        try
        {
            return Native_IsMonitoring();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking monitoring status");
            return false;
        }
    }

    public static Stats GetStats()
    {
        if (!_isInitialized) return Stats.Empty;

        try
        {
            return Native_GetStats();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error getting stats");
            return Stats.Empty;
        }
    }

    public static void SetThreatCallback(ThreatCallback callback)
    {
        if (!_isInitialized) return;
        
        try
        {
            Native_SetThreatCallback(callback);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting threat callback");
        }
    }

    public static void SetLogCallback(LogCallback callback)
    {
        if (!_isInitialized) return;
        
        try
        {
            Native_SetLogCallback(callback);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting log callback");
        }
    }

    public static void SetLogLevel(int level)
    {
        if (!_isInitialized) return;
        
        try
        {
            Native_SetLogLevel(level);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting log level");
        }
    }

    #endregion
}
