namespace PlantDoctor.Agent.Core.Artifact;

/// <summary>Diagnostic artifact — the cross-app contract with PlantDoctor.Portal.
/// Field names MUST match docs/contracts/diagnostic-artifact.schema.json.</summary>
public sealed class DiagnosticArtifact
{
    public string ArtifactId { get; set; } = Guid.NewGuid().ToString();
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string PlantId { get; set; } = "DEMO-PLANT-01";
    public IncidentInfo Incident { get; set; } = new();
    public List<RecentLogEntry> RecentLogEntries { get; set; } = new();
    public List<SensorSnapshotEntry> SensorSnapshot { get; set; } = new();
    public ComConnectionState ComConnectionState { get; set; } = new();
    public AiAnalysis AiAnalysis { get; set; } = new();
    public List<ChatMessage> OperatorChatTranscript { get; set; } = new();
}

public sealed class IncidentInfo
{
    public DateTime DetectedAtUtc { get; set; }
    public string Severity { get; set; } = "Warning";
    public string Source { get; set; } = "Application";
    public string? ErrorCode { get; set; }
    public string PrimaryMessage { get; set; } = string.Empty;
}

public sealed class RecentLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "Info";
    public string Source { get; set; } = "Application";
    public string? SensorName { get; set; }
    public string? Value { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
}

public sealed class SensorSnapshotEntry
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Status { get; set; } = "OK";
}

public sealed class ComConnectionState
{
    public string Port { get; set; } = string.Empty;
    public string Status { get; set; } = "Disconnected";
    public DateTime LastEventUtc { get; set; }
}

public sealed class AiAnalysis
{
    public string ModelUsed { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SuspectedRootCause { get; set; } = string.Empty;
    public string Confidence { get; set; } = "Low";
}

public sealed class ChatMessage
{
    public string Role { get; set; } = "operator";
    public string Message { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
