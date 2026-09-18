using System.Text;
using PlantDoctor.Agent.Core.Logs;

namespace PlantDoctor.Agent.Core.Ai;

public static class PromptBuilder
{
    public const string SystemPrompt =
        "You are an expert industrial diagnostics assistant for a manufacturing plant. " +
        "Your role is to analyze log entries, sensor readings, and system events to provide " +
        "actionable insights to plant operators and maintenance engineers.\n\n" +
        "When analyzing incidents or answering questions, follow this structure:\n\n" +
        "1. **Summary**: Brief overview of what happened (1-2 sentences)\n" +
        "2. **Root Cause Analysis**: Most likely cause(s) based on evidence in the logs\n" +
        "3. **Evidence**: Specific log entries or sensor readings that support your analysis\n" +
        "4. **Impact**: What systems or processes are affected\n" +
        "5. **Recommendations**: Concrete next steps, prioritized by urgency\n" +
        "6. **Prevention**: Suggestions to avoid recurrence\n\n" +
        "Guidelines:\n" +
        "- Be specific and reference actual data from the logs when available\n" +
        "- Use clear, professional language suitable for engineering documentation\n" +
        "- Prioritize safety-critical issues first\n" +
        "- Distinguish between confirmed facts and educated hypotheses\n" +
        "- Suggest diagnostic commands or checks that can verify your analysis\n" +
        "- If insufficient data is available, state what additional information would help\n" +
        "- Do not invent facts not present in the logs\n" +
        "- Format your response with clear sections and bullet points for readability";

    public static string BuildIncidentPrompt(LogEntry incident, IEnumerable<LogEntry> context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("INCIDENT:");
        sb.AppendLine($"  {incident.Timestamp:o} [{incident.Level}] ({incident.Source}) {incident.Message}");
        if (!string.IsNullOrEmpty(incident.ErrorCode)) sb.AppendLine($"  ErrorCode: {incident.ErrorCode}");
        if (!string.IsNullOrEmpty(incident.StackTrace)) sb.AppendLine($"  StackTrace: {incident.StackTrace}");
        sb.AppendLine();
        sb.AppendLine("RECENT LOG CONTEXT (last 50 entries):");
        foreach (var e in context.TakeLast(50))
            sb.AppendLine($"  {e.Timestamp:o} [{e.Level}] ({e.Source}) {e.Message}");
        sb.AppendLine();
        sb.AppendLine("Provide a detailed analysis following the structure: Summary, Root Cause, Evidence, Impact, Recommendations, Prevention.");
        return sb.ToString();
    }

    public static string BuildChatPrompt(
        string userMessage,
        IEnumerable<LogEntry> recent,
        string historyContext = "",
        string plantId = "",
        string incidentContext = "")
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(plantId)) sb.AppendLine($"PLANT ID: {plantId}");
        sb.AppendLine("PLANT LOGS (complete available 20-minute window):");
        foreach (var e in recent)
        {
            sb.Append($"  {e.Timestamp:o} [{e.Level}] ({e.Source})");
            if (!string.IsNullOrWhiteSpace(e.SensorName)) sb.Append($" Sensor={e.SensorName}");
            if (!string.IsNullOrWhiteSpace(e.Value)) sb.Append($" Value={e.Value}");
            if (!string.IsNullOrWhiteSpace(e.ErrorCode)) sb.Append($" ErrorCode={e.ErrorCode}");
            sb.AppendLine($" Message={e.Message}");
            if (!string.IsNullOrWhiteSpace(e.StackTrace)) sb.AppendLine($"    StackTrace={e.StackTrace}");
        }
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(incidentContext))
        {
            sb.AppendLine("KNOWN INCIDENTS AND AI ARTIFACTS:");
            sb.AppendLine(incidentContext);
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(historyContext))
        {
            sb.AppendLine("CONVERSATION HISTORY:");
            sb.AppendLine(historyContext);
            sb.AppendLine();
        }
        sb.AppendLine($"OPERATOR QUESTION: {userMessage}");
        sb.AppendLine();
        sb.AppendLine("Provide a detailed, structured response following the guidelines in the system prompt.");
        return sb.ToString();
    }
}
