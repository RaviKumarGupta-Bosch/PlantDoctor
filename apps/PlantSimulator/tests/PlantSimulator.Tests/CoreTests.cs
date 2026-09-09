using FluentAssertions;
using PlantSimulator.Core.Com;
using PlantSimulator.Core.Errors;
using PlantSimulator.Core.Logging;
using PlantSimulator.Core.Sensors;
using PlantSimulator.Contracts;
using Xunit;
using System.IO;
using System.Text.Json;

namespace PlantSimulator.Tests;

// ============================================================================
// SensorSimulationService Tests - Comprehensive Coverage
// ============================================================================

public class SensorSimulationServiceTests
{
    private static SensorDefinition Def() => new()
    { Name = "T", Unit = "C", Min = 0, Max = 100, InitialValue = 50, WarningThreshold = 80, CriticalThreshold = 90 };

    // --- POSITIVE TESTS ---

    [Fact]
    public void Tick_ReturnsValueWithinBounds()
    {
        var svc = new SensorSimulationService(new[] { Def() });
        for (var i = 0; i < 100; i++)
            svc.Tick(Def()).Value.Should().BeInRange(0, 100);
    }

    [Fact]
    public void Tick_ProducesCorrectStatus()
    {
        var d = Def();
        var svc = new SensorSimulationService(new[] { d });
        var reading = svc.Tick(d);
        reading.Status.Should().BeOneOf("OK", "Warning", "Critical");
    }

