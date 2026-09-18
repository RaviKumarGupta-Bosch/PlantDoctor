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
    private readonly ILogTailer _tailer;
    private readonly IOllamaClient _ollama;
    private readonly IArtifactWriter _writer;
    private readonly Queue<LogEntry> _buffer = new();
    private readonly List<ChatSessionMessage> _sessionHistory = new();
    private readonly HashSet<string> _analyzedIncidents = new();
    private const int MaxSessionHistory = 50;
    private int _incidentCounter;

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
        _tailer.EntryReceived += OnEntry;
        _tailer.Start();
        
        // Set up efficient CollectionView for live filtering
        _filteredLogsView = new CollectionViewSource() { Source = LiveLog }.View;
        _filteredLogsView.Filter = ApplyFilterPredicate;
        
        _ = CheckOllamaAsync();
    }

    private bool ApplyFilterPredicate(object item)
    {
        if (item is not LogEntry entry) return false;
        
        // Apply level filter
        if (LogLevelFilter != "All Levels" && entry.Level != LogLevelFilter)
            return false;
        
        // Apply text filter
        if (!string.IsNullOrWhiteSpace(LogFilter))
        {
            var filter = LogFilter.ToLower();
            var matches = entry.Message.ToLower().Contains(filter) ||
                entry.Level.ToLower().Contains(filter) ||
                entry.Source.ToLower().Contains(filter) ||
                (entry.SensorName != null && entry.SensorName.ToLower().Contains(filter));
            return matches;
        }
        
        return true;
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
            
            // CollectionView automatically filters - no need to rebuild FilteredLogs
            
            // Create incident card for errors/warnings (without auto-analysis)
            if (e.Level is "Warning" or "Error" or "Critical")
            {
                var incidentKey = $"{e.Timestamp:O}|{e.Level}|{e.Message}";
                if (!_analyzedIncidents.Contains(incidentKey))
                {
                    _analyzedIncidents.Add(incidentKey);
                    var card = new IncidentCardVm(e, "⏳ Not analyzed yet — click Analyze");
                    Incidents.Insert(0, card);
                }
            }
        });
    }

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
        
        // If already analyzed, skip
        if (!string.IsNullOrEmpty(card.AiSummary) && card.AiSummary != "⏳ Not analyzed yet — click Analyze")
            return;
        
        // Show loading state
        IsAnalyzing = true;
        card.SetAnalyzing(true);
        
        try
        {
            var prompt = PromptBuilder.BuildIncidentPrompt(card.Entry, _buffer);
            
            var analysisTask = _ollama.GenerateAsync(PromptBuilder.SystemPrompt, prompt);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            
            var completedTask = await Task.WhenAny(analysisTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                card.SetAiSummary("⚠️ Analysis timed out — AI may be busy. Try again later.");
                card.SetAnalyzing(false);
                return;
            }
            
            var summary = await analysisTask;
            card.SetAiSummary(summary);
            card.SetAnalyzing(false);
        }
        catch (Exception ex)
        {
            card.SetAiSummary($"❌ Analysis failed: {ex.Message}");
            card.SetAnalyzing(false);
        }
        finally
        {
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
    private async Task SendChat()
    {
        if (string.IsNullOrWhiteSpace(ChatInput) || IsProcessing) return;
        
        IsProcessing = true;
        var user = ChatInput.Trim();
        ChatInput = string.Empty;
        
        // Add user message to chat and session history
        Chat.Add(new ChatBubbleVm("operator", user));
        _sessionHistory.Add(new ChatSessionMessage { Role = "operator", Message = user });
        
        // Build prompt with session history (last 10 messages for context)
        var recentHistory = _sessionHistory.TakeLast(10).Select(m => $"{m.Role}: {m.Message}").ToList();
        var historyContext = recentHistory.Any() ? $"\n\nRecent conversation:\n{string.Join("\n", recentHistory)}" : "";
        var prompt = PromptBuilder.BuildChatPrompt(user, _buffer, historyContext);
        
        try
        {
            var answer = await _ollama.GenerateAsync(PromptBuilder.SystemPrompt, prompt);
            Chat.Add(new ChatBubbleVm("assistant", answer));
            _sessionHistory.Add(new ChatSessionMessage { Role = "assistant", Message = answer });
            
            // Trim session history to max size
            while (_sessionHistory.Count > MaxSessionHistory)
                _sessionHistory.RemoveAt(0);
        }
        catch (Exception ex)
        {
            Chat.Add(new ChatBubbleVm("assistant", $"(error: {ex.Message})"));
        }
        finally
        {
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
        if (!Chat.Any())
        {
            MessageBox.Show("No chat history to export.", "Export Chat", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            FileName = $"chat-session-{PlantId}-{DateTime.UtcNow:yyyyMMddHHmmss}.txt",
            Filter = "Text (*.txt)|*.txt|JSON (*.json)|*.json"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            if (dlg.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var chatMessages = Chat.Select(c => new Core.Artifact.ChatMessage
                {
                    Role = c.Role,
                    Message = c.Text,
                    TimestampUtc = c.TimestampUtc
                }).ToList();

                var json = System.Text.Json.JsonSerializer.Serialize(chatMessages, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(dlg.FileName, json);
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"PlantDoctor Agent - Chat Session");
                sb.AppendLine($"Plant ID: {PlantId}");
                sb.AppendLine($"Exported: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
                sb.AppendLine(new string('=', 60));
                sb.AppendLine();

                foreach (var bubble in Chat)
                {
                    sb.AppendLine($"[{bubble.Role.ToUpper()}] {bubble.TimestampUtc:HH:mm:ss}");
                    sb.AppendLine(bubble.Text);
                    sb.AppendLine(new string('-', 40));
                    sb.AppendLine();
                }

                await File.WriteAllTextAsync(dlg.FileName, sb.ToString());
            }

            MessageBox.Show($"Chat exported to:\n{dlg.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
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
        var artifact = BuildArtifact(card);
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
            ModelUsed = "mistral:latest",
            Summary = card.AiSummary,
            SuspectedRootCause = card.AiSummary,
            Confidence = "Medium"
        },
        OperatorChatTranscript = Chat.Select(c => new Core.Artifact.ChatMessage
        { Role = c.Role, Message = c.Text, TimestampUtc = c.TimestampUtc }).ToList()
    };
}

public sealed class IncidentCardVm : ObservableObject
{
    public LogEntry Entry { get; }
    
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
    public void SetResolved(bool resolved) => IsResolved = resolved;
    public void SetAnalyzing(bool analyzing) => IsAnalyzing = analyzing;
}

public sealed record ChatBubbleVm(string Role, string Text)
{
    public DateTime TimestampUtc { get; } = DateTime.UtcNow;
}

public sealed record ChatSessionMessage
{
    public string Role { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
