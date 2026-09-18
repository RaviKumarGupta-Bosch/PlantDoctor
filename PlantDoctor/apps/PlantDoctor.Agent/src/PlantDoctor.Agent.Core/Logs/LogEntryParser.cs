using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlantDoctor.Agent.Core.Logs;

/// <summary>Mirrors the JSONL schema written by PlantSimulator.</summary>
public sealed record LogEntry(
    DateTime Timestamp,
    string Level,
    string Source,
    string? SensorName,
    string? Value,
    string? ErrorCode,
    string Message,
    string? StackTrace);

public static class LogEntryParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static bool TryParse(string jsonLine, out LogEntry entry)
    {
        entry = null!;
        if (string.IsNullOrWhiteSpace(jsonLine)) return false;
        try
        {
            var raw = JsonSerializer.Deserialize<Raw>(jsonLine, Options);
            if (raw is null) return false;
            entry = new LogEntry(raw.Timestamp, raw.Level ?? "Info", raw.Source ?? "Application",
                raw.SensorName, raw.Value, raw.ErrorCode, raw.Message ?? string.Empty, raw.StackTrace);
            return true;
        }
        catch (JsonException) { return false; }
    }

    private sealed record Raw(DateTime Timestamp, string? Level, string? Source,
        string? SensorName, string? Value, string? ErrorCode, string? Message, string? StackTrace);
}
