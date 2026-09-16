using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using PlantDoctor.Agent.Core.Ai;
using PlantDoctor.Agent.Core.Artifact;
using PlantDoctor.Agent.Core.Logs;
using Xunit;

namespace PlantDoctor.Agent.Tests.Integration;

/// <summary>
/// Integration tests that connect to a real Ollama instance at http://localhost:11434.
/// These tests verify the full stack: configuration loading, HTTP communication, and model responses.
/// </summary>
public class OllamaIntegrationTests
{
    private static readonly string BaseUrl = "http://localhost:11434";
    private static readonly string Model = "mistral:latest";

    private OllamaClient CreateClient()
    {
        var handler = new HttpClientHandler { UseDefaultCredentials = true };
        var http = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromMinutes(2) };
        var opts = Options.Create(new OllamaOptions { BaseUrl = BaseUrl, Model = Model });
        return new OllamaClient(http, opts);
    }

    [Fact]
    public async Task IsReachableAsync_ReturnsTrue_WhenOllamaIsRunning()
    {
        var client = CreateClient();
        var result = await client.IsReachableAsync();
        result.Should().BeTrue("Ollama should be reachable at {0}", BaseUrl);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsResponse_ForSimpleQuestion()
    {
        var client = CreateClient();
        var systemPrompt = "You are a helpful manufacturing expert assistant.";
        var userPrompt = "What is predictive maintenance? Answer in one sentence.";
        
        var result = await client.GenerateAsync(systemPrompt, userPrompt);
        
        result.Should().NotBeNullOrEmpty();
        result.Length.Should().BeGreaterThan(10);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsResponse_ForIncidentAnalysis()
    {
        var client = CreateClient();
        var systemPrompt = "You are a manufacturing plant diagnostic expert. Analyze sensor incidents and provide actionable recommendations.";
        var userPrompt = "Sensor Temperature is reporting 2000°C with error code OUT_OF_RANGE. Recent context shows temperature was at 75°C. What could be the issue?";
        
        var result = await client.GenerateAsync(systemPrompt, userPrompt);
        
        result.Should().NotBeNullOrEmpty();
        result.ToLower().Should().Contain("sensor");
    }

    [Fact]
    public async Task GenerateAsync_ReturnsDifferentResponses_ForDifferentPrompts()
    {
        var client = CreateClient();
        var systemPrompt = "You are a helpful assistant.";
        
        var response1 = await client.GenerateAsync(systemPrompt, "What is 2+2?");
        var response2 = await client.GenerateAsync(systemPrompt, "What is the capital of France?");
        
        response1.Should().NotBeNullOrEmpty();
        response2.Should().NotBeNullOrEmpty();
        // Responses should be different since prompts are different
        response1.Should().NotBeEquivalentTo(response2);
    }

    [Fact]
    public async Task GenerateAsync_HandlesLongContext()
    {
        var client = CreateClient();
        var systemPrompt = "You are a manufacturing plant diagnostic expert.";
        
        var contextEntries = string.Join("\n", Enumerable.Range(1, 20).Select(i => 
            $"Log entry {i}: Sensor {i} reading {50 + i}°C, status OK"));
        var userPrompt = $"Recent log context:\n{contextEntries}\n\nWhich sensor shows the highest temperature?";
        
        var result = await client.GenerateAsync(systemPrompt, userPrompt);
        
        result.Should().NotBeNullOrEmpty();
    }
}

/// <summary>
/// Tests that verify the configuration is loaded correctly from appsettings.json
/// </summary>
public class ConfigurationTests
{
    [Fact]
    public void OllamaOptions_Model_IsMistralLatest()
    {
        // This test verifies the configuration file has the correct model
        // The test project outputs to tests\PlantDoctor.Agent.Tests\bin\Debug\net8.0\
        // The UI project outputs to src\PlantDoctor.Agent.UI\bin\Debug\net8.0-windows\
        // We need to find the UI's appsettings.json from the test's perspective
        
        var testOutputDir = AppDomain.CurrentDomain.BaseDirectory;
        
        // Navigate from tests\...\bin\Debug\net8.0\ up to the solution root, then to UI output
        var possiblePaths = new[]
        {
            Path.Combine(testOutputDir, "..", "..", "..", "..", "src", "PlantDoctor.Agent.UI", "bin", "Debug", "net8.0-windows", "appsettings.json"),
            Path.Combine(testOutputDir, "..", "..", "..", "..", "..", "..", "apps", "PlantDoctor.Agent", "src", "PlantDoctor.Agent.UI", "bin", "Debug", "net8.0-windows", "appsettings.json"),
            Path.Combine(testOutputDir, "..", "..", "..", "..", "..", "..", "..", "apps", "PlantDoctor.Agent", "src", "PlantDoctor.Agent.UI", "bin", "Debug", "net8.0-windows", "appsettings.json"),
        };
        
        string? sourceAppSettings = null;
        foreach (var p in possiblePaths)
        {
            var resolved = Path.GetFullPath(p);
            if (File.Exists(resolved))
            {
                sourceAppSettings = resolved;
                break;
            }
        }
        
        // Also check source location
        if (string.IsNullOrEmpty(sourceAppSettings))
        {
            var sourcePaths = new[]
            {
                Path.Combine(testOutputDir, "..", "..", "..", "..", "..", "..", "apps", "PlantDoctor.Agent", "src", "PlantDoctor.Agent.UI", "appsettings.json"),
                Path.Combine(testOutputDir, "..", "..", "..", "..", "..", "..", "..", "apps", "PlantDoctor.Agent", "src", "PlantDoctor.Agent.UI", "appsettings.json"),
            };
            foreach (var p in sourcePaths)
            {
                var resolved = Path.GetFullPath(p);
                if (File.Exists(resolved))
                {
                    sourceAppSettings = resolved;
                    break;
                }
            }
        }
        
        sourceAppSettings.Should().NotBeNull(
            $"appsettings.json should exist. Test output dir: {testOutputDir}");
        
        if (!string.IsNullOrEmpty(sourceAppSettings))
        {
            var json = File.ReadAllText(sourceAppSettings);
            using var doc = JsonDocument.Parse(json);
            var model = doc.RootElement
                .GetProperty("Ollama")
                .GetProperty("Model")
                .GetString();
            
            model.Should().Be("mistral:latest", 
                $"The Ollama model should be mistral:latest, not a missing model like llama3.1:8b. File: {sourceAppSettings}");
        }
    }

    [Fact]
    public void OllamaOptions_BaseUrl_IsLocalhost()
    {
        var sourceAppSettings = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "appsettings.json");
        
        if (!File.Exists(sourceAppSettings))
        {
            sourceAppSettings = Path.Combine(
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", 
                    "PlantDoctor.Agent.UI", "appsettings.json")));
        }
        
        if (File.Exists(sourceAppSettings))
        {
            var json = File.ReadAllText(sourceAppSettings);
            using var doc = JsonDocument.Parse(json);
            var baseUrl = doc.RootElement
                .GetProperty("Ollama")
                .GetProperty("BaseUrl")
                .GetString();
            
            baseUrl.Should().Be("http://localhost:11434",
                "Ollama should use localhost for loopback-only access");
        }
    }

    [Fact]
    public void AgentOptions_PlantId_IsSet()
    {
        var sourceAppSettings = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "appsettings.json");
        
        if (!File.Exists(sourceAppSettings))
        {
            sourceAppSettings = Path.Combine(
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", 
                    "PlantDoctor.Agent.UI", "appsettings.json")));
        }
        
        if (File.Exists(sourceAppSettings))
        {
            var json = File.ReadAllText(sourceAppSettings);
            using var doc = JsonDocument.Parse(json);
            var plantId = doc.RootElement
                .GetProperty("Agent")
                .GetProperty("PlantId")
                .GetString();
            
            plantId.Should().NotBeNullOrEmpty();
        }
    }
}

