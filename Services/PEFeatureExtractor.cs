using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;

namespace AfterlifeWinUI.Services;

/// <summary>
/// Extracts features from PE (Portable Executable) files for AI-based malware detection.
/// Implements byte histogram and string-based feature extraction matching the Python training pipeline.
/// </summary>
public class PEFeatureExtractor
{
    // String detection patterns for suspicious content
    private static readonly Dictionary<string, Regex> StringPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["url"] = new Regex(@"\b(?:http|https|ftp)://[a-zA-Z0-9\-._~:?#\[\]@!$&'()*+,;=]+", RegexOptions.Compiled),
        ["ipv4_addr"] = new Regex(@"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b", RegexOptions.Compiled),
        ["http://"] = new Regex(@"http://", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["https://"] = new Regex(@"https://", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["powershell"] = new Regex(@"powershell", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["cmd"] = new Regex(@"\bcmd\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["base64"] = new Regex(@"base64", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["crypt"] = new Regex(@"crypt", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["registry"] = new Regex(@"\b(?:HKEY_|HKLM|HKCU)", RegexOptions.Compiled),
        ["download"] = new Regex(@"download", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["connect"] = new Regex(@"connect", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["socket"] = new Regex(@"socket", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["process"] = new Regex(@"process", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["inject"] = new Regex(@"inject", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["password"] = new Regex(@"password", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["credential"] = new Regex(@"credential", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["shell"] = new Regex(@"shell", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["execute"] = new Regex(@"execute", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["admin"] = new Regex(@"admin", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["elevation"] = new Regex(@"elevat", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    };

    // String vocabulary mapping (must match training)
    private static readonly Dictionary<string, int> StringVocabulary = new()
    {
        [".click("] = 0, ["/EmbeddedFile"] = 1, ["/FlateDecode"] = 2, ["/URI"] = 3, ["/bin/"] = 4, ["/dev/"] = 5, ["/proc/"] = 6,
        ["/tmp/"] = 7, ["/usr/"] = 8, ["<script"] = 9, ["Invoke-Command"] = 10, ["Invoke-Expression"] = 11, ["base64"] = 12,
        ["base64string"] = 13, ["btc_wallet"] = 14, ["cache"] = 15, ["certificate"] = 16, ["clipboard"] = 17, ["command"] = 18,
        ["connect"] = 19, ["cookie"] = 20, ["create"] = 21, ["crypt"] = 22, ["debug"] = 23, ["decode"] = 24, ["delete"] = 25,
        ["desktop"] = 26, ["directory"] = 27, ["disk"] = 28, ["dos_msg"] = 29, ["download"] = 30, ["email_addr"] = 31,
        ["encode"] = 32, ["enum"] = 33, ["environment"] = 34, ["exit"] = 35, ["file"] = 36, ["file_path"] = 37, ["ftp"] = 38,
        ["get"] = 39, ["hidden"] = 40, ["hostname"] = 41, ["html"] = 42, ["http"] = 43, ["http://"] = 44, ["https://"] = 45,
        ["install"] = 46, ["internet"] = 47, ["ipv4_addr"] = 48, ["ipv6_addr"] = 49, ["javascript"] = 50, ["keyboard"] = 51,
        ["mac_addr"] = 52, ["memory"] = 53, ["module"] = 54, ["mutex"] = 55, ["onlick"] = 56, ["password"] = 57, ["post"] = 58,
        ["powershell"] = 59, ["privilege"] = 60, ["process"] = 61, ["registry_key"] = 62, ["remote"] = 63, ["resource"] = 64,
        ["security"] = 65, ["service"] = 66, ["shell"] = 67, ["snapshot"] = 68, ["system"] = 69, ["thread"] = 70, ["token"] = 71,
        ["url"] = 72, ["useragent"] = 73, ["wallet"] = 74
    };

    /// <summary>
    /// Extract histogram features (256 byte frequencies + 256 byte-entropy features = 512 total)
    /// </summary>
    public float[] ExtractHistogramFeatures(byte[] data)
    {
        var features = new float[512];

        if (data == null || data.Length == 0)
            return features;

        // Part 1: Byte histogram (256 features) - normalized frequency of each byte value
        var histogram = new int[256];
        foreach (byte b in data)
        {
            histogram[b]++;
        }

        float total = data.Length;
        for (int i = 0; i < 256; i++)
        {
            features[i] = histogram[i] / total;
        }

        // Part 2: Byte-entropy histogram (256 features)
        // This computes a 2D histogram of (entropy_bin, byte_high_nibble)
        var byteEntropyHist = ComputeByteEntropyHistogram(data);
        for (int i = 0; i < 256; i++)
        {
            features[256 + i] = byteEntropyHist[i];
        }

        return features;
    }

    /// <summary>
    /// Compute byte-entropy histogram (16x16 = 256 features)
    /// Uses sliding window entropy calculation
    /// </summary>
    private float[] ComputeByteEntropyHistogram(byte[] data, int windowSize = 2048, int stepSize = 1024)
    {
        var output = new int[16, 16]; // [entropy_bin, byte_high_nibble]

        if (data.Length < windowSize)
        {
            // For small files, compute entropy once
            int entropyBin = ComputeEntropyBin(data);
            var nibbleCounts = new int[16];
            foreach (byte b in data)
            {
                nibbleCounts[b >> 4]++;
            }
            for (int i = 0; i < 16; i++)
            {
                output[entropyBin, i] += nibbleCounts[i];
            }
        }
        else
        {
            // Sliding window
            for (int pos = 0; pos <= data.Length - windowSize; pos += stepSize)
            {
                var window = new ArraySegment<byte>(data, pos, windowSize);
                int entropyBin = ComputeEntropyBin(window);
                
                var nibbleCounts = new int[16];
                foreach (byte b in window)
                {
                    nibbleCounts[b >> 4]++;
                }
                for (int i = 0; i < 16; i++)
                {
                    output[entropyBin, i] += nibbleCounts[i];
                }
            }
        }

        // Flatten and normalize
        var result = new float[256];
        float sum = 0;
        for (int i = 0; i < 16; i++)
        {
            for (int j = 0; j < 16; j++)
            {
                result[i * 16 + j] = output[i, j];
                sum += output[i, j];
            }
        }

        if (sum > 0)
        {
            for (int i = 0; i < 256; i++)
            {
                result[i] /= sum;
            }
        }

        return result;
    }

    /// <summary>
    /// Compute entropy bin (0-15) for a data block
    /// </summary>
    private int ComputeEntropyBin(IEnumerable<byte> data)
    {
        var counts = new int[16];
        int total = 0;
        
        foreach (byte b in data)
        {
            counts[b >> 4]++;
            total++;
        }

        if (total == 0) return 0;

        double entropy = 0;
        foreach (int count in counts)
        {
            if (count > 0)
            {
                double p = (double)count / total;
                entropy -= p * Math.Log2(p);
            }
        }

        // Scale entropy (0-4 bits for 16 symbols) to 0-15 bins
        // Multiply by 2 to spread across bins, then multiply by 2 again for 16 bins
        int bin = (int)(entropy * 2 * 2);
        return Math.Clamp(bin, 0, 15);
    }

    /// <summary>
    /// Extract string-based features (243 features total)
    /// Includes: string statistics + printable distribution + string pattern counts
    /// </summary>
    public float[] ExtractStringFeatures(byte[] data)
    {
        // Feature layout:
        // 0: numstrings
        // 1: avlength
        // 2: printables
        // 3: entropy
        // 4-99: printabledist (96 features)
        // 100-174: string_counts (75 features based on vocabulary)
        // 175-242: reserved/padding for model alignment
        
        var features = new float[243];

        if (data == null || data.Length == 0)
            return features;

        // Extract printable ASCII strings (5+ chars)
        var strings = ExtractStrings(data, minLength: 5);

        if (strings.Count == 0)
            return features;

        // String statistics
        features[0] = strings.Count; // numstrings
        features[1] = (float)strings.Average(s => s.Length); // avlength
        
        // Count printable characters
        int printables = strings.Sum(s => s.Length);
        features[2] = printables;

        // String entropy
        var allChars = string.Join("", strings);
        features[3] = (float)ComputeStringEntropy(allChars);

        // Printable character distribution (96 features: ASCII 32-127)
        var charCounts = new int[96];
        foreach (char c in allChars)
        {
            int idx = c - 32;
            if (idx >= 0 && idx < 96)
            {
                charCounts[idx]++;
            }
        }
        
        float charTotal = charCounts.Sum();
        for (int i = 0; i < 96; i++)
        {
            features[4 + i] = charTotal > 0 ? charCounts[i] / charTotal : 0;
        }

        // String pattern counts (vocabulary-based)
        var patternCounts = CountStringPatterns(strings);
        foreach (var kvp in StringVocabulary)
        {
            int featureIdx = 100 + kvp.Value;
            if (featureIdx < 175 && patternCounts.TryGetValue(kvp.Key, out int count))
            {
                features[featureIdx] = count;
            }
        }

        return features;
    }

    /// <summary>
    /// Extract printable ASCII strings from binary data
    /// </summary>
    private List<string> ExtractStrings(byte[] data, int minLength = 5)
    {
        var strings = new List<string>();
        var current = new StringBuilder();

        foreach (byte b in data)
        {
            if (b >= 0x20 && b <= 0x7E) // Printable ASCII
            {
                current.Append((char)b);
            }
            else
            {
                if (current.Length >= minLength)
                {
                    strings.Add(current.ToString());
                }
                current.Clear();
            }
        }

        if (current.Length >= minLength)
        {
            strings.Add(current.ToString());
        }

        return strings;
    }

    /// <summary>
    /// Compute Shannon entropy of a string
    /// </summary>
    private double ComputeStringEntropy(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;

        var freq = new Dictionary<char, int>();
        foreach (char c in s)
        {
            freq.TryGetValue(c, out int count);
            freq[c] = count + 1;
        }

        double entropy = 0;
        double len = s.Length;
        foreach (int count in freq.Values)
        {
            double p = count / len;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }

    /// <summary>
    /// Count occurrences of suspicious string patterns
    /// </summary>
    private Dictionary<string, int> CountStringPatterns(List<string> strings)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var combined = string.Join("\n", strings);

        // Check each vocabulary term
        foreach (var term in StringVocabulary.Keys)
        {
            int count = 0;
            
            // Simple substring search for most terms
            int idx = 0;
            while ((idx = combined.IndexOf(term, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                idx += term.Length;
            }
            
            if (count > 0)
            {
                counts[term] = count;
            }
        }

        // Additional regex-based pattern matching
        foreach (var kvp in StringPatterns)
        {
            var matches = kvp.Value.Matches(combined);
            if (matches.Count > 0)
            {
                counts.TryGetValue(kvp.Key, out int existing);
                counts[kvp.Key] = existing + matches.Count;
            }
        }

        return counts;
    }

    /// <summary>
    /// Check if a file is a valid PE (Portable Executable)
    /// </summary>
    public bool IsPEFile(byte[] data)
    {
        if (data == null || data.Length < 64)
            return false;

        // Check DOS header magic "MZ"
        if (data[0] != 0x4D || data[1] != 0x5A)
            return false;

        // Get PE header offset from DOS header (at offset 0x3C)
        int peOffset = BitConverter.ToInt32(data, 0x3C);
        
        if (peOffset < 0 || peOffset + 4 > data.Length)
            return false;

        // Check PE signature "PE\0\0"
        return data[peOffset] == 0x50 && 
               data[peOffset + 1] == 0x45 && 
               data[peOffset + 2] == 0x00 && 
               data[peOffset + 3] == 0x00;
    }

    /// <summary>
    /// Extract all features for a file
    /// </summary>
    public PEFeatures? ExtractFeatures(string filePath)
    {
        try
        {
            var data = File.ReadAllBytes(filePath);
            return ExtractFeatures(data);
        }
        catch (Exception ex)
        {
            Log.Debug("[PEFeatureExtractor] Error reading file {Path}: {Message}", filePath, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Extract all features from binary data
    /// </summary>
    public PEFeatures? ExtractFeatures(byte[] data)
    {
        if (data == null || data.Length == 0)
            return null;

        // For histogram models, we can process any file
        // For string features, PE files work best but we can still extract strings from any file
        
        var histogramFeatures = ExtractHistogramFeatures(data);
        var stringFeatures = ExtractStringFeatures(data);
        var isPE = IsPEFile(data);

        // Extract additional PE metadata if it's a valid PE
        bool isSigned = false;
        int sectionCount = 0;
        int importCount = 0;

        if (isPE)
        {
            ExtractPEMetadata(data, out isSigned, out sectionCount, out importCount);
        }

        return new PEFeatures
        {
            HistogramFeatures = histogramFeatures,
            StringFeatures = stringFeatures,
            IsPE = isPE,
            FileSize = data.Length,
            Entropy = ComputeFileEntropy(data),
            IsSigned = isSigned,
            SectionCount = sectionCount,
            ImportCount = importCount
        };
    }

    /// <summary>
    /// Extract PE metadata (signature status, section count, import count)
    /// </summary>
    private void ExtractPEMetadata(byte[] data, out bool isSigned, out int sectionCount, out int importCount)
    {
        isSigned = false;
        sectionCount = 0;
        importCount = 0;

        try
        {
            if (data.Length < 64)
                return;

            // Get PE header offset
            int peOffset = BitConverter.ToInt32(data, 0x3C);
            if (peOffset < 0 || peOffset + 24 > data.Length)
                return;

            // Validate PE signature
            if (data[peOffset] != 0x50 || data[peOffset + 1] != 0x45)
                return;

            // Skip PE signature (4 bytes) to COFF header
            int coffOffset = peOffset + 4;

            // Machine type is at offset 0, number of sections at offset 2
            if (coffOffset + 20 > data.Length)
                return;
                
            sectionCount = BitConverter.ToUInt16(data, coffOffset + 2);

            // Size of optional header at offset 16
            int optionalHeaderSize = BitConverter.ToUInt16(data, coffOffset + 16);
            
            if (optionalHeaderSize == 0)
                return;
            
            // Optional header starts after COFF header (20 bytes)
            int optionalHeaderOffset = coffOffset + 20;
            
            if (optionalHeaderOffset + optionalHeaderSize > data.Length)
                return;

            // Check if PE32 or PE32+ (first 2 bytes of optional header)
            ushort magic = BitConverter.ToUInt16(data, optionalHeaderOffset);
            bool isPE32Plus = magic == 0x20b;
            bool isPE32 = magic == 0x10b;
            
            if (!isPE32 && !isPE32Plus)
                return;

            // Number of data directories is at offset 92 (PE32) or 108 (PE32+)
            int numDirsOffset = optionalHeaderOffset + (isPE32Plus ? 108 : 92);
            if (numDirsOffset + 4 > data.Length)
                return;
                
            uint numDataDirs = BitConverter.ToUInt32(data, numDirsOffset);
            if (numDataDirs > 16)  // Sanity check
                numDataDirs = 16;

            // Data directories start at different offsets:
            // PE32: offset 96 in optional header
            // PE32+: offset 112 in optional header
            int dataDirectoryOffset = optionalHeaderOffset + (isPE32Plus ? 112 : 96);
            
            // Security directory is the 5th entry (index 4), each entry is 8 bytes
            if (numDataDirs > 4)
            {
                int securityDirOffset = dataDirectoryOffset + (4 * 8);
                
                if (securityDirOffset + 8 <= data.Length)
                {
                    uint securityRVA = BitConverter.ToUInt32(data, securityDirOffset);
                    uint securitySize = BitConverter.ToUInt32(data, securityDirOffset + 4);
                    // Only consider signed if security data actually exists at valid location
                    isSigned = securitySize > 0 && securityRVA > 0 && securityRVA + securitySize <= data.Length;
                }
            }

            // Import directory is the 2nd entry (index 1)
            if (numDataDirs > 1)
            {
                int importDirOffset = dataDirectoryOffset + (1 * 8);
                
                if (importDirOffset + 8 <= data.Length)
                {
                    uint importRVA = BitConverter.ToUInt32(data, importDirOffset);
                    uint importSize = BitConverter.ToUInt32(data, importDirOffset + 4);
                    
                    // Rough estimate: each import descriptor is 20 bytes
                    if (importSize > 0 && importSize < 0x100000)  // Sanity check
                    {
                        importCount = (int)(importSize / 20);
                    }
                }
            }
        }
        catch
        {
            // Ignore parsing errors - just use default values
        }
    }

    /// <summary>
    /// Compute overall file entropy
    /// </summary>
    private float ComputeFileEntropy(byte[] data)
    {
        if (data == null || data.Length == 0)
            return 0;

        var counts = new int[256];
        foreach (byte b in data)
        {
            counts[b]++;
        }

        double entropy = 0;
        double len = data.Length;
        foreach (int count in counts)
        {
            if (count > 0)
            {
                double p = count / len;
                entropy -= p * Math.Log2(p);
            }
        }

        return (float)entropy;
    }
}

/// <summary>
/// Container for extracted PE features
/// </summary>
public class PEFeatures
{
    /// <summary>
    /// Histogram features (512 values): byte histogram + byte-entropy histogram
    /// </summary>
    public float[] HistogramFeatures { get; set; } = Array.Empty<float>();

    /// <summary>
    /// String features (243 values): string statistics + patterns
    /// </summary>
    public float[] StringFeatures { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Whether the file is a valid PE
    /// </summary>
    public bool IsPE { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Overall file entropy (0-8)
    /// </summary>
    public float Entropy { get; set; }

    /// <summary>
    /// Whether the PE file is digitally signed (has security directory)
    /// </summary>
    public bool IsSigned { get; set; }

    /// <summary>
    /// Number of PE sections
    /// </summary>
    public int SectionCount { get; set; }

    /// <summary>
    /// Number of imported functions
    /// </summary>
    public int ImportCount { get; set; }
}
