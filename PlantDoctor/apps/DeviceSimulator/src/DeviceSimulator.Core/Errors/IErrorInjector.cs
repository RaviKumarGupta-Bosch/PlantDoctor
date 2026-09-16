using PlantDoctor.Contracts;

namespace DeviceSimulator.Core.Errors;

public interface IErrorInjector
{
    ErrorEventDto SensorTimeout(string sensorName);
    ErrorEventDto ComDisconnect(string port);
    ErrorEventDto OverflowException();
    ErrorEventDto OutOfRangeValue(string sensorName, double value, string unit);
}