/// <summary>
/// End-to-end test that verifies the full chat flow works
/// </summary>
public class ChatFlowTests
{
    [Fact]
    public async Task FullChatFlow_SendsMessageAndGetResponse()
    {
        // Arrange
        var handler = new HttpClientHandler { UseDefaultCredentials = true };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromMinutes(2) };
        var opts = Options.Create(new OllamaOptions { BaseUrl = "http://localhost:11434", Model = "mistral:latest" });
        var client = new OllamaClient(http, opts);
        
        // Act - Test 1: Simple greeting
        var greeting = await client.GenerateAsync(
            "You are a helpful manufacturing expert.",
            "Hello, please introduce yourself briefly.");
        
        // Assert - Test 1
        greeting.Should().NotBeNullOrEmpty();
        greeting.Length.Should().BeGreaterThan(5);
        
        // Act - Test 2: Incident analysis
        var incidentResponse = await client.GenerateAsync(
            "You are a manufacturing plant diagnostic expert. Analyze sensor incidents and provide actionable recommendations.",
            "Sensor Temperature is reporting 2000°C with error code OUT_OF_RANGE. Recent context shows temperature was at 75°C. What could be the issue?");
        
        // Assert - Test 2
        incidentResponse.Should().NotBeNullOrEmpty();
        incidentResponse.ToLower().Should().Contain("sensor");
    }
}
