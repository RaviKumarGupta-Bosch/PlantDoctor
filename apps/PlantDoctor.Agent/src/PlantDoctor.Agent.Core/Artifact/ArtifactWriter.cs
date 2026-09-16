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

    public async Task WriteJsonAsync(DiagnosticArtifact a, string filePath, CancellationToken ct = default)
    {
        await using var fs = File.Create(filePath);
        await JsonSerializer.SerializeAsync(fs, a, Options, ct);
    }

    public async Task WriteZipAsync(DiagnosticArtifact a, string zipPath, CancellationToken ct = default)
    {
        await using var fs = File.Create(zipPath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        var entry = zip.CreateEntry($"{Path.GetFileNameWithoutExtension(zipPath)}.json");
        await using var es = entry.Open();
        await JsonSerializer.SerializeAsync(es, a, Options, ct);
    }
}
