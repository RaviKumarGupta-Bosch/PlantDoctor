using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using PlantDoctor.Contracts;
using PlantMonitor.Core.Diagnostics;
using PlantMonitor.Core.Ingest;
using PlantMonitor.Core.Logging;
using PlantMonitor.ServiceHost;
using Xunit;

namespace PlantMonitor.Tests;

public class DiagnosticCodesTests
{
    [Theory]
    [InlineData(TransportChannel.NamedPipe, DeviceFault.Timeout, "E101")]
    [InlineData(TransportChannel.NamedPipe, DeviceFault.OutOfRange, "E102")]
    [InlineData(TransportChannel.Tcp, DeviceFault.Timeout, "E201")]
    [InlineData(TransportChannel.Bluetooth, DeviceFault.OutOfRange, "E302")]
    [InlineData(TransportChannel.ComPort, DeviceFault.CommunicationDropped, "E403")]
    [InlineData(TransportChannel.ComPort, DeviceFault.Unclassified, "E499")]
    public void Device_codes_use_the_block_then_fault_layout(TransportChannel channel, DeviceFault fault, string expected)
        => DiagnosticCodes.ForDevice(channel, fault).Should().Be(expected);

    [Theory]
    [InlineData(ProcessingFault.MalformedFrame, "E901")]
    [InlineData(ProcessingFault.UnexpectedDisconnect, "E906")]
    [InlineData(ProcessingFault.LogWriteFailed, "E907")]
    [InlineData(ProcessingFault.UnhandledProcessingError, "E999")]
    public void Application_codes_live_in_the_E9xx_block(ProcessingFault fault, string expected)
        => DiagnosticCodes.ForApplication(fault).Should().Be(expected);

    [Theory]
    [InlineData("SENSOR_TIMEOUT", DeviceFault.Timeout)]
    [InlineData("out_of_range", DeviceFault.OutOfRange)]
    [InlineData("COM_DROPPED", DeviceFault.CommunicationDropped)]
    [InlineData("UNHANDLED_EXCEPTION", DeviceFault.UnhandledException)]
    [InlineData("SOMETHING_NEW", DeviceFault.Unclassified)]
    [InlineData(null, DeviceFault.Unclassified)]
    public void Device_codes_are_classified_into_the_catalog_taxonomy(string? reported, DeviceFault expected)
        => DiagnosticCodes.Classify(reported).Should().Be(expected);

    [Fact]
    public void Every_catalog_code_is_unique()
    {
        var codes = DiagnosticCodes.All().Select(e => e.Code).ToList();
        codes.Should().OnlyHaveUniqueItems();
        codes.Should().HaveCountGreaterThan(20);
    }

    [Fact]
    public void Each_device_owns_its_own_block()
    {
        foreach (var channel in TransportEndpoints.All)
        {
            var device = DiagnosticCodes.DeviceNameFor(channel);
            DiagnosticCodes.All()
                .Where(e => e.Source == device)
                .Should().OnlyContain(e => e.Code.StartsWith("E" + DiagnosticCodes.ForDevice(channel, DeviceFault.Timeout)[1]));
        }
    }

    [Fact]
    public void FromDevice_stamps_the_catalog_code_and_keeps_the_original()
    {
        var reported = new ErrorEventDto { ErrorCode = "SENSOR_TIMEOUT", Message = "Sensor 'Pressure' timed out." };

        var coded = DiagnosticEvents.FromDevice(TransportChannel.Bluetooth, reported);

        coded.ErrorCode.Should().Be("E301");
        coded.DeviceErrorCode.Should().Be("SENSOR_TIMEOUT");
        coded.SensorName.Should().Be("Pressure");
        coded.Message.Should().Be("Sensor 'Pressure' timed out.");
    }

    [Fact]
    public void FromDevice_falls_back_to_the_unclassified_code_for_unknown_faults()
        => DiagnosticEvents.FromDevice(TransportChannel.Tcp, new ErrorEventDto { ErrorCode = "WOBBLE" })
            .ErrorCode.Should().Be("E299");

