using PlantSimulator.Core.Sensors;

namespace PlantSimulator.UI;

internal static class DefaultSensors
{
    public static IEnumerable<SensorDefinition> Build() => new[]
    {
        new SensorDefinition { Name = "Temperature", Unit = "°C", Min = 0, Max = 200, InitialValue = 75, WarningThreshold = 140, CriticalThreshold = 170 },
        new SensorDefinition { Name = "Pressure",    Unit = "bar", Min = 0, Max = 20, InitialValue = 8, WarningThreshold = 15, CriticalThreshold = 18 },
        new SensorDefinition { Name = "VibrationX",  Unit = "mm/s", Min = 0, Max = 50, InitialValue = 5, WarningThreshold = 30, CriticalThreshold = 40 },
        new SensorDefinition { Name = "VibrationY",  Unit = "mm/s", Min = 0, Max = 50, InitialValue = 5, WarningThreshold = 30, CriticalThreshold = 40 },
        new SensorDefinition { Name = "RPM",         Unit = "rpm", Min = 0, Max = 6000, InitialValue = 1500, WarningThreshold = 5000, CriticalThreshold = 5500 },
        new SensorDefinition { Name = "Voltage",     Unit = "V", Min = 0, Max = 500, InitialValue = 230, WarningThreshold = 400, CriticalThreshold = 450 }
    };
}
