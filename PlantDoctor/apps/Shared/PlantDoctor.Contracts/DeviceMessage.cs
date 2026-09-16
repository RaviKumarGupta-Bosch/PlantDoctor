using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlantDoctor.Contracts;

public static class DeviceMessageTypes
{
    public const string Sensor = "sensor";
    public const string Scan = "scan";
    public const string Error = "error";
}

/// <summary>
/// One newline-delimited JSON frame on the device→main-app wire. Every transport carries
/// this same envelope so a single device can report readings, scans and faults on its own channel.
/// </summary>
public sealed class DeviceMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = DeviceMessageTypes.Sensor;
    [JsonPropertyName("device")] public string Device { get; set; } = string.Empty;
    [JsonPropertyName("reading")] public SensorReadingDto? Reading { get; set; }
    [JsonPropertyName("scan")] public ScanEventDto? Scan { get; set; }
    [JsonPropertyName("error")] public ErrorEventDto? Error { get; set; }

    public static DeviceMessage ForReading(string device, SensorReadingDto reading) =>
        new() { Type = DeviceMessageTypes.Sensor, Device = device, Reading = reading };

    public static DeviceMessage ForScan(string device, ScanEventDto scan) =>
        new() { Type = DeviceMessageTypes.Scan, Device = device, Scan = scan };

    public static DeviceMessage ForError(string device, ErrorEventDto error) =>
        new() { Type = DeviceMessageTypes.Error, Device = device, Error = error };
}

/// <summary>Newline-delimited JSON codec used by both ends of every device channel.</summary>
public static class WireFormat
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Serialises to a single line — the payload must never contain a raw newline.</summary>
    public static string Serialize(DeviceMessage message) => JsonSerializer.Serialize(message, Options);

    public static bool TryParse(string line, out DeviceMessage message)
    {
        message = default!;
        if (string.IsNullOrWhiteSpace(line)) return false;
        try
        {
            var parsed = JsonSerializer.Deserialize<DeviceMessage>(line, Options);
            if (parsed is null) return false;
            message = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
