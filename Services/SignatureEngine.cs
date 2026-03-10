using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LightningDB;
using Serilog;

namespace AfterlifeWinUI.Services;

/// <summary>
/// Signature-based malware detection engine using LMDB hash database
/// Supports MD5, SHA1, and SHA256 hash lookups
/// </summary>
public class SignatureEngine : IDisposable
{
    private LightningEnvironment? _env;
    private bool _initialized;
    private bool _disposed;
    private string _dbPath = string.Empty;
    private uint _signatureCount;

    public bool IsInitialized => _initialized;
    public uint SignatureCount => _signatureCount;

    /// <summary>
    /// Initialize the signature engine with the LMDB database path
    /// The path should be the folder containing data.mdb (the .lmdb folder)
    /// </summary>
    public bool Initialize(string dbPath)
    {
        if (_initialized)
        {
            Log.Warning("[SignatureEngine] Already initialized");
            return true;
        }

        try
        {
            // Resolve the actual LMDB folder path
            string? lmdbPath = ResolveLmdbPath(dbPath);
            
            if (string.IsNullOrEmpty(lmdbPath))
            {
                Log.Error("[SignatureEngine] Could not find LMDB database at: {Path}", dbPath);
                return false;
            }

            _dbPath = lmdbPath;
            Log.Debug("[SignatureEngine] Opening LMDB at: {Path}", lmdbPath);

            // Open LMDB environment - the path must be the folder containing data.mdb
            _env = new LightningEnvironment(lmdbPath)
            {
                MaxDatabases = 10,
                MapSize = 10L * 1024 * 1024 * 1024 // 10 GB max
            };
            
            // Open read-only without NoSubDir (LMDB expects a directory)
            _env.Open(EnvironmentOpenFlags.ReadOnly);

            // Count signatures across all databases
            _signatureCount = CountSignatures();

            _initialized = true;
            Log.Information("[SignatureEngine] Initialized with {Count:N0} signatures from {Path}", 
                _signatureCount, lmdbPath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SignatureEngine] Failed to initialize from {Path}", dbPath);
            return false;
        }
    }

    /// <summary>
    /// Resolve the actual LMDB folder path
    /// Handles various input formats: folder path, .lmdb folder, or parent folder
    /// </summary>
    private string? ResolveLmdbPath(string inputPath)
    {
        // If it's already a valid LMDB folder (contains data.mdb)
        if (Directory.Exists(inputPath) && File.Exists(Path.Combine(inputPath, "data.mdb")))
        {
            return inputPath;
        }

        // If it's a path ending with .lmdb but doesn't have data.mdb directly
        if (inputPath.EndsWith(".lmdb", StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(inputPath) && File.Exists(Path.Combine(inputPath, "data.mdb")))
            {
                return inputPath;
            }
        }

        // Check if there's a malware.lmdb subfolder
        var lmdbSubfolder = Path.Combine(inputPath, "malware.lmdb");
        if (Directory.Exists(lmdbSubfolder) && File.Exists(Path.Combine(lmdbSubfolder, "data.mdb")))
        {
            return lmdbSubfolder;
        }

        // Check if parent has malware.lmdb
        var parentPath = Path.GetDirectoryName(inputPath);
        if (!string.IsNullOrEmpty(parentPath))
        {
            lmdbSubfolder = Path.Combine(parentPath, "malware.lmdb");
            if (Directory.Exists(lmdbSubfolder) && File.Exists(Path.Combine(lmdbSubfolder, "data.mdb")))
            {
                return lmdbSubfolder;
            }
        }

        Log.Warning("[SignatureEngine] No data.mdb found at {Path}", inputPath);
        return null;
    }

