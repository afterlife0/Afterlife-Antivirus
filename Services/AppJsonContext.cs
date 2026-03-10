using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AfterlifeWinUI.Services;

/// <summary>
/// JSON serialization context for AOT/trimming compatibility.
/// Uses source generators instead of reflection-based serialization.
/// </summary>
[JsonSerializable(typeof(AppSettingsData))]
[JsonSerializable(typeof(WindowSettingsService.WindowSettings))]
[JsonSerializable(typeof(ThreatHistoryData))]
[JsonSerializable(typeof(ThreatRecord))]
[JsonSerializable(typeof(List<ThreatRecord>))]
[JsonSerializable(typeof(List<string>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class AppJsonContext : JsonSerializerContext
{
}
