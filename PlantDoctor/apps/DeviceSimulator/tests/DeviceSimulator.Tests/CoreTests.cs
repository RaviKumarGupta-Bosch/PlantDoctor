using System.Net;
using System.Net.Sockets;
using System.Text;
using DeviceSimulator.Core.Com;
using DeviceSimulator.Core.Errors;
using DeviceSimulator.Core.Scanner;
using DeviceSimulator.Core.Sensors;
using DeviceSimulator.Core.Transports;
using FluentAssertions;
using PlantDoctor.Contracts;
using Xunit;

namespace DeviceSimulator.Tests;

public class SensorSimulationServiceTests
{
    private static SensorSimulationService Create() => new(DefaultSensors.Build());

    [Fact]
    public void Build_returns_the_three_transport_backed_sensors()
    {
        DefaultSensors.Build().Select(s => s.Name)
            .Should().Equal("Temperature", "Vibration", "Pressure");
    }

    [Theory]
    [InlineData("Temperature", TransportChannel.NamedPipe)]
    [InlineData("Vibration", TransportChannel.Tcp)]
    [InlineData("Pressure", TransportChannel.Bluetooth)]
    public void Each_sensor_is_pinned_to_its_transport(string sensor, TransportChannel expected)
        => DefaultSensors.ChannelFor(sensor).Should().Be(expected);

    [Fact]
    public void ChannelFor_rejects_unknown_sensors()
        => FluentActions.Invoking(() => DefaultSensors.ChannelFor("Humidity"))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void Tick_stays_within_the_sensor_bounds()
    {
        var svc = Create();
        var def = svc.Sensors[0];

        for (var i = 0; i < 200; i++)
        {
            var reading = svc.Tick(def);
            reading.Value.Should().BeInRange(def.Min, def.Max);
            reading.Unit.Should().Be(def.Unit);
            reading.Name.Should().Be(def.Name);
        }
    }

    [Fact]
    public void FreezeSensor_pins_the_value()
    {
        var svc = Create();
        var def = svc.Sensors[0];
        var before = svc.Tick(def).Value;

        svc.FreezeSensor(def.Name);

        svc.Tick(def).Value.Should().Be(before);
        svc.Tick(def).Value.Should().Be(before);
    }

    [Fact]
    public void InjectOutOfRange_applies_once_then_recovers()
    {
        var svc = Create();
        var def = svc.Sensors[0];

        svc.InjectOutOfRange(def.Name);

        svc.Tick(def).Value.Should().Be(def.Max * 10);
        svc.Tick(def).Value.Should().BeInRange(def.Min, def.Max);
    }

    [Fact]
    public void Status_escalates_with_the_configured_thresholds()
    {
        var svc = new SensorSimulationService(new[]
        {
            new SensorDefinition { Name = "Hot", Unit = "°C", Min = 0, Max = 100, InitialValue = 0, WarningThreshold = 50, CriticalThreshold = 80 }
        });
        var def = svc.Sensors[0];

        svc.Tick(def).Status.Should().Be("OK");

        svc.InjectOutOfRange(def.Name);
        svc.Tick(def).Status.Should().Be("Critical");
    }
}

public class ScannerSimulatorTests
{
    [Fact]
    public void Starts_closed()
        => new ScannerSimulator().Status.Should().Be(ChannelStatus.Closed);

    [Fact]
    public void Connect_then_disconnect_tracks_status_and_port()
    {
        var scanner = new ScannerSimulator();

        scanner.Connect("COM4");
        scanner.Status.Should().Be(ChannelStatus.Connected);
        scanner.CurrentPort.Should().Be("COM4");

        scanner.Disconnect();
        scanner.Status.Should().Be(ChannelStatus.Closed);
    }

    [Fact]
    public void NextScan_emits_a_coded_event_on_the_current_port()
    {
        var scanner = new ScannerSimulator();
        scanner.Connect("COM2");
        ScanEventDto? raised = null;
        scanner.ScanReceived += (_, e) => raised = e;

        var scan = scanner.NextScan();

        raised.Should().BeSameAs(scan);
        scan.Port.Should().Be("COM2");
        scan.CodeType.Should().BeOneOf("Barcode", "QR");
        scan.Code.Should().MatchRegex(@"^(SKU|ASSET|BATCH|PART)-\d{5}$");
    }
}

public class ErrorInjectorTests
{
    private readonly ErrorInjector _injector = new();

    [Fact]
    public void SensorTimeout_is_reported_against_the_sensor()
    {
        var e = _injector.SensorTimeout("Temperature");
        e.ErrorCode.Should().Be("SENSOR_TIMEOUT");
        e.Source.Should().Be("Sensor");
        e.SensorName.Should().Be("Temperature");
    }

