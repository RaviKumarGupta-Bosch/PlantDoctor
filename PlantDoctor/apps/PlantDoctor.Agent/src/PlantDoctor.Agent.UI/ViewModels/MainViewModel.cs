using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Data;
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
    private const int IncidentLimit = 200;
    private static readonly TimeSpan AiRequestTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ExportLogWindow = TimeSpan.FromMinutes(20);
    private readonly ILogTailer _tailer;
    private readonly IOllamaClient _ollama;
    private readonly IArtifactWriter _writer;
    private readonly Queue<LogEntry> _buffer = new();
    private readonly List<ChatSessionMessage> _sessionHistory = new();
    private readonly HashSet<string> _incidentKeys = new();

    public ObservableCollection<LogEntry> LiveLog { get; } = new();
    private readonly ICollectionView _filteredLogsView;
    public ICollectionView FilteredLogsView => _filteredLogsView;
    public ObservableCollection<IncidentCardVm> Incidents { get; } = new();
    public ObservableCollection<ChatBubbleVm> Chat { get; } = new();

    [ObservableProperty] private string _chatInput = string.Empty;
    [ObservableProperty] private string _logFilter = string.Empty;
    [ObservableProperty] private string _logLevelFilter = "All Levels";
    [ObservableProperty] private string _ollamaStatus = "Checking...";
    [ObservableProperty] private string _plantId = "DEMO-PLANT-01";
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private bool _isAnalyzing;

    public MainViewModel(ILogTailer tailer, IOllamaClient ollama, IArtifactWriter writer)
    {
        _tailer = tailer; _ollama = ollama; _writer = writer;
        _filteredLogsView = new CollectionViewSource() { Source = LiveLog }.View;
        _filteredLogsView.Filter = ApplyFilterPredicate;

        _tailer.EntryReceived += OnEntry;
        _tailer.Start();
        _ = CheckOllamaAsync();
    }

    private bool ApplyFilterPredicate(object item)
    {
        if (item is not LogEntry entry) return false;

        if (!string.Equals(LogLevelFilter, "All Levels", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entry.Level, LogLevelFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        // Apply text filter
        if (!string.IsNullOrWhiteSpace(LogFilter))
        {
            var filter = LogFilter.Trim();
            var matches = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss").Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                entry.Message.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                entry.Level.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                entry.Source.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (entry.SensorName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (entry.Value?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (entry.ErrorCode?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (entry.StackTrace?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
            return matches;
        }

        return true;
    }

    private async Task CheckOllamaAsync() =>
        OllamaStatus = await _ollama.IsReachableAsync() ? "Ollama connected" : "Ollama not detected — please start Ollama";

    private void OnEntry(object? _, LogEntry e)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _buffer.Enqueue(e);
            while (_buffer.Count > BufferSize) _buffer.Dequeue();
            LiveLog.Insert(0, e);
            while (LiveLog.Count > BufferSize) LiveLog.RemoveAt(LiveLog.Count - 1);

            // CollectionView automatically filters - no need to rebuild FilteredLogs

            // Create incident card for errors/warnings (without auto-analysis)
            if (e.Level is "Warning" or "Error" or "Critical")
            {
                var incidentKey = GetIncidentKey(e);
                if (_incidentKeys.Add(incidentKey))
                {
                    var card = new IncidentCardVm(e, "⏳ Not analyzed yet — click Analyze");
                    Incidents.Insert(0, card);
                    while (Incidents.Count > IncidentLimit)
                    {
                        var oldest = Incidents[^1];
                        Incidents.RemoveAt(Incidents.Count - 1);
                        _incidentKeys.Remove(GetIncidentKey(oldest.Entry));
                    }
                }
            }
        });
    }

    partial void OnLogFilterChanged(string value) => _filteredLogsView.Refresh();

    partial void OnLogLevelFilterChanged(string value) => _filteredLogsView.Refresh();

    [RelayCommand]
    private void RefreshLogs()
    {
        // CollectionView automatically refreshes when items change
        _filteredLogsView.Refresh();
    }

    [RelayCommand]
    private async Task AnalyzeIncidentAsync(IncidentCardVm? card)
    {
        if (card is null) return;

        // Show loading state
        IsAnalyzing = true;
        card.SetAnalyzing(true);
        card.SetAiSummary("Analysis in progress...");

        try
        {
            var prompt = PromptBuilder.BuildIncidentPrompt(card.Entry, ReadPlantLogs());
            using var timeout = new CancellationTokenSource(AiRequestTimeout);
            var summary = await _ollama.GenerateAsync(PromptBuilder.SystemPrompt, prompt, timeout.Token);
            card.SetAiSummary(summary);
        }
        catch (OperationCanceledException)
        {
            card.SetAiSummary("Analysis timed out after 2 minutes. The AI request was cancelled; click Analyze to retry.");
        }
        catch (Exception ex)
        {
            card.SetAiSummary($"Analysis failed: {ex.Message}");
        }
        finally
        {
            card.SetAnalyzing(false);
            IsAnalyzing = false;
        }
    }

    [RelayCommand]
    private void SetIncidentResolved(IncidentCardVm? card)
    {
        if (card is null) return;
        card.SetResolved(!card.IsResolved);
    }

    [RelayCommand]
    private void RemoveIncident(IncidentCardVm? card)
    {
        if (card is null) return;
        if (Incidents.Remove(card))
            _incidentKeys.Remove(GetIncidentKey(card.Entry));
    }

    private static string GetIncidentKey(LogEntry entry) =>
        $"{entry.Timestamp:O}|{entry.Level}|{entry.Message}";

    [RelayCommand]
    private async Task SendChat()
    {
        if (string.IsNullOrWhiteSpace(ChatInput) || IsProcessing) return;

        IsProcessing = true;
        var user = ChatInput.Trim();
        ChatInput = string.Empty;

        // Add user message to chat and session history
        Chat.Add(new ChatBubbleVm("operator", user));
        _sessionHistory.Add(new ChatSessionMessage { Role = "operator", Message = user });

        var historyContext = string.Join("\n", _sessionHistory
            .Take(Math.Max(0, _sessionHistory.Count - 1))
            .Select(message => $"{message.Role}: {message.Message}"));
        var incidentContext = string.Join("\n", Incidents.Select(card =>
            $"{card.Entry.Timestamp:o} [{card.Entry.Level}] ({card.Entry.Source}) " +
            $"ErrorCode={card.Entry.ErrorCode ?? "none"} Message={card.Entry.Message} AI={card.AiSummary}"));
        var prompt = PromptBuilder.BuildChatPrompt(
            user, ReadPlantLogs(), historyContext, PlantId, incidentContext);
        var pending = new ChatBubbleVm("assistant", "Response generation in progress...", true);
        Chat.Add(pending);

        try
        {
            using var timeout = new CancellationTokenSource(AiRequestTimeout);
            var answer = await _ollama.GenerateAsync(PromptBuilder.SystemPrompt, prompt, timeout.Token);
            pending.Text = answer;
            _sessionHistory.Add(new ChatSessionMessage { Role = "assistant", Message = answer });
        }
        catch (OperationCanceledException)
        {
            pending.Text = "AI response timed out after 2 minutes. The request was cancelled; please try again.";
        }
        catch (Exception ex)
        {
            pending.Text = $"AI response failed: {ex.Message}";
        }
        finally
        {
            pending.IsInProgress = false;
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void ClearChat()
    {
        Chat.Clear();
        _sessionHistory.Clear();
    }

    [RelayCommand]
    private async Task ExportChat()
    {
        var dlg = new SaveFileDialog
        {
            FileName = $"plant-agent-artifact-{PlantId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip",
            Filter = "ZIP Archive (*.zip)|*.zip",
            DefaultExt = ".zip"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var artifact = BuildArtifact(Incidents.FirstOrDefault(), ReadPlantLogs());
            await _writer.WriteZipAsync(artifact, dlg.FileName);
            MessageBox.Show($"Logs from the last 20 minutes and available chat history were exported to:\n{dlg.FileName}",
                "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export chat:\n{ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ExportArtifact(IncidentCardVm? card)
    {
        if (card is null) return;
        var artifact = BuildArtifact(card, ReadPlantLogs());
        var dlg = new SaveFileDialog
        {
            FileName = $"diagnostic-artifact-{PlantId}-{DateTime.UtcNow:yyyyMMddHHmmss}.zip",
            Filter = "ZIP Archive (*.zip)|*.zip"
        };
        if (dlg.ShowDialog() != true) return;
        await _writer.WriteZipAsync(artifact, dlg.FileName);
        MessageBox.Show($"Saved: {dlg.FileName}\n\nTransfer via USB, email, or file share to the PlantDoctor web portal.",
            "Artifact exported");
    }

    private IReadOnlyList<LogEntry> ReadPlantLogs()
    {
        try
        {
            return _tailer.ReadRecent(ExportLogWindow);
        }
        catch (IOException)
        {
            return _buffer.ToList();
        }
        catch (UnauthorizedAccessException)
        {
            return _buffer.ToList();
        }
    }

    private DiagnosticArtifact BuildArtifact(IncidentCardVm? card, IReadOnlyList<LogEntry> logs)
    {
        var contextEntry = card?.Entry ?? logs.LastOrDefault();
        return new DiagnosticArtifact
        {
            PlantId = PlantId,
            Incident = new IncidentInfo
            {
                DetectedAtUtc = contextEntry?.Timestamp ?? DateTime.UtcNow,
                Severity = card?.Entry.Level ?? "Warning",
                Source = contextEntry?.Source ?? "Application",
                ErrorCode = contextEntry?.ErrorCode,
                PrimaryMessage = contextEntry?.Message ?? "Plant context export"
            },
            RecentLogEntries = logs.Select(e => new RecentLogEntry
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
                ModelUsed = "mistral:latest",
                Summary = card?.AiSummary ?? string.Empty,
                SuspectedRootCause = card?.AiSummary ?? string.Empty,
                Confidence = card is null ? "Low" : "Medium"
            },
            OperatorChatTranscript = Chat
                .Where(message => !message.IsInProgress)
                .Select(c => new Core.Artifact.ChatMessage
                { Role = c.Role, Message = c.Text, TimestampUtc = c.TimestampUtc }).ToList()
        };
    }
}

public sealed partial class IncidentCardVm : ObservableObject
{
    public LogEntry Entry { get; }
    public string ResolutionActionText => IsResolved ? "Reopen" : "Set as Resolved";

    [ObservableProperty] private string _aiSummary;
    [ObservableProperty] private bool _isResolved;
    [ObservableProperty] private bool _isAnalyzing;

    public IncidentCardVm(LogEntry entry, string aiSummary)
    {
        Entry = entry;
        _aiSummary = aiSummary;
        _isResolved = false;
        _isAnalyzing = false;
    }

    public void SetAiSummary(string summary) => AiSummary = summary;
    public void SetResolved(bool resolved)
    {
        IsResolved = resolved;
        OnPropertyChanged(nameof(ResolutionActionText));
    }
    public void SetAnalyzing(bool analyzing) => IsAnalyzing = analyzing;
}

public sealed partial class ChatBubbleVm : ObservableObject
{
    public string Role { get; }
    public DateTime TimestampUtc { get; } = DateTime.UtcNow;

    [ObservableProperty] private string _text;
    [ObservableProperty] private bool _isInProgress;

    public ChatBubbleVm(string role, string text, bool isInProgress = false)
    {
        Role = role;
        _text = text;
        _isInProgress = isInProgress;
    }
}

public sealed record ChatSessionMessage
{
    public string Role { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
