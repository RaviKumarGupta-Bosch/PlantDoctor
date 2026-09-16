using PlantDoctor.Contracts;

namespace DeviceSimulator.Core.Sensors;

public interface ISensorSimulationService
{
    IReadOnlyList<SensorDefinition> Sensors { get; }
    SensorReadingDto Tick(SensorDefinition sensor);
    void FreezeSensor(string name);
    void InjectOutOfRange(string name);
}
