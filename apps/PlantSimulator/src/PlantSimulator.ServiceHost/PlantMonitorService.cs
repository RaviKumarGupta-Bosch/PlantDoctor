using System.Collections.Concurrent;
using PlantSimulator.Contracts;
using PlantSimulator.Core.Logging;

namespace PlantSimulator.ServiceHost;

/// <summary>WCF service implementation. Fans out reports to the in-process logger and event bus.</summary>
public sealed class PlantMonitorService : IPlantMonitorService
{
    private readonly IPlantLogger _logger;
    private readonly ConcurrentDictionary<string, SensorReadingDto> _lastSensors = new();
    private ComEventDto? _lastCom;
    private ScanEventDto? _lastScan;

    public event EventHandler<ErrorEventDto>? ErrorReported;
    public event EventHandler<SensorReadingDto>? SensorReported;
    public event EventHandler<ComEventDto>? ComReported;
    public event EventHandler<ScanEventDto>? ScanReported;

    public PlantMonitorService(IPlantLogger logger) => _logger = logger;

    public void ReportSensorReading(SensorReadingDto dto)
    {
        _lastSensors[dto.Name] = dto;
        _logger.Log(dto);
        SensorReported?.Invoke(this, dto);
    }

    public void ReportError(ErrorEventDto dto)
    {
        _logger.Log(dto);
        ErrorReported?.Invoke(this, dto);
    }

    public void ReportComEvent(ComEventDto dto)
    {
        _lastCom = dto;
        _logger.Log(dto);
        ComReported?.Invoke(this, dto);
    }

    public void ReportScanEvent(ScanEventDto dto)
    {
        _lastScan = dto;
        _logger.Log(dto);
        ScanReported?.Invoke(this, dto);
    }

    public PlantSnapshotDto GetCurrentSnapshot() => new()
    {
        TakenAtUtc = DateTime.UtcNow,
        Sensors = _lastSensors.Values.ToList(),
        Com = _lastCom,
        LastScan = _lastScan
    };
}
