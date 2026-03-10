using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Serilog;

namespace AfterlifeWinUI.Services;

/// <summary>
/// YARA-based malware detection engine using native libyara64.dll
/// Provides YARA rule compilation and file scanning
/// </summary>
public class YaraEngine : IDisposable
{
    private static bool _yaraInitialized = false;
    private static readonly object _initLock = new();
    
    private IntPtr _compiledRules = IntPtr.Zero;
    private bool _initialized;
    private bool _disposed;
    private string _rulesPath = string.Empty;
    private string _compiledRulesPath = string.Empty;
    private uint _ruleCount;
    private readonly object _scanLock = new();
    private List<string> _compilationErrors = new();

    public bool IsInitialized => _initialized;
    public uint RuleCount => _ruleCount;
    public bool CanScan => _compiledRules != IntPtr.Zero;

    /// <summary>
    /// Initialize the YARA engine with rules file
    /// </summary>
    public bool Initialize(string rulesPath)
    {
        if (_initialized)
        {
            Log.Warning("[YaraEngine] Already initialized");
            return true;
        }

        try
        {
            if (!File.Exists(rulesPath))
            {
                Log.Warning("[YaraEngine] Rules file not found: {Path}", rulesPath);
                _initialized = true;
                return true;
            }

            _rulesPath = rulesPath;
            
            // Initialize YARA library
            if (!InitializeYaraLibrary())
            {
                _ruleCount = CountRulesInFile(rulesPath);
                Log.Warning("[YaraEngine] Failed to initialize native YARA - counted {Count:N0} rules", _ruleCount);
                _initialized = true;
                return true;
            }

            // Try to load pre-compiled rules first
            _compiledRulesPath = Path.ChangeExtension(rulesPath, ".yarc");
            if (File.Exists(_compiledRulesPath) && 
                File.GetLastWriteTime(_compiledRulesPath) >= File.GetLastWriteTime(rulesPath))
            {
                if (LoadCompiledRules(_compiledRulesPath))
                {
                    _initialized = true;
                    Log.Information("[YaraEngine] Loaded pre-compiled rules: {Count:N0} YARA rules (scanning: enabled)", _ruleCount);
                    return true;
                }
            }

            // Compile rules from source
            if (CompileRules(rulesPath))
            {
                // Save compiled rules for faster loading next time
                SaveCompiledRules(_compiledRulesPath);
                _initialized = true;
                Log.Information("[YaraEngine] Compiled {Count:N0} YARA rules (scanning: enabled)", _ruleCount);
                return true;
            }
            else
            {
                // Fall back to counting rules
                _ruleCount = CountRulesInFile(rulesPath);
                Log.Warning("[YaraEngine] Compilation failed - counted {Count:N0} rules (scanning: disabled)", _ruleCount);
                _initialized = true;
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[YaraEngine] Failed to initialize");
            _ruleCount = CountRulesInFile(rulesPath);
            _initialized = true;
            return true;
        }
    }

    /// <summary>
    /// Initialize the native YARA library
    /// </summary>
    private static bool InitializeYaraLibrary()
    {
        if (_yaraInitialized)
            return true;

        lock (_initLock)
        {
            if (_yaraInitialized)
                return true;

            try
            {
                int result = YaraNative.yr_initialize();
                if (result != YaraNative.ERROR_SUCCESS)
                {
                    Log.Error("[YaraEngine] yr_initialize failed: {Error}", YaraNative.GetErrorMessage(result));
                    return false;
                }

                _yaraInitialized = true;
                Log.Debug("[YaraEngine] Native YARA library initialized");
                return true;
            }
            catch (DllNotFoundException ex)
            {
                Log.Warning("[YaraEngine] libyara64.dll not found: {Message}", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[YaraEngine] Failed to initialize native YARA");
                return false;
            }
        }
    }

    /// <summary>
    /// Load pre-compiled YARA rules
    /// </summary>
    private bool LoadCompiledRules(string compiledPath)
    {
        try
        {
            int result = YaraNative.yr_rules_load(compiledPath, out _compiledRules);
            if (result != YaraNative.ERROR_SUCCESS)
            {
                Log.Debug("[YaraEngine] Failed to load compiled rules: {Error}", YaraNative.GetErrorMessage(result));
                return false;
            }

            // Count rules by parsing the source file
            _ruleCount = CountRulesInFile(_rulesPath);
            Log.Debug("[YaraEngine] Loaded compiled rules from {Path}", compiledPath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[YaraEngine] Error loading compiled rules");
            return false;
        }
    }

    /// <summary>
    /// Save compiled rules to file
    /// </summary>
    private void SaveCompiledRules(string compiledPath)
    {
        if (_compiledRules == IntPtr.Zero)
            return;

        try
        {
            int result = YaraNative.yr_rules_save(_compiledRules, compiledPath);
            if (result == YaraNative.ERROR_SUCCESS)
            {
                Log.Debug("[YaraEngine] Saved compiled rules to {Path}", compiledPath);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[YaraEngine] Error saving compiled rules");
        }
    }

    /// <summary>
    /// Compile YARA rules from source file
    /// </summary>
    private bool CompileRules(string rulesPath)
    {
        IntPtr compiler = IntPtr.Zero;
        _compilationErrors.Clear();

        try
        {
            // Create compiler
            int result = YaraNative.yr_compiler_create(out compiler);
            if (result != YaraNative.ERROR_SUCCESS)
            {
                Log.Error("[YaraEngine] yr_compiler_create failed: {Error}", YaraNative.GetErrorMessage(result));
                return false;
            }

            // Set error callback
            YaraNative.YR_COMPILER_CALLBACK_FUNC errorCallback = CompilerErrorCallback;
            YaraNative.yr_compiler_set_callback(compiler, errorCallback, IntPtr.Zero);

            // Read and compile rules
            string rulesContent = File.ReadAllText(rulesPath);
            int errors = YaraNative.yr_compiler_add_string(compiler, rulesContent, null);

            if (errors > 0)
            {
                Log.Warning("[YaraEngine] Compilation had {Count} errors", errors);
                foreach (var error in _compilationErrors)
                {
                    Log.Debug("[YaraEngine] {Error}", error);
                }
                // Continue anyway - some rules may have compiled successfully
            }

            // Get compiled rules
            result = YaraNative.yr_compiler_get_rules(compiler, out _compiledRules);
            if (result != YaraNative.ERROR_SUCCESS)
            {
                Log.Error("[YaraEngine] yr_compiler_get_rules failed: {Error}", YaraNative.GetErrorMessage(result));
                return false;
            }

            _ruleCount = CountRulesInFile(rulesPath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[YaraEngine] Error compiling rules");
            return false;
        }
        finally
        {
            if (compiler != IntPtr.Zero)
            {
                YaraNative.yr_compiler_destroy(compiler);
            }
        }
    }

    /// <summary>
    /// Compiler error callback
    /// </summary>
    private void CompilerErrorCallback(int errorLevel, string fileName, int lineNumber, IntPtr rule, string message, IntPtr userData)
    {
        string levelStr = errorLevel == 0 ? "ERROR" : "WARNING";
        string errorMsg = $"{levelStr}: Line {lineNumber}: {message}";
        _compilationErrors.Add(errorMsg);
        
        if (errorLevel == 0) // Error
        {
            Log.Debug("[YaraEngine] Compile error at line {Line}: {Message}", lineNumber, message);
        }
    }

    /// <summary>
    /// Count the number of YARA rules in a file
    /// </summary>
    private uint CountRulesInFile(string rulesPath)
    {
        try
        {
            if (!File.Exists(rulesPath))
                return 0;
                
            var content = File.ReadAllText(rulesPath);
            var rulePattern = new Regex(@"^\s*rule\s+\w+", RegexOptions.Multiline);
            var matches = rulePattern.Matches(content);
            return (uint)matches.Count;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[YaraEngine] Error counting rules in {Path}", rulesPath);
            return 0;
        }
    }

    /// <summary>
    /// Scan a file for YARA rule matches
    /// </summary>
    public ScanResult ScanFile(string filePath)
    {
        var result = new ScanResult();

        if (!_initialized || _compiledRules == IntPtr.Zero)
        {
            return result;
        }

        if (!File.Exists(filePath))
        {
            return result;
        }

        try
        {
            lock (_scanLock)
            {
                var matchedRules = new List<string>();
                
                // Create callback to capture matches
                YaraNative.YR_CALLBACK_FUNC callback = (context, message, messageData, userData) =>
                {
                    if (message == YaraNative.CALLBACK_MSG_RULE_MATCHING)
                    {
                        try
                        {
                            // messageData points to YR_RULE structure
                            var rule = Marshal.PtrToStructure<YaraNative.YR_RULE>(messageData);
                            if (rule.identifier != IntPtr.Zero)
                            {
                                string ruleName = Marshal.PtrToStringAnsi(rule.identifier) ?? "Unknown";
                                
                                // Filter out rules known to cause false positives
                                if (!IsHighFalsePositiveRule(ruleName))
                                {
                                    matchedRules.Add(ruleName);
                                }
                                else
                                {
                                    Log.Debug("[YaraEngine] Filtered high-FP rule: {Rule} for {Path}", ruleName, filePath);
                                }
                            }
                        }
                        catch
                        {
                            // Don't add unknown rules - they're likely errors
                        }
                    }
                    return YaraNative.CALLBACK_CONTINUE;
                };

                // Scan the file
                int scanResult = YaraNative.yr_rules_scan_file(
                    _compiledRules,
                    filePath,
                    0, // flags
                    callback,
                    IntPtr.Zero,
                    60 // timeout in seconds
                );

                if (scanResult != YaraNative.ERROR_SUCCESS && 
                    scanResult != YaraNative.ERROR_SCAN_TIMEOUT)
                {
                    Log.Debug("[YaraEngine] Scan error for {Path}: {Error}", 
                        filePath, YaraNative.GetErrorMessage(scanResult));
                }

                if (matchedRules.Count > 0)
                {
                    // Calculate confidence based on rule quality and match count
                    float confidence = CalculateConfidence(filePath, matchedRules);
                    
                    // Only report if confidence is above threshold
                    if (confidence >= 0.70f)
                    {
                        result.IsThreat = true;
                        result.ThreatName = matchedRules[0];
                        result.ThreatType = "yara";
                        result.DetectedBy = "YARA";
                        result.Confidence = confidence;

                        if (matchedRules.Count > 1)
                        {
                            result.ThreatName = $"{matchedRules[0]} (+{matchedRules.Count - 1} rules)";
                        }

                        Log.Warning("[YaraEngine] YARA detection: {Path} -> {Rules} (Confidence: {Conf:P0})",
                            filePath, string.Join(", ", matchedRules), confidence);
                    }
                    else
                    {
                        Log.Debug("[YaraEngine] Low confidence detection filtered: {Path} -> {Rules} (Confidence: {Conf:P0})",
                            filePath, string.Join(", ", matchedRules), confidence);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug("[YaraEngine] Scan error for {Path}: {Message}", filePath, ex.Message);
        }

        return result;
    }

    /// <summary>
    /// Check if a rule is known to cause high false positive rates
    /// </summary>
    private static bool IsHighFalsePositiveRule(string ruleName)
    {
        if (string.IsNullOrEmpty(ruleName))
            return true;

        var lowerName = ruleName.ToLowerInvariant();

        // Rules that commonly cause false positives on legitimate software
        var highFpPatterns = new[]
        {
            // Generic detection rules that match too broadly
            "generic_string",
            "suspicious_string",
            "packed_binary",
            "obfuscated_code",
            "entropy_check",
            "base64_content",
            "hex_string",
            "contains_pe",
            "test_rule",
            "demo_rule",
            "example_",
            
            // Generic/broad detection rules with high FP rates
            "win_trojan_generic",
            "win_backdoor_generic",
            "win_downloader_generic",
            
            // Installer/packer detection (legitimate software uses these)
            "nsis_installer",
            "inno_setup",
            "installshield",
            "nullsoft",
            "upx_packed", // Many legitimate apps use UPX
            "aspack",
            "pecompact",
        };

        foreach (var pattern in highFpPatterns)
        {
            if (lowerName.Contains(pattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Calculate confidence score based on matched rules and file characteristics
    /// </summary>
    private float CalculateConfidence(string filePath, List<string> matchedRules)
    {
        float baseConfidence = 0.70f;
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        // Increase confidence for multiple rule matches
        if (matchedRules.Count >= 3)
            baseConfidence += 0.15f;
        else if (matchedRules.Count >= 2)
            baseConfidence += 0.08f;

        // Increase confidence for executable files
        var executableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".sys", ".scr", ".com", ".bat", ".cmd", ".ps1",
            ".vbs", ".js", ".wsf", ".msi", ".jar"
        };

        if (executableExtensions.Contains(extension))
        {
            baseConfidence += 0.10f;
        }

        // Decrease confidence for media/document files (more FP prone)
        var lowRiskExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp",
            ".mp3", ".mp4", ".avi", ".mkv", ".wav", ".flac",
            ".txt", ".md", ".json", ".xml", ".csv", ".log",
            ".ttf", ".otf", ".woff", ".woff2",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"
        };

        if (lowRiskExtensions.Contains(extension))
        {
            baseConfidence -= 0.25f;
        }

        // Check for high-confidence rule name patterns
        foreach (var ruleName in matchedRules)
        {
            var lowerRule = ruleName.ToLowerInvariant();
            
            // High confidence patterns (known malware families)
            if (lowerRule.Contains("ransomware") || 
                lowerRule.Contains("cryptolocker") ||
                lowerRule.Contains("emotet") ||
                lowerRule.Contains("trickbot") ||
                lowerRule.Contains("cobalt") ||
                lowerRule.Contains("mimikatz"))
            {
                baseConfidence += 0.15f;
                break;
            }

            // Medium confidence patterns
            if (lowerRule.Contains("trojan") ||
                lowerRule.Contains("backdoor") ||
                lowerRule.Contains("keylogger") ||
                lowerRule.Contains("stealer"))
            {
                baseConfidence += 0.08f;
            }
        }

        // Clamp to valid range
        return Math.Clamp(baseConfidence, 0.0f, 1.0f);
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            if (_compiledRules != IntPtr.Zero)
            {
                YaraNative.yr_rules_destroy(_compiledRules);
                _compiledRules = IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[YaraEngine] Error during disposal");
        }

        _initialized = false;
        _disposed = true;
        Log.Debug("[YaraEngine] Disposed");
    }

    /// <summary>
    /// Finalize YARA library (call at application shutdown)
    /// </summary>
    public static void FinalizeYara()
    {
        if (_yaraInitialized)
        {
            try
            {
                YaraNative.yr_finalize();
                _yaraInitialized = false;
                Log.Debug("[YaraEngine] Native YARA library finalized");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[YaraEngine] Error finalizing YARA");
            }
        }
    }
}