    [Fact]
    public void Tick_ProducesValidTimestamp()
    {
        var d = Def();
        var svc = new SensorSimulationService(new[] { d });
        var reading = svc.Tick(d);
        reading.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Tick_ProducesValidNameAndUnit()
    {
        var d = Def();
        var svc = new SensorSimulationService(new[] { d });
        var reading = svc.Tick(d);
        reading.Name.Should().Be("T");
        reading.Unit.Should().Be("C");
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

    [Fact]
    public void MultipleSensors_Independent()
    {
        var defs = new[]
        {
            Def(),
            new SensorDefinition { Name = "P", Unit = "bar", Min = 0, Max = 20, InitialValue = 10, WarningThreshold = 15, CriticalThreshold = 18 }
        };
        var svc = new SensorSimulationService(defs);
        var r1 = svc.Tick(defs[0]);
        var r2 = svc.Tick(defs[1]);
        r1.Name.Should().Be("T");
        r2.Name.Should().Be("P");
    }

    [Fact]
    public void Sensors_Property_ReturnsAllSensors()
    {
        var defs = new[] { Def(), new SensorDefinition { Name = "H", Unit = "%", Min = 0, Max = 100, InitialValue = 50, WarningThreshold = 80, CriticalThreshold = 90 } };
        var svc = new SensorSimulationService(defs);
        svc.Sensors.Should().HaveCount(2);
    }

    [Fact]
    public void Tick_WithZeroRange_ProducesConstantValue()
    {
        var d = new SensorDefinition { Name = "Z", Unit = "m", Min = 10, Max = 10, InitialValue = 10, WarningThreshold = 10, CriticalThreshold = 10 };
        var svc = new SensorSimulationService(new[] { d });
        for (var i = 0; i < 10; i++)
            svc.Tick(d).Value.Should().Be(10);
    }

    [Fact]
    public void Tick_WithNegativeRange_ProducesValuesInRange()
    {
        var d = new SensorDefinition { Name = "N", Unit = "°", Min = -20, Max = -10, InitialValue = -15, WarningThreshold = -12, CriticalThreshold = -11 };
        var svc = new SensorSimulationService(new[] { d });
        for (var i = 0; i < 50; i++)
            svc.Tick(d).Value.Should().BeInRange(-20, -10);
    }

    // --- NEGATIVE TESTS ---

    [Fact]
    public void Tick_WithEmptySensorsList_ProducesNoReadings()
    {
        var svc = new SensorSimulationService(Array.Empty<SensorDefinition>());
        svc.Sensors.Should().BeEmpty();
    }

    [Fact]
    public void FreezeSensor_UnknownSensor_DoesNotThrow()
    {
        var svc = new SensorSimulationService(new[] { Def() });
        var ex = Record.Exception(() => svc.FreezeSensor("NonExistent"));
        ex.Should().BeNull();
    }

    [Fact]
    public void InjectOutOfRange_UnknownSensor_DoesNotThrow()
    {
        var svc = new SensorSimulationService(new[] { Def() });
        var ex = Record.Exception(() => svc.InjectOutOfRange("NonExistent"));
        ex.Should().BeNull();
    }

    // --- EDGE CASES ---

    [Fact]
    public void Tick_InitialValueIsReturnedFirst()
    {
        var d = Def();
        var svc = new SensorSimulationService(new[] { d });
        var reading = svc.Tick(d);
        reading.Value.Should().BeApproximately(d.InitialValue, 1.0);
    }

    [Fact]
    public void FreezeSensor_CanBeFrozenMultipleTimes()
    {
        var d = Def();
        var svc = new SensorSimulationService(new[] { d });
        svc.FreezeSensor(d.Name);
        svc.FreezeSensor(d.Name);
        var first = svc.Tick(d).Value;
        var later = svc.Tick(d).Value;
        later.Should().Be(first);
    }

    [Fact]
    public void InjectOutOfRange_CanBeInjectedMultipleTimes()
    {
        var d = Def();
        var svc = new SensorSimulationService(new[] { d });
        svc.InjectOutOfRange(d.Name);
        svc.InjectOutOfRange(d.Name);
        svc.Tick(d).Value.Should().BeGreaterThan(d.Max);
    }

    [Fact]
    public void Tick_ProducesConsistentStepSize()
    {
        var d = Def();
        var svc = new SensorSimulationService(new[] { d });
        var reading1 = svc.Tick(d);
        var reading2 = svc.Tick(d);
        Math.Abs(reading2.Value - reading1.Value).Should().BeLessThan((d.Max - d.Min) * 0.02);
    }
}

// ============================================================================
// ErrorInjector Tests - Comprehensive Coverage
// ============================================================================

public class ErrorInjectorTests
{
    // --- POSITIVE TESTS ---

    [Fact]
    public void OverflowException_CarriesStackTrace()
    {
        var e = new ErrorInjector().OverflowException();
        e.StackTrace.Should().NotBeNullOrWhiteSpace();
        e.ErrorCode.Should().Be("UNHANDLED_EXCEPTION");
        e.Level.Should().Be("Critical");
    }

    [Fact]
    public void OverflowException_ProducesValidTimestamp()
    {
        var e = new ErrorInjector().OverflowException();
        e.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void OverflowException_CarriesMessage()
    {
        var e = new ErrorInjector().OverflowException();
        e.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SensorTimeout_CarriesSensorName()
    {
        var e = new ErrorInjector().SensorTimeout("Temp");
        e.SensorName.Should().Be("Temp");
        e.ErrorCode.Should().Be("SENSOR_TIMEOUT");
        e.Level.Should().Be("Error");
    }

    [Fact]
    public void SensorTimeout_ProducesDescriptiveMessage()
    {
        var e = new ErrorInjector().SensorTimeout("Humidity");
        e.Message.Should().Contain("Humidity");
        e.Message.Should().Contain("not reported");
    }

    [Fact]
    public void ComDisconnect_CarriesPort()
    {
        var e = new ErrorInjector().ComDisconnect("COM3");
        e.Message.Should().Contain("COM3");
        e.ErrorCode.Should().Be("COM_DROPPED");
        e.Level.Should().Be("Critical");
    }

    [Fact]
    public void ComDisconnect_ProducesDescriptiveMessage()
    {
        var e = new ErrorInjector().ComDisconnect("COM1");
        e.Message.Should().Contain("disconnected");
    }

    [Fact]
    public void OutOfRangeValue_CarriesValue()
    {
        var e = new ErrorInjector().OutOfRangeValue("T", 999, "C");
        e.Value.Should().Be("999");
        e.ErrorCode.Should().Be("OUT_OF_RANGE");
        e.Level.Should().Be("Warning");
    }

    [Fact]
    public void OutOfRangeValue_ProducesDescriptiveMessage()
    {
        var e = new ErrorInjector().OutOfRangeValue("pH", 14.5, "");
        e.Message.Should().Contain("pH");
        e.Message.Should().Contain("14.5");
    }

    [Fact]
    public void AllErrorTypes_ProduceValidTimestamps()
    {
        var injector = new ErrorInjector();
        var errors = new[]
        {
            injector.OverflowException(),
            injector.SensorTimeout("T"),
            injector.ComDisconnect("COM1"),
            injector.OutOfRangeValue("T", 100, "C")
        };
        foreach (var e in errors)
            e.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    // --- NEGATIVE TESTS ---

    [Fact]
    public void SensorTimeout_EmptySensorName_DoesNotThrow()
    {
        var e = new ErrorInjector().SensorTimeout("");
        e.SensorName.Should().BeEmpty();
        e.ErrorCode.Should().Be("SENSOR_TIMEOUT");
    }

    [Fact]
    public void ComDisconnect_EmptyPort_DoesNotThrow()
    {
        var e = new ErrorInjector().ComDisconnect("");
        e.ErrorCode.Should().Be("COM_DROPPED");
    }

    [Fact]
    public void OutOfRangeValue_NegativeValue_DoesNotThrow()
    {
        var e = new ErrorInjector().OutOfRangeValue("T", -50, "C");
        e.Value.Should().Be("-50");
        e.ErrorCode.Should().Be("OUT_OF_RANGE");
    }

    [Fact]
    public void OutOfRangeValue_ZeroValue_DoesNotThrow()
    {
        var e = new ErrorInjector().OutOfRangeValue("T", 0, "C");
        e.Value.Should().Be("0");
        e.ErrorCode.Should().Be("OUT_OF_RANGE");
    }

    // --- EDGE CASES ---

    [Fact]
    public void OverflowException_SourceIsApplication()
    {
        var e = new ErrorInjector().OverflowException();
        e.Source.Should().Be("Application");
    }

    [Fact]
    public void SensorTimeout_SourceIsSensor()
    {
        var e = new ErrorInjector().SensorTimeout("T");
        e.Source.Should().Be("Sensor");
    }

    [Fact]
    public void ComDisconnect_SourceIsCOM()
    {
        var e = new ErrorInjector().ComDisconnect("COM1");
        e.Source.Should().Be("COM");
    }

    [Fact]
    public void OutOfRangeValue_SourceIsSensor()
    {
        var e = new ErrorInjector().OutOfRangeValue("T", 100, "C");
        e.Source.Should().Be("Sensor");
    }
}

// ============================================================================
// ComPortSimulator Tests - Comprehensive Coverage
// ============================================================================

public class ComPortSimulatorTests
{
    // --- POSITIVE TESTS ---

    [Fact]
    public void Connect_SetsStatus()
    {
        var sim = new ComPortSimulator();
        sim.Connect("COM5");
        sim.Status.Should().Be("Connected");
        sim.CurrentPort.Should().Be("COM5");
    }

    [Fact]
    public void Disconnect_SetsStatus()
    {
        var sim = new ComPortSimulator();
        sim.Connect("COM1");
        sim.Disconnect();
        sim.Status.Should().Be("Disconnected");
    }

    [Fact]
    public void InjectDisconnect_SetsErrorStatus()
    {
        var sim = new ComPortSimulator();
        sim.Connect("COM1");
        sim.InjectDisconnect();
        sim.Status.Should().Be("Error");
    }

    [Fact]
    public void NextFrame_ProducesValidFrame()
    {
        var sim = new ComPortSimulator();
        sim.Connect("COM1");
        var frame = sim.NextFrame();
        frame.Should().MatchRegex(@"^(TX|RX): [0-9A-F]{2}( [0-9A-F]{2}){7}$");
    }

    [Fact]
    public void Connect_RaisesEvent()
    {
        var sim = new ComPortSimulator();
        ComEventDto? capturedEvent = null;
        sim.Event += (_, e) => capturedEvent = e;
        sim.Connect("COM2");
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Port.Should().Be("COM2");
        capturedEvent.Status.Should().Be("Connected");
    }

    [Fact]
    public void Disconnect_RaisesEvent()
    {
        var sim = new ComPortSimulator();
        sim.Connect("COM1");
        ComEventDto? capturedEvent = null;
        sim.Event += (_, e) => capturedEvent = e;
        sim.Disconnect();
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Status.Should().Be("Disconnected");
    }

    [Fact]
    public void InjectDisconnect_RaisesEventWithRawFrame()
    {
        var sim = new ComPortSimulator();
        sim.Connect("COM1");
        ComEventDto? capturedEvent = null;
        sim.Event += (_, e) => capturedEvent = e;
        sim.InjectDisconnect();
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Status.Should().Be("Error");
        capturedEvent.RawFrame.Should().Contain("dropped");
    }

    [Fact]
    public void NextFrame_ProducesMultipleFrames()
    {
        var sim = new ComPortSimulator();
        sim.Connect("COM1");
        var frames = new List<string>();
        for (var i = 0; i < 10; i++)
            frames.Add(sim.NextFrame());
        frames.Should().BeEquivalentTo(frames.Where(f => f.StartsWith("TX: ") || f.StartsWith("RX: ")).ToList());
    }

    [Fact]
    public void NextFrame_ProducesHexBytes()
    {
        var sim = new ComPortSimulator();
        sim.Connect("COM1");
        var frame = sim.NextFrame();
        var parts = frame.Split(' ');
        parts.Length.Should().Be(9); // TX/RX prefix + 8 hex bytes
        parts[0].Should().BeOneOf("TX:", "RX:");
        foreach (var part in parts.Skip(1))
            part.Should().MatchRegex(@"^[0-9A-F]{2}$");
    }

    // --- NEGATIVE TESTS ---

    [Fact]
    public void Connect_AfterDisconnect_Reconnects()
    {
        var sim = new ComPortSimulator();
        sim.Connect("COM1");
        sim.Disconnect();
        sim.Connect("COM2");
        sim.Status.Should().Be("Connected");
        sim.CurrentPort.Should().Be("COM2");
    }

    [Fact]
    public void Disconnect_WhenAlreadyDisconnected_DoesNotThrow()
    {
        var sim = new ComPortSimulator();
        var ex = Record.Exception(() => sim.Disconnect());
        ex.Should().BeNull();
    }

    [Fact]
    public void InjectDisconnect_WhenNotConnected_DoesNotThrow()
    {
        var sim = new ComPortSimulator();
        var ex = Record.Exception(() => sim.InjectDisconnect());
        ex.Should().BeNull();
        sim.Status.Should().Be("Error");
    }

    // --- EDGE CASES ---

    [Fact]
    public void Connect_WithEmptyPort_SetsEmptyPort()
    {
        var sim = new ComPortSimulator();
        sim.Connect("");
        sim.CurrentPort.Should().BeEmpty();
        sim.Status.Should().Be("Connected");
    }

    [Fact]
    public void Connect_WithLongPortName_DoesNotThrow()
    {
        var sim = new ComPortSimulator();
        sim.Connect("COM99");
        sim.CurrentPort.Should().Be("COM99");
    }

    [Fact]
    public void MultipleConnects_UpdatesPort()
    {
        var sim = new ComPortSimulator();
        sim.Connect("COM1");
        sim.Connect("COM2");
        sim.Connect("COM3");
        sim.CurrentPort.Should().Be("COM3");
    }

    [Fact]
    public void Status_InitialValueIsDisconnected()
    {
        var sim = new ComPortSimulator();
        sim.Status.Should().Be("Disconnected");
    }

    [Fact]
    public void CurrentPort_InitialValueIsNull()
    {
        var sim = new ComPortSimulator();
        sim.CurrentPort.Should().BeNull();
    }
}

// ============================================================================
// JsonlPlantLogger Tests - Comprehensive Coverage
// ============================================================================

public class JsonlPlantLoggerTests
{
    // --- POSITIVE TESTS ---

    [Fact]
    public void Log_CreatesLogFile()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"PlantDoctor-test-{Guid.NewGuid()}");
        var logger = new JsonlPlantLogger(folder);
        var evt = new ErrorEventDto
        {
            TimestampUtc = DateTime.UtcNow,
            Level = "Error",
            Source = "Sensor",
            Message = "test"
        };
        logger.Log(evt);
        var logFilePath = logger.CurrentLogFilePath;
        logger.Dispose();

        try
        {
            logFilePath.Should().NotBeNullOrEmpty();
            File.Exists(logFilePath).Should().BeTrue();
            var lines = File.ReadAllLines(logFilePath);
            lines.Should().HaveCount(1);
            lines[0].Should().Contain("test");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Log_ErrorEvent_WritesCorrectJson()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"PlantDoctor-test-{Guid.NewGuid()}");
        var logger = new JsonlPlantLogger(folder);
        var evt = new ErrorEventDto
        {
            TimestampUtc = DateTime.UtcNow,
            Level = "Critical",
            Source = "COM",
            ErrorCode = "COM_DROPPED",
            Message = "port dropped"
        };
        logger.Log(evt);
        var logFilePath = logger.CurrentLogFilePath;
        logger.Dispose();

        try
        {
            var lines = File.ReadAllLines(logFilePath);
            var json = lines[0];
            var obj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            obj!["level"].GetString().Should().Be("Critical");
            obj!["source"].GetString().Should().Be("COM");
            obj!["errorCode"].GetString().Should().Be("COM_DROPPED");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Log_SensorReading_WritesCorrectJson()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"PlantDoctor-test-{Guid.NewGuid()}");
        var logger = new JsonlPlantLogger(folder);
        var reading = new SensorReadingDto
        {
            Name = "Temp",
            Value = 25.5,
            Unit = "C",
            Status = "OK",
            TimestampUtc = DateTime.UtcNow
        };
        logger.Log(reading);
        var logFilePath = logger.CurrentLogFilePath;
        logger.Dispose();

        try
        {
            var lines = File.ReadAllLines(logFilePath);
            var json = lines[0];
            var obj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            obj!["sensorName"].GetString().Should().Be("Temp");
            obj!["source"].GetString().Should().Be("Sensor");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Log_ComEvent_WritesCorrectJson()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"PlantDoctor-test-{Guid.NewGuid()}");
        var logger = new JsonlPlantLogger(folder);
        var evt = new ComEventDto
        {
            TimestampUtc = DateTime.UtcNow,
            Port = "COM1",
            Status = "Connected",
            RawFrame = "AA BB CC"
        };
        logger.Log(evt);
        var logFilePath = logger.CurrentLogFilePath;
        logger.Dispose();

        try
        {
            var lines = File.ReadAllLines(logFilePath);
            var json = lines[0];
            var obj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            obj!["source"].GetString().Should().Be("COM");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Log_MultipleEntries_AppendsToSameFile()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"PlantDoctor-test-{Guid.NewGuid()}");
        var logger = new JsonlPlantLogger(folder);
        logger.Log(new ErrorEventDto { TimestampUtc = DateTime.UtcNow, Level = "Error", Source = "Test", Message = "msg1" });
        logger.Log(new ErrorEventDto { TimestampUtc = DateTime.UtcNow, Level = "Warning", Source = "Test", Message = "msg2" });
        var logFilePath = logger.CurrentLogFilePath;
        logger.Dispose();

        try
        {
            var lines = File.ReadAllLines(logFilePath);
            lines.Should().HaveCount(2);
            lines[0].Should().Contain("msg1");
            lines[1].Should().Contain("msg2");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void CurrentLogFilePath_ContainsDate()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"PlantDoctor-test-{Guid.NewGuid()}");
        var logger = new JsonlPlantLogger(folder);
        var path = logger.CurrentLogFilePath;
        logger.Dispose();
        path.Should().Contain($"plant-{DateTime.UtcNow:yyyyMMdd}.jsonl");
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch { }
    }

    // --- NEGATIVE TESTS ---

    [Fact]
    public void Log_ToNonExistentFolder_CreatesFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"PlantDoctor-test-{Guid.NewGuid()}", "sub");
        var logger = new JsonlPlantLogger(folder);
        logger.Log(new ErrorEventDto { TimestampUtc = DateTime.UtcNow, Level = "Error", Source = "Test", Message = "test" });
        Directory.Exists(folder).Should().BeTrue();
        logger.Dispose();
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch { }
    }

    [Fact]
    public void Dispose_MultipleTimes_DoesNotThrow()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"PlantDoctor-test-{Guid.NewGuid()}");
        var logger = new JsonlPlantLogger(folder);
        var ex1 = Record.Exception(() => logger.Dispose());
        var ex2 = Record.Exception(() => logger.Dispose());
        ex1.Should().BeNull();
        ex2.Should().BeNull();
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch { }
    }

    // --- EDGE CASES ---

    [Fact]
    public void Log_EmptyMessage_DoesNotThrow()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"PlantDoctor-test-{Guid.NewGuid()}");
        var logger = new JsonlPlantLogger(folder);
        var ex = Record.Exception(() => logger.Log(new ErrorEventDto { TimestampUtc = DateTime.UtcNow, Level = "Error", Source = "Test", Message = "" }));
        ex.Should().BeNull();
        logger.Dispose();
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch { }
    }

    [Fact]
    public void Log_NullSensorName_DoesNotThrow()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"PlantDoctor-test-{Guid.NewGuid()}");
        var logger = new JsonlPlantLogger(folder);
        var ex = Record.Exception(() => logger.Log(new ErrorEventDto { TimestampUtc = DateTime.UtcNow, Level = "Error", Source = "Test", SensorName = null! }));
        ex.Should().BeNull();
        logger.Dispose();
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch { }
    }

    [Fact]
    public void Log_WithNullStackTrace_DoesNotThrow()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"PlantDoctor-test-{Guid.NewGuid()}");
        var logger = new JsonlPlantLogger(folder);
        var ex = Record.Exception(() => logger.Log(new ErrorEventDto { TimestampUtc = DateTime.UtcNow, Level = "Error", Source = "Test", StackTrace = null! }));
        ex.Should().BeNull();
        logger.Dispose();
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch { }
    }
}

