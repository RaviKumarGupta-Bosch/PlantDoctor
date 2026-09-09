using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PlantDoctor.Agent.Core.Ai;
using PlantDoctor.Agent.Core.Artifact;
using PlantDoctor.Agent.Core.Logs;

namespace PlantDoctor.Agent.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const int BufferSize = 500;
    private readonly ILogTailer _tailer;
    private readonly IOllamaClient _ollama;
    private readonly IArtifactWriter _writer;
    private readonly Queue<LogEntry> _buffer = new();

    public ObservableCollection<LogEntry> LiveLog { get; } = new();
    public ObservableCollection<IncidentCardVm> Incidents { get; } = new();
    public ObservableCollection<ChatBubbleVm> Chat { get; } = new();

    [ObservableProperty] private string _chatInput = string.Empty;
    [ObservableProperty] private string _ollamaStatus = "Checking...";
    [ObservableProperty] private string _plantId = "DEMO-PLANT-01";

    public MainViewModel(ILogTailer tailer, IOllamaClient ollama, IArtifactWriter writer)
    {
        _tailer = tailer; _ollama = ollama; _writer = writer;
        _tailer.EntryReceived += OnEntry;
        _tailer.Start();
        _ = CheckOllamaAsync();
    }

    private async Task CheckOllamaAsync() =>
        OllamaStatus = await _ollama.IsReachableAsync() ? "Ollama connected" : "Ollama not detected — please start Ollama";

    private void OnEntry(object? _, LogEntry e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _buffer.Enqueue(e);
            while (_buffer.Count > BufferSize) _buffer.Dequeue();
            LiveLog.Insert(0, e);
            while (LiveLog.Count > BufferSize) LiveLog.RemoveAt(LiveLog.Count - 1);
            if (e.Level is "Warning" or "Error" or "Critical")
                _ = AnalyzeAsync(e);
        });
    }

    private async Task AnalyzeAsync(LogEntry incident)
    {
        var prompt = PromptBuilder.BuildIncidentPrompt(incident, _buffer);
        string summary;
        try { summary = await _ollama.GenerateAsync(PromptBuilder.SystemPrompt, prompt); }
        catch (Exception ex) { summary = $"(Ollama call failed: {ex.Message})"; }
        Application.Current.Dispatcher.Invoke(() =>
            Incidents.Insert(0, new IncidentCardVm(incident, summary)));
    }

    [RelayCommand]
    private async Task SendChat()
    {
        if (string.IsNullOrWhiteSpace(ChatInput)) return;
        var user = ChatInput.Trim();
        ChatInput = string.Empty;
        Chat.Add(new ChatBubbleVm("operator", user));
        var prompt = PromptBuilder.BuildChatPrompt(user, _buffer);
        try
        {
            var answer = await _ollama.GenerateAsync(PromptBuilder.SystemPrompt, prompt);
            Chat.Add(new ChatBubbleVm("assistant", answer));
        }
        catch (Exception ex)
        {
            Chat.Add(new ChatBubbleVm("assistant", $"(error: {ex.Message})"));
        }
    }

    [RelayCommand]
    private async Task ExportArtifact(IncidentCardVm? card)
    {
        if (card is null) return;
        var artifact = BuildArtifact(card);
        var dlg = new SaveFileDialog
        {
            FileName = $"diagnostic-artifact-{PlantId}-{DateTime.UtcNow:yyyyMMddHHmmss}.json",
            Filter = "JSON (*.json)|*.json|ZIP (*.zip)|*.zip"
        };
        if (dlg.ShowDialog() != true) return;
        if (dlg.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            await _writer.WriteZipAsync(artifact, dlg.FileName);
        else
            await _writer.WriteJsonAsync(artifact, dlg.FileName);
        MessageBox.Show($"Saved: {dlg.FileName}\n\nTransfer via USB, email, or file share to the PlantDoctor web portal.",
            "Artifact exported");
    }

    private DiagnosticArtifact BuildArtifact(IncidentCardVm card) => new()
    {
        PlantId = PlantId,
        Incident = new IncidentInfo
        {
            DetectedAtUtc = card.Entry.Timestamp,
            Severity = card.Entry.Level,
            Source = card.Entry.Source,
            ErrorCode = card.Entry.ErrorCode,
            PrimaryMessage = card.Entry.Message
        },
        RecentLogEntries = _buffer.TakeLast(50).Select(e => new RecentLogEntry
        {
            Timestamp = e.Timestamp,
            Level = e.Level,
            Source = e.Source,
            SensorName = e.SensorName,
            Value = e.Value,
            ErrorCode = e.ErrorCode,
            Message = e.Message,
            StackTrace = e.StackTrace
        }).ToList(),
        AiAnalysis = new AiAnalysis
        {
            ModelUsed = "llama3.1:8b",
            Summary = card.AiSummary,
            SuspectedRootCause = card.AiSummary,
            Confidence = "Medium"
        },
        OperatorChatTranscript = Chat.Select(c => new ChatMessage
        { Role = c.Role, Message = c.Text, TimestampUtc = c.TimestampUtc }).ToList()
    };
}

public sealed record IncidentCardVm(LogEntry Entry, string AiSummary);
public sealed record ChatBubbleVm(string Role, string Text)
{
    public DateTime TimestampUtc { get; } = DateTime.UtcNow;
}