    [Fact]
    public void FromApplication_describes_the_fault_and_names_the_channel()
    {
        var error = DiagnosticEvents.FromApplication(
            ProcessingFault.MalformedFrame, "{bad", TransportChannel.ComPort);

        error.ErrorCode.Should().Be("E901");
        error.Source.Should().Be("Application");
        error.SensorName.Should().Be("Scanner");
        error.Message.Should().Contain("not valid JSON").And.Contain("{bad");
    }
}

public class CommunicationHubTests
{
    private const TransportChannel Channel = TransportChannel.ComPort;

    private static async Task<TcpClient> ConnectDeviceAsync()
    {
        var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", TransportEndpoints.PortFor(Channel));
        return client;
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(25);
        }
    }

    [Fact]
    public void Channels_start_out_not_listening()
    {
        using var hub = new CommunicationHub();
        foreach (var channel in TransportEndpoints.All)
            hub.IsListening(channel).Should().BeFalse();
    }

    [Fact]
    public void StopChannel_reports_stopped_and_is_idempotent()
    {
        using var hub = new CommunicationHub();
        var statuses = new List<string>();
        hub.ChannelStatusChanged += (_, e) => { if (e.Channel == Channel) statuses.Add(e.Status); };

        hub.StartChannel(Channel);
        hub.StopChannel(Channel);
        hub.StopChannel(Channel);

        hub.IsListening(Channel).Should().BeFalse();
        statuses.Count(s => s == ChannelStatus.Stopped).Should().Be(1);
    }

    [Fact]
    public async Task Disconnecting_a_sensor_channel_leaves_the_scanner_channel_listening()
    {
        using var hub = new CommunicationHub();
        hub.Start();
        await WaitForAsync(() => TransportEndpoints.All.All(hub.IsListening));

        hub.StopChannel(TransportChannel.Tcp);

        hub.IsListening(TransportChannel.Tcp).Should().BeFalse();
        hub.IsListening(TransportChannel.ComPort).Should().BeTrue();
        hub.IsListening(TransportChannel.NamedPipe).Should().BeTrue();
    }

    [Fact]
    public async Task Received_frames_are_surfaced_as_typed_messages()
    {
        using var hub = new CommunicationHub();
        var received = new List<DeviceMessage>();
        hub.MessageReceived += (_, e) => { lock (received) received.Add(e.Message); };

        hub.StartChannel(Channel);
        await WaitForAsync(() => hub.IsListening(Channel));

        using var device = await ConnectDeviceAsync();
        await using var writer = new StreamWriter(device.GetStream(), new UTF8Encoding(false)) { AutoFlush = true };
        await writer.WriteLineAsync(WireFormat.Serialize(
            DeviceMessage.ForScan("Scanner", new ScanEventDto { Code = "SKU-42424", CodeType = "QR", Port = "COM3" })));

        await WaitForAsync(() => { lock (received) return received.Count > 0; });

        lock (received)
        {
            received.Should().HaveCount(1);
            received[0].Type.Should().Be(DeviceMessageTypes.Scan);
            received[0].Scan!.Code.Should().Be("SKU-42424");
        }

        hub.StopChannel(Channel);
    }

    [Fact]
    public async Task Malformed_frames_are_ignored_without_killing_the_channel()
    {
        using var hub = new CommunicationHub();
        var received = 0;
        var processingErrors = new List<ErrorEventDto>();
        hub.MessageReceived += (_, _) => Interlocked.Increment(ref received);
        hub.ProcessingErrorRaised += (_, e) => { lock (processingErrors) processingErrors.Add(e); };

        hub.StartChannel(Channel);
        await WaitForAsync(() => hub.IsListening(Channel));

        using var device = await ConnectDeviceAsync();
        await using var writer = new StreamWriter(device.GetStream(), new UTF8Encoding(false)) { AutoFlush = true };
        await writer.WriteLineAsync("{not json");
        await writer.WriteLineAsync(WireFormat.Serialize(
            DeviceMessage.ForError("Scanner", new ErrorEventDto { ErrorCode = "COM_DROPPED" })));

        await WaitForAsync(() => Volatile.Read(ref received) > 0);

        Volatile.Read(ref received).Should().Be(1);
        hub.IsListening(Channel).Should().BeTrue();

        lock (processingErrors)
        {
            processingErrors.Should().ContainSingle(e => e.ErrorCode == "E901")
                .Which.SensorName.Should().Be("Scanner");
        }

        hub.StopChannel(Channel);
    }

    [Fact]
    public async Task A_frame_from_the_wrong_device_is_rejected_as_E904()
    {
        using var hub = new CommunicationHub();
        var received = 0;
        var processingErrors = new List<ErrorEventDto>();
        hub.MessageReceived += (_, _) => Interlocked.Increment(ref received);
        hub.ProcessingErrorRaised += (_, e) => { lock (processingErrors) processingErrors.Add(e); };

        hub.StartChannel(Channel);
        await WaitForAsync(() => hub.IsListening(Channel));

        using var device = await ConnectDeviceAsync();
        await using var writer = new StreamWriter(device.GetStream(), new UTF8Encoding(false)) { AutoFlush = true };
        await writer.WriteLineAsync(WireFormat.Serialize(
            DeviceMessage.ForReading("Temperature", new SensorReadingDto { Name = "Temperature" })));

        await WaitForAsync(() => { lock (processingErrors) return processingErrors.Count > 0; });

        Volatile.Read(ref received).Should().Be(0);
        lock (processingErrors)
        {
            processingErrors.Should().ContainSingle(e => e.ErrorCode == "E904");
        }

        hub.StopChannel(Channel);
    }

    [Fact]
    public async Task A_frame_missing_its_payload_is_rejected_as_E903()
    {
        using var hub = new CommunicationHub();
        var processingErrors = new List<ErrorEventDto>();
        hub.ProcessingErrorRaised += (_, e) => { lock (processingErrors) processingErrors.Add(e); };

        hub.StartChannel(Channel);
        await WaitForAsync(() => hub.IsListening(Channel));

        using var device = await ConnectDeviceAsync();
        await using var writer = new StreamWriter(device.GetStream(), new UTF8Encoding(false)) { AutoFlush = true };
        await writer.WriteLineAsync("""{"type":"scan","device":"Scanner"}""");

        await WaitForAsync(() => { lock (processingErrors) return processingErrors.Count > 0; });

        lock (processingErrors)
        {
            processingErrors.Should().ContainSingle(e => e.ErrorCode == "E903");
        }

        hub.StopChannel(Channel);
    }

    [Fact]
    public async Task A_device_dropping_its_socket_is_reported_as_E906()
    {
        using var hub = new CommunicationHub();
        var processingErrors = new List<ErrorEventDto>();
        hub.ProcessingErrorRaised += (_, e) => { lock (processingErrors) processingErrors.Add(e); };

        hub.StartChannel(Channel);
        await WaitForAsync(() => hub.IsListening(Channel));

        var device = await ConnectDeviceAsync();
        await WaitForAsync(() => false, 200); // let the hub accept the connection
        device.Dispose();

        await WaitForAsync(() => { lock (processingErrors) return processingErrors.Count > 0; });

        lock (processingErrors)
        {
            processingErrors.Should().ContainSingle(e => e.ErrorCode == "E906")
                .Which.SensorName.Should().Be("Scanner");
        }

        hub.StopChannel(Channel);
    }

    [Fact]
    public async Task Stopping_a_channel_drops_the_connected_device()
    {
        using var hub = new CommunicationHub();
        hub.StartChannel(Channel);
        await WaitForAsync(() => hub.IsListening(Channel));

        using var device = await ConnectDeviceAsync();
        await using var writer = new StreamWriter(device.GetStream(), new UTF8Encoding(false)) { AutoFlush = true };
        await writer.WriteLineAsync(WireFormat.Serialize(DeviceMessage.ForScan("Scanner", new ScanEventDto())));

        hub.StopChannel(Channel);

        await WaitForAsync(() => !device.Connected ||
                                 (device.Client.Poll(0, SelectMode.SelectRead) && device.Client.Available == 0));

        var socketIsDead = !device.Connected ||
                           (device.Client.Poll(0, SelectMode.SelectRead) && device.Client.Available == 0);
        socketIsDead.Should().BeTrue();
    }
}

