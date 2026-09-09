namespace PlantDoctor.Agent.Core.Ai;

public sealed class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.1:8b";
}

public interface IOllamaClient
{
    Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
    Task<bool> IsReachableAsync(CancellationToken ct = default);
}
