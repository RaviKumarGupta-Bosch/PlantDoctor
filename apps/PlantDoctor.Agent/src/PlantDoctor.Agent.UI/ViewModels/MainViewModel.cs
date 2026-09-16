using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading;
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
    private readonly List<ChatSessionMessage> _sessionHistory = new();
    private readonly HashSet<string> _analyzedIncidents = new();
    private const int MaxSessionHistory = 50;
    private int _incidentCounter;

    public ObservableCollection<LogEntry> LiveLog { get; } = new();
    public ObservableCollection<LogEntry> FilteredLogs { get; } = new();
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
            
            // Add to filtered logs as well
            FilteredLogs.Insert(0, e);
            while (FilteredLogs.Count > BufferSize) FilteredLogs.RemoveAt(FilteredLogs.Count - 1);
            
            if (e.Level is "Warning" or "Error" or "Critical")
                _ = AnalyzeAsync(e);
        });
    }

    partial void OnLogFilterChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnLogLevelFilterChanged(string value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = LiveLog.AsEnumerable();
        
        // Apply level filter
        if (LogLevelFilter != "All Levels")
        {
            filtered = filtered.Where(e => e.Level == LogLevelFilter);
        }
        
        // Apply text filter
        if (!string.IsNullOrWhiteSpace(LogFilter))
        {
            var filter = LogFilter.ToLower();
            filtered = filtered.Where(e => 
                e.Message.ToLower().Contains(filter) ||
                e.Level.ToLower().Contains(filter) ||
                e.Source.ToLower().Contains(filter) ||
                (e.SensorName != null && e.SensorName.ToLower().Contains(filter)));
        }
        
        // Rebuild filtered collection
        FilteredLogs.Clear();
        foreach (var entry in filtered)
        {
            FilteredLogs.Add(entry);
        }
    }

    [RelayCommand]
    private void RefreshLogs()
    {
        ApplyFilters();
    }

    private async Task AnalyzeAsync(LogEntry incident)
    {
        // Deduplication: skip if we've already analyzed this incident
        var incidentKey = $"{incident.Timestamp:O}|{incident.Level}|{incident.Message}";
        if (_analyzedIncidents.Contains(incidentKey))
            return;
        _analyzedIncidents.Add(incidentKey);

        // Show loading state
        IsAnalyzing = true;
        var incidentId = Interlocked.Increment(ref _incidentCounter);
        var loadingCard = new IncidentCardVm(incident, "⏳ Analyzing...");
        
        Application.Current.Dispatcher.Invoke(() =>
            Incidents.Insert(0, loadingCard));

        // Run AI analysis in background with timeout
        try
        {
            var prompt = PromptBuilder.BuildIncidentPrompt(incident, _buffer);
            
            // Use a task with timeout instead of waiting indefinitely
            var analysisTask = _ollama.GenerateAsync(PromptBuilder.SystemPrompt, prompt);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            
            var completedTask = await Task.WhenAny(analysisTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                // Timeout - add placeholder
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var timeoutCard = new IncidentCardVm(incident, "⚠️ Analysis timed out — AI may be busy. Try again later.");
                    var idx = Incidents.IndexOf(loadingCard);
                    if (idx >= 0) Incidents[idx] = timeoutCard;
                });
                return;
            }
            
            var summary = analysisTask.Result;
            
            // Update the incident card with AI analysis
            Application.Current.Dispatcher.Invoke(() =>
            {
                var newCard = new IncidentCardVm(incident, summary);
                var idx = Incidents.IndexOf(loadingCard);
                if (idx >= 0) Incidents[idx] = newCard;
            });
        }
        catch (Exception ex)
        {
            // Handle any exceptions and show error
            Application.Current.Dispatcher.Invoke(() =>
            {
                var errorCard = new IncidentCardVm(incident, $"❌ Analysis failed: {ex.Message}");
                var idx = Incidents.IndexOf(loadingCard);
                if (idx >= 0) Incidents[idx] = errorCard;
            });
        }
        finally
        {
            IsAnalyzing = false;
        }
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
            ModelUsed = "mistral:latest",
            Summary = card.AiSummary,
            SuspectedRootCause = card.AiSummary,
            Confidence = "Medium"
        },
        OperatorChatTranscript = Chat.Select(c => new Core.Artifact.ChatMessage
        { Role = c.Role, Message = c.Text, TimestampUtc = c.TimestampUtc }).ToList()
    };
}

public sealed record IncidentCardVm(LogEntry Entry, string AiSummary);
public sealed record ChatBubbleVm(string Role, string Text)
{
    public DateTime TimestampUtc { get; } = DateTime.UtcNow;
}

public sealed record ChatSessionMessage
{
    public string Role { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
