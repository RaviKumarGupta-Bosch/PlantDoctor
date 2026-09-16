using System.Runtime.Serialization;

namespace PlantDoctor.Contracts;

[DataContract]
public class SensorReadingDto
{
    [DataMember] public string Name { get; set; } = string.Empty;
    [DataMember] public double Value { get; set; }
    [DataMember] public string Unit { get; set; } = string.Empty;
    [DataMember] public string Status { get; set; } = "OK";
    [DataMember] public DateTime TimestampUtc { get; set; }
}

[DataContract]
public class ErrorEventDto
{
    [DataMember] public DateTime TimestampUtc { get; set; }
    [DataMember] public string Level { get; set; } = "Error";
    [DataMember] public string Source { get; set; } = "Application";
    [DataMember] public string? SensorName { get; set; }
    [DataMember] public string? Value { get; set; }
    [DataMember] public string? ErrorCode { get; set; }

    /// <summary>The device's own semantic code, kept when the main application stamps a catalog code.</summary>
    [DataMember] public string? DeviceErrorCode { get; set; }

    [DataMember] public string Message { get; set; } = string.Empty;
    [DataMember] public string? StackTrace { get; set; }
}

[DataContract]
public class ComEventDto
{
    [DataMember] public DateTime TimestampUtc { get; set; }
    [DataMember] public string Port { get; set; } = string.Empty;
    [DataMember] public string Status { get; set; } = "Disconnected";
    [DataMember] public string? RawFrame { get; set; }
}

[DataContract]
public class ScanEventDto
{
    [DataMember] public DateTime TimestampUtc { get; set; }
    [DataMember] public string Port { get; set; } = string.Empty;
    [DataMember] public string Code { get; set; } = string.Empty;
    [DataMember] public string CodeType { get; set; } = "Barcode";
}

[DataContract]
public class PlantSnapshotDto
{
    [DataMember] public DateTime TakenAtUtc { get; set; }
    [DataMember] public List<SensorReadingDto> Sensors { get; set; } = new();
    [DataMember] public ComEventDto? Com { get; set; }
    [DataMember] public ScanEventDto? LastScan { get; set; }
}
