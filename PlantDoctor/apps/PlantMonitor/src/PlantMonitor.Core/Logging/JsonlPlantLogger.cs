using System.Text.Json;
using PlantDoctor.Contracts;

namespace PlantMonitor.Core.Logging;

/// <summary>
/// JSONL rolling file logger owned exclusively by the main application. This file is the
/// integration contract with PlantDoctor.Agent, which tails it with a FileSystemWatcher.
/// </summary>
public sealed class JsonlPlantLogger : IPlantLogger, IDisposable
{
    private readonly string _folder;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private DateOnly _openDate;

    public string CurrentLogFilePath => Path.Combine(_folder, $"plant-{DateTime.UtcNow:yyyyMMdd}.jsonl");

    public JsonlPlantLogger(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(_folder);
    }

    public void Log(ErrorEventDto e) => WriteLine(new
    {
        timestamp = e.TimestampUtc,
        level = e.Level,
        source = e.Source,
        sensorName = e.SensorName,
        value = e.Value,
        errorCode = e.ErrorCode,
        deviceErrorCode = e.DeviceErrorCode,
        message = e.Message,
        stackTrace = e.StackTrace
    });

    public void Log(SensorReadingDto r) => WriteLine(new
    {
        timestamp = r.TimestampUtc,
        level = r.Status == "OK" ? "Info" : r.Status,
        source = "Sensor",
        sensorName = r.Name,
        value = r.Value.ToString("0.###"),
        message = $"{r.Name}={r.Value:0.##}{r.Unit}"
    });

    public void Log(ComEventDto e) => WriteLine(new
    {
        timestamp = e.TimestampUtc,
        level = e.Status == "Error" ? "Error" : "Info",
        source = "COM",
        message = $"COM {e.Port} {e.Status} {e.RawFrame}"
    });

    public void Log(ScanEventDto e) => WriteLine(new
    {
        timestamp = e.TimestampUtc,
        level = "Info",
        source = "Scanner",
        message = $"Scan {e.CodeType} '{e.Code}' on {e.Port}"
    });

    private void WriteLine(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        lock (_lock)
        {
            EnsureWriter();
            _writer!.WriteLine(json);
            _writer.Flush();
        }
    }

    private void EnsureWriter()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (_writer is null || today != _openDate)
        {
            _writer?.Dispose();
            _writer = new StreamWriter(new FileStream(CurrentLogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
            _openDate = today;
        }
    }

    public void Dispose() { lock (_lock) { _writer?.Dispose(); _writer = null; } }
}
