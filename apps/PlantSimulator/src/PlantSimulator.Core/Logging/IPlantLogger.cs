using PlantSimulator.Contracts;

namespace PlantSimulator.Core.Logging;

public interface IPlantLogger
{
    void Log(ErrorEventDto evt);
    void Log(SensorReadingDto reading);
    void Log(ComEventDto evt);
    void Log(ScanEventDto evt);
    string CurrentLogFilePath { get; }
}
