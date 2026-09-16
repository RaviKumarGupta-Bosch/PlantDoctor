using PlantSimulator.Contracts;

namespace PlantSimulator.Core.Errors;

public sealed class ErrorInjector : IErrorInjector
{
    public ErrorEventDto SensorTimeout(string name) => new()
    {
        TimestampUtc = DateTime.UtcNow,
        Level = "Error",
        Source = "Sensor",
        SensorName = name,
        ErrorCode = "SENSOR_TIMEOUT",
        Message = $"Sensor '{name}' has not reported within the expected interval."
    };

    public ErrorEventDto ComDisconnect(string port) => new()
    {
        TimestampUtc = DateTime.UtcNow,
        Level = "Critical",
        Source = "COM",
        ErrorCode = "COM_DROPPED",
        Message = $"COM port {port} disconnected during transmission."
    };

    public ErrorEventDto OverflowException()
    {
        try
        {
            checked { int x = int.MaxValue; x++; }
            return new ErrorEventDto(); // unreachable
        }
        catch (OverflowException ex)
        {
            return new ErrorEventDto
            {
                TimestampUtc = DateTime.UtcNow,
                Level = "Critical",
                Source = "Application",
                ErrorCode = "UNHANDLED_EXCEPTION",
                Message = ex.Message,
                StackTrace = ex.ToString()
            };
        }
    }

    public ErrorEventDto OutOfRangeValue(string name, double value, string unit) => new()
    {
        TimestampUtc = DateTime.UtcNow,
        Level = "Warning",
        Source = "Sensor",
        SensorName = name,
        Value = value.ToString("0.##"),
        ErrorCode = "OUT_OF_RANGE",
        Message = $"Sensor '{name}' reported {value}{unit} — outside valid bounds."
    };
}
