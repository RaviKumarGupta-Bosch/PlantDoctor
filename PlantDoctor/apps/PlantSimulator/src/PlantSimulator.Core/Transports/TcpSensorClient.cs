using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using PlantSimulator.Contracts;

namespace PlantSimulator.Core.Transports;

/// <summary>Vibration sensor's transport: streams readings to the main app over a TCP socket.</summary>
public sealed class TcpSensorClient : ISensorTransportClient
{
    public string ChannelLabel => "TCP Socket";
    public string? LastError { get; private set; }

    public async Task<bool> SendAsync(SensorReadingDto reading, CancellationToken ct = default)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, CommunicationHub.VibrationTcpPort, ct);
            using var stream = client.GetStream();
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reading) + "\n");
            await stream.WriteAsync(bytes, ct);
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
