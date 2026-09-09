using System.Text;
using PlantDoctor.Agent.Core.Logs;

namespace PlantDoctor.Agent.Core.Ai;

public static class PromptBuilder
{
    public const string SystemPrompt =
        "You are an industrial diagnostics assistant embedded in an air-gapped plant. " +
        "Given recent structured log entries and sensor readings, explain likely root causes " +
        "in concise plain language, and suggest what a developer should investigate. " +
        "Be terse: 3-6 sentences. Do not invent facts not present in the logs.";

    public static string BuildIncidentPrompt(LogEntry incident, IEnumerable<LogEntry> context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("INCIDENT:");
        sb.AppendLine($"  {incident.Timestamp:o} [{incident.Level}] ({incident.Source}) {incident.Message}");
        if (!string.IsNullOrEmpty(incident.ErrorCode)) sb.AppendLine($"  ErrorCode: {incident.ErrorCode}");
        if (!string.IsNullOrEmpty(incident.StackTrace)) sb.AppendLine($"  StackTrace: {incident.StackTrace}");
        sb.AppendLine();
        sb.AppendLine("RECENT LOG CONTEXT:");
        foreach (var e in context.TakeLast(50))
            sb.AppendLine($"  {e.Timestamp:o} [{e.Level}] ({e.Source}) {e.Message}");
        sb.AppendLine();
        sb.AppendLine("Explain the most likely root cause and suggest next steps.");
        return sb.ToString();
    }

    public static string BuildChatPrompt(string userMessage, IEnumerable<LogEntry> recent)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RECENT LOGS:");
        foreach (var e in recent.TakeLast(30))
            sb.AppendLine($"  {e.Timestamp:o} [{e.Level}] ({e.Source}) {e.Message}");
        sb.AppendLine();
        sb.AppendLine($"OPERATOR: {userMessage}");
        return sb.ToString();
    }
}
