namespace PlantDoctor.Contracts;

/// <summary>Identifies which physical/simulated transport a device communicates over.</summary>
public enum TransportChannel
{
    /// <summary>Temperature sensor — local IPC over a named pipe.</summary>
    NamedPipe,

    /// <summary>Vibration sensor — loopback TCP socket.</summary>
    Tcp,

    /// <summary>Pressure sensor — simulated Bluetooth RFCOMM (loopback TCP + air latency).</summary>
    Bluetooth,

    /// <summary>Barcode/QR scanner — simulated COM port bridged over loopback TCP.</summary>
    ComPort
}

/// <summary>
/// Wire addresses shared by the Device Simulator (client side) and the Plant Monitor
/// main application (listener side). Both apps must agree on these values.
/// </summary>
public static class TransportEndpoints
{
    public const string TemperaturePipeName = "PlantDoctor.Temperature";
    public const int VibrationTcpPort = 51001;
    public const int BluetoothSimPort = 51002;
    public const int ScannerComPort = 51003;

    public static readonly IReadOnlyList<TransportChannel> All = new[]
    {
        TransportChannel.NamedPipe,
        TransportChannel.Tcp,
        TransportChannel.Bluetooth,
        TransportChannel.ComPort
    };

    /// <summary>TCP port backing a channel. Throws for <see cref="TransportChannel.NamedPipe"/>, which has no port.</summary>
    public static int PortFor(TransportChannel channel) => channel switch
    {
        TransportChannel.Tcp => VibrationTcpPort,
        TransportChannel.Bluetooth => BluetoothSimPort,
        TransportChannel.ComPort => ScannerComPort,
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Channel is not TCP-backed.")
    };

    public static string DescribeFor(TransportChannel channel) => channel switch
    {
        TransportChannel.NamedPipe => $@"\\.\pipe\{TemperaturePipeName}",
        TransportChannel.Tcp => $"tcp://127.0.0.1:{VibrationTcpPort}",
        TransportChannel.Bluetooth => $"bt-sim://127.0.0.1:{BluetoothSimPort}",
        TransportChannel.ComPort => $"com-bridge://127.0.0.1:{ScannerComPort}",
        _ => "-"
    };

    public static string LabelFor(TransportChannel channel) => channel switch
    {
        TransportChannel.NamedPipe => "Named Pipe",
        TransportChannel.Tcp => "TCP Socket",
        TransportChannel.Bluetooth => "Bluetooth (Simulated)",
        TransportChannel.ComPort => "COM Port (Bridged)",
        _ => "Unknown"
    };
}

/// <summary>Status strings shared by both apps so the UI colour converters stay in sync.</summary>
public static class ChannelStatus
{
    public const string Closed = "Closed";
    public const string Stopped = "Stopped";
    public const string Listening = "Listening";
    public const string Connected = "Connected";
    public const string Error = "Error";
}
