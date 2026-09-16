using PlantSimulator.Contracts;

namespace PlantSimulator.Core.Sensors;

public interface ISensorSimulationService
{
    IReadOnlyList<SensorDefinition> Sensors { get; }
    SensorReadingDto Tick(SensorDefinition sensor);
    void FreezeSensor(string name);
    void InjectOutOfRange(string name);
}