public class JsonlPlantLoggerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "PlantMonitorTests", Guid.NewGuid().ToString("N"));

    /// <summary>Reads while the logger still holds the file open, exactly as PlantDoctor.Agent tails it.</summary>
    private static string[] ReadSharedLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line) lines.Add(line);
        return lines.ToArray();
    }

    [Fact]
    public void Writes_one_json_object_per_event()
    {
        using (var log = new JsonlPlantLogger(_folder))
        {
            log.Log(new SensorReadingDto { Name = "Temperature", Value = 80.5, Unit = "°C", Status = "OK", TimestampUtc = DateTime.UtcNow });
            log.Log(new ErrorEventDto { ErrorCode = "SENSOR_TIMEOUT", Message = "boom", TimestampUtc = DateTime.UtcNow });
            log.Log(new ScanEventDto { Code = "SKU-11111", CodeType = "QR", Port = "COM3", TimestampUtc = DateTime.UtcNow });

            var lines = ReadSharedLines(log.CurrentLogFilePath);
            lines.Should().HaveCount(3);
            lines[0].Should().Contain("\"source\":\"Sensor\"").And.Contain("Temperature");
            lines[1].Should().Contain("SENSOR_TIMEOUT");
            lines[2].Should().Contain("\"source\":\"Scanner\"").And.Contain("SKU-11111");
        }
    }

    [Fact]
    public void Non_ok_readings_are_logged_at_their_own_level()
    {
        using var log = new JsonlPlantLogger(_folder);
        log.Log(new SensorReadingDto { Name = "Pressure", Value = 19, Unit = "bar", Status = "Critical", TimestampUtc = DateTime.UtcNow });

        ReadSharedLines(log.CurrentLogFilePath).Single().Should().Contain("\"level\":\"Critical\"");
    }

    [Fact]
    public void Errors_carry_both_the_catalog_code_and_the_device_code()
    {
        using var log = new JsonlPlantLogger(_folder);
        log.Log(DiagnosticEvents.FromDevice(TransportChannel.NamedPipe,
            new ErrorEventDto { ErrorCode = "SENSOR_TIMEOUT", Message = "timed out", TimestampUtc = DateTime.UtcNow }));

        var line = ReadSharedLines(log.CurrentLogFilePath).Single();
        line.Should().Contain("\"errorCode\":\"E101\"");
        line.Should().Contain("\"deviceErrorCode\":\"SENSOR_TIMEOUT\"");
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}

