namespace DeviceSimulator.Core.Sensors;

public sealed class SensorDefinition
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required double Min { get; init; }
    public required double Max { get; init; }
    public required double InitialValue { get; init; }
    public required double WarningThreshold { get; init; }
    public required double CriticalThreshold { get; init; }
}
