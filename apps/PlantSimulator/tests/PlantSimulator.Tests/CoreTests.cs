using FluentAssertions;
using PlantSimulator.Core.Errors;
using PlantSimulator.Core.Sensors;
using Xunit;

namespace PlantSimulator.Tests;

public class SensorSimulationServiceTests
{
    private static SensorDefinition Def() => new()
    { Name = "T", Unit = "C", Min = 0, Max = 100, InitialValue = 50, WarningThreshold = 80, CriticalThreshold = 90 };

    [Fact]
    public void Tick_ReturnsValueWithinBounds()
    {
        var svc = new SensorSimulationService(new[] { Def() });
        for (var i = 0; i < 100; i++)
            svc.Tick(Def()).Value.Should().BeInRange(0, 100);
    }

    [Fact]
    public void FreezeSensor_KeepsValueConstant()
    {
        var d = Def();
        var svc = new SensorSimulationService(new[] { d });
        svc.FreezeSensor(d.Name);
        var first = svc.Tick(d).Value;
        var later = svc.Tick(d).Value;
        later.Should().Be(first);
    }

    [Fact]
    public void InjectOutOfRange_ProducesValueAboveMax()
    {
        var d = Def();
        var svc = new SensorSimulationService(new[] { d });
        svc.InjectOutOfRange(d.Name);
        svc.Tick(d).Value.Should().BeGreaterThan(d.Max);
    }
}

public class ErrorInjectorTests
{
    [Fact]
    public void OverflowException_CarriesStackTrace()
    {
        var e = new ErrorInjector().OverflowException();
        e.StackTrace.Should().NotBeNullOrWhiteSpace();
        e.ErrorCode.Should().Be("UNHANDLED_EXCEPTION");
    }

    [Fact]
    public void SensorTimeout_CarriesSensorName()
    {
        var e = new ErrorInjector().SensorTimeout("Temp");
        e.SensorName.Should().Be("Temp");
        e.ErrorCode.Should().Be("SENSOR_TIMEOUT");
    }
}
