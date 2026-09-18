using System.IO.Compression;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
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

    [Fact]
    public void TryParse_EmptyLine_ReturnsFalse() =>
        LogEntryParser.TryParse("", out _).Should().BeFalse();

    [Fact]
    public void TryParse_WhitespaceLine_ReturnsFalse() =>
        LogEntryParser.TryParse("   ", out _).Should().BeFalse();

    [Fact]
    public void TryParse_FullEntry_ParsesAllFields()
    {
        var line = """{"timestamp":"2026-01-02T03:04:05Z","level":"Critical","source":"COM","sensorName":"Temp1","value":"999","errorCode":"E001","message":"Sensor timeout","stackTrace":"at Program.Main()"}""";
        LogEntryParser.TryParse(line, out var e).Should().BeTrue();
        e.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
        e.Timestamp.Year.Should().Be(2026);
        e.Timestamp.Month.Should().Be(1);
        e.Timestamp.Day.Should().Be(2);
        e.Timestamp.Hour.Should().Be(3);
        e.Timestamp.Minute.Should().Be(4);
        e.Timestamp.Second.Should().Be(5);
        e.Level.Should().Be("Critical");
        e.Source.Should().Be("COM");
        e.SensorName.Should().Be("Temp1");
        e.Value.Should().Be("999");
        e.ErrorCode.Should().Be("E001");
        e.Message.Should().Be("Sensor timeout");
        e.StackTrace.Should().Be("at Program.Main()");
    }

    [Fact]
    public void TryParse_MinimalEntry_UsesDefaults()
    {
        var line = """{"timestamp":"2026-01-02T03:04:05Z"}""";
        LogEntryParser.TryParse(line, out var e).Should().BeTrue();
        e.Level.Should().Be("Info");
        e.Source.Should().Be("Application");
        e.Message.Should().BeEmpty();
    }
}

public class JsonlLogTailerTests
{
    [Fact]
    public void ReadRecent_ReturnsEntriesWithinWindowFromActiveFile()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"plantdoctor-agent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var logPath = Path.Combine(folder, "plant-20260918.jsonl");
        File.WriteAllLines(logPath,
        [
            "{\"timestamp\":\"2026-09-18T11:30:00Z\",\"level\":\"Info\",\"message\":\"old\"}",
            "{\"timestamp\":\"2026-09-18T11:45:00Z\",\"level\":\"Warning\",\"message\":\"within window\"}",
            "{\"timestamp\":\"2026-09-18T12:00:00Z\",\"level\":\"Error\",\"message\":\"latest\"}"
        ]);

