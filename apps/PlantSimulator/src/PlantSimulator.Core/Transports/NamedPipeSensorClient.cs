using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using PlantSimulator.Contracts;

namespace PlantSimulator.Core.Transports;

/// <summary>Temperature sensor's transport: streams readings to the main app over a named pipe.</summary>
public sealed class NamedPipeSensorClient : ISensorTransportClient
{
    public string ChannelLabel => "Named Pipe";
    public string? LastError { get; private set; }

    public async Task<bool> SendAsync(SensorReadingDto reading, CancellationToken ct = default)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", CommunicationHub.PipeName,
                PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync(500, ct);
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reading) + "\n");
            await client.WriteAsync(bytes, ct);
            await client.FlushAsync(ct);
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
