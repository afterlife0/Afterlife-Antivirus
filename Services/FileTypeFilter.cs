using System;
using System.Collections.Generic;
using System.IO;
using Serilog;

namespace AfterlifeWinUI.Services;

/// <summary>
/// Service to filter files based on type, reducing false positives by skipping
/// known safe file types that are unlikely to contain executable malware.
/// </summary>
public static class FileTypeFilter
{
    /// <summary>
    /// File extensions that are safe and should be skipped from deep scanning.
    /// These file types cannot contain executable code or are extremely unlikely
    /// to be used as malware delivery vectors.
    /// </summary>
    private static readonly HashSet<string> SafeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images - cannot execute code
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".webp", ".svg", ".tiff", ".tif",
        ".raw", ".cr2", ".nef", ".arw", ".dng", ".heic", ".heif", ".avif",
        
        // Audio - cannot execute code
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a", ".opus", ".aiff",
        
        // Video - cannot execute code (media containers without scripts)
        ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpeg", ".mpg",
        
        // Fonts - cannot execute code
        ".ttf", ".otf", ".woff", ".woff2", ".eot",
        
        // Plain text - cannot execute code
        ".txt", ".md", ".markdown", ".log", ".csv", ".tsv", ".ini", ".cfg",
        ".json", ".yaml", ".yml", ".xml", ".toml",
        
        // Source code text files (not compiled/executable)
        ".cs", ".vb", ".fs", ".cpp", ".c", ".h", ".hpp", ".java", ".kt", ".swift",
        ".py", ".rb", ".go", ".rs", ".ts", ".js", ".jsx", ".tsx", ".vue", ".svelte",
        ".css", ".scss", ".sass", ".less", ".html", ".htm", ".xaml", ".razor",
        ".sql", ".sh", ".bash", ".zsh", ".fish", ".ps1", ".psm1",
        
        // Data/Database files
        ".db", ".sqlite", ".sqlite3", ".mdb", ".accdb",
        
        // Certificate/Key files (text-based)
        ".pem", ".crt", ".cer", ".key",
        
        // Archive metadata
        ".torrent",
        
        // Subtitle files
        ".srt", ".sub", ".ass", ".ssa", ".vtt",
    };

    /// <summary>
    /// File extensions that are potentially dangerous and should always be scanned.
    /// These file types can contain executable code or scripts.
    /// </summary>
    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Windows executables
        ".exe", ".dll", ".sys", ".drv", ".ocx", ".cpl", ".scr", ".com",
        
        // Scripts that can execute
        ".bat", ".cmd", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh",
        ".ps1", ".psm1", ".psd1",
        
        // Office documents with macros
        ".docm", ".xlsm", ".pptm", ".dotm", ".xltm", ".potm",
        ".doc", ".xls", ".ppt", // Old formats can have macros
        
        // Other executable formats
        ".msi", ".msp", ".msu",
        ".jar", ".class",
        ".hta", ".html", ".htm", // Can contain scripts
        ".chm", ".hlp",
        ".lnk", ".url", // Shortcuts can point to malware
        ".reg", // Registry files
        ".inf", ".pif",
        
        // Archives (can contain malware)
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz",
        ".cab", ".iso", ".img", ".vhd", ".vhdx",
        
        // PDF can contain scripts
        ".pdf",
    };

    /// <summary>
    /// Minimum file size to scan (in bytes). Very small files are unlikely to be malware.
    /// </summary>
    private const long MinScanSize = 100; // 100 bytes minimum

    /// <summary>
    /// Maximum file size to scan (in bytes). Very large files take too long and are 
    /// less likely to be simple malware droppers.
    /// </summary>
    private const long MaxScanSize = 500 * 1024 * 1024; // 500 MB maximum

    /// <summary>
    /// Determines if a file should be scanned based on its type and characteristics.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>True if the file should be scanned, false if it should be skipped</returns>
    public static bool ShouldScanFile(string filePath)
    {
        return ShouldScanFile(filePath, out _);
    }

    /// <summary>
    /// Determines if a file should be scanned based on its type and characteristics.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="reason">Reason for the decision</param>
    /// <returns>True if the file should be scanned, false if it should be skipped</returns>
    public static bool ShouldScanFile(string filePath, out string reason)
    {
        reason = string.Empty;

        try
        {
            // Check if file exists
            if (!File.Exists(filePath))
            {
                reason = "File does not exist";
                return false;
            }

            var fileInfo = new FileInfo(filePath);
            var extension = fileInfo.Extension.ToLowerInvariant();

            // Check file size
            if (fileInfo.Length < MinScanSize)
            {
                reason = $"File too small ({fileInfo.Length} bytes)";
                return false;
            }

            if (fileInfo.Length > MaxScanSize)
            {
                reason = $"File too large ({fileInfo.Length / (1024 * 1024)} MB)";
                return false;
            }

            // Check if it's a known safe extension
            if (SafeExtensions.Contains(extension))
            {
                reason = $"Safe file type: {extension}";
                return false;
            }

            // Always scan dangerous extensions
            if (DangerousExtensions.Contains(extension))
            {
                reason = $"Potentially dangerous file type: {extension}";
                return true;
            }

            // For unknown extensions, scan them to be safe
            reason = $"Unknown file type: {extension}";
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug("[FileTypeFilter] Error checking file {Path}: {Message}", filePath, ex.Message);
            reason = "Error checking file";
            return false;
        }
    }

    /// <summary>
    /// Check if a file extension is known to be safe
    /// </summary>
    public static bool IsSafeExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return false;
        
        if (!extension.StartsWith("."))
            extension = "." + extension;
            
        return SafeExtensions.Contains(extension);
    }

    /// <summary>
    /// Check if a file extension is known to be dangerous
    /// </summary>
    public static bool IsDangerousExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return false;
        
        if (!extension.StartsWith("."))
            extension = "." + extension;
            
        return DangerousExtensions.Contains(extension);
    }

    /// <summary>
    /// Get the file type category for display purposes
    /// </summary>
    public static string GetFileTypeCategory(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (string.IsNullOrEmpty(extension))
            return "Unknown";

        if (SafeExtensions.Contains(extension))
            return "Safe";

        if (DangerousExtensions.Contains(extension))
            return "Potentially Dangerous";

        return "Unknown";
    }
}
