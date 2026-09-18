using System.IO.Compression;
using System.Text.Json;

namespace PlantDoctor.Agent.Core.Artifact;

public interface IArtifactWriter
{
    Task WriteJsonAsync(DiagnosticArtifact artifact, string filePath, CancellationToken ct = default);
    Task WriteZipAsync(DiagnosticArtifact artifact, string zipPath, CancellationToken ct = default);
}

public sealed class ArtifactWriter : IArtifactWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task WriteJsonAsync(DiagnosticArtifact a, string filePath, CancellationToken ct = default)
    {
        await using var fs = File.Create(filePath);
        await JsonSerializer.SerializeAsync(fs, a, Options, ct);
    }

    public async Task WriteZipAsync(DiagnosticArtifact a, string zipPath, CancellationToken ct = default)
    {
        await using var fs = File.Create(zipPath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var artifactEntry = zip.CreateEntry("diagnostic-artifact.json");
        await using (var artifactStream = artifactEntry.Open())
        {
            await JsonSerializer.SerializeAsync(artifactStream, a, Options, ct);
        }

        if (a.RecentLogEntries.Count > 0)
        {
            var logEntry = zip.CreateEntry("plant-last-20-minutes.jsonl");
            await using var logStream = logEntry.Open();
            await using var writer = new StreamWriter(logStream);
            foreach (var log in a.RecentLogEntries)
            {
                ct.ThrowIfCancellationRequested();
                await writer.WriteLineAsync(JsonSerializer.Serialize(log, CompactOptions));
            }
        }

        if (a.OperatorChatTranscript.Count > 0)
        {
            var chatEntry = zip.CreateEntry("chat-history.json");
            await using var chatStream = chatEntry.Open();
            await JsonSerializer.SerializeAsync(chatStream, a.OperatorChatTranscript, Options, ct);
        }
    }
}
