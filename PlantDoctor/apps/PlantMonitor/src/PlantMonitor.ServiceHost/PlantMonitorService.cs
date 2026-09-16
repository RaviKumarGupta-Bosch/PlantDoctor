using System.Collections.Concurrent;
using PlantDoctor.Contracts;
using PlantMonitor.Core.Diagnostics;
using PlantMonitor.Core.Logging;

namespace PlantMonitor.ServiceHost;

/// <summary>
/// WCF service implementation and the single writer of the JSONL log. Everything the
/// ingestion hub receives is funnelled through here so the log stays the one integration
/// contract with PlantDoctor.Agent.
/// </summary>
public sealed class PlantMonitorService : IPlantMonitorService
{
    private readonly IPlantLogger _logger;
    private readonly ConcurrentDictionary<string, SensorReadingDto> _lastSensors = new();
    private ComEventDto? _lastCom;
    private ScanEventDto? _lastScan;
    private bool _reportingLogFailure;

    public event EventHandler<ErrorEventDto>? ErrorReported;
    public event EventHandler<SensorReadingDto>? SensorReported;
    public event EventHandler<ComEventDto>? ComReported;
    public event EventHandler<ScanEventDto>? ScanReported;

    public PlantMonitorService(IPlantLogger logger) => _logger = logger;

    public void ReportSensorReading(SensorReadingDto dto)
    {
        _lastSensors[dto.Name] = dto;
        TryLog(() => _logger.Log(dto));
        SensorReported?.Invoke(this, dto);
    }

    public void ReportError(ErrorEventDto dto)
    {
        TryLog(() => _logger.Log(dto));
        ErrorReported?.Invoke(this, dto);
    }

    public void ReportComEvent(ComEventDto dto)
    {
        _lastCom = dto;
        TryLog(() => _logger.Log(dto));
        ComReported?.Invoke(this, dto);
    }

    public void ReportScanEvent(ScanEventDto dto)
    {
        _lastScan = dto;
        TryLog(() => _logger.Log(dto));
        ScanReported?.Invoke(this, dto);
    }

    /// <summary>A failed write is surfaced as E907 but never re-logged, which would recurse.</summary>
    private void TryLog(Action write)
    {
        try
        {
            write();
        }
        catch (Exception ex)
        {
            if (_reportingLogFailure) return;
            _reportingLogFailure = true;
            try
            {
                ErrorReported?.Invoke(this,
                    DiagnosticEvents.FromApplication(ProcessingFault.LogWriteFailed, ex.Message, exception: ex));
            }
            finally { _reportingLogFailure = false; }
        }
    }

    public PlantSnapshotDto GetCurrentSnapshot() => new()
    {
        TakenAtUtc = DateTime.UtcNow,
        Sensors = _lastSensors.Values.ToList(),
        Com = _lastCom,
        LastScan = _lastScan
    };
}
