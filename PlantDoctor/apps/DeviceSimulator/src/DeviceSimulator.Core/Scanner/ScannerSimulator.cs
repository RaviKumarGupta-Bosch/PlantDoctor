using PlantDoctor.Contracts;

namespace DeviceSimulator.Core.Scanner;

/// <inheritdoc cref="IScannerSimulator"/>
public sealed class ScannerSimulator : IScannerSimulator
{
    private static readonly string[] Prefixes = { "SKU", "ASSET", "BATCH", "PART" };
    private readonly Random _rng = new();

    public string? CurrentPort { get; private set; }
    public string Status { get; private set; } = ChannelStatus.Closed;
    public event EventHandler<ScanEventDto>? ScanReceived;

    public void Connect(string port)
    {
        CurrentPort = port;
        Status = ChannelStatus.Connected;
    }

    public void Disconnect()
    {
        Status = ChannelStatus.Closed;
    }

    public ScanEventDto NextScan()
    {
        var scan = new ScanEventDto
        {
            TimestampUtc = DateTime.UtcNow,
            Port = CurrentPort ?? "-",
            Code = $"{Prefixes[_rng.Next(Prefixes.Length)]}-{_rng.Next(10000, 99999)}",
            CodeType = _rng.Next(2) == 0 ? "Barcode" : "QR"
        };
        ScanReceived?.Invoke(this, scan);
        return scan;
    }
}
