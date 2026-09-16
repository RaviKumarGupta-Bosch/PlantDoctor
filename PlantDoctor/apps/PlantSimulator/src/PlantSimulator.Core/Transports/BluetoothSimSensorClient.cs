using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using PlantSimulator.Contracts;

namespace PlantSimulator.Core.Transports;

/// <summary>
/// Pressure sensor's transport: simulates a Bluetooth RFCOMM channel. Real Bluetooth
/// pairing requires physical radios, so this emulates the same publish semantics over a
/// dedicated loopback TCP port with an added pairing/air latency and synthetic RSSI.
/// </summary>
public sealed class BluetoothSimSensorClient : ISensorTransportClient
{
    private readonly Random _rng = new();
    public string ChannelLabel => "Bluetooth (Simulated)";
    public string? LastError { get; private set; }
    public int LastRssi { get; private set; } = -60;

    public async Task<bool> SendAsync(SensorReadingDto reading, CancellationToken ct = default)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, CommunicationHub.BluetoothSimPort, ct);
            await Task.Delay(_rng.Next(10, 40), ct); // simulated pairing / air latency
            using var stream = client.GetStream();
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reading) + "\n");
            await stream.WriteAsync(bytes, ct);
            LastRssi = -_rng.Next(40, 90);
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }
}