public class PlantMonitorServiceTests
{
    private sealed class RecordingLogger : IPlantLogger
    {
        public List<object> Logged { get; } = new();
        public string CurrentLogFilePath => "in-memory";
        public void Log(ErrorEventDto evt) => Logged.Add(evt);
        public void Log(SensorReadingDto reading) => Logged.Add(reading);
        public void Log(ComEventDto evt) => Logged.Add(evt);
        public void Log(ScanEventDto evt) => Logged.Add(evt);
    }

    [Fact]
    public void Every_report_is_logged_and_republished()
    {
        var logger = new RecordingLogger();
        var service = new PlantMonitorService(logger);
        SensorReadingDto? sensor = null;
        ScanEventDto? scan = null;
        service.SensorReported += (_, e) => sensor = e;
        service.ScanReported += (_, e) => scan = e;

        service.ReportSensorReading(new SensorReadingDto { Name = "Vibration", Value = 3 });
        service.ReportScanEvent(new ScanEventDto { Code = "BATCH-00001" });

        logger.Logged.Should().HaveCount(2);
        sensor!.Name.Should().Be("Vibration");
        scan!.Code.Should().Be("BATCH-00001");
    }

    [Fact]
    public void Snapshot_keeps_the_latest_value_per_sensor()
    {
        var service = new PlantMonitorService(new RecordingLogger());

        service.ReportSensorReading(new SensorReadingDto { Name = "Temperature", Value = 70 });
        service.ReportSensorReading(new SensorReadingDto { Name = "Temperature", Value = 90 });
        service.ReportSensorReading(new SensorReadingDto { Name = "Pressure", Value = 8 });

        var snapshot = service.GetCurrentSnapshot();

        snapshot.Sensors.Should().HaveCount(2);
        snapshot.Sensors.Single(s => s.Name == "Temperature").Value.Should().Be(90);
    }
}
