using PlantSimulator.Core.Sensors;

namespace PlantSimulator.UI;

/// <summary>
/// The 3 device blocks shown in the simulator UI, each routed to the main application
/// over a distinct transport: Temperature (Named Pipe), Vibration (TCP), Pressure (Bluetooth).
/// </summary>
internal static class DefaultSensors
{
    public static IEnumerable<SensorDefinition> Build() => new[]
    {
        new SensorDefinition { Name = "Temperature", Unit = "°C", Min = 0, Max = 200, InitialValue = 75, WarningThreshold = 140, CriticalThreshold = 170 },
        new SensorDefinition { Name = "Vibration",   Unit = "mm/s", Min = 0, Max = 50, InitialValue = 5, WarningThreshold = 30, CriticalThreshold = 40 },
        new SensorDefinition { Name = "Pressure",    Unit = "bar", Min = 0, Max = 20, InitialValue = 8, WarningThreshold = 15, CriticalThreshold = 18 }
    };
}