        try
        {
            using var tailer = new JsonlLogTailer(folder);
            tailer.Start();

            var entries = tailer.ReadRecent(TimeSpan.FromMinutes(20));

            entries.Select(entry => entry.Message).Should().Equal("within window", "latest");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

public class ArtifactWriterTests
{
    [Fact]
    public async Task WriteJsonAsync_ProducesCamelCase()
    {
        var writer = new ArtifactWriter();
        var artifact = new DiagnosticArtifact { PlantId = "P1" };
        var path = Path.GetTempFileName();
        try
        {
            await writer.WriteJsonAsync(artifact, path);
            var json = await File.ReadAllTextAsync(path);
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("plantId").GetString().Should().Be("P1");
            doc.RootElement.TryGetProperty("recentLogEntries", out _).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task WriteJsonAsync_ProducesValidJson()
    {
        var writer = new ArtifactWriter();
        var artifact = new DiagnosticArtifact { PlantId = "TestPlant" };
        var path = Path.GetTempFileName();
        try
        {
            await writer.WriteJsonAsync(artifact, path);
            var json = await File.ReadAllTextAsync(path);
            JsonDocument.Parse(json); // Should not throw
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task WriteZipAsync_CreatesZipFile()
    {
        var writer = new ArtifactWriter();
        var artifact = new DiagnosticArtifact { PlantId = "P1" };
        var path = Path.GetTempFileName() + ".zip";
        try
        {
            await writer.WriteZipAsync(artifact, path);
            File.Exists(path).Should().BeTrue();
            using var stream = File.OpenRead(path);
            using var archive = new ZipArchive(stream);
            archive.Entries.Should().HaveCount(1);
            archive.Entries[0].FullName.Should().Be("diagnostic-artifact.json");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }


    [Fact]
    public async Task WriteZipAsync_IncludesLogsAndChat_WhenPresent()
    {
        var writer = new ArtifactWriter();
        var artifact = new DiagnosticArtifact
        {
            RecentLogEntries =
            [
                new RecentLogEntry { Timestamp = DateTime.UtcNow, Message = "running" }
            ],
            OperatorChatTranscript =
            [
                new ChatMessage { Role = "operator", Message = "status?" }
            ]
        };
        var path = Path.GetTempFileName() + ".zip";
        try
        {
            await writer.WriteZipAsync(artifact, path);
            using var stream = File.OpenRead(path);
            using var archive = new ZipArchive(stream);
            archive.Entries.Select(entry => entry.FullName).Should().BeEquivalentTo(
                "diagnostic-artifact.json", "plant-last-20-minutes.jsonl", "chat-history.json");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
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

    [Fact]
    public void BuildChatPrompt_IncludesCompletePlantContext()
    {
        var logs = new[]
        {
            new LogEntry(DateTime.UtcNow, "Error", "Sensor", "Temperature", "2000 C", "OUT_OF_RANGE", "hot", "sensor stack")
        };

        var prompt = PromptBuilder.BuildChatPrompt(
            "why?", logs, "operator: previous question", "PLANT-1", "OUT_OF_RANGE analysis");

        prompt.Should().ContainAll("PLANT-1", "Temperature", "2000 C", "OUT_OF_RANGE", "sensor stack",
            "previous question", "OUT_OF_RANGE analysis");
    }

    [Fact]
    public void BuildIncidentPrompt_IncludesStackTrace()
    {
        var incident = new LogEntry(DateTime.UtcNow, "Error", "Application", null, null, null, "crash", "at Line 42");
        var p = PromptBuilder.BuildIncidentPrompt(incident, Array.Empty<LogEntry>());
        p.Should().Contain("at Line 42");
    }

    [Fact]
    public void BuildIncidentPrompt_LimitsContextTo50Entries()
    {
        var incident = new LogEntry(DateTime.UtcNow, "Error", "Sensor", null, null, null, "test", null);
        var ctx = Enumerable.Range(1, 100).Select(i => new LogEntry(DateTime.UtcNow, "Info", "Sensor", null, null, null, $"msg{i}", null));
        var p = PromptBuilder.BuildIncidentPrompt(incident, ctx);
        // Should only contain last 50 entries
        p.Split('\n').Count(l => l.StartsWith("  msg"))
            .Should().BeLessOrEqualTo(50);
    }
}

public class OllamaClientTests
{
    [Fact]
    public async Task IsReachableAsync_ReturnsTrue_WhenOllamaResponds()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));

        var http = new HttpClient(handler.Object);
        var opts = Options.Create(new OllamaOptions { BaseUrl = "http://localhost:11434" });
        var client = new OllamaClient(http, opts);

        var result = await client.IsReachableAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsReachableAsync_ReturnsFalse_WhenOllamaFails()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));

        var http = new HttpClient(handler.Object);
        var opts = Options.Create(new OllamaOptions { BaseUrl = "http://localhost:11434" });
        var client = new OllamaClient(http, opts);

        var result = await client.IsReachableAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_ReturnsResponse_WhenSuccessful()
    {
        var responseJson = """{"message":{"role":"assistant","content":"The sensor is overheating."}}""";
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            });

        var http = new HttpClient(handler.Object);
        var opts = Options.Create(new OllamaOptions { BaseUrl = "http://localhost:11434", Model = "llama3.1:8b" });
        var client = new OllamaClient(http, opts);

        var result = await client.GenerateAsync("System prompt", "User prompt");
        result.Should().Contain("overheating");
    }
}
