using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Serilog;

namespace AfterlifeWinUI.Services;

/// <summary>
/// AI-based malware detection engine using ONNX models.
/// Uses ensemble of LightGBM models trained on PE file features.
/// </summary>
public class AIDetectionEngine : IDisposable
{
    private readonly PEFeatureExtractor _featureExtractor;
    private readonly List<(string Name, InferenceSession Session, ModelType Type)> _models = new();
    private bool _initialized;
    private bool _disposed;
    private readonly object _lock = new();

    /// <summary>
    /// Model types
    /// </summary>
    public enum ModelType
    {
        Histogram,  // Uses byte histogram + entropy features (512 features)
        String      // Uses string-based features (243 features)
    }

    public bool IsInitialized => _initialized;
    public int ModelCount => _models.Count;
    public bool CanScan => _initialized && _models.Count > 0;

    public AIDetectionEngine()
    {
        _featureExtractor = new PEFeatureExtractor();
    }

    /// <summary>
    /// Initialize the AI engine by loading ONNX models
    /// </summary>
    public bool Initialize(string? modelsPath = null)
    {
        if (_initialized)
        {
            Log.Warning("[AIEngine] Already initialized");
            return true;
        }

        try
        {
            // Find models directory
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] searchPaths = modelsPath != null
                ? new[] { modelsPath }
                : new[]
                {
                    Path.Combine(baseDir, "resources", "ai_models"),
                    Path.Combine(baseDir, "..", "..", "..", "..", "resources", "ai_models"),
                    Path.Combine(baseDir, "..", "..", "..", "resources", "ai_models"),
                };

            string? foundPath = null;
            foreach (var path in searchPaths)
            {
                var normalizedPath = Path.GetFullPath(path);
                if (Directory.Exists(normalizedPath))
                {
                    var onnxFiles = Directory.GetFiles(normalizedPath, "*.onnx");
                    if (onnxFiles.Length > 0)
                    {
                        foundPath = normalizedPath;
                        Log.Information("[AIEngine] Found models at: {Path}", normalizedPath);
                        break;
                    }
                }
            }

            if (foundPath == null)
            {
                Log.Warning("[AIEngine] No ONNX models found");
                _initialized = true;
                return true;
            }

            // Load all ONNX models
            var sessionOptions = new SessionOptions();
            sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            
            // Use CPU execution provider (works everywhere)
            // For GPU acceleration, you could add CUDA or DirectML providers

            foreach (var modelFile in Directory.GetFiles(foundPath, "*.onnx"))
            {
                try
                {
                    var modelName = Path.GetFileNameWithoutExtension(modelFile);
                    var session = new InferenceSession(modelFile, sessionOptions);
                    
                    // Determine model type from name
                    ModelType modelType = modelName.Contains("histogram", StringComparison.OrdinalIgnoreCase)
                        ? ModelType.Histogram
                        : ModelType.String;

                    _models.Add((modelName, session, modelType));
                    Log.Debug("[AIEngine] Loaded model: {Name} (Type: {Type})", modelName, modelType);
                }
                catch (Exception ex)
                {
                    Log.Warning("[AIEngine] Failed to load model {Path}: {Message}", modelFile, ex.Message);
                }
            }

            _initialized = true;
            Log.Information("[AIEngine] Initialized with {Count} models ({Histogram} histogram, {String} string)",
                _models.Count,
                _models.Count(m => m.Type == ModelType.Histogram),
                _models.Count(m => m.Type == ModelType.String));

            return _models.Count > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AIEngine] Failed to initialize");
            _initialized = true;
            return false;
        }
    }

    /// <summary>
    /// Scan a file using AI detection.
    /// Only scans Win32 PE files (EXE, DLL, SYS) as the models were trained on PE features.
    /// </summary>
    public ScanResult ScanFile(string filePath)
    {
        var result = new ScanResult();

        if (!CanScan)
            return result;

        if (!File.Exists(filePath))
            return result;

        try
        {
            // Only scan Win32 PE files - models were trained exclusively on PE file features
            // Check extension first for quick filtering
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var peExtensions = new HashSet<string> { ".exe", ".dll", ".sys", ".scr", ".drv", ".ocx", ".cpl" };
            
            // Always verify PE signature regardless of extension (to handle renamed files)
            if (!IsValidPEFile(filePath))
            {
                // Not a PE file - skip AI detection (would cause false positives)
                return result;
            }
            
            // Skip if extension suggests non-executable even if has MZ header (installers, archives)
            var skipExtensions = new HashSet<string> { ".msi", ".msp", ".msu", ".cab" };
            if (skipExtensions.Contains(extension))
            {
                return result;
            }

            // Extract features
            var features = _featureExtractor.ExtractFeatures(filePath);
            if (features == null || !features.IsPE)
            {
                // Feature extraction failed or not a valid PE
                return result;
            }

            // Run prediction
            var prediction = Predict(features);
            
            // Apply trust heuristics to reduce false positives on legitimate software
            bool isTrusted = ApplyTrustHeuristics(features, prediction);
            if (isTrusted)
            {
                Log.Debug("[AIEngine] Trusted (signed/installer): {Path} | Score: {Score:F4}", 
                    filePath, prediction.AverageScore);
                return result;
            }
            
            if (prediction.IsMalware)
            {
                result.IsThreat = true;
                result.ThreatName = DetermineThreatName(prediction, features);
                result.ThreatType = "ai";
                result.DetectedBy = "AI";
                result.Confidence = prediction.Confidence;

                Log.Warning("[AIEngine] AI detection: {Path} | Score: {Score:F4} | Confidence: {Conf:P0}",
                    filePath, prediction.AverageScore, prediction.Confidence);
            }
            else
            {
                Log.Debug("[AIEngine] Clean: {Path} | Score: {Score:F4}", filePath, prediction.AverageScore);
            }
        }
        catch (Exception ex)
        {
            Log.Debug("[AIEngine] Scan error for {Path}: {Message}", filePath, ex.Message);
        }

        return result;
    }

    /// <summary>
    /// Run prediction using ensemble of models
    /// </summary>
    private AIPrediction Predict(PEFeatures features)
    {
        var scores = new List<float>();
        var histogramScores = new List<float>();
        var stringScores = new List<float>();

        lock (_lock)
        {
            foreach (var (name, session, modelType) in _models)
            {
                try
                {
                    float[] inputFeatures = modelType == ModelType.Histogram
                        ? features.HistogramFeatures
                        : features.StringFeatures;

                    // Validate feature count
                    var inputMeta = session.InputMetadata;
                    var inputName = inputMeta.Keys.First();
                    var expectedShape = inputMeta[inputName].Dimensions;
                    int expectedFeatures = expectedShape.Length > 1 ? expectedShape[1] : expectedShape[0];

                    // Pad or truncate features if needed
                    if (inputFeatures.Length != expectedFeatures)
                    {
                        var adjusted = new float[expectedFeatures];
                        Array.Copy(inputFeatures, adjusted, Math.Min(inputFeatures.Length, expectedFeatures));
                        inputFeatures = adjusted;
                    }

                    // Create input tensor
                    var inputTensor = new DenseTensor<float>(inputFeatures, new[] { 1, inputFeatures.Length });
                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
                    };

                    // Run inference
                    using var results = session.Run(inputs);
                    
                    // LightGBM ONNX models output:
                    // - Output 0 (label): int64 tensor with predicted class [0 or 1]
                    // - Output 1 (probabilities): sequence of maps {class_id -> probability}
                    float score = 0;
                    bool gotScore = false;

                    // Log output names and types for debugging
                    var outputNames = string.Join(", ", results.Select(r => $"{r.Name}:{r.Value?.GetType().Name}"));
                    Log.Verbose("[AIEngine] {Model} outputs: {Outputs}", name, outputNames);

                    // Try to get probabilities from the second output (preferred)
                    if (results.Count > 1)
                    {
                        var probOutput = results.ElementAt(1);
                        var probValue = probOutput.Value;

                        // The ONNX Runtime returns sequences as DisposableList<DisposableNamedOnnxValue>
                        // which wraps the map entries. We need to extract probabilities properly.
                        
                        // Method 1: Direct cast to known types
                        if (probValue is IList<IDictionary<long, float>> probListLong && probListLong.Count > 0)
                        {
                            if (probListLong[0].TryGetValue(1L, out float prob))
                            {
                                score = prob;
                                gotScore = true;
                            }
                        }
                        // Method 2: IEnumerable of dictionaries
                        else if (probValue is IEnumerable<IDictionary<long, float>> probEnumLong)
                        {
                            var first = probEnumLong.FirstOrDefault();
                            if (first != null && first.TryGetValue(1L, out float prob))
                            {
                                score = prob;
                                gotScore = true;
                            }
                        }
                        // Method 3: DisposableList containing DisposableNamedOnnxValue (maps)
                        // The sequence output type is seq(map(int64,tensor(float)))
                        else if (probValue is System.Collections.IList list && list.Count > 0)
                        {
                            // Each item in the list is a DisposableNamedOnnxValue containing a map
                            var firstItem = list[0];
                            if (firstItem is DisposableNamedOnnxValue mapOnnxValue)
                            {
                                // The map is stored as the Value, try to extract
                                if (mapOnnxValue.Value is IDictionary<long, float> mapDict)
                                {
                                    if (mapDict.TryGetValue(1L, out float prob))
                                    {
                                        score = prob;
                                        gotScore = true;
                                    }
                                }
                                // Try AsEnumerable for map type
                                else if (mapOnnxValue.ValueType == OnnxValueType.ONNX_TYPE_MAP)
                                {
                                    try
                                    {
                                        var mapEntries = mapOnnxValue.AsEnumerable<KeyValuePair<long, float>>();
                                        foreach (var kvp in mapEntries)
                                        {
                                            if (kvp.Key == 1L)
                                            {
                                                score = kvp.Value;
                                                gotScore = true;
                                                break;
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                            // Try direct dictionary access
                            else if (firstItem is IDictionary<long, float> directDict)
                            {
                                if (directDict.TryGetValue(1L, out float prob))
                                {
                                    score = prob;
                                    gotScore = true;
                                }
                            }
                        }
                        // Method 4: Try AsEnumerable on NamedOnnxValue for sequence types
                        else if (probOutput.ValueType == OnnxValueType.ONNX_TYPE_SEQUENCE)
                        {
                            try
                            {
                                // For sequence of maps, iterate through the sequence
                                var sequence = probOutput.AsEnumerable<NamedOnnxValue>();
                                foreach (var mapValue in sequence)
                                {
                                    // Each item in sequence is a map
                                    if (mapValue.Value is IDictionary<long, float> mapDict)
                                    {
                                        if (mapDict.TryGetValue(1L, out float prob))
                                        {
                                            score = prob;
                                            gotScore = true;
                                            break;
                                        }
                                    }
                                    // Try as DisposableNamedOnnxValue containing the map
                                    else if (mapValue.ValueType == OnnxValueType.ONNX_TYPE_MAP)
                                    {
                                        var mapEnumerable = mapValue.AsEnumerable<KeyValuePair<long, float>>();
                                        foreach (var kvp in mapEnumerable)
                                        {
                                            if (kvp.Key == 1L)
                                            {
                                                score = kvp.Value;
                                                gotScore = true;
                                                break;
                                            }
                                        }
                                        if (gotScore) break;
                                    }
                                }
                            }
                            catch (Exception seqEx)
                            {
                                Log.Debug("[AIEngine] {Model} - sequence extraction failed: {Error}", name, seqEx.Message);
                            }
                        }
                        // Method 5: Try generic enumerable as last resort
                        else if (probValue is System.Collections.IEnumerable enumerable)
                        {
                            foreach (var item in enumerable)
                            {
                                if (item is IDictionary<long, float> dict && dict.TryGetValue(1L, out float prob))
                                {
                                    score = prob;
                                    gotScore = true;
                                    break;
                                }
                                // Try dynamic access
                                else if (item != null)
                                {
                                    try
                                    {
                                        dynamic dynItem = item;
                                        if (dynItem.ContainsKey(1L))
                                        {
                                            score = (float)dynItem[1L];
                                            gotScore = true;
                                            Log.Verbose("[AIEngine] {Model} - got prob via dynamic: {Score:F4}", name, score);
                                            break;
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                        
                        if (!gotScore)
                        {
                            Log.Debug("[AIEngine] {Model} - could not extract probability, type: {Type}, valueType: {VType}",
                                name, probValue?.GetType().FullName ?? "null", probOutput.ValueType);
                        }
                    }

                    // Fallback: use the label output (less precise)
                    if (!gotScore && results.Count > 0)
                    {
                        var labelOutput = results.First();
                        if (labelOutput.Value is DenseTensor<long> labelTensor)
                        {
                            // Label 1 = malware, convert to probability-like score
                            score = labelTensor[0] == 1 ? 0.75f : 0.25f;
                            gotScore = true;
                            Log.Debug("[AIEngine] Using label fallback for {Name}: label={Label}", name, labelTensor[0]);
                        }
                        else if (labelOutput.Value is long[] labelArray && labelArray.Length > 0)
                        {
                            score = labelArray[0] == 1 ? 0.75f : 0.25f;
                            gotScore = true;
                            Log.Debug("[AIEngine] Using label array fallback for {Name}: label={Label}", name, labelArray[0]);
                        }
                    }

                    if (gotScore)
                    {
                        scores.Add(score);
                        if (modelType == ModelType.Histogram)
                            histogramScores.Add(score);
                        else
                            stringScores.Add(score);
                        
                        Log.Verbose("[AIEngine] Model {Name} score: {Score:F4}", name, score);
                    }
                    else
                    {
                        Log.Warning("[AIEngine] Model {Name} - could not extract score", name);
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug("[AIEngine] Model {Name} inference error: {Message}", name, ex.Message);
                }
            }
        }

        if (scores.Count == 0)
        {
            return new AIPrediction { AverageScore = 0, Confidence = 0, IsMalware = false };
        }

        // Calculate ensemble score
        float avgScore = scores.Average();
        float histogramAvg = histogramScores.Count > 0 ? histogramScores.Average() : 0;
        float stringAvg = stringScores.Count > 0 ? stringScores.Average() : 0;

        // Calculate confidence based on model agreement
        float stdDev = (float)Math.Sqrt(scores.Average(s => Math.Pow(s - avgScore, 2)));
        float agreement = 1.0f - Math.Min(stdDev * 2, 0.5f); // Higher agreement = lower std dev

        // Log detailed scores for debugging
        Log.Debug("[AIEngine] Ensemble scores - Avg: {Avg:F4}, Histogram: {Hist:F4}, String: {Str:F4}, StdDev: {Std:F4}, Agreement: {Agr:F4}",
            avgScore, histogramAvg, stringAvg, stdDev, agreement);

        // Determine if malware based on threshold
        // Threshold calibration: balance between false positives and false negatives
        bool isMalware;
        float confidence;

        if (avgScore >= 0.70f)
        {
            // High score - definitely malware
            isMalware = true;
            confidence = Math.Min(avgScore * agreement, 0.99f);
        }
        else if (avgScore >= 0.55f)
        {
            // Medium-high - likely malware
            float maxScore = scores.Max();
            isMalware = maxScore >= 0.65f || agreement >= 0.6f;
            confidence = avgScore * agreement;
        }
        else if (avgScore >= 0.45f)
        {
            // Suspicious zone - need strong indicators
            float maxScore = scores.Max();
            // Flag if max score is high (at least one model very confident)
            // OR if both histogram and string models agree it's suspicious
            isMalware = maxScore >= 0.70f || (histogramAvg >= 0.5f && stringAvg >= 0.45f);
            confidence = avgScore * agreement * 0.85f; // Slightly reduce confidence
        }
        else
        {
            // Below threshold - clean
            isMalware = false;
            confidence = (1 - avgScore) * agreement;
        }

        return new AIPrediction
        {
            AverageScore = avgScore,
            HistogramScore = histogramAvg,
            StringScore = stringAvg,
            Confidence = confidence,
            IsMalware = isMalware,
            ModelAgreement = agreement
        };
    }

    /// <summary>
    /// Determine a descriptive threat name based on features
    /// </summary>
    private string DetermineThreatName(AIPrediction prediction, PEFeatures features)
    {
        var parts = new List<string>();

        // Base name
        if (prediction.AverageScore >= 0.9f)
            parts.Add("Malware.AI.High");
        else if (prediction.AverageScore >= 0.7f)
            parts.Add("Malware.AI.Medium");
        else
            parts.Add("Malware.AI.Suspicious");

        // Add characteristics
        if (features.Entropy > 7.0f)
            parts.Add("Packed");
        
        if (!features.IsPE)
            parts.Add("NonPE");

        // Score indicator
        parts.Add($"({prediction.AverageScore:F2})");

        return string.Join(".", parts);
    }

    /// <summary>
    /// Check if file is a valid Win32 PE (MZ + PE signature)
    /// </summary>
    private static bool IsValidPEFile(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (fs.Length < 64)
                return false;

            // Check MZ signature
            byte[] dosHeader = new byte[64];
            if (fs.Read(dosHeader, 0, 64) < 64)
                return false;

            if (dosHeader[0] != 0x4D || dosHeader[1] != 0x5A) // "MZ"
                return false;

            // Get PE header offset from DOS header (at offset 0x3C)
            int peOffset = BitConverter.ToInt32(dosHeader, 0x3C);
            if (peOffset < 0 || peOffset > fs.Length - 4)
                return false;

            // Check PE signature
            fs.Seek(peOffset, SeekOrigin.Begin);
            byte[] peSignature = new byte[4];
            if (fs.Read(peSignature, 0, 4) < 4)
                return false;

            // "PE\0\0"
            return peSignature[0] == 0x50 && peSignature[1] == 0x45 && 
                   peSignature[2] == 0x00 && peSignature[3] == 0x00;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Apply trust heuristics to reduce false positives on legitimate software
    /// Only applies when AI score is LOW - doesn't override suspicious detections
    /// </summary>
    private static bool ApplyTrustHeuristics(PEFeatures features, AIPrediction prediction)
    {
        // Never trust if score is in the suspicious range (0.4+)
        // Trust heuristics only apply to clearly clean files
        if (prediction.AverageScore >= 0.40f)
            return false;

        // Trust signed executables with very low scores
        if (features.IsSigned && prediction.AverageScore < 0.35f)
        {
            Log.Debug("[AIEngine] Trust: Signed executable with very low score");
            return true;
        }

        // Large files with many imports and low scores are usually legitimate
        if (features.FileSize > 10_000_000 && features.ImportCount > 100 && prediction.AverageScore < 0.30f)
        {
            Log.Debug("[AIEngine] Trust: Large file with many imports");
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            foreach (var (_, session, _) in _models)
            {
                try
                {
                    session.Dispose();
                }
                catch { }
            }
            _models.Clear();
        }

        _disposed = true;
        _initialized = false;
        Log.Debug("[AIEngine] Disposed");
    }
}

/// <summary>
/// AI prediction result
/// </summary>
public class AIPrediction
{
    public float AverageScore { get; set; }
    public float HistogramScore { get; set; }
    public float StringScore { get; set; }
    public float Confidence { get; set; }
    public float ModelAgreement { get; set; }
    public bool IsMalware { get; set; }
}
