namespace PlantDoctor.Agent.Core.Logs;

/// <summary>Tails the newest plant-*.jsonl file in a folder, incrementally reading appended lines.
/// Uses file size tracking for reliable incremental reading.</summary>
public sealed class JsonlLogTailer : ILogTailer
{
    private readonly string _folder;
    private FileSystemWatcher? _watcher;
    private string? _currentFile;
    private long _lastSize;
    private readonly object _lock = new();
    private readonly System.Timers.Timer _pollTimer;
    private const int PollIntervalMs = 500; // Poll every 500ms for responsiveness

    public event EventHandler<LogEntry>? EntryReceived;

    public JsonlLogTailer(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(_folder);

        // Create timer for polling
        _pollTimer = new System.Timers.Timer(PollIntervalMs)
        {
            AutoReset = true,
            Enabled = false
        };
        _pollTimer.Elapsed += (_, _) => Poll();
    }

    public void Start()
    {
        OpenNewestFile();

        // Start FileSystemWatcher for immediate detection
        _watcher = new FileSystemWatcher(_folder, "plant-*.jsonl")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _watcher.Changed += (_, _) => Poll();
        _watcher.Created += (_, _) => { OpenNewestFile(); Poll(); };

        // Start timer-based polling as fallback
        _pollTimer.Start();
    }

    public IReadOnlyList<LogEntry> ReadRecent(TimeSpan window)
    {
        string? currentFile;
        lock (_lock)
        {
            currentFile = _currentFile;
        }

        if (currentFile is null || !File.Exists(currentFile)) return [];

        var entries = new List<LogEntry>();
        using var stream = new FileStream(currentFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            if (LogEntryParser.TryParse(line, out var entry)) entries.Add(entry);
        }

        if (entries.Count == 0) return entries;

        var cutoff = entries.Max(entry => entry.Timestamp) - window;
        return entries.Where(entry => entry.Timestamp >= cutoff).ToList();
    }

    private void OpenNewestFile()
    {
        lock (_lock)
        {
            var newest = new DirectoryInfo(_folder)
                .GetFiles("plant-*.jsonl")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            if (newest is null) return;

            var isNewFile = newest.FullName != _currentFile;
            _currentFile = newest.FullName;

            if (isNewFile)
            {
                // New file detected - start from beginning to catch any missed entries
                _lastSize = 0;
                System.Diagnostics.Debug.WriteLine($"JsonlLogTailer: Opened new log file: {_currentFile}");
            }
            else
            {
                // Same file - track its current size
                try
                {
                    _lastSize = newest.Length;
                    System.Diagnostics.Debug.WriteLine($"JsonlLogTailer: Tracking existing file: {_currentFile} (size: {_lastSize} bytes)");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"JsonlLogTailer: Failed to get file size: {ex.Message}");
                }
            }
        }
    }

    private void Poll()
    {
        lock (_lock)
        {
            if (_currentFile is null) return;

            try
            {
                var fileInfo = new FileInfo(_currentFile);
                if (!fileInfo.Exists) return;

                // Check if file has grown
                if (fileInfo.Length <= _lastSize) return;

                System.Diagnostics.Debug.WriteLine($"JsonlLogTailer: File grew from {_lastSize} to {fileInfo.Length} bytes");

                // Read only new lines
                var newLines = ReadNewLines(_currentFile, _lastSize);
                _lastSize = fileInfo.Length;

                foreach (var line in newLines)
                {
                    if (LogEntryParser.TryParse(line, out var entry))
                    {
                        EntryReceived?.Invoke(this, entry);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"JsonlLogTailer: Failed to parse line: {line.Substring(0, Math.Min(100, line.Length))}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"JsonlLogTailer: Error polling file: {ex.Message}");
            }
        }
    }

    private static IEnumerable<string> ReadNewLines(string filePath, long startPosition)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(startPosition, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    public void Dispose()
    {
        _pollTimer?.Stop();
        _pollTimer?.Dispose();
        _watcher?.Dispose();
    }
}
