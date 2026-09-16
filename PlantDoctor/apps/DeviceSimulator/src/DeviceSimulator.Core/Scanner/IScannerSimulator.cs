using PlantDoctor.Contracts;

namespace DeviceSimulator.Core.Scanner;

/// <summary>Simulates a barcode/QR scanner device attached over a COM port.</summary>
public interface IScannerSimulator
{
    string? CurrentPort { get; }
    string Status { get; }
    event EventHandler<ScanEventDto>? ScanReceived;

    void Connect(string port);
    void Disconnect();
    ScanEventDto NextScan();
}