    [Fact]
    public void ComDisconnect_is_critical_and_names_the_port()
    {
        var e = _injector.ComDisconnect("COM3");
        e.ErrorCode.Should().Be("COM_DROPPED");
        e.Level.Should().Be("Critical");
        e.Message.Should().Contain("COM3");
    }

    [Fact]
    public void OverflowException_captures_a_real_stack_trace()
    {
        var e = _injector.OverflowException();
        e.ErrorCode.Should().Be("UNHANDLED_EXCEPTION");
        e.StackTrace.Should().NotBeNullOrWhiteSpace();
        e.StackTrace.Should().Contain(nameof(OverflowException));
    }

    [Fact]
    public void OutOfRangeValue_is_a_warning_carrying_the_value()
    {
        var e = _injector.OutOfRangeValue("Pressure", 200, "bar");
        e.ErrorCode.Should().Be("OUT_OF_RANGE");
        e.Level.Should().Be("Warning");
        e.Value.Should().Be("200");
    }
}

public class ComPortSimulatorTests
{
    [Fact]
    public void InjectDisconnect_moves_to_error_and_raises_a_frame()
    {
        var com = new ComPortSimulator();
        ComEventDto? raised = null;
        com.Connect("COM1");
        com.Event += (_, e) => raised = e;

        com.InjectDisconnect();

        com.Status.Should().Be(ChannelStatus.Error);
        raised!.RawFrame.Should().Be("dropped mid-transmission");
    }

    [Fact]
    public void NextFrame_is_a_direction_tagged_hex_telegram()
        => new ComPortSimulator().NextFrame()
            .Should().MatchRegex(@"^(TX|RX): ([0-9A-F]{2} ){7}[0-9A-F]{2}$");
}

public class TcpDeviceClientTests
{
    [Fact]
    public async Task Connect_fails_cleanly_when_the_main_application_is_not_listening()
    {
        using var client = new TcpDeviceClient(TransportChannel.Tcp);

        (await client.ConnectAsync()).Should().BeFalse();
        client.IsConnected.Should().BeFalse();
        client.LastError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Send_delivers_a_single_json_line_to_the_listener()
    {
        var listener = new TcpListener(IPAddress.Loopback, TransportEndpoints.PortFor(TransportChannel.Tcp));
        listener.Start();
        try
        {
            using var client = new TcpDeviceClient(TransportChannel.Tcp);
            var connectTask = client.ConnectAsync();
            using var server = await listener.AcceptTcpClientAsync();
            (await connectTask).Should().BeTrue();

            var reading = new SensorReadingDto { Name = "Vibration", Value = 12.5, Unit = "mm/s", TimestampUtc = DateTime.UtcNow };
            (await client.SendAsync(DeviceMessage.ForReading("Vibration", reading))).Should().BeTrue();

            using var reader = new StreamReader(server.GetStream(), Encoding.UTF8);
            var line = await reader.ReadLineAsync();

            WireFormat.TryParse(line!, out var message).Should().BeTrue();
            message.Type.Should().Be(DeviceMessageTypes.Sensor);
            message.Reading!.Name.Should().Be("Vibration");
            message.Reading.Value.Should().Be(12.5);
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task Send_fails_after_the_listener_goes_away()
    {
        var listener = new TcpListener(IPAddress.Loopback, TransportEndpoints.PortFor(TransportChannel.Bluetooth));
        listener.Start();

        using var client = new TcpDeviceClient(TransportChannel.Bluetooth);
        var connectTask = client.ConnectAsync();
        var server = await listener.AcceptTcpClientAsync();
        (await connectTask).Should().BeTrue();

        server.Dispose();
        listener.Stop();
        await Task.Delay(100);

        (await client.SendAsync(DeviceMessage.ForError("Pressure", new ErrorEventDto()))).Should().BeFalse();
        client.IsConnected.Should().BeFalse();
    }
}

public class WireFormatTests
{
    [Fact]
    public void Serialized_message_never_spans_more_than_one_line()
    {
        var line = WireFormat.Serialize(DeviceMessage.ForScan("Scanner", new ScanEventDto { Code = "SKU-12345" }));
        line.Should().NotContain("\n");
    }

    [Fact]
    public void Round_trips_each_message_kind()
    {
        foreach (var original in new[]
                 {
                     DeviceMessage.ForReading("Temperature", new SensorReadingDto { Name = "Temperature", Value = 80 }),
                     DeviceMessage.ForScan("Scanner", new ScanEventDto { Code = "PART-99999" }),
                     DeviceMessage.ForError("Temperature", new ErrorEventDto { ErrorCode = "SENSOR_TIMEOUT" })
                 })
        {
            WireFormat.TryParse(WireFormat.Serialize(original), out var parsed).Should().BeTrue();
            parsed.Type.Should().Be(original.Type);
            parsed.Device.Should().Be(original.Device);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{not json")]
    public void Rejects_malformed_frames(string line)
        => WireFormat.TryParse(line, out _).Should().BeFalse();
}
