namespace PlantDoctor.Agent.Core.Logs;

/// <summary>
/// Interface for tailing (watching) JSONL log files for new entries.
/// Implementations use FileSystemWatcher to detect file changes and
/// incrementally read new lines.
/// </summary>
public interface ILogTailer : IDisposable
{
    /// <summary>
    /// Event raised when a new log entry is detected.
    /// </summary>
    event EventHandler<LogEntry>? EntryReceived;

    /// <summary>
    /// Starts watching the log folder for new entries.
    /// </summary>
    void Start();

    /// <summary>
    /// Reads entries from the active log file within the requested time window.
    /// </summary>
    IReadOnlyList<LogEntry> ReadRecent(TimeSpan window);
}
