using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace PlantDoctor.Agent.Core.Ai;

/// <summary>Calls Ollama's /api/chat endpoint. Loopback-only (http://localhost:11434) — not internet.</summary>
public sealed class OllamaClient : IOllamaClient
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _opts;

    public OllamaClient(HttpClient http, IOptions<OllamaOptions> opts)
    {
        _opts = opts.Value;
        _http = http;
        _http.BaseAddress = new Uri(_opts.BaseUrl);
        _http.Timeout = TimeSpan.FromMinutes(2);
    }

    public async Task<bool> IsReachableAsync(CancellationToken ct = default)
    {
        try
        {
            using var r = await _http.GetAsync("/api/tags", ct);
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var req = new ChatRequest(_opts.Model, new[]
        {
            new ChatMsg("system", systemPrompt),
            new ChatMsg("user", userPrompt)
        }, Stream: false);

        using var resp = await _http.PostAsJsonAsync("/api/chat", req, ct);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);
        return body?.Message?.Content ?? string.Empty;
    }

    private sealed record ChatRequest(string Model, ChatMsg[] Messages, [property: JsonPropertyName("stream")] bool Stream);
    private sealed record ChatMsg(string Role, string Content);
    private sealed record ChatResponse([property: JsonPropertyName("message")] ChatMsg? Message);
}
