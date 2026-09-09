using System.Text.Json;
using FluentAssertions;
using PlantDoctor.Agent.Core.Ai;
using PlantDoctor.Agent.Core.Artifact;
using PlantDoctor.Agent.Core.Logs;
using Xunit;

namespace PlantDoctor.Agent.Tests;

public class LogEntryParserTests
{
    [Fact]
    public void TryParse_ValidLine_Succeeds()
    {
        var line = """{"timestamp":"2026-01-02T03:04:05Z","level":"Error","source":"Sensor","message":"boom"}""";
        LogEntryParser.TryParse(line, out var e).Should().BeTrue();
        e.Level.Should().Be("Error");
        e.Message.Should().Be("boom");
    }

    [Fact]
    public void TryParse_Junk_ReturnsFalse() =>
        LogEntryParser.TryParse("not json", out _).Should().BeFalse();
}

public class ArtifactWriterTests
{
    [Fact]
    public async Task WriteJsonAsync_ProducesCamelCase()
    {
        var writer = new ArtifactWriter();
        var artifact = new DiagnosticArtifact { PlantId = "P1" };
        var path = Path.GetTempFileName();
        await writer.WriteJsonAsync(artifact, path);
        var json = await File.ReadAllTextAsync(path);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("plantId").GetString().Should().Be("P1");
        doc.RootElement.TryGetProperty("recentLogEntries", out _).Should().BeTrue();
    }
}

public class PromptBuilderTests
{
    [Fact]
    public void BuildIncidentPrompt_IncludesIncidentAndContext()
    {
        var incident = new LogEntry(DateTime.UtcNow, "Error", "Sensor", "T", "999", "OOR", "hot", null);
        var ctx = new[] { new LogEntry(DateTime.UtcNow, "Info", "Sensor", "T", "80", null, "T=80", null) };
        var p = PromptBuilder.BuildIncidentPrompt(incident, ctx);
        p.Should().Contain("hot").And.Contain("T=80");
    }

    [Fact]
    public void BuildChatPrompt_IncludesOperatorMessage()
    {
        var p = PromptBuilder.BuildChatPrompt("why?", Array.Empty<LogEntry>());
        p.Should().Contain("why?");
    }
}
