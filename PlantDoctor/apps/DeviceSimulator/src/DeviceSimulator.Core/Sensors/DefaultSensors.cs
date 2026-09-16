using PlantDoctor.Contracts;

namespace DeviceSimulator.Core.Sensors;

/// <summary>
/// The three simulated sensor devices, each pinned to the transport it reports over:
/// Temperature (named pipe), Vibration (TCP), Pressure (simulated Bluetooth).
/// </summary>
public static class DefaultSensors
{
    public static IReadOnlyList<SensorDefinition> Build() => new[]
    {
        new SensorDefinition { Name = "Temperature", Unit = "°C", Min = 0, Max = 200, InitialValue = 75, WarningThreshold = 140, CriticalThreshold = 170 },
        new SensorDefinition { Name = "Vibration",   Unit = "mm/s", Min = 0, Max = 50, InitialValue = 5, WarningThreshold = 30, CriticalThreshold = 40 },
        new SensorDefinition { Name = "Pressure",    Unit = "bar", Min = 0, Max = 20, InitialValue = 8, WarningThreshold = 15, CriticalThreshold = 18 }
    };

    public static TransportChannel ChannelFor(string sensorName) => sensorName switch
    {
        "Temperature" => TransportChannel.NamedPipe,
        "Vibration" => TransportChannel.Tcp,
        "Pressure" => TransportChannel.Bluetooth,
        _ => throw new ArgumentOutOfRangeException(nameof(sensorName), sensorName, "Unknown sensor device.")
    };
}