    private uint CountSignatures()
    {
        uint count = 0;
        
        try
        {
            using var tx = _env!.BeginTransaction(TransactionBeginFlags.ReadOnly);
            
            // Count from md5, sha1, sha256 databases
            string[] dbs = { "md5", "sha1", "sha256" };
            foreach (var dbName in dbs)
            {
                try
                {
                    using var db = tx.OpenDatabase(dbName, new DatabaseConfiguration { Flags = DatabaseOpenFlags.None });
                    var entries = tx.GetEntriesCount(db);
                    count += (uint)entries;
                    Log.Debug("[SignatureEngine] {DbName} database: {Count:N0} entries", dbName, entries);
                }
                catch (LightningException ex) when (ex.StatusCode == -30798) // MDB_NOTFOUND
                {
                    Log.Debug("[SignatureEngine] Database '{DbName}' not found", dbName);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[SignatureEngine] Error counting signatures");
        }

        return count;
    }

    /// <summary>
    /// Scan a file for malware signatures - no file size limit
    /// Uses streaming hash computation for large files
    /// </summary>
    public ScanResult ScanFile(string filePath)
    {
        var result = new ScanResult();
        
        if (!_initialized || _env == null)
        {
            return result;
        }

        if (!File.Exists(filePath))
        {
            return result;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            Log.Verbose("[SignatureEngine] Scanning file: {Path} ({Size:N0} bytes)", filePath, fileInfo.Length);

            // Use streaming hash computation for all files
            byte[] md5Hash, sha1Hash, sha256Hash;
            
            using (var stream = File.OpenRead(filePath))
            {
                // Compute all hashes in parallel-like fashion using transforms
                md5Hash = ComputeHashFromStream(stream, MD5.Create());
                stream.Position = 0;
                sha256Hash = ComputeHashFromStream(stream, SHA256.Create());
                stream.Position = 0;
                sha1Hash = ComputeHashFromStream(stream, SHA1.Create());
            }

            // Check MD5
            var threatName = LookupHash(md5Hash, "md5");
            if (!string.IsNullOrEmpty(threatName))
            {
                result.IsThreat = true;
                result.ThreatName = threatName;
                result.ThreatType = "signature";
                result.DetectedBy = "SignatureEngine (MD5)";
                result.Confidence = 1.0f;
                Log.Warning("[SignatureEngine] MD5 match: {Path} -> {Threat}", filePath, threatName);
                return result;
            }

            // Check SHA256
            threatName = LookupHash(sha256Hash, "sha256");
            if (!string.IsNullOrEmpty(threatName))
            {
                result.IsThreat = true;
                result.ThreatName = threatName;
                result.ThreatType = "signature";
                result.DetectedBy = "SignatureEngine (SHA256)";
                result.Confidence = 1.0f;
                Log.Warning("[SignatureEngine] SHA256 match: {Path} -> {Threat}", filePath, threatName);
                return result;
            }

            // Check SHA1
            threatName = LookupHash(sha1Hash, "sha1");
            if (!string.IsNullOrEmpty(threatName))
            {
                result.IsThreat = true;
                result.ThreatName = threatName;
                result.ThreatType = "signature";
                result.DetectedBy = "SignatureEngine (SHA1)";
                result.Confidence = 1.0f;
                Log.Warning("[SignatureEngine] SHA1 match: {Path} -> {Threat}", filePath, threatName);
                return result;
            }
        }
        catch (IOException ex)
        {
            Log.Debug("[SignatureEngine] IO error scanning file {Path}: {Message}", filePath, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Debug("[SignatureEngine] Access denied to file {Path}: {Message}", filePath, ex.Message);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[SignatureEngine] Error scanning file: {Path}", filePath);
        }

        return result;
    }

    /// <summary>
    /// Compute hash from stream - memory efficient for large files
    /// </summary>
    private static byte[] ComputeHashFromStream(Stream stream, HashAlgorithm algorithm)
    {
        using (algorithm)
        {
            return algorithm.ComputeHash(stream);
        }
    }

    private string? LookupHash(byte[] hash, string dbName)
    {
        if (_env == null) return null;

        try
        {
            using var tx = _env.BeginTransaction(TransactionBeginFlags.ReadOnly);
            
            try
            {
                using var db = tx.OpenDatabase(dbName, new DatabaseConfiguration { Flags = DatabaseOpenFlags.None });
                
                var result = tx.Get(db, hash);
                if (result.resultCode == MDBResultCode.Success)
                {
                    return Encoding.UTF8.GetString(result.value.CopyToNewArray());
                }
            }
            catch (LightningException ex) when (ex.StatusCode == -30798) // MDB_NOTFOUND
            {
                // Database not found - not an error
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[SignatureEngine] Error looking up hash in {Db}", dbName);
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _env?.Dispose();
        _env = null;
        _initialized = false;
        _disposed = true;
        
        Log.Debug("[SignatureEngine] Disposed");
    }
}

/// <summary>
/// Result of a malware scan
/// </summary>
public class ScanResult
{
    public bool IsThreat { get; set; }
    public string ThreatName { get; set; } = string.Empty;
    public string ThreatType { get; set; } = string.Empty;
    public string DetectedBy { get; set; } = string.Empty;
    public float Confidence { get; set; }

    public static ScanResult Empty => new();
}
