using PlantDoctor.Contracts;

namespace DeviceSimulator.Core.Sensors;

/// <summary>Random-walk based sensor simulator. Fault injection is thread-safe.</summary>
public sealed class SensorSimulationService : ISensorSimulationService
{
    private readonly Random _rng = new();
    private readonly Dictionary<string, double> _current = new();
    private readonly HashSet<string> _frozen = new();
    private readonly HashSet<string> _outOfRange = new();
    private readonly object _lock = new();

    public IReadOnlyList<SensorDefinition> Sensors { get; }

    public SensorSimulationService(IEnumerable<SensorDefinition> sensors)
    {
        Sensors = sensors.ToList();
        foreach (var s in Sensors) _current[s.Name] = s.InitialValue;
    }

    public SensorReadingDto Tick(SensorDefinition s)
    {
        lock (_lock)
        {
            double value = _current[s.Name];
            if (_outOfRange.Remove(s.Name))
                value = s.Max * 10;
            else if (!_frozen.Contains(s.Name))
            {
                var step = (s.Max - s.Min) * 0.01 * (_rng.NextDouble() - 0.5);
                value = Math.Clamp(value + step, s.Min, s.Max);
                _current[s.Name] = value;
            }

            var status = value >= s.CriticalThreshold ? "Critical"
                : value >= s.WarningThreshold ? "Warning" : "OK";

            return new SensorReadingDto
            {
                Name = s.Name,
                Value = value,
                Unit = s.Unit,
                Status = status,
                TimestampUtc = DateTime.UtcNow
            };
        }
    }

    public void FreezeSensor(string name) { lock (_lock) _frozen.Add(name); }
    public void InjectOutOfRange(string name) { lock (_lock) _outOfRange.Add(name); }
}