// ============================================================================
// SensorDefinition Tests
// ============================================================================

public class SensorDefinitionTests
{
    [Fact]
    public void SensorDefinition_CanBeCreatedWithAllProperties()
    {
        var def = new SensorDefinition
        {
            Name = "Temp",
            Unit = "°C",
            Min = -10,
            Max = 50,
            InitialValue = 25,
            WarningThreshold = 40,
            CriticalThreshold = 45
        };
        def.Name.Should().Be("Temp");
        def.Unit.Should().Be("°C");
        def.Min.Should().Be(-10);
        def.Max.Should().Be(50);
        def.InitialValue.Should().Be(25);
        def.WarningThreshold.Should().Be(40);
        def.CriticalThreshold.Should().Be(45);
    }

    [Fact]
    public void SensorDefinition_WithDefaultValues_DoesNotThrow()
    {
        var def = new SensorDefinition
        {
            Name = "",
            Unit = "",
            Min = 0,
            Max = 0,
            InitialValue = 0,
            WarningThreshold = 0,
            CriticalThreshold = 0
        };
        def.Name.Should().BeEmpty();
    }
}

// ============================================================================
// DTO Tests
// ============================================================================

public class DtoTests
{
    [Fact]
    public void SensorReadingDto_CanBeCreatedWithAllProperties()
    {
        var dto = new SensorReadingDto
        {
            Name = "Temp",
            Value = 25.5,
            Unit = "C",
            Status = "OK",
            TimestampUtc = DateTime.UtcNow
        };
        dto.Name.Should().Be("Temp");
        dto.Value.Should().Be(25.5);
        dto.Unit.Should().Be("C");
        dto.Status.Should().Be("OK");
    }

    [Fact]
    public void ErrorEventDto_CanBeCreatedWithAllProperties()
    {
        var dto = new ErrorEventDto
        {
            TimestampUtc = DateTime.UtcNow,
            Level = "Critical",
            Source = "Application",
            SensorName = "Temp",
            Value = "999",
            ErrorCode = "OUT_OF_RANGE",
            Message = "Sensor out of range",
            StackTrace = "at Program.Main()"
        };
        dto.Level.Should().Be("Critical");
        dto.Source.Should().Be("Application");
        dto.SensorName.Should().Be("Temp");
        dto.ErrorCode.Should().Be("OUT_OF_RANGE");
    }

    [Fact]
    public void ComEventDto_CanBeCreatedWithAllProperties()
    {
        var dto = new ComEventDto
        {
            TimestampUtc = DateTime.UtcNow,
            Port = "COM1",
            Status = "Connected",
            RawFrame = "AA BB CC"
        };
        dto.Port.Should().Be("COM1");
        dto.Status.Should().Be("Connected");
        dto.RawFrame.Should().Be("AA BB CC");
    }
}
